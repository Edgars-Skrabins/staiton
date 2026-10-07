namespace ClaudeCodeTerminal.App.Diff;

public enum FileChangeKind
{
    Added,
    Modified,
    Deleted,
}

public sealed record FileChange(string Path, FileChangeKind Kind, string DiffText);
