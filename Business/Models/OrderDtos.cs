using Data.Enums;
using System.Text.Json.Serialization;

namespace Business.Models;

public class OrderDto
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public OrderStatus Status { get; set; }
    public OrderType OrderType { get; set; }
    public decimal? DiscountPercentFixed { get; set; }
    public decimal AppliedDiscountPercent { get; set; }
    public bool IsEstimate { get; set; }
    public IReadOnlyList<OrderItemDto> Items { get; set; } = Array.Empty<OrderItemDto>();
    public decimal Subtotal { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal Total { get; set; }
}

public class OrderItemDto
{
    public int Id { get; set; }
    public int MenuItemId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string DishName { get; set; } = string.Empty;
    public string PortionName { get; set; } = string.Empty;
    public string PortionDescription { get; set; } = string.Empty;
    public bool? IsAvailable { get; set; }
    public decimal LineTotal { get; set; }
}

public class CreateOrderDto
{
    [JsonRequired]
    public OrderType OrderType { get; set; }
    public string? LoyaltyCode { get; set; }
}

public class UpdateDraftOrderDto
{
    [JsonRequired]
    public OrderType OrderType { get; set; }
    public string? LoyaltyCode { get; set; }
}

public class AddOrderItemDto
{
    public int MenuItemId { get; set; }
    public int Quantity { get; set; }
}

public class UpdateOrderItemDto
{
    public int Quantity { get; set; }
}
