using System.Reflection;
using Shipping.Contracts;

namespace Shipping.Kernel;

public sealed record LoadedCarrier(string Id, string Source, IShippingCarrier Instance);

public sealed record RejectedPlugin(string Source, string Reason);

/// <summary>
/// Discovers plugins in a directory: one sub-folder per plugin, and the folder name is the plugin's assembly name
/// (<c>plugins/Some.Carrier/Some.Carrier.dll</c>). Adding a plugin means adding a folder; nothing here changes.
/// A plugin that cannot be accepted is reported in <see cref="Rejected"/> and never stops the others from loading.
/// </summary>
public sealed class PluginCatalog : IDisposable
{
    private readonly List<LoadedCarrier> _carriers = [];
    private readonly List<RejectedPlugin> _rejected = [];
    private readonly List<PluginLoadContext> _contexts = [];

    private PluginCatalog() { }

    public IReadOnlyList<LoadedCarrier> Carriers => _carriers;

    public IReadOnlyList<RejectedPlugin> Rejected => _rejected;

    /// <summary>Weak handles to the load contexts that stayed alive; used to check that unloading really happens.</summary>
    internal IReadOnlyList<WeakReference> ContextHandles =>
        _contexts.Select(c => new WeakReference(c, trackResurrection: true)).ToList();

    public static PluginCatalog Load(string directory)
    {
        if (!Directory.Exists(directory)) throw new DirectoryNotFoundException($"Plugins directory not found: {directory}");

        var catalog = new PluginCatalog();
        // AssemblyLoadContext.LoadFromAssemblyPath only accepts absolute paths; callers may pass a relative one.
        foreach (var folder in Directory.GetDirectories(Path.GetFullPath(directory)).Order(StringComparer.Ordinal))
            catalog.LoadFolder(folder);
        return catalog;
    }

    private void LoadFolder(string folder)
    {
        var source = Path.GetFileName(folder);
        var dll = Path.Combine(folder, source + ".dll");
        if (!File.Exists(dll))
        {
            _rejected.Add(new RejectedPlugin(source, $"no {source}.dll in the folder"));
            return;
        }

        var context = new PluginLoadContext(dll);
        var accepted = 0;
        try
        {
            var assembly = context.LoadFromAssemblyPath(dll);
            var candidates = assembly.GetTypes()
                .Where(t => t is { IsClass: true, IsAbstract: false } && typeof(IShippingCarrier).IsAssignableFrom(t))
                .ToList();

            if (candidates.Count == 0)
                _rejected.Add(new RejectedPlugin(source, "no class implementing IShippingCarrier"));

            foreach (var type in candidates)
                if (TryAccept(type, source)) accepted++;
        }
        catch (Exception ex)
        {
            _rejected.Add(new RejectedPlugin(source, $"could not be loaded: {ex.GetType().Name}: {ex.Message}"));
        }

        if (accepted > 0) _contexts.Add(context);
        else context.Unload();
    }

    private bool TryAccept(Type type, string source)
    {
        var marker = type.GetCustomAttribute<CarrierAttribute>();
        if (marker is null)
            return Reject(source, $"{type.Name} implements IShippingCarrier but has no [Carrier] attribute");

        if (marker.ContractVersion != ShippingContract.Version)
            return Reject(source, $"{type.Name} targets contract version {marker.ContractVersion}, the kernel supports {ShippingContract.Version}");

        var existing = _carriers.FirstOrDefault(c => string.Equals(c.Id, marker.Id, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
            return Reject(source, $"duplicate carrier id '{marker.Id}' (already provided by {existing.Source})");

        IShippingCarrier instance;
        try
        {
            instance = (IShippingCarrier)Activator.CreateInstance(type)!;
        }
        catch (Exception ex)
        {
            return Reject(source, $"{type.Name} could not be created: {ex.GetType().Name}: {ex.Message}");
        }

        _carriers.Add(new LoadedCarrier(marker.Id, source, instance));
        return true;
    }

    private bool Reject(string source, string reason)
    {
        _rejected.Add(new RejectedPlugin(source, reason));
        return false;
    }

    /// <summary>Forgets every plugin and starts unloading their contexts. Completion is up to the garbage collector.</summary>
    public void Dispose()
    {
        _carriers.Clear();
        foreach (var context in _contexts) context.Unload();
        _contexts.Clear();
    }
}
