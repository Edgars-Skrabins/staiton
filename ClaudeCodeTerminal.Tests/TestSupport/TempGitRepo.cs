using LibGit2Sharp;

namespace ClaudeCodeTerminal.Tests.TestSupport;

public sealed class TempGitRepo : IDisposable
{
    public string Path { get; }

    public TempGitRepo()
    {
        Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "cct-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path);
        Repository.Init(Path);
    }

    public string WriteFile(string relativePath, string content)
    {
        var fullPath = System.IO.Path.Combine(Path, relativePath);
        var directory = System.IO.Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
            Directory.CreateDirectory(directory);

        File.WriteAllText(fullPath, content);
        return fullPath;
    }

    public void Commit(string message = "test commit")
    {
        using var repo = new Repository(Path);
        Commands.Stage(repo, "*");
        var signature = new Signature("Test", "test@example.com", DateTimeOffset.Now);
        repo.Commit(message, signature, signature);
    }

    public Repository OpenRepository() => new(Path);

    public void Dispose()
    {
        TempDirectory.ForceDelete(Path);
    }
}
