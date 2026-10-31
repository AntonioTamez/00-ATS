using Library.Core.Ports.Outbound;
using Microsoft.Extensions.DependencyInjection;

namespace Library.Adapters.Outbound;

public static class DependencyInjection
{
    /// <summary>Plugs the driven adapters. Pass a file path to persist as JSON; omit it for in-memory.</summary>
    public static IServiceCollection AddOutboundAdapters(this IServiceCollection services, string? jsonFilePath = null)
    {
        if (jsonFilePath is null)
            services.AddSingleton<IBookRepository, InMemoryBookRepository>();
        else
            services.AddSingleton<IBookRepository>(_ => new JsonFileBookRepository(jsonFilePath));

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IOverdueNotifier>(_ => new ConsoleOverdueNotifier());
        return services;
    }
}
