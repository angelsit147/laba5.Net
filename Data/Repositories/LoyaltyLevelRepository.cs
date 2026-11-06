using Data.Data;
using Data.Entities;
using Data.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Data.Repositories;

public class LoyaltyLevelRepository : Repository<LoyaltyLevel>, ILoyaltyLevelRepository
{
    public LoyaltyLevelRepository(RestaurantDbContext context) : base(context)
    {
    }

    public Task<LoyaltyLevel?> FindByNameAsync(string name, CancellationToken ct = default)
    {
        return Context.LoyaltyLevels.SingleOrDefaultAsync(entity => entity.Name == name, ct);
    }
}
