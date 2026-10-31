using System.Reflection;
using Clinic.Appointments.Adapters;
using Clinic.Appointments.Application;
using Clinic.Appointments.Domain;
using Clinic.Billing.Adapters;
using Clinic.Billing.Application;
using Clinic.Billing.Domain;
using Clinic.Patients.Adapters;
using Clinic.Patients.Application;
using Clinic.Patients.Domain;
using Clinic.SharedKernel;

namespace Clinic.Tests;

/// <summary>
/// Two ideas checked together. SCREAMING: the solution's folders name the clinic's capabilities.
/// CLEAN: inside each capability, source dependencies point inward, and the capabilities meet only at their outer edge.
/// </summary>
public class ArchitectureTests
{
    private sealed record Capability(string Name, Assembly Domain, Assembly Application, Assembly Adapters);

    private static readonly Capability[] Capabilities =
    [
        new("Patients", typeof(Patient).Assembly, typeof(RegisterPatient).Assembly, typeof(PatientsModule).Assembly),
        new("Appointments", typeof(Appointment).Assembly, typeof(BookAppointment).Assembly, typeof(AppointmentsModule).Assembly),
        new("Billing", typeof(Invoice).Assembly, typeof(ChargeLateCancellationFee).Assembly, typeof(BillingModule).Assembly),
    ];

    public static TheoryData<string> Names => new(Capabilities.Select(c => c.Name));

    private static Capability Get(string name) => Capabilities.Single(c => c.Name == name);

    private static readonly string Kernel = typeof(RuleViolationException).Assembly.GetName().Name!;

