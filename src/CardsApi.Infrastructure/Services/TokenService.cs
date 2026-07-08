using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CardsApi.Application.Common;
using CardsApi.Application.Interfaces.Repositories;
using CardsApi.Application.Interfaces.Services;
using CardsApi.Domain.Entities;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CardsApi.Infrastructure.Services;

public class TokenService : ITokenService
{
    private readonly ISessionRepository _sessions;
    private readonly IConfiguration _configuration;

    public TokenService(ISessionRepository sessions, IConfiguration configuration)
    {
        _sessions = sessions;
        _configuration = configuration;
    }

    public async Task<IssuedToken> IssueTokenAsync(User user)
    {
        var minutes = GetAccessTokenMinutes();
        var now = DateTime.UtcNow;

        var session = new Session
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(minutes)
        };

        await _sessions.AddAsync(session, CancellationToken.None);
        await _sessions.SaveChangesAsync(CancellationToken.None);

        var token = BuildJwt(user.Id, session.Id, session.ExpiresAt);
        return new IssuedToken(token, session.ExpiresAt);
    }

    public async Task<IssuedToken> RotateTokenAsync(Guid currentSessionId, Guid userId)
    {
        var now = DateTime.UtcNow;

        var currentSession = await _sessions.FindByIdAsync(currentSessionId, CancellationToken.None)
            ?? throw ApiException.Unauthorized("Session not found.");

        if (currentSession.UserId != userId)
        {
            throw ApiException.Unauthorized("Session does not belong to the authenticated user.");
        }

        if (!currentSession.IsActive(now))
        {
            throw ApiException.Unauthorized("Current session is expired or already revoked. Please log in again.");
        }

        var minutes = GetAccessTokenMinutes();

        var newSession = new Session
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(minutes)
        };

        currentSession.RevokedAt = now;
        currentSession.ReplacedBySessionId = newSession.Id;

        await _sessions.AddAsync(newSession, CancellationToken.None);
        await _sessions.SaveChangesAsync(CancellationToken.None);

        var token = BuildJwt(userId, newSession.Id, newSession.ExpiresAt);
        return new IssuedToken(token, newSession.ExpiresAt);
    }

    private int GetAccessTokenMinutes()
    {
        return int.TryParse(_configuration["Jwt:AccessTokenMinutes"], out var m) ? m : 30;
    }

    private string BuildJwt(Guid userId, Guid sessionId, DateTime expiresAtUtc)
    {
        var signingKey = _configuration["Jwt:SigningKey"]
            ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");
        var issuer = _configuration["Jwt:Issuer"];
        var audience = _configuration["Jwt:Audience"];

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, sessionId.ToString()),
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: expiresAtUtc,
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
