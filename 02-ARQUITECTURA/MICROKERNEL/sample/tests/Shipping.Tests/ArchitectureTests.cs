using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;
using Shipping.Contracts;
using Shipping.Kernel;

namespace Shipping.Tests;

/// <summary>
/// The microkernel's rules, as executable checks: the core is minimal and plugin-free, the contract is tiny,
/// and plugins depend on the contract and on nothing else of ours.
/// </summary>
public class ArchitectureTests
{
    private static string[] References(string dllPath)
    {
        using var pe = new PEReader(File.OpenRead(dllPath));
        var md = pe.GetMetadataReader();
        return md.AssemblyReferences.Select(h => md.GetString(md.GetAssemblyReference(h).Name)).Order().ToArray();
    }

    private static string[] References(Type type) => References(type.Assembly.Location);

    private static bool IsPluginName(string name) => name.StartsWith("Carriers.") || name.StartsWith("Fixtures.");

    private static string[] ProjectReferences(string csproj) =>
        Regex.Matches(File.ReadAllText(csproj), "<ProjectReference\\s+Include=\"([^\"]+)\"").Select(m => Path.GetFileNameWithoutExtension(m.Groups[1].Value.Replace('\\', '/'))).Order().ToArray();

    private static string Src(string project) => Path.Combine(Sandbox.SampleRoot(), "src", project, project + ".csproj");

    private static string[] StagedPlugins() => Directory.GetDirectories(Sandbox.Artifacts("plugins"));

    // ---------- the contract ----------

    [Fact]
    public void TheContractsReferenceOnlyTheFramework()
    {
        var refs = References(typeof(IShippingCarrier));

        Assert.DoesNotContain(refs, r => r.StartsWith("Shipping.") || IsPluginName(r));
        Assert.Empty(ProjectReferences(Src("Shipping.Contracts")));
    }

    [Fact]
    public void TheContractIsTiny_BecauseEveryPluginEverWrittenDependsOnIt()
    {
        var types = typeof(IShippingCarrier).Assembly.GetExportedTypes().Select(t => t.Name).Order();

        Assert.Equal(["CarrierAttribute", "IShippingCarrier", "Parcel", "Quote", "ShippingContract"], types);
        Assert.Single(typeof(IShippingCarrier).GetMethods());
    }

    // ---------- the core ----------

    [Fact]
    public void TheKernelKnowsTheContracts_AndNoPlugin()
    {
        var refs = References(typeof(PluginCatalog));

        Assert.Contains("Shipping.Contracts", refs);
        Assert.DoesNotContain(refs, IsPluginName);
        Assert.Equal(["Shipping.Contracts"], ProjectReferences(Src("Shipping.Kernel")));
    }

    [Fact]
    public void TheKernelIsMinimal_ItsPublicSurfaceIsDiscoveryAndComparisonOnly()
    {
        var types = typeof(PluginCatalog).Assembly.GetExportedTypes().Select(t => t.Name).Order();

        Assert.Equal(["CarrierFailure", "LoadedCarrier", "PluginCatalog", "QuoteResult", "QuoteService", "RejectedPlugin"], types);
    }

    [Fact]
    public void TheHostReferencesTheKernelAndTheContracts_NeverAPlugin() =>
        Assert.Equal(["Shipping.Contracts", "Shipping.Kernel"], ProjectReferences(Src("Shipping.Host")));

    [Fact]
    public void NoSourceFileOfTheKernelOrHostMentionsAConcreteCarrier()
    {
        var files = new[] { "Shipping.Kernel", "Shipping.Host" }
            .SelectMany(p => Directory.GetFiles(Path.Combine(Sandbox.SampleRoot(), "src", p), "*.cs", SearchOption.AllDirectories))
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"));

        foreach (var file in files)
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotMatch("Carriers\\.(Standard|Express)|Fixtures\\.|\"standard\"|\"express\"", text);
        }
    }

    // ---------- the plugins ----------

    [Fact]
    public void ThePluginsToCheckWereActuallyBuiltAndStaged() =>
        Assert.Equal(["Carriers.Express", "Carriers.Standard"], StagedPlugins().Select(p => Path.GetFileName(p)!).Order());

    [Fact]
    public void EveryPluginReferencesTheContracts_AndNeitherTheKernelNorAnotherPlugin()
    {
        foreach (var folder in StagedPlugins())
        {
            var name = Path.GetFileName(folder)!;
            var refs = References(Path.Combine(folder, name + ".dll"));

            Assert.Contains("Shipping.Contracts", refs);
            Assert.DoesNotContain("Shipping.Kernel", refs);
            Assert.DoesNotContain(refs, r => IsPluginName(r) && r != name);
        }
    }

    [Fact]
    public void EveryPluginFolderCarriesOnlyItsOwnCode_TheContractsAreNotCopied()
    {
        foreach (var folder in StagedPlugins())
        {
            var dlls = Directory.GetFiles(folder, "*.dll").Select(f => Path.GetFileName(f)!).ToArray();

            Assert.Equal([Path.GetFileName(folder) + ".dll"], dlls);
            Assert.True(File.Exists(Path.Combine(folder, Path.GetFileName(folder) + ".deps.json")), "the resolver needs the .deps.json");
        }
    }

    [Fact]
    public void ThePluginProjectsReferenceOnlyTheContractsProject()
    {
        foreach (var csproj in Directory.GetFiles(Path.Combine(Sandbox.SampleRoot(), "src", "plugins"), "*.csproj", SearchOption.AllDirectories))
        {
            Assert.Equal(["Shipping.Contracts"], ProjectReferences(csproj));
            Assert.Contains("<Private>false</Private>", File.ReadAllText(csproj));
            Assert.Contains("<EnableDynamicLoading>true</EnableDynamicLoading>", File.ReadAllText(csproj));
        }
    }
}
