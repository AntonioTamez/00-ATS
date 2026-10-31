using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.RegularExpressions;
using Faas.Runtime;
using Orders.Functions;
using static Orders.Tests.Support;

namespace Orders.Tests;

/// <summary>The rules that make these functions serverless-friendly, written as tests so they cannot rot.</summary>
public class ArchitectureTests
{
    private static readonly Assembly PlatformAssembly = typeof(FunctionHost).Assembly;
    private static readonly Assembly FunctionsAssembly = typeof(PlaceOrderFunction).Assembly;

    private static readonly Type[] TriggerInterfaces = [typeof(IQueueFunction), typeof(IHttpFunction), typeof(ITimerFunction)];

    private static IReadOnlyList<Type> FunctionTypes { get; } = FunctionsAssembly.GetTypes()
        .Where(t => !t.IsAbstract && t.GetInterfaces().Any(i => TriggerInterfaces.Contains(i)))
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

    private static string[] ProjectReferences(string project) =>
        Regex.Matches(File.ReadAllText(Path.Combine(SourceFolder(project), project + ".csproj")), "<ProjectReference\\s+Include=\"([^\"]+)\"")
            .Select(m => m.Groups[1].Value.Replace('\\', '/'))
            .ToArray();

    private static IEnumerable<string> SourceFiles(string project) =>
        Directory.EnumerateFiles(SourceFolder(project), "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
                        && !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    private static void AssertNoneMatch(IEnumerable<string> files, string pattern, string why)
    {
        var regex = new Regex(pattern);
        foreach (var path in files)
            Assert.False(regex.IsMatch(File.ReadAllText(path)), $"{Path.GetFileName(path)}: {why}");
    }

    // ---------- dependencies ----------

    [Fact]
    public void ThePlatformReferencesOnlyTheFramework()
    {
        var references = ReferencedAssemblies(PlatformAssembly);

        Assert.NotEmpty(references);
        Assert.DoesNotContain(references, name => name.StartsWith("Orders.", StringComparison.Ordinal));
    }

    [Fact]
    public void TheFunctionsReferenceThePlatformContracts_NeverTheCompositionRoot()
    {
        var references = ReferencedAssemblies(FunctionsAssembly);

        Assert.Contains("Faas.Runtime", references);
        Assert.DoesNotContain("Orders.Local", references);
    }

    [Fact]
    public void TheProjectFilesSayTheSame()
    {
        Assert.Empty(ProjectReferences("Faas.Runtime"));
        Assert.Equal(["../Faas.Runtime/Faas.Runtime.csproj"], ProjectReferences("Orders.Functions"));
        Assert.Equal(
            ["../Faas.Runtime/Faas.Runtime.csproj", "../Orders.Functions/Orders.Functions.csproj"],
            ProjectReferences("Orders.Local"));
    }

    [Fact]
    public void ThePlatformKnowsNothingAboutOrders() =>
        AssertNoneMatch(SourceFiles("Faas.Runtime"), "\\b(Order|Orders|Sku|Stock|Customer|Reserve\\w*)\\b", "mentions the orders domain");

    // ---------- the functions ----------

    [Fact]
    public void ThereAreThreeFunctions_OnePerTrigger_EachPublicAndSealed()
    {
        Assert.Equal(["DailySummaryFunction", "PlaceOrderFunction", "ReserveStockFunction"], FunctionTypes.Select(t => t.Name));
        Assert.All(FunctionTypes, t => Assert.True(t.IsPublic && t.IsSealed, $"{t.Name} must be public and sealed"));
        Assert.All(FunctionTypes, t => Assert.Single(t.GetInterfaces(), i => TriggerInterfaces.Contains(i)));
    }

    [Fact]
    public void FunctionsKeepNoStateBetweenInvocations()
    {
        foreach (var function in FunctionTypes)
        {
            var fields = function.GetFields(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            foreach (var field in fields)
            {
                Assert.True(field.IsInitOnly || field.IsLiteral, $"{function.Name}.{field.Name} must be readonly");
                var isMutableCollection = field.FieldType.GetInterfaces().Append(field.FieldType)
                    .Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(ICollection<>));
                Assert.False(isMutableCollection, $"{function.Name}.{field.Name} is a mutable collection ({field.FieldType.Name})");
            }
        }
    }

    [Fact]
    public void FunctionsReceiveOnlyPortsThroughTheirConstructor()
    {
        foreach (var function in FunctionTypes)
        {
            var constructor = Assert.Single(function.GetConstructors());
            Assert.All(constructor.GetParameters(), p => Assert.True(p.ParameterType.IsInterface, $"{function.Name} takes {p.ParameterType.Name}, which is not an interface"));
        }
    }

    [Fact]
    public void NoFunctionMentionsAnotherFunction_TheyTalkOnlyThroughQueues()
    {
        foreach (var function in FunctionTypes)
        {
            var source = File.ReadAllText(Path.Combine(SourceFolder("Orders.Functions"), function.Name + ".cs"));
            foreach (var other in FunctionTypes.Where(t => t != function))
                Assert.False(Regex.IsMatch(source, $"\\b{other.Name}\\b"), $"{function.Name} mentions {other.Name}");
        }
    }

    [Fact]
    public void FunctionsDoNotKnowThePlatformHostNorAnyConcreteAdapter() =>
        AssertNoneMatch(SourceFiles("Orders.Functions"), "\\b(FunctionHost|InMemory\\w*|LocalApp)\\b", "knows the host or a concrete adapter");

    [Fact]
    public void FunctionsReadTheTimeFromTheContext() =>
        AssertNoneMatch(SourceFiles("Orders.Functions"), "\\b(DateTime|DateTimeOffset)\\.(Now|UtcNow)\\b|TimeProvider\\.System", "reads the clock directly; use context.Now");

    [Fact]
    public void FunctionsDoNotStartBackgroundWork() =>
        AssertNoneMatch(SourceFiles("Orders.Functions"), "\\bTask\\.(Run|Factory)\\b|new Thread\\b|ThreadPool|\\.Result\\b|\\.Wait\\(", "starts background work or blocks; the function must finish what it starts");

    [Fact]
    public void FunctionsTouchNoFilesConsoleOrEnvironment() =>
        AssertNoneMatch(SourceFiles("Orders.Functions"), "\\b(File|Directory|Console|Environment|HttpClient)\\.|new HttpClient", "does its own I/O instead of using a port");

    [Fact]
    public void OnlyTheCompositionRootRegistersFunctions()
    {
        AssertNoneMatch(SourceFiles("Orders.Functions"), "\\.Add(Http|Queue|Timer)\\(", "registers functions; only Orders.Local may");
        Assert.Matches("\\.AddQueue\\(", File.ReadAllText(Path.Combine(SourceFolder("Orders.Local"), "LocalApp.cs")));
    }
}
