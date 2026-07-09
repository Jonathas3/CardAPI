using Cards.Application.Interfaces.Repositories;
using Cards.Application.Interfaces.Services;

namespace Cards.Infrastructure.Services;

public class SessionValidator : ISessionValidator
{
    private readonly ISessionRepository _sessions;

    public SessionValidator(ISessionRepository sessions)
    {
        _sessions = sessions;
    }

    public async Task<bool> IsActiveAsync(Guid sessionId, DateTime utcNow, CancellationToken ct)
    {
        var session = await _sessions.FindByIdAsync(sessionId, ct);
        return session is not null && session.IsActive(utcNow);
    }
}
