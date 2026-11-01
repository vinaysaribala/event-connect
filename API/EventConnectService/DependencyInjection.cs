using EventConnectService.Interfaces;
using EventConnectService.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EventConnectService;

public static class DependencyInjection
{
    public static IServiceCollection AddEventConnectServices(this IServiceCollection services)
    {
        services.AddScoped<IEventService, EventService>();
        return services;
    }
}
