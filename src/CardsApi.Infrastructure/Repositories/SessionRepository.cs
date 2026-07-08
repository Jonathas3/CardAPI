using CardsApi.Application.Interfaces.Repositories;
using CardsApi.Domain.Entities;
using CardsApi.Infrastructure.Data;

namespace CardsApi.Infrastructure.Repositories;

public class SessionRepository : GenericRepository<Session>, ISessionRepository
{
    public SessionRepository(AppDbContext db) : base(db)
    {
    }

    public Task<Session?> FindByIdAsync(Guid sessionId, CancellationToken ct) =>
        DbSet.FindAsync([sessionId], ct).AsTask();
}
