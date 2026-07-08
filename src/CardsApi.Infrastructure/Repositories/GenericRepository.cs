using CardsApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CardsApi.Infrastructure.Repositories;

public abstract class GenericRepository<TEntity> where TEntity : class
{
    protected readonly AppDbContext Db;
    protected readonly DbSet<TEntity> DbSet;

    protected GenericRepository(AppDbContext db)
    {
        Db = db;
        DbSet = db.Set<TEntity>();
    }

    public Task AddAsync(TEntity entity, CancellationToken ct)
    {
        DbSet.Add(entity);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => Db.SaveChangesAsync(ct);
}
