using CardsApi.Application.Interfaces.Repositories;
using CardsApi.Application.Interfaces.Services;
using CardsApi.Infrastructure.Data;
using CardsApi.Infrastructure.Repositories;
using CardsApi.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CardsApi.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(configuration.GetConnectionString("Default")));

        services.AddScoped<ICardRepository, CardRepository>();
        services.AddScoped<ISessionRepository, SessionRepository>();
        services.AddScoped<IUserRepository, UserRepository>();

        services.AddScoped<IPasswordHasher, BCryptPasswordHasher>();
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<ISessionValidator, SessionValidator>();
        services.AddSingleton<ICryptoService, CryptoService>();

        return services;
    }
}
