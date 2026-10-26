using System.Text.RegularExpressions;
using Clinic.Patients;

namespace Clinic.Tests;

/// <summary>
/// "What does the architecture scream?" turned into checks. They read the project's folders and files from disk,
/// then the SAME checks are pointed at the bad example (Clinic.Technical) to prove they can tell the difference.
/// Naming rules are heuristics: a word list is not a mind. They catch the common failure, not every possible one.
/// </summary>
public partial class ScreamingTests
{
    private static readonly string[] TechnicalWords =
    [
        "controllers", "services", "repositories", "models", "entities", "dtos", "helpers", "utils", "utilities",
        "managers", "mappers", "viewmodels", "views", "handlers",
    ];

    private static readonly string[] IgnoredFolders = ["bin", "obj", "Properties"];

    private static string ProjectRoot(string project)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "src", project);
            if (Directory.Exists(candidate)) return candidate;
        }
        throw new DirectoryNotFoundException($"Could not find src/{project}.");
    }

    private static string[] TopFolders(string project) =>
        Directory.GetDirectories(ProjectRoot(project)).Select(d => Path.GetFileName(d)!)
            .Where(n => !IgnoredFolders.Contains(n)).Order().ToArray();

    private static IEnumerable<string> TechnicalFolders(string project) =>
        TopFolders(project).Where(n => TechnicalWords.Contains(n.ToLowerInvariant()));

    private static IEnumerable<string> SourceFiles(string folder) =>
        Directory.EnumerateFiles(folder, "*.cs", SearchOption.AllDirectories)
            .Where(f => !f.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") &&
                        !f.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"));

    private static readonly string[] Capabilities = ["Appointments", "Billing", "Patients"];

    [GeneratedRegex(@"^\s*using\s+Clinic\.(\w+)", RegexOptions.Multiline)]
    private static partial Regex UsingPattern();

    [GeneratedRegex(@"^\s*namespace\s+([\w.]+)\s*;", RegexOptions.Multiline)]
    private static partial Regex NamespacePattern();

    // ---------- 1. open the folder: what does it say? ----------

    [Fact]
    public void OpeningTheProjectFolder_YouReadTheClinicsCapabilities() =>
        Assert.Equal(Capabilities, TopFolders("Clinic"));

    [Fact]
    public void TheProjectFolder_ContainsNoTechnicalRoleFolders() =>
        Assert.Empty(TechnicalFolders("Clinic"));

    [Fact]
    public void TheBadExample_IsRecognizedAsFrameworkShaped()
    {
        var technical = TechnicalFolders("Clinic.Technical").Order().ToArray();

        Assert.Equal(["Controllers", "Models", "Repositories", "Services"], technical);
        Assert.Equal(technical, TopFolders("Clinic.Technical"));       // ...and it has nothing else: nothing says "clinic"
    }

    // ---------- 2. each capability is named in code, too ----------

    [Fact]
    public void EveryFileInACapabilityFolder_LivesInThatCapabilitysNamespace()
    {
        foreach (var capability in Capabilities)
        {
            foreach (var file in SourceFiles(Path.Combine(ProjectRoot("Clinic"), capability)))
            {
                var ns = NamespacePattern().Match(File.ReadAllText(file)).Groups[1].Value;
                Assert.True(ns == $"Clinic.{capability}", $"{Path.GetFileName(file)} is in '{ns}', expected 'Clinic.{capability}'");
            }
        }
    }

    [Fact]
    public void EveryCapability_HasUseCasesNamedAfterWhatTheyDo()
    {
        var verbs = new[] { "Register", "Get", "Book", "Cancel", "List", "Charge" };
        foreach (var capability in Capabilities)
        {
            var files = SourceFiles(Path.Combine(ProjectRoot("Clinic"), capability)).Select(Path.GetFileNameWithoutExtension);
            Assert.Contains(files, f => verbs.Any(v => f!.StartsWith(v)));
        }
    }

    // ---------- 3. capabilities depend on each other in one direction only ----------

    private static string[] CapabilitiesUsedBy(string capability) =>
        SourceFiles(Path.Combine(ProjectRoot("Clinic"), capability))
            .SelectMany(f => UsingPattern().Matches(File.ReadAllText(f)).Select(m => m.Groups[1].Value))
            .Where(Capabilities.Contains).Distinct().Order().ToArray();

    [Fact]
    public void Patients_DoesNotDependOnAnyOtherCapability() =>
        Assert.Empty(CapabilitiesUsedBy("Patients"));

    [Fact]
    public void Appointments_DependsOnlyOnPatients() =>
        Assert.Equal(["Patients"], CapabilitiesUsedBy("Appointments"));

    [Fact]
    public void Billing_DependsOnlyOnAppointments_ByImplementingWhatAppointmentsAskedFor() =>
        Assert.Equal(["Appointments"], CapabilitiesUsedBy("Billing"));

    // ---------- 4. the framework stays at the edge ----------

    [Fact]
    public void TheWebFramework_AppearsOnlyInEndpointsSetupAndProgramFiles()
    {
        var offenders = SourceFiles(ProjectRoot("Clinic"))
            .Where(f => File.ReadAllText(f) is var text &&
                        (text.Contains("Microsoft.AspNetCore") || text.Contains("IEndpointRouteBuilder") || text.Contains("HttpContext") || text.Contains("Results.")))
            .Select(Path.GetFileName)
            .Where(n => !n!.EndsWith("Endpoints.cs") && n != "Program.cs" && n != "ErrorHandling.cs")
            .ToList();

        Assert.Empty(offenders);
    }

    [Fact]
    public void TheUseCases_NeverMentionTheWebFramework()
    {
        var useCaseFiles = Capabilities
            .SelectMany(c => SourceFiles(Path.Combine(ProjectRoot("Clinic"), c)))
            .Where(f => !f.EndsWith("Endpoints.cs"));

        Assert.All(useCaseFiles, f =>
        {
            var text = File.ReadAllText(f);
            Assert.DoesNotContain("AspNetCore", text);
            Assert.DoesNotContain("HttpContext", text);
            Assert.DoesNotContain("IResult", text);
        });
    }

    [Fact]
    public void TheUseCaseClasses_ArePlainClasses_ThatNeedNoWebServerToRun()
    {
        var useCases = new[] { typeof(RegisterPatient), typeof(GetPatient), typeof(Appointments.BookAppointment), typeof(Appointments.CancelAppointment), typeof(Appointments.ListAppointments), typeof(Billing.ListInvoices) };

        Assert.All(useCases, t =>
        {
            var used = t.GetConstructors().SelectMany(c => c.GetParameters().Select(p => p.ParameterType))
                .Concat(t.GetMethods().Where(m => m.DeclaringType == t).SelectMany(m => m.GetParameters().Select(p => p.ParameterType).Append(m.ReturnType)));
            Assert.DoesNotContain(used, u => u.Namespace?.StartsWith("Microsoft.AspNetCore") == true);
        });
    }
}
