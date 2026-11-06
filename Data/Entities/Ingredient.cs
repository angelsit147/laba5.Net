namespace Data.Entities;

public class Ingredient : BaseEntity
{
    public string Name { get; set; } = string.Empty;
    public ICollection<DishIngredient> DishIngredients { get; set; } = new List<DishIngredient>();
}
