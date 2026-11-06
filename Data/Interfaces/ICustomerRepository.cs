using Data.Entities;

namespace Data.Interfaces;

public interface ICustomerRepository : IRepository<Customer>
{
    Task<Customer?> FindByLoyaltyCodeAsync(string loyaltyCode, CancellationToken ct = default);
    Task<IReadOnlyList<Customer>> GetAllWithLevelsAsync(CancellationToken ct = default);
}