    private static string[] ClinicRefs(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(a => a.Name!).Where(n => n.StartsWith("Clinic.")).Order().ToArray();

    private static string Name(Assembly assembly) => assembly.GetName().Name!;

    // ================= CLEAN: the dependency rule =================

    [Theory]
    [MemberData(nameof(Names))]
    public void Entities_KnowNothingButTheSharedKernel(string name) =>
        Assert.All(ClinicRefs(Get(name).Domain), r => Assert.Equal(Kernel, r));

    [Theory]
    [MemberData(nameof(Names))]
    public void UseCases_KnowOnlyTheirOwnEntities_AndTheSharedKernel(string name)
    {
        var c = Get(name);

        Assert.All(ClinicRefs(c.Application), r => Assert.True(r == Name(c.Domain) || r == Kernel, $"{Name(c.Application)} references {r}"));
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void TheInnerCircles_NeverTouchTheWebFrameworkNorTheDiContainer(string name)
    {
        var c = Get(name);
        foreach (var inner in new[] { c.Domain, c.Application })
            Assert.DoesNotContain(inner.GetReferencedAssemblies().Select(a => a.Name!),
                n => n.StartsWith("Microsoft.AspNetCore") || n.StartsWith("Microsoft.Extensions.DependencyInjection"));
    }

    [Fact]
    public void TheAdaptersOfEachCapability_MeetOtherCapabilitiesOnlyThroughTheirApplicationLayer()
    {
        // Patients.Adapters: no other capability. Appointments.Adapters: Patients.Application. Billing.Adapters: Appointments.Application.
        string[] Others(Capability c) => ClinicRefs(c.Adapters).Where(r => !r.StartsWith($"Clinic.{c.Name}.") && r != Kernel).ToArray();

        Assert.Empty(Others(Get("Patients")));
        Assert.Equal([Name(Get("Patients").Application)], Others(Get("Appointments")));
        Assert.Equal([Name(Get("Appointments").Application)], Others(Get("Billing")));
    }

    [Fact]
    public void NoCapabilitysInnerCircles_ReferenceAnotherCapability()
    {
        foreach (var c in Capabilities)
            foreach (var inner in new[] { c.Domain, c.Application })
                Assert.DoesNotContain(ClinicRefs(inner),
                    r => Capabilities.Any(other => other.Name != c.Name && r.StartsWith($"Clinic.{other.Name}.")));
    }

    [Fact]
    public void TheCapabilityGraph_HasOneDirection_BillingToAppointmentsToPatients()
    {
        bool Uses(string from, string to) => ClinicRefs(Get(from).Adapters).Any(r => r.StartsWith($"Clinic.{to}."));

        Assert.True(Uses("Billing", "Appointments"));
        Assert.True(Uses("Appointments", "Patients"));
        Assert.False(Uses("Patients", "Appointments"));
        Assert.False(Uses("Patients", "Billing"));
        Assert.False(Uses("Appointments", "Billing"));
    }

    [Fact]
    public void ThePortsAreImplemented_OnlyByAdapters()
    {
        var ports = Capabilities.SelectMany(c => c.Application.GetExportedTypes()).Where(t => t.IsInterface).ToList();
        var allAssemblies = Capabilities.SelectMany(c => new[] { c.Domain, c.Application, c.Adapters });

        var implementers = allAssemblies.SelectMany(a => a.GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.GetInterfaces().Any(ports.Contains))
            .ToList();

        Assert.NotEmpty(implementers);
        Assert.All(implementers, t => Assert.EndsWith(".Adapters", Name(t.Assembly)));
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void Entities_HaveNoPublicConstructorsNorSetters(string name)
    {
        var entities = Get(name).Domain.GetExportedTypes().Where(t => t is { IsClass: true } && !typeof(Exception).IsAssignableFrom(t));

        Assert.NotEmpty(entities);
        Assert.All(entities, t =>
        {
            Assert.Empty(t.GetConstructors());
            Assert.All(t.GetProperties(), p => Assert.True(p.SetMethod is null || !p.SetMethod.IsPublic, $"{t.Name}.{p.Name}"));
        });
    }

    [Theory]
    [MemberData(nameof(Names))]
    public void WhatCrossesTheBoundary_IsASimpleDto_NeverAnEntity(string name)
    {
        var c = Get(name);
        var entityTypes = c.Domain.GetExportedTypes().Where(t => t.IsClass && !typeof(Exception).IsAssignableFrom(t)).ToHashSet();
        var useCases = c.Application.GetExportedTypes().Where(t => t.IsClass && t.IsSealed && !t.Name.EndsWith("Dto"));

        var leaks = useCases
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(m => !m.IsSpecialName)
            .SelectMany(m => m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType))
            .Where(t => entityTypes.Contains(t) || (t.IsGenericType && t.GetGenericArguments().Any(entityTypes.Contains)))
            .Select(t => t.Name)
            .ToList();

        Assert.Empty(leaks);
    }

    [Fact]
    public void TheSharedKernel_ContainsNothingButTheTwoFailures()
    {
        var types = typeof(RuleViolationException).Assembly.GetExportedTypes().Select(t => t.Name).Order();

        Assert.Equal(["NotFoundException", "RuleViolationException"], types);
    }

    [Fact]
    public void TheHost_IsTheCompositionRoot_ItKnowsTheAdaptersAndNothingInside()
    {
        var host = typeof(Program).Assembly;

        Assert.Equal(
            Capabilities.Select(c => Name(c.Adapters)).Append(Kernel).Order().ToArray(),
            ClinicRefs(host));
    }

    // ================= SCREAMING: what the folders say =================

    private static readonly string[] TechnicalWords =
        ["controllers", "services", "repositories", "models", "entities", "dtos", "helpers", "utils", "utilities", "managers", "domain", "application", "infrastructure"];

    private static string SrcRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "src");
            if (Directory.Exists(Path.Combine(candidate, "Clinic.Host"))) return candidate;
        }
        throw new DirectoryNotFoundException("Could not find the src folder.");
    }

    private static string[] SubFolders(string path) =>
        Directory.GetDirectories(path).Select(d => Path.GetFileName(d)!).Where(n => n is not ("bin" or "obj" or "Properties")).Order().ToArray();

    [Fact]
    public void OpeningTheSolutionsSourceFolder_YouReadTheClinicsCapabilities() =>
        Assert.Equal(["Appointments", "Billing", "Clinic.Host", "Clinic.SharedKernel", "Patients"], SubFolders(SrcRoot()));

    [Fact]
    public void NoTopLevelFolder_IsNamedAfterATechnicalRoleOrALayer() =>
        Assert.DoesNotContain(SubFolders(SrcRoot()), n => TechnicalWords.Contains(n.ToLowerInvariant()));

    [Theory]
    [MemberData(nameof(Names))]
    public void InsideACapability_TheLayersAreTheCleanCircles(string name) =>
        Assert.Equal(
            [$"Clinic.{name}.Adapters", $"Clinic.{name}.Application", $"Clinic.{name}.Domain"],
            SubFolders(Path.Combine(SrcRoot(), name)));
}
