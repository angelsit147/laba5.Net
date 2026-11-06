namespace Data.Entities;

public class MenuItem : BaseEntity
{
    public int DishId { get; set; }
    public string PortionName { get; set; } = string.Empty;
    public string PortionDescription { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsAvailable { get; set; }
    public Dish Dish { get; set; } = null!;
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
