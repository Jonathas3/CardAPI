using Cards.Domain.Entities;

namespace Cards.Application.Interfaces.Services;

public record IssuedToken(string AccessToken, DateTime ExpiresAtUtc);

public interface ITokenService
{
    Task<IssuedToken> IssueTokenAsync(User user);

    Task<IssuedToken> RotateTokenAsync(Guid currentSessionId, Guid userId);
}
