using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Shop.SharedKernel;

public static class SharedKernelExtensions
{
    /// <summary>Registers the only things every module may share: the event bus and the clock.</summary>
    public static IServiceCollection AddSharedKernel(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton<IEventBus, InMemoryEventBus>();
        return services;
    }
}
