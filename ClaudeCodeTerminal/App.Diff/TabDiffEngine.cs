using ClaudeCodeTerminal.App.Core;
using LibGit2Sharp;

namespace ClaudeCodeTerminal.App.Diff;

public sealed class TabDiffEngine : IDisposable
{
    private readonly Repository _repository;
    private TabBaseline? _baseline;

    public bool IsStale { get; private set; }

    public TabDiffEngine(ProjectContext project)
    {
        _repository = project.OpenRepository();
    }

    public void BuildBaseline()
    {
        _baseline = BaselineBuilder.Build(_repository);
        IsStale = false;
    }

    public void MarkStale()
    {
        IsStale = true;
    }

    public IReadOnlyList<FileChange> Recompute()
    {
        if (_baseline is null)
            BuildBaseline();

        var patch = _repository.Diff.Compare<Patch>(_baseline!.Tree, DiffTargets.WorkingDirectory);

        var changes = new List<FileChange>();
        foreach (var entry in patch)
        {
            var kind = entry.Status switch
            {
                ChangeKind.Added => FileChangeKind.Added,
                ChangeKind.Untracked => FileChangeKind.Added,
                ChangeKind.Deleted => FileChangeKind.Deleted,
                _ => FileChangeKind.Modified,
            };

            changes.Add(new FileChange(entry.Path, kind, entry.Patch));
        }

        IsStale = false;
        return changes;
    }

    public void Dispose()
    {
        _repository.Dispose();
    }
}
