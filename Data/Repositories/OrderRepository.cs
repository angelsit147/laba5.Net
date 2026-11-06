using Data.Data;
using Data.Entities;
using Data.Enums;
using Data.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Data.Repositories;

public class OrderRepository : Repository<Order>, IOrderRepository
{
    public OrderRepository(RestaurantDbContext context) : base(context)
    {
    }

    public Task<Order?> GetWithItemsAsync(int id, CancellationToken ct = default)
    {
        return Context.Orders.Include(entity => entity.Items)
            .SingleOrDefaultAsync(entity => entity.Id == id, ct);
    }

    public void RemoveOrderItem(OrderItem item)
    {
        Context.OrderItems.Remove(item);
    }

    public Task<Order?> GetWithCurrentMenuAsync(int id, bool tracking = false, CancellationToken ct = default)
    {
        IQueryable<Order> query = Context.Orders;
        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return query.Include(entity => entity.Items)
            .ThenInclude(entity => entity.MenuItem)
            .ThenInclude(entity => entity.Dish)
            .ThenInclude(entity => entity.DishIngredients)
            .ThenInclude(entity => entity.Ingredient)
            .Include(entity => entity.Customer)
            .ThenInclude(entity => entity!.LoyaltyLevel)
            .AsSingleQuery()
            .SingleOrDefaultAsync(entity => entity.Id == id, ct);
    }

    public async Task<IReadOnlyList<Order>> GetCompletedByCustomerAsync(int customerId, CancellationToken ct = default)
    {
        return await Context.Orders.AsNoTracking().Include(entity => entity.Items)
            .Where(entity => entity.CustomerId == customerId && entity.Status == OrderStatus.Completed)
            .OrderBy(entity => entity.Id).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<Order>> GetCompletedForCustomersAsync(
        IReadOnlyCollection<int> customerIds, CancellationToken ct = default)
    {
        return await Context.Orders.AsNoTracking().Include(entity => entity.Items)
            .Where(entity => entity.CustomerId.HasValue && customerIds.Contains(entity.CustomerId.Value)
                && entity.Status == OrderStatus.Completed)
            .OrderBy(entity => entity.Id).ToListAsync(ct);
    }
}
