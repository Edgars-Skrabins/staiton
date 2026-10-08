using ClaudeCodeTerminal.App.Core;
using ClaudeCodeTerminal.App.Diff;
using ClaudeCodeTerminal.Tests.TestSupport;

namespace ClaudeCodeTerminal.Tests.Diff;

public class TabDiffEngineTests
{
    private static ProjectContext OpenProject(TempGitRepo repo)
    {
        var opened = ProjectContext.TryOpen(repo.Path, out var project, out var error);
        Assert.True(opened, error);
        return project!;
    }

    [Fact]
    public void Recompute_DetectsModifiedFile()
    {
        using var repo = new TempGitRepo();
        repo.WriteFile("file.txt", "original\n");
        repo.Commit();

        var project = OpenProject(repo);
        using var engine = new TabDiffEngine(project);
        engine.BuildBaseline();

        repo.WriteFile("file.txt", "changed\n");

        var changes = engine.Recompute();

        var change = Assert.Single(changes);
        Assert.Equal("file.txt", change.Path);
        Assert.Equal(FileChangeKind.Modified, change.Kind);
        Assert.Contains("changed", change.DiffText);
    }

    [Fact]
    public void Recompute_DetectsBrandNewUntrackedFile()
    {
        using var repo = new TempGitRepo();
        repo.WriteFile("existing.txt", "hi");
        repo.Commit();

        var project = OpenProject(repo);
        using var engine = new TabDiffEngine(project);
        engine.BuildBaseline();

        repo.WriteFile("brand-new.txt", "fresh content");

        var changes = engine.Recompute();

        var change = Assert.Single(changes);
        Assert.Equal("brand-new.txt", change.Path);
        Assert.Equal(FileChangeKind.Added, change.Kind);
    }

    [Fact]
    public void Recompute_DetectsDeletedFile()
    {
        using var repo = new TempGitRepo();
        repo.WriteFile("doomed.txt", "goodbye");
        repo.Commit();

        var project = OpenProject(repo);
        using var engine = new TabDiffEngine(project);
        engine.BuildBaseline();

        File.Delete(Path.Combine(repo.Path, "doomed.txt"));

        var changes = engine.Recompute();

        var change = Assert.Single(changes);
        Assert.Equal("doomed.txt", change.Path);
        Assert.Equal(FileChangeKind.Deleted, change.Kind);
    }

    [Fact]
    public void Recompute_IgnoresUntrackedFileThatAlreadyExistedAtBaseline()
    {
        using var repo = new TempGitRepo();
        repo.WriteFile("already-there.txt", "content");

        var project = OpenProject(repo);
        using var engine = new TabDiffEngine(project);
        engine.BuildBaseline();

        var changes = engine.Recompute();

        Assert.Empty(changes);
    }

    [Fact]
    public void Recompute_ReturnsNoChangesImmediatelyAfterBaseline()
    {
        using var repo = new TempGitRepo();
        repo.WriteFile("file.txt", "content\n");
        repo.Commit();

        var project = OpenProject(repo);
        using var engine = new TabDiffEngine(project);
        engine.BuildBaseline();

        var changes = engine.Recompute();

        Assert.Empty(changes);
    }

    [Fact]
    public void TwoTabs_HaveIndependentBaselines()
    {
        using var repo = new TempGitRepo();
        repo.WriteFile("shared.txt", "v1\n");
        repo.Commit();

        var project = OpenProject(repo);

        using var tabA = new TabDiffEngine(project);
        tabA.BuildBaseline();

        repo.WriteFile("shared.txt", "v2\n");

        using var tabB = new TabDiffEngine(project);
        tabB.BuildBaseline();

        repo.WriteFile("shared.txt", "v3\n");

        var changesA = tabA.Recompute();
        var changesB = tabB.Recompute();

        var changeA = Assert.Single(changesA);
        var changeB = Assert.Single(changesB);
        Assert.Contains("v3", changeA.DiffText);
        Assert.Contains("v3", changeB.DiffText);
        Assert.DoesNotContain("v1", changeB.DiffText);
    }
}
