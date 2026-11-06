using AutoMapper;
using Business.Interfaces;
using Business.Models;
using Business.Pricing;
using Business.Validation;
using Data.Entities;
using Data.Enums;
using Data.Interfaces;

namespace Business.Services;

public class LoyaltyService : ILoyaltyService
{
    private readonly IUnitOfWork unitOfWork;
    private readonly IMapper mapper;

    public LoyaltyService(IUnitOfWork unitOfWork, IMapper mapper)
    {
        this.unitOfWork = unitOfWork;
        this.mapper = mapper;
    }

    public async Task<IReadOnlyList<LoyaltyLevelDto>> GetAllAsync(CancellationToken ct = default)
    {
        var levels = await unitOfWork.LoyaltyLevels.GetAllAsync(ct);
        return mapper.Map<List<LoyaltyLevelDto>>(levels.OrderBy(level => level.MinOrders)
            .ThenBy(level => level.MinTotalSpent));
    }

    public async Task<LoyaltyLevelDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        if (id <= 0)
        {
            throw new RestaurantException(RestaurantErrorType.Validation, "invalid_id", "Id must be positive.");
        }

        var level = await unitOfWork.LoyaltyLevels.GetByIdAsync(id, ct)
            ?? throw new RestaurantException(RestaurantErrorType.NotFound, "loyalty_level_not_found",
                "Loyalty level was not found.");
        return mapper.Map<LoyaltyLevelDto>(level);
    }

    public async Task RecalculateCustomerAsync(int customerId, Order completedOrder, CancellationToken ct = default)
    {
        if (completedOrder is null || completedOrder.Status != OrderStatus.Completed
            || completedOrder.CustomerId != customerId)
        {
            throw new RestaurantException(RestaurantErrorType.Validation, "invalid_completed_order",
                "The pending completed order must belong to this customer.");
        }

        var customer = await unitOfWork.Customers.GetByIdAsync(customerId, ct)
            ?? throw new RestaurantException(RestaurantErrorType.NotFound, "customer_not_found",
                "Customer was not found.");
        var levels = LoyaltyPolicyValidator.ValidateAndOrder(await unitOfWork.LoyaltyLevels.GetAllAsync(ct));
        var history = await unitOfWork.Orders.GetCompletedByCustomerAsync(customerId, ct);
        var selected = ChooseLevel(customerId, history, levels, completedOrder);
        if (customer.LoyaltyLevelId != selected.Id)
        {
            var tracked = await unitOfWork.LoyaltyLevels.GetByIdAsync(selected.Id, ct)
                ?? throw new RestaurantException(RestaurantErrorType.NotFound, "loyalty_level_not_found",
                    "Loyalty level was not found.");
            customer.LoyaltyLevelId = tracked.Id;
            customer.LoyaltyLevel = tracked;
        }
    }

    public async Task<IReadOnlyList<LoyaltyLevelDto>> UpdateLevelsAsync(
        UpdateLoyaltyLevelsDto dto, CancellationToken ct = default)
    {
        if (dto?.Levels is null || dto.Levels.Any(level => level is null || level.Id <= 0)
            || dto.Levels.Select(level => level.Id).Distinct().Count() != dto.Levels.Count)
        {
            throw new RestaurantException(RestaurantErrorType.Validation, "invalid_level_ids",
                "Provide each existing level Id exactly once.");
        }

        IReadOnlyList<LoyaltyLevelDto> result = Array.Empty<LoyaltyLevelDto>();
        await unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var existing = await unitOfWork.LoyaltyLevels.GetAllAsync(token);
            var byId = existing.ToDictionary(level => level.Id);
            if (dto.Levels.Any(level => !byId.ContainsKey(level.Id)))
            {
                throw new RestaurantException(RestaurantErrorType.NotFound, "loyalty_level_not_found",
                    "An unknown level Id was supplied.");
            }

            if (dto.Levels.Count != existing.Count)
            {
                throw new RestaurantException(RestaurantErrorType.Validation, "missing_level_ids",
                    "The update must contain the full set of existing levels.");
            }

            var candidates = dto.Levels.Select(update => new LoyaltyLevel
            {
                Id = update.Id, Name = byId[update.Id].Name, DiscountPercent = update.DiscountPercent,
                MinOrders = update.MinOrders, MinTotalSpent = update.MinTotalSpent
            }).ToList();
            var ordered = LoyaltyPolicyValidator.ValidateAndOrder(candidates);
            var customers = await unitOfWork.Customers.GetAllWithLevelsAsync(token);
            var history = await unitOfWork.Orders.GetCompletedForCustomersAsync(
                customers.Select(customer => customer.Id).ToArray(), token);
            var grouped = history.Where(order => order.CustomerId.HasValue).ToLookup(order => order.CustomerId!.Value);
            var assignments = customers.ToDictionary(customer => customer.Id,
                customer => ChooseLevel(customer.Id, grouped[customer.Id], ordered).Id);


            var trackedLevels = new Dictionary<int, LoyaltyLevel>();
            foreach (var level in ordered)
            {
                trackedLevels.Add(level.Id, await unitOfWork.LoyaltyLevels.GetByIdAsync(level.Id, token)
                    ?? throw new RestaurantException(RestaurantErrorType.NotFound, "loyalty_level_not_found",
                        "Loyalty level was not found."));
            }

            foreach (var candidate in ordered)
            {
                var tracked = trackedLevels[candidate.Id];
                tracked.DiscountPercent = candidate.DiscountPercent;
                tracked.MinOrders = candidate.MinOrders;
                tracked.MinTotalSpent = candidate.MinTotalSpent;
            }

            foreach (var customer in customers)
            {
                var selected = trackedLevels[assignments[customer.Id]];
                customer.LoyaltyLevelId = selected.Id;
                customer.LoyaltyLevel = selected;
            }

            await unitOfWork.SaveAsync(token);
            result = mapper.Map<List<LoyaltyLevelDto>>(ordered.Select(level => trackedLevels[level.Id]));
        }, ct);
        return result;
    }

    private static LoyaltyLevel ChooseLevel(int customerId, IEnumerable<Order> history,
        IReadOnlyList<LoyaltyLevel> levels, Order? completedOrder = null)
    {
        var completed = history.Where(order => order.CustomerId == customerId && order.Status == OrderStatus.Completed)
            .Where(order => completedOrder is null || order.Id != completedOrder.Id).ToList();
        if (completedOrder is not null)
        {
            completed.Add(completedOrder);
        }

        decimal spent = 0;
        try
        {
            foreach (var order in completed)
            {
                spent = checked(spent + OrderPricing.HistoricalTotal(order));
            }
        }
        catch (OverflowException)
        {
            throw new RestaurantException(RestaurantErrorType.Validation, "money_overflow",
                "Completed spending exceeds decimal capacity.");
        }

        return levels.Last(level => completed.Count >= level.MinOrders && spent >= level.MinTotalSpent);
    }
}
