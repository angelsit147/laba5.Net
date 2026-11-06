namespace Data.Entities;

public class OrderItem : BaseEntity
{
    public int OrderId { get; set; }
    public int MenuItemId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string DishNameFixed { get; set; } = string.Empty;
    public string PortionNameFixed { get; set; } = string.Empty;
    public string PortionDescriptionFixed { get; set; } = string.Empty;
    public Order Order { get; set; } = null!;
    public MenuItem MenuItem { get; set; } = null!;
}
