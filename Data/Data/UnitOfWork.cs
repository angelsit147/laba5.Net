using System.Data;
using Data.Entities;
using Data.Interfaces;
using Data.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Data.Data;

public class UnitOfWork : IUnitOfWork
{
    private readonly RestaurantDbContext context;

    public UnitOfWork(RestaurantDbContext context)
    {
        this.context = context;
        Ingredients = new Repository<Ingredient>(context);
        Dishes = new DishRepository(context);
        MenuItems = new MenuItemRepository(context);
        Customers = new CustomerRepository(context);
        LoyaltyLevels = new LoyaltyLevelRepository(context);
        Orders = new OrderRepository(context);
    }

    public IRepository<Ingredient> Ingredients { get; }
    public IDishRepository Dishes { get; }
    public IMenuItemRepository MenuItems { get; }
    public ICustomerRepository Customers { get; }
    public ILoyaltyLevelRepository LoyaltyLevels { get; }
    public IOrderRepository Orders { get; }

    public Task<int> SaveAsync(CancellationToken ct = default)
    {
        return context.SaveChangesAsync(ct);
    }

    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(operation);
        if (context.Database.CurrentTransaction is not null)
        {
            throw new InvalidOperationException("Nested transactions are not supported.");
        }

        await using var transaction = await context.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);
        try
        {
            await operation(ct);
            ct.ThrowIfCancellationRequested();
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await transaction.RollbackAsync(CancellationToken.None);
            throw;
        }
    }
}
