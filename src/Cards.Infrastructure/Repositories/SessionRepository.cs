using Cards.Application.Interfaces.Repositories;
using Cards.Domain.Entities;
using Cards.Infrastructure.Data;

namespace Cards.Infrastructure.Repositories;

public class SessionRepository : GenericRepository<Session>, ISessionRepository
{
    public SessionRepository(AppDbContext db) : base(db)
    {
    }

    public Task<Session?> FindByIdAsync(Guid sessionId, CancellationToken ct) =>
        DbSet.FindAsync([sessionId], ct).AsTask();
}
