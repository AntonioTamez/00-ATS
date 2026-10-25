using Microsoft.Extensions.DependencyInjection;
using Shop.Domain.Services;

namespace Shop.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddSingleton<OrderPlacementService>();
        services.AddScoped<RegisterCustomerService>();
        services.AddScoped<PlaceOrderService>();
        services.AddScoped<CancelOrderService>();
        services.AddScoped<GetOrderService>();
        return services;
    }
}
