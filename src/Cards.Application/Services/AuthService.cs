using Cards.Application.Common;
using Cards.Application.Interfaces.Repositories;
using Cards.Application.Interfaces.Services;
using Microsoft.Extensions.Logging;

namespace Cards.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        ILogger<AuthService> logger)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _logger = logger;
    }

    public async Task<IssuedToken> LoginAsync(string username, string password, CancellationToken ct)
    {
        var user = await _users.FindByUsernameAsync(username, ct);

        if (user is null || !_passwordHasher.Verify(password, user.PasswordHash))
        {
            _logger.LogWarning("Failed login attempt for username {Username}", username);
            throw ApiException.Unauthorized("Invalid username or password.");
        }

        return await _tokenService.IssueTokenAsync(user);
    }
}
