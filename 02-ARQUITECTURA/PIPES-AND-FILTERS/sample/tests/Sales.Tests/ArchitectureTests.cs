using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;
using Sales.Filters;
using Sales.Host;
using Sales.Pipeline;
using static Sales.Tests.Support;

namespace Sales.Tests;

/// <summary>The rules that make this a pipes-and-filters design, written as tests so they cannot rot.</summary>
public class ArchitectureTests
{
    private static readonly Assembly CoreAssembly = typeof(IFilter<,>).Assembly;
    private static readonly Assembly FiltersAssembly = typeof(ParseCsvFilter).Assembly;

    private static IReadOnlyList<Type> FilterTypes { get; } = FiltersAssembly.GetTypes()
        .Where(t => !t.IsAbstract && t.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IFilter<,>)))
        .OrderBy(t => t.Name, StringComparer.Ordinal)
        .ToList();

    private static IReadOnlyList<string> ReferencedAssemblies(Assembly assembly)
    {
        using var stream = File.OpenRead(assembly.Location);
        using var pe = new PEReader(stream);
        var reader = pe.GetMetadataReader();
        return reader.AssemblyReferences
            .Select(handle => reader.GetString(reader.GetAssemblyReference(handle).Name))
            .ToList();
    }

    private static string[] ProjectReferences(string project)
    {
        var csproj = File.ReadAllText(Path.Combine(SourceFolder(project), project + ".csproj"));
        return Regex.Matches(csproj, "<ProjectReference\\s+Include=\"([^\"]+)\"")
            .Select(m => m.Groups[1].Value.Replace('\\', '/'))
            .ToArray();
    }

    private static IEnumerable<string> SourceFiles(string project) =>
        Directory.EnumerateFiles(SourceFolder(project), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    // ---------- dependencies ----------

    [Fact]
    public void TheCoreReferencesOnlyTheFramework()
    {
        var references = ReferencedAssemblies(CoreAssembly);

        Assert.NotEmpty(references);
        Assert.DoesNotContain(references, name => name.StartsWith("Sales.", StringComparison.Ordinal));
    }

    [Fact]
    public void TheFiltersReferenceTheCore_NeverTheComposition()
    {
        var references = ReferencedAssemblies(FiltersAssembly);

        Assert.Contains("Sales.Pipeline", references);
        Assert.DoesNotContain("Sales.Host", references);
    }

    [Fact]
    public void TheProjectFilesSayTheSame()
    {
        Assert.Empty(ProjectReferences("Sales.Pipeline"));
        Assert.Equal(["../Sales.Pipeline/Sales.Pipeline.csproj"], ProjectReferences("Sales.Filters"));
        Assert.Equal(
            ["../Sales.Pipeline/Sales.Pipeline.csproj", "../Sales.Filters/Sales.Filters.csproj"],
            ProjectReferences("Sales.Host"));
    }

    [Fact]
    public void TheCoreKnowsNothingAboutSales()
    {
        var domainWords = new Regex("\\b(Sale|RawSale|PricedSale|SourceLine|Csv|Vat|Sku)\\b", RegexOptions.IgnoreCase);

        foreach (var path in SourceFiles("Sales.Pipeline"))
            Assert.False(domainWords.IsMatch(File.ReadAllText(path)), $"{Path.GetFileName(path)} mentions the sales domain");
    }

    // ---------- the filters ----------

    [Fact]
    public void ThereAreFiveFilters_EachPublicAndSealed()
    {
        Assert.Equal(
            ["ApplyVatFilter", "BlockedSkusFilter", "DeduplicateFilter", "ParseCsvFilter", "ValidateSaleFilter"],
            FilterTypes.Select(t => t.Name));
        Assert.All(FilterTypes, t => Assert.True(t.IsPublic && t.IsSealed, $"{t.Name} must be public and sealed"));
    }

    [Fact]
    public void NoFilterMentionsAnotherFilter()
    {
        foreach (var filter in FilterTypes)
        {
            var source = File.ReadAllText(Path.Combine(SourceFolder("Sales.Filters"), filter.Name + ".cs"));
            foreach (var other in FilterTypes.Where(t => t != filter))
                Assert.False(Regex.IsMatch(source, $"\\b{other.Name}\\b"), $"{filter.Name} mentions {other.Name}");
        }
    }

    [Fact]
    public void FiltersHoldNoMutableState_SoOneInstanceCanServeAnyNumberOfRuns()
    {
        foreach (var filter in FilterTypes)
        {
            var fields = filter.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            foreach (var field in fields)
            {
                Assert.True(field.IsInitOnly, $"{filter.Name}.{field.Name} must be readonly");
                var isMutableCollection = field.FieldType.GetInterfaces().Append(field.FieldType)
                    .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICollection<>));
                Assert.False(isMutableCollection, $"{filter.Name}.{field.Name} is a mutable collection ({field.FieldType.Name})");
            }
        }
    }

    [Fact]
    public void FilterNamesAreUniqueKebabCase()
    {
        var names = SalesPipelines.Standard().Stages;

        Assert.Equal(names.Count, names.Distinct().Count());
        Assert.All(names, name => Assert.Matches("^[a-z]+(-[a-z]+)*$", name));
    }

    // ---------- the chain ----------

    [Fact]
    public void ThenOnlyAcceptsAFilterWhoseInputIsThePipelinesOutput()
    {
        var pipelineOut = typeof(Pipeline<,>).GetGenericArguments()[1];
        var parameter = typeof(Pipeline<,>).GetMethod(nameof(Pipeline<,>.Then))!.GetParameters().Single().ParameterType;

        Assert.Equal(typeof(IFilter<,>), parameter.GetGenericTypeDefinition());
        Assert.Equal(pipelineOut, parameter.GetGenericArguments()[0]);
    }

    [Fact]
    public void OnlyTheCompositionRootChainsFilters()
    {
        var chaining = new Regex("\\.Then\\(");

        foreach (var path in SourceFiles("Sales.Pipeline").Concat(SourceFiles("Sales.Filters")))
            Assert.False(chaining.IsMatch(File.ReadAllText(path)), $"{Path.GetFileName(path)} composes filters; only Sales.Host may");

        Assert.Matches(chaining, File.ReadAllText(Path.Combine(SourceFolder("Sales.Host"), "SalesPipelines.cs")));
    }
}
