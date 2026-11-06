namespace Data.Entities;

public class Customer : BaseEntity
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string LoyaltyCode { get; set; } = string.Empty;
    public int LoyaltyLevelId { get; set; }
    public LoyaltyLevel LoyaltyLevel { get; set; } = null!;
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
