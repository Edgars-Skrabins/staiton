namespace ClaudeCodeTerminal.Tests.TestSupport;

public sealed class TempDirectory : IDisposable
{
    public string Path { get; }

    public TempDirectory()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cct-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
    }

    public void Dispose()
    {
        ForceDelete(Path);
    }

    public static void ForceDelete(string path)
    {
        if (!Directory.Exists(path))
            return;

        foreach (var file in Directory.GetFiles(path, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);

        Directory.Delete(path, recursive: true);
    }
}
