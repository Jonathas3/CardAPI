using System.IdentityModel.Tokens.Jwt;
using System.Text;
using Cards.Api.Common;
using Cards.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace Cards.Api.Configuration;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddApiAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var jwtSigningKey = configuration["Jwt:SigningKey"]
            ?? throw new InvalidOperationException("Jwt:SigningKey is not configured.");
        var jwtIssuer = configuration["Jwt:Issuer"];
        var jwtAudience = configuration["Jwt:Audience"];

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtIssuer,
                    ValidateAudience = true,
                    ValidAudience = jwtAudience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var jti = context.Principal?.FindFirst(JwtRegisteredClaimNames.Jti)?.Value;

                        if (jti is null || !Guid.TryParse(jti, out var sessionId))
                        {
                            context.Fail("Invalid token.");
                            return;
                        }

                        var validator = context.HttpContext.RequestServices.GetRequiredService<ISessionValidator>();
                        var isActive = await validator.IsActiveAsync(sessionId, DateTime.UtcNow, context.HttpContext.RequestAborted);

                        if (!isActive)
                        {
                            context.Fail("Session has expired or been revoked.");
                        }
                    },
                    OnChallenge = context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/json";
                        var body = new ErrorResponse
                        {
                            ErrorCode = "UNAUTHORIZED",
                            Message = "Missing, invalid or expired credentials.",
                            TraceId = context.HttpContext.TraceIdentifier
                        };
                        return context.Response.WriteAsync(
                            System.Text.Json.JsonSerializer.Serialize(body, ErrorResponse.JsonOptions));
                    }
                };
            });

        services.AddAuthorization();

        return services;
    }
}
