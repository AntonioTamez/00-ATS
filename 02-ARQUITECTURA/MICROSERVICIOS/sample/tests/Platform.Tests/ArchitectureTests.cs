using System.Reflection;
using Catalog.Service;
using Gateway;
using Orders.Service;

namespace Platform.Tests;

/// <summary>What makes them microservices and not three projects in a trench coat, as executable rules.</summary>
public class ArchitectureTests
{
    private sealed record Service(string Name, Assembly Assembly, string EntryName);

    private static readonly Service[] Services =
    [
        new("Catalog.Service", typeof(CatalogServiceEntry).Assembly, nameof(CatalogServiceEntry)),
        new("Orders.Service", typeof(OrdersServiceEntry).Assembly, nameof(OrdersServiceEntry)),
        new("Gateway", typeof(GatewayEntry).Assembly, nameof(GatewayEntry)),
    ];

    public static TheoryData<string> ServiceNames => new(Services.Select(s => s.Name));

    private static Service Get(string name) => Services.Single(s => s.Name == name);

    private static bool IsPlatform(string name) =>
        name is "mscorlib" or "netstandard" or "WindowsBase" || name.StartsWith("System") || name.StartsWith("Microsoft");

    [Theory]
    [MemberData(nameof(ServiceNames))]
    public void AService_ReferencesNothingOfOurs_NoOtherService_NoSharedLibrary(string name)
    {
        var ours = Get(name).Assembly.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(n => !IsPlatform(n))
            .ToList();

        Assert.Empty(ours);
    }

    [Theory]
    [MemberData(nameof(ServiceNames))]
    public void AService_DoesNotExposeItsInternals(string name)
    {
        var service = Get(name);

        var exported = service.Assembly.GetExportedTypes().Select(t => t.Name).Where(n => n != service.EntryName);

        // Only the HTTP contract DTOs may be public; no service class, store or client leaks out.
        Assert.DoesNotContain(exported, n => n.EndsWith("Store") || n.EndsWith("Service") || n.EndsWith("Client"));
    }

    [Fact]
    public void TheDataOfEachService_IsInternalToIt()
    {
        var stores = Services.SelectMany(s => s.Assembly.GetTypes()).Where(t => t.Name.EndsWith("Store")).ToList();

        Assert.NotEmpty(stores);
        Assert.All(stores, t => Assert.False(t.IsVisible, $"{t.FullName} is visible outside its service"));
    }

    [Fact]
    public void TheGateway_HasNoBusinessTypes() =>
        // "Program" is the class C# generates for top-level statements; it holds no business code.
        Assert.Equal([nameof(GatewayEntry)], Get("Gateway").Assembly.GetExportedTypes().Select(t => t.Name).Where(n => n != "Program"));

    [Fact]
    public void Orders_HasItsOwnCopyOfTheCatalogContract_NotTheCatalogTypes()
    {
        var orderTypes = Get("Orders.Service").Assembly.GetTypes().Select(t => t.FullName);

        Assert.DoesNotContain(orderTypes, n => n!.StartsWith("Catalog."));
    }
}
