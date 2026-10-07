using LibGit2Sharp;

namespace ClaudeCodeTerminal.App.Diff;

public sealed class TabBaseline
{
    public required Tree Tree { get; init; }
}

public static class BaselineBuilder
{
    public static TabBaseline Build(Repository repo)
    {
        var status = repo.RetrieveStatus();

        var treeDefinition = repo.Head.Tip is { } tip
            ? TreeDefinition.From(tip)
            : new TreeDefinition();

        var workingDirectory = repo.Info.WorkingDirectory;

        foreach (var entry in status.Added.Concat(status.Staged).Concat(status.Modified).Concat(status.Untracked))
        {
            var absolutePath = Path.Combine(workingDirectory, entry.FilePath);
            try
            {
                var blob = repo.ObjectDatabase.CreateBlob(absolutePath);
                treeDefinition.Add(entry.FilePath, blob, Mode.NonExecutableFile);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
            catch (LibGit2SharpException)
            {
            }
        }

        foreach (var entry in status.Removed.Concat(status.Missing))
        {
            try
            {
                treeDefinition.Remove(entry.FilePath);
            }
            catch (LibGit2SharpException)
            {
            }
        }

        var tree = repo.ObjectDatabase.CreateTree(treeDefinition);
        return new TabBaseline { Tree = tree };
    }
}
