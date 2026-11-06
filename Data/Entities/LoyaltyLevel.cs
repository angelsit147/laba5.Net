namespace Data.Entities;

public class LoyaltyLevel : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public decimal DiscountPercent { get; set; }
    public int MinOrders { get; set; }
    public decimal MinTotalSpent { get; set; }
    public ICollection<Customer> Customers { get; set; } = new List<Customer>();
}
