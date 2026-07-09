using Cards.Application.Interfaces.Services;
using Cards.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Cards.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<ICardService, CardService>();

        return services;
    }
}
