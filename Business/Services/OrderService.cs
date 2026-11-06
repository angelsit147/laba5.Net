using Business.Interfaces;
using Business.Models;
using Business.Pricing;
using Business.Validation;
using Data.Entities;
using Data.Enums;
using Data.Interfaces;

namespace Business.Services;

public class OrderService : IOrderService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly ILoyaltyService loyaltyService;

    public OrderService(IUnitOfWork unitOfWork, ILoyaltyService loyaltyService)
    {
        this.unitOfWork = unitOfWork;
        this.loyaltyService = loyaltyService;
    }

    public async Task<OrderDto> CreateAsync(CreateOrderDto dto, CancellationToken ct = default)
    {
        if (dto is null)
        {
            throw Invalid("invalid_request", "Order data is required.");
        }

        ValidateOrderType(dto.OrderType);
        var customer = await ResolveCustomerAsync(dto.LoyaltyCode, ct);
        var order = new Order
        {
            CreatedAt = DateTime.UtcNow, OrderType = dto.OrderType, Status = OrderStatus.Draft,
            CustomerId = customer?.Id, Customer = customer, DiscountPercentFixed = null
        };
        await unitOfWork.Orders.AddAsync(order, ct);
        return await SaveDraftAsync(order, ct);
    }

    public async Task<OrderDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return Project(await FindAsync(id, false, ct));
    }

    public async Task<OrderDto> UpdateDraftAsync(int id, UpdateDraftOrderDto dto, CancellationToken ct = default)
    {
        if (dto is null)
        {
            throw Invalid("invalid_request", "Order data is required.");
        }

        ValidateOrderType(dto.OrderType);
        var order = await FindAsync(id, true, ct);
        EnsureStatus(order, OrderStatus.Draft);
        var customer = await ResolveCustomerAsync(dto.LoyaltyCode, ct);
        order.OrderType = dto.OrderType;
        order.CustomerId = customer?.Id;
        order.Customer = customer;
        return await SaveDraftAsync(order, ct);
    }

    public async Task<OrderDto> AddItemAsync(int id, AddOrderItemDto dto, CancellationToken ct = default)
    {
        if (dto is null)
        {
            throw Invalid("invalid_request", "Order item data is required.");
        }

        Positive(dto.MenuItemId);
        PositiveQuantity(dto.Quantity);
        var order = await FindAsync(id, true, ct);
        EnsureStatus(order, OrderStatus.Draft);
        var menu = await unitOfWork.MenuItems.GetWithDishAsync(dto.MenuItemId, ct)
            ?? throw new RestaurantException(RestaurantErrorType.NotFound, "menu_item_not_found", "Menu item was not found.");
        EnsureAvailable(menu);
        var existing = order.Items.SingleOrDefault(item => item.MenuItemId == dto.MenuItemId);
        int quantity;
        try
        {
            quantity = checked((existing?.Quantity ?? 0) + dto.Quantity);
        }
        catch (OverflowException)
        {
            throw Invalid("quantity_overflow", "Quantity exceeds int capacity.");
        }

        OrderPricing.LineTotal(quantity, menu.Price);
        if (existing is null)
        {
            existing = new OrderItem { Order = order, OrderId = order.Id, MenuItemId = menu.Id, MenuItem = menu };
            order.Items.Add(existing);
        }

        existing.Quantity = quantity;
        CopySnapshot(existing, menu.Price, menu.Dish.Name, menu.PortionName, menu.PortionDescription);
        return await SaveDraftAsync(order, ct);
    }

    public async Task<OrderDto> UpdateItemAsync(int id, int itemId, UpdateOrderItemDto dto, CancellationToken ct = default)
    {
        if (dto is null)
        {
            throw Invalid("invalid_request", "Order item data is required.");
        }

        PositiveQuantity(dto.Quantity);
        var order = await FindAsync(id, true, ct);
        EnsureStatus(order, OrderStatus.Draft);
        var item = FindItem(order, itemId);
        var menu = item.MenuItem;
        OrderPricing.LineTotal(dto.Quantity, menu.Price);
        item.Quantity = dto.Quantity;
        CopySnapshot(item, menu.Price, menu.Dish.Name, menu.PortionName, menu.PortionDescription);
        return await SaveDraftAsync(order, ct);
    }

    public async Task RemoveItemAsync(int id, int itemId, CancellationToken ct = default)
    {
        var order = await FindAsync(id, true, ct);
        EnsureStatus(order, OrderStatus.Draft);
        var item = FindItem(order, itemId);
        unitOfWork.Orders.RemoveOrderItem(item);
        order.Items.Remove(item);
        await unitOfWork.SaveAsync(ct);
    }

    public async Task<OrderDto> ConfirmAsync(int id, CancellationToken ct = default)
    {
        OrderDto result = null!;
        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var order = await FindAsync(id, true, token);
            EnsureStatus(order, OrderStatus.Draft);
            if (order.Items.Count == 0)
            {
                throw new RestaurantException(RestaurantErrorType.Conflict, "empty_order", "An empty order cannot be confirmed.");
            }


            var quote = Project(order);
            if (quote.Items.Any(item => item.IsAvailable != true))
            {
                throw new RestaurantException(RestaurantErrorType.Conflict, "menu_item_unavailable",
                    "All portions and dishes must be available and dishes must have ingredients.");
            }

            var current = quote.Items.ToDictionary(item => item.MenuItemId);
            foreach (var item in order.Items)
            {
                var value = current[item.MenuItemId];
                CopySnapshot(item, value.UnitPrice, value.DishName, value.PortionName, value.PortionDescription);
            }

            order.DiscountPercentFixed = quote.AppliedDiscountPercent;
            order.Status = OrderStatus.Confirmed;
            await unitOfWork.SaveAsync(token);
            result = Project(order);
        }, ct);
        return result;
    }

    public async Task<OrderDto> CompleteAsync(int id, CancellationToken ct = default)
    {
        OrderDto result = null!;
        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var order = await FindAsync(id, true, token);
            EnsureStatus(order, OrderStatus.Confirmed);
            _ = Project(order);
            order.Status = OrderStatus.Completed;
            if (order.CustomerId.HasValue)
            {
                await loyaltyService.RecalculateCustomerAsync(order.CustomerId.Value, order, token);
            }

            await unitOfWork.SaveAsync(token);
            result = Project(order);
        }, ct);
        return result;
    }

    public async Task<OrderDto> CancelAsync(int id, CancellationToken ct = default)
    {
        var order = await FindAsync(id, true, ct);
        if (order.Status is not (OrderStatus.Draft or OrderStatus.Confirmed))
        {
            throw InvalidState();
        }

        order.Status = OrderStatus.Cancelled;
        var result = Project(order);
        await unitOfWork.SaveAsync(ct);
        return result;
    }

    private async Task<OrderDto> SaveDraftAsync(Order order, CancellationToken ct)
    {
        _ = Project(order);
        await unitOfWork.SaveAsync(ct);
        return Project(order);
    }

    private async Task<Order> FindAsync(int id, bool tracking, CancellationToken ct)
    {
        Positive(id);
        return await unitOfWork.Orders.GetWithCurrentMenuAsync(id, tracking, ct)
            ?? throw new RestaurantException(RestaurantErrorType.NotFound, "order_not_found", "Order was not found.");
    }

    private async Task<Customer?> ResolveCustomerAsync(string? value, CancellationToken ct)
    {
        if (value is null)
        {
            return null;
        }

        var code = value.Trim().ToUpperInvariant();
        if (code.Length != 32 || code.Any(character => !char.IsAsciiHexDigit(character)))
        {
            throw Invalid("invalid_loyalty_code", "Loyalty code must contain 32 hexadecimal characters.");
        }

        return await unitOfWork.Customers.FindByLoyaltyCodeAsync(code, ct)
            ?? throw new RestaurantException(RestaurantErrorType.NotFound, "loyalty_code_not_found", "Loyalty card was not found.");
    }

    private static OrderDto Project(Order order)
    {
        var draft = order.Status == OrderStatus.Draft;
        decimal discount;
        if (draft)
        {
            discount = 0m;
            if (order.CustomerId.HasValue)
            {
                discount = order.Customer?.LoyaltyLevel?.DiscountPercent
                    ?? throw new RestaurantException(RestaurantErrorType.Conflict, "customer_level_missing", "Customer level is missing.");
            }
        }
        else if (order.DiscountPercentFixed.HasValue)
        {
            discount = order.DiscountPercentFixed.Value;
        }
        else if (order.Status == OrderStatus.Cancelled)
        {
            discount = 0m;
        }
        else
        {
            throw new RestaurantException(RestaurantErrorType.Conflict, "missing_fixed_discount", "Historical discount is missing.");
        }

        var items = order.Items.Select(item => new OrderItemDto
        {
            Id = item.Id, MenuItemId = item.MenuItemId, Quantity = item.Quantity,
            UnitPrice = draft ? item.MenuItem.Price : item.UnitPrice,
            DishName = draft ? item.MenuItem.Dish.Name : item.DishNameFixed,
            PortionName = draft ? item.MenuItem.PortionName : item.PortionNameFixed,
            PortionDescription = draft ? item.MenuItem.PortionDescription : item.PortionDescriptionFixed,
            IsAvailable = draft ? Available(item.MenuItem) : null
        }).ToList();
        foreach (var item in items)
        {
            item.LineTotal = OrderPricing.LineTotal(item.Quantity, item.UnitPrice);
        }

        var totals = OrderPricing.Calculate(items.Select(item => (item.Quantity, item.UnitPrice)), discount);
        return new OrderDto
        {
            Id = order.Id, CreatedAt = order.CreatedAt, Status = order.Status, OrderType = order.OrderType,
            DiscountPercentFixed = order.DiscountPercentFixed, AppliedDiscountPercent = discount,
            IsEstimate = draft || (order.Status == OrderStatus.Cancelled && !order.DiscountPercentFixed.HasValue),
            Items = items, Subtotal = totals.Subtotal, DiscountAmount = totals.DiscountAmount, Total = totals.Total
        };
    }

    private static void CopySnapshot(OrderItem item, decimal price, string dishName, string portionName, string description)
    {
        item.UnitPrice = price;
        item.DishNameFixed = dishName;
        item.PortionNameFixed = portionName;
        item.PortionDescriptionFixed = description;
    }

    private static bool Available(MenuItem menu) =>
        menu.IsAvailable && menu.Dish.IsAvailable && menu.Dish.DishIngredients.Count > 0;

    private static void EnsureAvailable(MenuItem menu)
    {
        if (!Available(menu))
        {
            throw new RestaurantException(RestaurantErrorType.Conflict, "menu_item_unavailable", "The portion is unavailable.");
        }
    }

    private static OrderItem FindItem(Order order, int itemId)
    {
        Positive(itemId);
        return order.Items.SingleOrDefault(item => item.Id == itemId && item.OrderId == order.Id)
            ?? throw new RestaurantException(RestaurantErrorType.NotFound, "order_item_not_found", "Order item was not found in this order.");
    }

    private static void ValidateOrderType(OrderType type)
    {
        if (!Enum.IsDefined(type))
        {
            throw Invalid("invalid_order_type", "Order type is invalid.");
        }
    }

    private static void Positive(int id)
    {
        if (id <= 0)
        {
            throw Invalid("invalid_id", "Id must be positive.");
        }
    }

    private static void PositiveQuantity(int quantity)
    {
        if (quantity <= 0)
        {
            throw Invalid("invalid_quantity", "Quantity must be positive.");
        }
    }

    private static void EnsureStatus(Order order, OrderStatus required)
    {
        if (order.Status != required)
        {
            throw InvalidState();
        }
    }

    private static RestaurantException InvalidState() =>
        new(RestaurantErrorType.Conflict, "invalid_order_state", "The operation is not allowed in this order status.");

    private static RestaurantException Invalid(string code, string message) =>
        new(RestaurantErrorType.Validation, code, message);
}
