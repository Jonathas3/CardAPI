using CardsApi.Application.Interfaces.Services;
using CardsApi.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace CardsApi.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICardService, CardService>();

        return services;
    }
}
