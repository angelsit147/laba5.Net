using Data.Entities;

namespace Data.Interfaces;

public interface IMenuItemRepository : IRepository<MenuItem>
{
    Task<MenuItem?> GetWithDishAsync(int id, CancellationToken ct = default);
    Task<IReadOnlyList<MenuItem>> GetPriceListAsync(bool availableOnly = true, CancellationToken ct = default);
    Task<bool> HasOrderItemsAsync(int menuItemId, CancellationToken ct = default);
}
