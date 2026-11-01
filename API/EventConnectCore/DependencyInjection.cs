using EventConnectCore.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace EventConnectCore;

public static class DependencyInjection
{
    public static IServiceCollection AddEventConnectCore(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<EventConnectDbContext>(options => options.UseSqlServer(connectionString));
        return services;
    }
}
