using Microsoft.Extensions.DependencyInjection;
using Users.Application.ListUsers;
using Users.Application.Ports.In;

namespace Users.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddScoped<IListUsersQuery, ListUsersHandler>();
        return services;
    }
}
