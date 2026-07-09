using Cards.Domain.Entities;

namespace Cards.Application.Interfaces.Repositories;

public interface ISessionRepository
{
    Task<Session?> FindByIdAsync(Guid sessionId, CancellationToken ct);

    Task AddAsync(Session session, CancellationToken ct);

    Task SaveChangesAsync(CancellationToken ct);
}
