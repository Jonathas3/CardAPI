using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using CardsApi.Application.Common;
using CardsApi.Application.Dtos;
using CardsApi.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CardsApi.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly ITokenService _tokenService;

    public AuthController(IAuthService authService, ITokenService tokenService)
    {
        _authService = authService;
        _tokenService = tokenService;
    }

    /// <summary>Autentica um usuário do seed e emite um token de acesso válido por 30 minutos.</summary>
    /// <remarks>
    /// Não há fluxo de cadastro (não exigido pelo enunciado): os usuários já vêm prontos
    /// no seed da migration do EF Core (ver <c>Migrations/InitialCreate</c>).
    /// </remarks>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TokenResponseDto>> Login([FromBody] LoginRequestDto dto, CancellationToken ct)
    {
        var issued = await _authService.LoginAsync(dto.Username, dto.Password, ct);

        return Ok(new TokenResponseDto
        {
            AccessToken = issued.AccessToken,
            ExpiresAtUtc = issued.ExpiresAtUtc
        });
    }

    /// <summary>Rotaciona o token atual: revoga o antigo e emite um novo, com nova validade de 30 minutos.</summary>
    /// <remarks>
    /// O token usado para chamar este endpoint precisa ainda estar válido (não expirado,
    /// não revogado). Uma vez expirado, o token não pode mais ser rotacionado — o cliente
    /// deve chamar <c>/api/auth/login</c> novamente.
    /// </remarks>
    [HttpPost("refresh")]
    [Authorize]
    [ProducesResponseType(typeof(TokenResponseDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<TokenResponseDto>> Refresh(CancellationToken ct)
    {
        var userId = User.GetUserId();
        var sessionId = User.GetSessionId();

        var issued = await _tokenService.RotateTokenAsync(sessionId, userId);

        return Ok(new TokenResponseDto
        {
            AccessToken = issued.AccessToken,
            ExpiresAtUtc = issued.ExpiresAtUtc
        });
    }
}

public static class ClaimsPrincipalExtensions
{
    public static Guid GetUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? principal.FindFirstValue(ClaimTypes.NameIdentifier);

        if (value is null || !Guid.TryParse(value, out var userId))
        {
            throw ApiException.Unauthorized();
        }

        return userId;
    }

    public static Guid GetSessionId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(JwtRegisteredClaimNames.Jti);

        if (value is null || !Guid.TryParse(value, out var sessionId))
        {
            throw ApiException.Unauthorized();
        }

        return sessionId;
    }
}
