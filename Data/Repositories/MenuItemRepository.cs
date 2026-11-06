using Data.Data;
using Data.Entities;
using Data.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Data.Repositories;

public class MenuItemRepository : Repository<MenuItem>, IMenuItemRepository
{
    public MenuItemRepository(RestaurantDbContext context) : base(context)
    {
    }

    public Task<MenuItem?> GetWithDishAsync(int id, CancellationToken ct = default)
    {
        return Context.MenuItems.Include(entity => entity.Dish).ThenInclude(entity => entity.DishIngredients)
            .SingleOrDefaultAsync(entity => entity.Id == id, ct);
    }

    public async Task<IReadOnlyList<MenuItem>> GetPriceListAsync(bool availableOnly = true, CancellationToken ct = default)
    {
        var query = Context.MenuItems.AsNoTracking().Include(entity => entity.Dish)
            .ThenInclude(entity => entity.DishIngredients).AsQueryable();

        if (availableOnly)
        {
            query = query.Where(entity => entity.IsAvailable && entity.Dish.IsAvailable
                && entity.Dish.DishIngredients.Any());
        }

        return await query.OrderBy(entity => entity.Id).ToListAsync(ct);
    }

    public Task<bool> HasOrderItemsAsync(int menuItemId, CancellationToken ct = default)
    {
        return Context.OrderItems.AnyAsync(entity => entity.MenuItemId == menuItemId, ct);
    }
}
