using System.Reflection;

namespace TodoApp.Tests;

/// <summary>Executable Dependency Rule: if someone adds a forbidden reference, the build goes red.</summary>
public class ArchitectureTests
{
    private const string Domain = "TodoApp.Domain";
    private const string Application = "TodoApp.Application";
    private const string Infrastructure = "TodoApp.Infrastructure";
    private const string Api = "TodoApp.Api";

    private static IEnumerable<string> ReferencesOf(string assemblyName) =>
        Assembly.Load(assemblyName).GetReferencedAssemblies().Select(a => a.Name!);

    [Fact]
    public void Domain_DependsOnNoProjectLayer() =>
        Assert.DoesNotContain(ReferencesOf(Domain), n => n.StartsWith("TodoApp."));

    [Fact]
    public void Application_DependsOnlyOnDomain()
    {
        var refs = ReferencesOf(Application).Where(n => n.StartsWith("TodoApp.")).ToList();
        Assert.Equal([Domain], refs);
    }

    [Fact]
    public void Infrastructure_DoesNotDependOnApi() =>
        Assert.DoesNotContain(ReferencesOf(Infrastructure), n => n == Api);

    [Theory]
    [InlineData(Domain)]
    [InlineData(Application)]
    public void InnerLayers_DoNotReferenceAspNetCore(string layer) =>
        Assert.DoesNotContain(ReferencesOf(layer), n => n.StartsWith("Microsoft.AspNetCore"));
}
