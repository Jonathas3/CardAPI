namespace Cards.Application.Interfaces.Services;

public interface IAuthService
{
    Task<IssuedToken> LoginAsync(string username, string password, CancellationToken ct);
}
