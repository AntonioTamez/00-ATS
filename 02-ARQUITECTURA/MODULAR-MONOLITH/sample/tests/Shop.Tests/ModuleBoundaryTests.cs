using System.Reflection;
using Catalog.Contracts;
using Catalog.Core;
using Notifications.Contracts;
using Notifications.Core;
using Ordering.Contracts;
using Ordering.Core;
using Shop.SharedKernel;

namespace Shop.Tests;

/// <summary>
/// The module boundaries as executable rules. If someone adds a forbidden project reference
/// (or makes an internal type public), the build still passes but these tests fail.
/// </summary>
public class ModuleBoundaryTests
{
    private sealed record Module(string Name, Assembly Core, Assembly Contracts);

    private static readonly Module[] Modules =
    [
        new("Catalog", typeof(CatalogModule).Assembly, typeof(ICatalogApi).Assembly),
        new("Ordering", typeof(OrderingModule).Assembly, typeof(IOrderingApi).Assembly),
        new("Notifications", typeof(NotificationsModule).Assembly, typeof(INotificationsApi).Assembly),
    ];

    private static readonly Assembly SharedKernel = typeof(IEventBus).Assembly;

    public static TheoryData<string> ModuleNames => new(Modules.Select(m => m.Name));

    private static Module Get(string name) => Modules.Single(m => m.Name == name);

    private static IEnumerable<string> References(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(a => a.Name!).Where(n => !IsFramework(n));

    private static bool IsFramework(string name) =>
        name is "System" or "netstandard" or "mscorlib" || name.StartsWith("System.") || name.StartsWith("Microsoft.");

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Core_NeverReferences_AnotherModulesCore(string name)
    {
        var module = Get(name);
        var otherCores = Modules.Where(m => m.Name != name).Select(m => m.Core.GetName().Name);

        var violations = References(module.Core).Intersect(otherCores!).ToList();

        Assert.Empty(violations);
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Contracts_NeverReference_AnyCore(string name)
    {
        var cores = Modules.Select(m => m.Core.GetName().Name);

        var violations = References(Get(name).Contracts).Intersect(cores!).ToList();

        Assert.Empty(violations);
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Core_ExposesOnlyItsModuleEntryPoint(string name)
    {
        var exported = Get(name).Core.GetExportedTypes().Select(t => t.Name).ToList();

        Assert.Equal([$"{name}Module"], exported);
    }

    [Theory]
    [MemberData(nameof(ModuleNames))]
    public void Contracts_ExposeOnlyDataAndInterfaces(string name)
    {
        // Allowed: interfaces (the API), enums, and sealed records/structs (plain data). No service classes.
        var offenders = Get(name).Contracts.GetExportedTypes()
            .Where(t => t.IsClass && !t.IsSealed)
            .Select(t => t.Name)
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void ModuleDependencies_AreExactlyTheDeclaredOnes()
    {
        var graph = Modules.ToDictionary(
            m => m.Name,
            m => Modules
                .Where(other => other.Name != m.Name &&
                                References(m.Core).Contains(other.Contracts.GetName().Name!))
                .Select(other => other.Name)
                .OrderBy(n => n)
                .ToArray());

        Assert.Empty(graph["Catalog"]);
        Assert.Equal(["Catalog"], graph["Ordering"]);
        Assert.Equal(["Ordering"], graph["Notifications"]);
    }

    [Fact]
    public void ModuleDependencies_HaveNoCycles()
    {
        var graph = Modules.ToDictionary(
            m => m.Name,
            m => Modules.Where(o => o.Name != m.Name && References(m.Core).Contains(o.Contracts.GetName().Name!))
                        .Select(o => o.Name).ToList());

        bool HasCycleFrom(string start, string current, HashSet<string> seen)
        {
            foreach (var next in graph[current])
            {
                if (next == start) return true;
                if (seen.Add(next) && HasCycleFrom(start, next, seen)) return true;
            }
            return false;
        }

        Assert.All(graph.Keys, module => Assert.False(HasCycleFrom(module, module, [])));
    }

    [Fact]
    public void SharedKernel_DoesNotKnowAnyModule()
    {
        var moduleAssemblies = Modules.SelectMany(m => new[] { m.Core, m.Contracts }).Select(a => a.GetName().Name);

        Assert.Empty(References(SharedKernel).Intersect(moduleAssemblies!));
    }

    [Fact]
    public void EveryModuleHasItsOwnData_NoStoreIsVisibleOutsideItsAssembly()
    {
        var publicStores = Modules
            .SelectMany(m => m.Core.GetTypes())
            .Where(t => t.Name.EndsWith("Store") || t.Name.EndsWith("Log"))
            .Where(t => t.IsVisible)
            .ToList();

        Assert.Empty(publicStores);
    }
}
