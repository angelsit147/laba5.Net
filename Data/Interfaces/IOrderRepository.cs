using Data.Entities;

namespace Data.Interfaces;

public interface IOrderRepository : IRepository<Order>
{
    Task<Order?> GetWithItemsAsync(int id, CancellationToken ct = default);
    Task<Order?> GetWithCurrentMenuAsync(int id, bool tracking = false, CancellationToken ct = default);
    Task<IReadOnlyList<Order>> GetCompletedByCustomerAsync(int customerId, CancellationToken ct = default);
    Task<IReadOnlyList<Order>> GetCompletedForCustomersAsync(IReadOnlyCollection<int> customerIds, CancellationToken ct = default);
    void RemoveOrderItem(OrderItem item);
}
