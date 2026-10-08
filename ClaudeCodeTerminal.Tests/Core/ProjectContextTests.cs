using ClaudeCodeTerminal.App.Core;
using ClaudeCodeTerminal.Tests.TestSupport;

namespace ClaudeCodeTerminal.Tests.Core;

public class ProjectContextTests
{
    [Fact]
    public void TryOpen_FailsForNonGitFolder()
    {
        using var temp = new TempDirectory();

        var opened = ProjectContext.TryOpen(temp.Path, out var context, out var error);

        Assert.False(opened);
        Assert.Null(context);
        Assert.NotNull(error);
    }

    [Fact]
    public void TryOpen_FailsForMissingFolder()
    {
        var missingPath = Path.Combine(Path.GetTempPath(), "cct-missing-" + Guid.NewGuid().ToString("N"));

        var opened = ProjectContext.TryOpen(missingPath, out var context, out _);

        Assert.False(opened);
        Assert.Null(context);
    }

    [Fact]
    public void TryOpen_SucceedsAndCreatesDataFolderForGitRepo()
    {
        using var repo = new TempGitRepo();

        var opened = ProjectContext.TryOpen(repo.Path, out var context, out var error);

        Assert.True(opened);
        Assert.NotNull(context);
        Assert.Null(error);
        Assert.True(Directory.Exists(Path.Combine(repo.Path, ".sain")));
        Assert.Single(context!.Presets.Presets);
    }

    [Fact]
    public void TryOpen_AddsDataFolderToGitignore()
    {
        using var repo = new TempGitRepo();

        ProjectContext.TryOpen(repo.Path, out _, out _);

        var gitignore = File.ReadAllText(Path.Combine(repo.Path, ".gitignore"));
        Assert.Contains(".sain/", gitignore);
    }
}
