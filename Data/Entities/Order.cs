using Data.Enums;

namespace Data.Entities;

public class Order : BaseEntity
{
    public int? CustomerId { get; set; }
    public DateTime CreatedAt { get; set; }
    public OrderStatus Status { get; set; }
    public OrderType OrderType { get; set; }
    public decimal? DiscountPercentFixed { get; set; }
    public Customer? Customer { get; set; }
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
}
