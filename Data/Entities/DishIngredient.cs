namespace Data.Entities;

public class DishIngredient : BaseEntity
{
    public int DishId { get; set; }
    public int IngredientId { get; set; }
    public Dish Dish { get; set; } = null!;
    public Ingredient Ingredient { get; set; } = null!;
}
