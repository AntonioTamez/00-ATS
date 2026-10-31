using System.Reflection;
using System.Runtime.Loader;
using Shipping.Contracts;

namespace Shipping.Kernel;

/// <summary>
/// One load context per plugin. Collectible, so the plugin can be unloaded. Its dependencies come from its own folder
/// (through the .deps.json), EXCEPT the contracts, which always come from the host.
/// </summary>
internal sealed class PluginLoadContext : AssemblyLoadContext
{
    private static readonly string ContractsAssembly = typeof(IShippingCarrier).Assembly.GetName().Name!;

    private readonly AssemblyDependencyResolver _resolver;

    public PluginLoadContext(string pluginPath)
        : base($"plugin:{Path.GetFileNameWithoutExtension(pluginPath)}", isCollectible: true)
    {
        _resolver = new AssemblyDependencyResolver(pluginPath);
    }

    protected override Assembly? Load(AssemblyName name)
    {
        // Type identity: IShippingCarrier from the host and IShippingCarrier from a second copy of the contracts
        // are two different types. Returning null here sends the request to the default context, i.e. the host's copy,
        // even when a careless plugin build dropped its own Shipping.Contracts.dll in its folder.
        if (name.Name == ContractsAssembly) return null;

        var path = _resolver.ResolveAssemblyToPath(name);
        return path is null ? null : LoadFromAssemblyPath(path);
    }
}
