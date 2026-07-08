namespace CardsApi.Application.Interfaces.Services;

public interface ISessionValidator
{
    Task<bool> IsActiveAsync(Guid sessionId, DateTime utcNow, CancellationToken ct);
}
