using System.Reflection;

namespace Shop.Tests;

/// <summary>Executable onion: every ring may depend on inner rings only.</summary>
public class ArchitectureTests
{
    private const string Model = "Shop.Domain.Model";
    private const string DomainServices = "Shop.Domain.Services";
    private const string Application = "Shop.Application";
    private const string Infrastructure = "Shop.Infrastructure";
    private const string Api = "Shop.Api";

    private static List<string> ShopRefs(string assembly) =>
        Assembly.Load(assembly).GetReferencedAssemblies().Select(a => a.Name!).Where(n => n.StartsWith("Shop.")).ToList();

    [Fact]
    public void Model_DependsOnNoOtherRing() => Assert.Empty(ShopRefs(Model));

    [Fact]
    public void DomainServices_DependOnlyOnModel() => Assert.Equal([Model], ShopRefs(DomainServices));

    [Fact]
    public void Application_DependsOnlyOnInnerRings()
    {
        var refs = ShopRefs(Application);
        Assert.NotEmpty(refs);
        Assert.All(refs, r => Assert.Contains(r, new[] { Model, DomainServices }));
    }

    [Fact]
    public void Infrastructure_DoesNotDependOnTheUiRing() => Assert.DoesNotContain(ShopRefs(Infrastructure), r => r == Api);

    [Theory]
    [InlineData(Model)]
    [InlineData(DomainServices)]
    [InlineData(Application)]
    public void InnerRings_DoNotReferenceAspNetCore(string ring) =>
        Assert.DoesNotContain(Assembly.Load(ring).GetReferencedAssemblies(), a => a.Name!.StartsWith("Microsoft.AspNetCore"));
}
