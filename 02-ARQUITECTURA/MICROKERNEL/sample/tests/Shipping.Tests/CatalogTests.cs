using System.Runtime.CompilerServices;
using System.Runtime.Loader;
using Shipping.Contracts;
using Shipping.Kernel;

namespace Shipping.Tests;

/// <summary>Discovery and loading: the kernel meets plugins it was never compiled against.</summary>
public class CatalogTests
{
    [Fact]
    public void PluginsAreDiscoveredFromFolders_TheKernelNeverReferencedThem()
    {
        using var sandbox = new Sandbox().With("Carriers.Standard", "Carriers.Express");

        using var catalog = PluginCatalog.Load(sandbox.Root);

        Assert.Equal(["express", "standard"], catalog.Carriers.Select(c => c.Id).Order());
        Assert.Empty(catalog.Rejected);
    }

    [Fact]
    public void InstallingAPlugin_IsCopyingAFolder_AndNothingElseChanges()
    {
        using var sandbox = new Sandbox().With("Carriers.Standard");
        using (var before = PluginCatalog.Load(sandbox.Root))
            Assert.Equal(["standard"], before.Carriers.Select(c => c.Id));

        sandbox.With("Carriers.Express");

        using var after = PluginCatalog.Load(sandbox.Root);
        Assert.Equal(["express", "standard"], after.Carriers.Select(c => c.Id).Order());
    }

    [Fact]
    public void AnEmptyDirectory_IsAKernelWithoutPlugins_NotAnError()
    {
        using var sandbox = new Sandbox();

        using var catalog = PluginCatalog.Load(sandbox.Root);

        Assert.Empty(catalog.Carriers);
        Assert.Empty(catalog.Rejected);
    }

    [Fact]
    public void ARelativeDirectoryWorksToo()
    {
        using var sandbox = new Sandbox().With("Carriers.Standard");
        var relative = Path.GetRelativePath(Directory.GetCurrentDirectory(), sandbox.Root);
        Assert.False(Path.IsPathRooted(relative), "the test needs a relative path (same drive)");

        using var catalog = PluginCatalog.Load(relative);

        Assert.Equal(["standard"], catalog.Carriers.Select(c => c.Id));
        Assert.Empty(catalog.Rejected);
    }

    [Fact]
    public void AMissingDirectory_IsAnError() =>
        Assert.Throws<DirectoryNotFoundException>(() => PluginCatalog.Load(Path.Combine(Path.GetTempPath(), "does-not-exist-" + Guid.NewGuid())));

    [Fact]
    public void EachPluginLivesInItsOwnLoadContext_ButSharesTheHostsContracts()
    {
        using var sandbox = new Sandbox().With("Carriers.Standard", "Carriers.Express");
        using var catalog = PluginCatalog.Load(sandbox.Root);

        var contexts = catalog.Carriers.Select(c => AssemblyLoadContext.GetLoadContext(c.Instance.GetType().Assembly)!).ToList();

        Assert.Equal(2, contexts.Distinct().Count());
        Assert.DoesNotContain(AssemblyLoadContext.Default, contexts);
        Assert.Same(AssemblyLoadContext.Default, AssemblyLoadContext.GetLoadContext(typeof(IShippingCarrier).Assembly));
        Assert.All(catalog.Carriers, c => Assert.Contains(typeof(IShippingCarrier), c.Instance.GetType().GetInterfaces()));
    }

    [Fact]
    public void ACarelessPluginThatShipsItsOwnContractsCopy_StillWorks_BecauseTheKernelSharesTheHostsOne()
    {
        using var sandbox = new Sandbox().With("Fixtures.BundledContracts");
        Assert.True(File.Exists(Path.Combine(sandbox.Root, "Fixtures.BundledContracts", "Shipping.Contracts.dll")),
            "the fixture is supposed to have the mistake: its own copy of the contracts");

        using var catalog = PluginCatalog.Load(sandbox.Root);

        Assert.Equal("bundled", Assert.Single(catalog.Carriers).Id);
        Assert.Empty(catalog.Rejected);
    }

    [Fact]
    public void APluginForAnUnknownContractVersion_IsRejected_WithAReason()
    {
        using var sandbox = new Sandbox().With("Fixtures.FutureContract");

        using var catalog = PluginCatalog.Load(sandbox.Root);

        Assert.Empty(catalog.Carriers);
        var rejected = Assert.Single(catalog.Rejected);
        Assert.Equal("Fixtures.FutureContract", rejected.Source);
        Assert.Contains("contract version 99", rejected.Reason);
    }

    [Fact]
    public void AClassWithoutTheCarrierAttribute_IsRejected()
    {
        using var sandbox = new Sandbox().With("Fixtures.NotAPlugin");

        using var catalog = PluginCatalog.Load(sandbox.Root);

        Assert.Empty(catalog.Carriers);
        Assert.Contains("[Carrier]", Assert.Single(catalog.Rejected).Reason);
    }

    [Fact]
    public void TwoPluginsWithTheSameId_TheFirstFolderWins_TheOtherIsRejected()
    {
        using var sandbox = new Sandbox().With("Carriers.Standard", "Fixtures.StandardClone");

        using var catalog = PluginCatalog.Load(sandbox.Root);

        Assert.Equal("Carriers.Standard", Assert.Single(catalog.Carriers).Source);
        var rejected = Assert.Single(catalog.Rejected);
        Assert.Equal("Fixtures.StandardClone", rejected.Source);
        Assert.Contains("duplicate carrier id 'standard'", rejected.Reason);
    }

    [Fact]
    public void AFileThatIsNotAnAssembly_IsRejected_AndTheOthersStillLoad()
    {
        using var sandbox = new Sandbox().With("Carriers.Standard");
        var broken = Directory.CreateDirectory(Path.Combine(sandbox.Root, "Broken.Plugin"));
        File.WriteAllText(Path.Combine(broken.FullName, "Broken.Plugin.dll"), "this is not a PE file");

        using var catalog = PluginCatalog.Load(sandbox.Root);

        Assert.Equal(["standard"], catalog.Carriers.Select(c => c.Id));
        var rejected = Assert.Single(catalog.Rejected);
        Assert.Equal("Broken.Plugin", rejected.Source);
        Assert.Contains("could not be loaded", rejected.Reason);
    }

    [Fact]
    public void AFolderWithoutItsDll_IsRejected()
    {
        using var sandbox = new Sandbox();
        Directory.CreateDirectory(Path.Combine(sandbox.Root, "Empty.Plugin"));

        using var catalog = PluginCatalog.Load(sandbox.Root);

        Assert.Contains("no Empty.Plugin.dll", Assert.Single(catalog.Rejected).Reason);
    }

    [Fact]
    public void DisposingTheCatalog_ForgetsThePlugins_AndTheirLoadContextsCanBeCollected()
    {
        using var sandbox = new Sandbox().With("Carriers.Standard", "Carriers.Express");

        var handles = LoadAndDispose(sandbox.Root);

        Assert.Equal(2, handles.Length);
        for (var i = 0; i < 10 && handles.Any(h => h.IsAlive); i++)
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
        }
        Assert.All(handles, h => Assert.False(h.IsAlive, "a plugin load context is still alive after Dispose"));
    }

    // Separate non-inlined method so no local of the test keeps a plugin alive (see Microsoft's unloadability guide).
    [MethodImpl(MethodImplOptions.NoInlining)]
    private static WeakReference[] LoadAndDispose(string directory)
    {
        var catalog = PluginCatalog.Load(directory);
        var handles = catalog.ContextHandles.ToArray();
        catalog.Dispose();
        return handles;
    }
}
