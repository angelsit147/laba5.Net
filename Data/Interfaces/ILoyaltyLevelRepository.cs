using Data.Entities;

namespace Data.Interfaces;

public interface ILoyaltyLevelRepository : IRepository<LoyaltyLevel>
{
    Task<LoyaltyLevel?> FindByNameAsync(string name, CancellationToken ct = default);
}
