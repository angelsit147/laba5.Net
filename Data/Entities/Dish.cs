namespace Data.Entities;

public class Dish : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsAvailable { get; set; }
    public ICollection<DishIngredient> DishIngredients { get; set; } = new List<DishIngredient>();
    public ICollection<MenuItem> MenuItems { get; set; } = new List<MenuItem>();
}
