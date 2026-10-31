using Library.Core.Ports.Inbound;
using Library.Core.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Library.Core;

public static class DependencyInjection
{
    /// <summary>Registers the inbound ports with their implementations.</summary>
    public static IServiceCollection AddLibraryCore(this IServiceCollection services)
    {
        services.AddScoped<IAddBook, AddBookService>();
        services.AddScoped<ILendBook, LendBookService>();
        services.AddScoped<IReturnBook, ReturnBookService>();
        services.AddScoped<IListBooks, ListBooksService>();
        return services;
    }
}
