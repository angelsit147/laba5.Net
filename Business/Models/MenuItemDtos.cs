namespace Business.Models;

public class MenuItemDto
{
    public int Id { get; set; }
    public int DishId { get; set; }
    public string DishName { get; set; } = string.Empty;
    public string PortionName { get; set; } = string.Empty;
    public string PortionDescription { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool IsAvailable { get; set; }
}

public class MenuItemWriteDto
{
    public int DishId { get; set; }
    public string PortionName { get; set; } = string.Empty;
    public string PortionDescription { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public bool? IsAvailable { get; set; }
}
