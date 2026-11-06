using Data.Data;
using Data.Entities;
using Data.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Data.Repositories;

public class DishRepository : Repository<Dish>, IDishRepository
{
    public DishRepository(RestaurantDbContext context) : base(context)
    {
    }

    public Task<Dish?> GetWithIngredientsAsync(int id, CancellationToken ct = default)
    {
        return Context.Dishes.Include(entity => entity.DishIngredients)
            .ThenInclude(entity => entity.Ingredient).SingleOrDefaultAsync(entity => entity.Id == id, ct);
    }

    public async Task<IReadOnlyList<Dish>> GetAllWithIngredientsAsync(CancellationToken ct = default)
    {
        return await Context.Dishes.AsNoTracking().Include(entity => entity.DishIngredients)
            .ThenInclude(entity => entity.Ingredient).OrderBy(entity => entity.Id).ToListAsync(ct);
    }

    public Task<bool> HasMenuItemsAsync(int dishId, CancellationToken ct = default)
    {
        return Context.MenuItems.AnyAsync(entity => entity.DishId == dishId, ct);
    }

    public Task<bool> IsIngredientUsedAsync(int ingredientId, CancellationToken ct = default)
    {
        return Context.DishIngredients.AnyAsync(entity => entity.IngredientId == ingredientId, ct);
    }

    public void RemoveIngredientLink(DishIngredient link)
    {
        Context.DishIngredients.Remove(link);
    }
}
