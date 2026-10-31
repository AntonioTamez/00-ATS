using System.Text.RegularExpressions;

namespace Helpdesk.Tests;

/// <summary>
/// Executable rule of vertical slices: minimize coupling between slices, maximize coupling inside one.
/// A slice may use Common, but never another slice; Common never depends on a slice
/// (except the single registration file that wires them).
/// </summary>
public class SliceIsolationTests
{
    private static readonly string[] Slices = ["OpenTicket", "AssignTicket", "CloseTicket", "GetTicket", "ListTickets"];

    private static string ApiRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "Helpdesk.Api", "Features")))
            dir = dir.Parent;
        return dir is null
            ? throw new InvalidOperationException("Could not locate src/Helpdesk.Api from the test output directory.")
            : Path.Combine(dir.FullName, "src", "Helpdesk.Api");
    }

    [Fact]
    public void AllExpectedSlicesExist_AndNoUnexpectedOnes()
    {
        var folders = Directory.GetDirectories(Path.Combine(ApiRoot(), "Features")).Select(Path.GetFileName);

        Assert.Equal(Slices.Order(), folders.Order());
    }

    [Theory]
    [InlineData("OpenTicket")]
    [InlineData("AssignTicket")]
    [InlineData("CloseTicket")]
    [InlineData("GetTicket")]
    [InlineData("ListTickets")]
    public void Slice_DoesNotReferenceAnyOtherSlice(string slice)
    {
        var files = Directory.GetFiles(Path.Combine(ApiRoot(), "Features", slice), "*.cs", SearchOption.AllDirectories);
        Assert.NotEmpty(files);

        foreach (var other in Slices.Where(s => s != slice))
            foreach (var file in files)
                Assert.False(Regex.IsMatch(File.ReadAllText(file), $@"\b{other}\b"),
                    $"{Path.GetFileName(file)} (slice {slice}) references slice {other}.");
    }

    [Fact]
    public void Common_DoesNotDependOnSlices_ExceptTheRegistration()
    {
        var files = Directory.GetFiles(Path.Combine(ApiRoot(), "Common"), "*.cs")
            .Where(f => Path.GetFileName(f) != "FeatureRegistration.cs");

        foreach (var file in files)
            Assert.DoesNotContain("Helpdesk.Api.Features", File.ReadAllText(file));
    }
}
