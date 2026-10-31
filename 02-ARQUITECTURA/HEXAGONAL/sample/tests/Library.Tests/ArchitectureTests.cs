using System.Reflection;

namespace Library.Tests;

/// <summary>Executable hexagon: the core must not know any adapter, and adapters must not know each other.</summary>
public class ArchitectureTests
{
    private const string Core = "Library.Core";
    private const string Outbound = "Library.Adapters.Outbound";
    private const string Rest = "Library.Adapters.Rest";
    private const string Cli = "Library.Adapters.Cli";

    private static List<string> LibraryRefs(string assembly) =>
        Assembly.Load(assembly).GetReferencedAssemblies().Select(a => a.Name!).Where(n => n.StartsWith("Library.")).ToList();

    [Fact]
    public void Core_ReferencesNoAdapter() => Assert.Empty(LibraryRefs(Core));

    [Fact]
    public void Core_DoesNotReferenceAspNetCore() =>
        Assert.DoesNotContain(Assembly.Load(Core).GetReferencedAssemblies(), a => a.Name!.StartsWith("Microsoft.AspNetCore"));

    [Fact]
    public void OutboundAdapters_DependOnlyOnCore() => Assert.Equal([Core], LibraryRefs(Outbound));

    [Theory]
    [InlineData(Rest)]
    [InlineData(Cli)]
    public void DrivingAdapters_DoNotKnowEachOther(string adapter)
    {
        var other = adapter == Rest ? Cli : Rest;
        Assert.DoesNotContain(LibraryRefs(adapter), n => n == other);
    }
}
