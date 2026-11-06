using Data.Data;
using Data.Entities;
using Data.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Data.Repositories;

public class CustomerRepository : Repository<Customer>, ICustomerRepository
{
    public CustomerRepository(RestaurantDbContext context) : base(context)
    {
    }

    public Task<Customer?> FindByLoyaltyCodeAsync(string loyaltyCode, CancellationToken ct = default)
    {
        return Context.Customers.Include(entity => entity.LoyaltyLevel)
            .SingleOrDefaultAsync(entity => entity.LoyaltyCode == loyaltyCode, ct);
    }

    public async Task<IReadOnlyList<Customer>> GetAllWithLevelsAsync(CancellationToken ct = default)
    {
        return await Context.Customers.Include(entity => entity.LoyaltyLevel)
            .OrderBy(entity => entity.Id).ToListAsync(ct);
    }
}
