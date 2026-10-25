using System.Reflection;
using Lending.Business;
using Lending.Data;

namespace Lending.Tests;

/// <summary>The layering rules as executable checks (a closed, three-layer design).</summary>
public class ArchitectureTests
{
    private static readonly Assembly Data = typeof(BookRepository).Assembly;
    private static readonly Assembly Business = typeof(LoanService).Assembly;
    private static readonly Assembly Web = typeof(Program).Assembly;

    private static IEnumerable<string> LendingRefs(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(a => a.Name!).Where(n => n.StartsWith("Lending."));

    [Fact]
    public void TheDataLayer_KnowsNoOtherLayer() =>
        Assert.Empty(LendingRefs(Data));

    [Fact]
    public void TheBusinessLayer_UsesTheDataLayer_AndNothingAbove() =>
        Assert.Equal(["Lending.Data"], LendingRefs(Business));

    [Fact]
    public void ThePresentationLayer_UsesOnlyTheBusinessLayer_ClosedLayers() =>
        Assert.Equal(["Lending.Business"], LendingRefs(Web));

    [Fact]
    public void NoDataRecord_IsEverExposedByTheBusinessLayer()
    {
        var recordTypes = Data.GetExportedTypes().Where(t => t.Name.EndsWith("Record")).ToHashSet();

        var leaks = Business.GetExportedTypes()
            .SelectMany(t => t.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .SelectMany(MemberTypes)
            .SelectMany(Flatten)
            .Where(recordTypes.Contains)
            .Select(t => t.Name)
            .Distinct()
            .ToList();

        Assert.Empty(leaks);
    }

    [Fact]
    public void BusinessServices_AreTheOnlyPublicWayIn_TheirNamesEndInService()
    {
        var entryPoints = Business.GetExportedTypes()
            .Select(t => t.Name)
            .Where(n => n.EndsWith("Service"))
            .OrderBy(n => n);

        Assert.Equal(["CatalogService", "LoanService", "MembershipService"], entryPoints);
    }

    /// <summary>
    /// A CHARACTERIZATION, not a rule to keep: it documents the classic layered trade-off. The business services
    /// receive concrete data-layer classes (not interfaces), so the business layer cannot be built without the data layer.
    /// Clean, Onion and Hexagonal invert exactly this dependency.
    /// </summary>
    [Fact]
    public void Characterization_BusinessServicesDependOnConcreteDataClasses()
    {
        var dataDependencies = new[] { typeof(CatalogService), typeof(MembershipService), typeof(LoanService) }
            .SelectMany(t => t.GetConstructors().Single().GetParameters())
            .Select(p => p.ParameterType)
            .Where(t => t.Assembly == Data)
            .ToList();

        Assert.NotEmpty(dataDependencies);
        Assert.All(dataDependencies, t => Assert.True(t.IsClass && !t.IsAbstract, $"{t.Name} is not a concrete class"));
    }

    private static IEnumerable<Type> MemberTypes(MemberInfo member) => member switch
    {
        MethodBase m => m.GetParameters().Select(p => p.ParameterType).Concat(m is MethodInfo mi ? [mi.ReturnType] : []),
        PropertyInfo p => [p.PropertyType],
        FieldInfo f => [f.FieldType],
        _ => [],
    };

    private static IEnumerable<Type> Flatten(Type type)
    {
        yield return type;
        if (type.HasElementType) foreach (var inner in Flatten(type.GetElementType()!)) yield return inner;
        if (type.IsGenericType) foreach (var arg in type.GetGenericArguments()) foreach (var inner in Flatten(arg)) yield return inner;
    }
}
