using Data.Data;
using Data.Entities;
using Data.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Data.Repositories;

public class Repository<TEntity> : IRepository<TEntity> where TEntity : BaseEntity
{
    protected RestaurantDbContext Context { get; }

    public Repository(RestaurantDbContext context)
    {
        Context = context;
    }

    public Task<TEntity?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        return Context.Set<TEntity>().FindAsync(new object[] { id }, ct).AsTask();
    }

    public async Task<IReadOnlyList<TEntity>> GetAllAsync(CancellationToken ct = default)
    {
        return await Context.Set<TEntity>().AsNoTracking().OrderBy(entity => entity.Id).ToListAsync(ct);
    }

    public async Task AddAsync(TEntity entity, CancellationToken ct = default)
    {
        await Context.Set<TEntity>().AddAsync(entity, ct);
    }

    public void Update(TEntity entity)
    {
        Context.Entry(entity).State = EntityState.Modified;
    }

    public void Remove(TEntity entity)
    {
        Context.Set<TEntity>().Remove(entity);
    }
}
