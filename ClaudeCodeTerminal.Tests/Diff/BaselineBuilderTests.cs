using ClaudeCodeTerminal.App.Diff;
using ClaudeCodeTerminal.Tests.TestSupport;
using LibGit2Sharp;

namespace ClaudeCodeTerminal.Tests.Diff;

public class BaselineBuilderTests
{
    [Fact]
    public void Build_WorksOnRepositoryWithNoCommits()
    {
        using var repo = new TempGitRepo();
        repo.WriteFile("new-file.txt", "hello");

        using var gitRepo = repo.OpenRepository();
        var baseline = BaselineBuilder.Build(gitRepo);

        Assert.NotNull(baseline.Tree["new-file.txt"]);
    }

    [Fact]
    public void Build_IncludesModifiedTrackedFileContent()
    {
        using var repo = new TempGitRepo();
        repo.WriteFile("tracked.txt", "original");
        repo.Commit();
        repo.WriteFile("tracked.txt", "changed");

        using var gitRepo = repo.OpenRepository();
        var baseline = BaselineBuilder.Build(gitRepo);

        var blob = (Blob)baseline.Tree["tracked.txt"].Target;
        Assert.Equal("changed", blob.GetContentText());
    }

    [Fact]
    public void Build_RemovesDeletedTrackedFiles()
    {
        using var repo = new TempGitRepo();
        repo.WriteFile("to-delete.txt", "bye");
        repo.Commit();
        File.Delete(Path.Combine(repo.Path, "to-delete.txt"));

        using var gitRepo = repo.OpenRepository();
        var baseline = BaselineBuilder.Build(gitRepo);

        Assert.Null(baseline.Tree["to-delete.txt"]);
    }

    [Fact]
    public void Build_IncludesUntrackedFiles()
    {
        using var repo = new TempGitRepo();
        repo.WriteFile("committed.txt", "a");
        repo.Commit();
        repo.WriteFile("scratch/untracked.txt", "untracked content");

        using var gitRepo = repo.OpenRepository();
        var baseline = BaselineBuilder.Build(gitRepo);

        var blob = (Blob)baseline.Tree["scratch/untracked.txt"].Target;
        Assert.Equal("untracked content", blob.GetContentText());
    }
}
