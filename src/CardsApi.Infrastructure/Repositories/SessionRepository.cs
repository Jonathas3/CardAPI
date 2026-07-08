using CardsApi.Application.Interfaces.Repositories;
using CardsApi.Domain.Entities;
using CardsApi.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CardsApi.Infrastructure.Repositories;

public class SessionRepository : ISessionRepository
{
    private readonly DbSet<Session> _sessions;
    private readonly AppDbContext _db;

    public SessionRepository(AppDbContext db)
    {
        _db = db;
        _sessions = db.Sessions;
    }

    public Task<Session?> FindByIdAsync(Guid sessionId, CancellationToken ct) =>
        _sessions.FindAsync(new object?[] { sessionId }, ct).AsTask();

    public Task AddAsync(Session session, CancellationToken ct)
    {
        _sessions.Add(session);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken ct) => _db.SaveChangesAsync(ct);
}
