using Data.Entities;

namespace Data.Interfaces;

public interface IUnitOfWork
{
    IRepository<Ingredient> Ingredients { get; }
    IDishRepository Dishes { get; }
    IMenuItemRepository MenuItems { get; }
    ICustomerRepository Customers { get; }
    ILoyaltyLevelRepository LoyaltyLevels { get; }
    IOrderRepository Orders { get; }
    Task<int> SaveAsync(CancellationToken ct = default);
    Task ExecuteInTransactionAsync(Func<CancellationToken, Task> operation, CancellationToken ct = default);
}
