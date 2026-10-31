namespace Shipping.Tests;

/// <summary>
/// A throw-away plugins directory. Plugins are COPIED from the staged build output (artifacts/), so a test can install
/// exactly the folders it wants and nothing locks the build output.
/// </summary>
internal sealed class Sandbox : IDisposable
{
    public string Root { get; } = Path.Combine(Path.GetTempPath(), "shipping-" + Guid.NewGuid().ToString("N"));

    public Sandbox() => Directory.CreateDirectory(Root);

    public static string SampleRoot()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (File.Exists(Path.Combine(dir.FullName, "Shipping.slnx"))) return dir.FullName;
        throw new DirectoryNotFoundException("Could not find Shipping.slnx above " + AppContext.BaseDirectory);
    }

    public static string Artifacts(string kind) => Path.Combine(SampleRoot(), "artifacts", kind);

    /// <summary>Installs plugins by copying their staged folders.</summary>
    public Sandbox With(params string[] names)
    {
        foreach (var name in names)
        {
            var source = new[] { "plugins", "fixtures" }.Select(k => Path.Combine(Artifacts(k), name)).First(Directory.Exists);
            CopyFolder(source, Path.Combine(Root, name));
        }
        return this;
    }

    private static void CopyFolder(string from, string to)
    {
        Directory.CreateDirectory(to);
        foreach (var file in Directory.GetFiles(from)) File.Copy(file, Path.Combine(to, Path.GetFileName(file)));
    }

    public void Dispose()
    {
        try { Directory.Delete(Root, recursive: true); }
        catch (IOException) { /* a context that is still unloading can hold a file for a moment; the temp folder is disposable */ }
        catch (UnauthorizedAccessException) { }
    }
}
