using Data.Entities;

namespace Data.Interfaces;

public interface IDishRepository : IRepository<Dish>
{
    Task<Dish?> GetWithIngredientsAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<Dish>> GetAllWithIngredientsAsync(CancellationToken ct = default);
    Task<bool> HasMenuItemsAsync(int dishId, CancellationToken ct = default);
    Task<bool> IsIngredientUsedAsync(int ingredientId, CancellationToken ct = default);
    void RemoveIngredientLink(DishIngredient link);
}
