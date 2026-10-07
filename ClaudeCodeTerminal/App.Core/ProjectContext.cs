using LibGit2Sharp;

namespace ClaudeCodeTerminal.App.Core;

public sealed class ProjectContext
{
    public string RootPath { get; }
    public string DataFolderPath { get; }
    public PresetStore Presets { get; }

    private ProjectContext(string rootPath)
    {
        RootPath = rootPath;
        DataFolderPath = Path.Combine(rootPath, ".sain");
        Presets = new PresetStore(DataFolderPath);
    }

    public static bool TryOpen(string folderPath, out ProjectContext? context, out string? error)
    {
        if (!Directory.Exists(folderPath))
        {
            context = null;
            error = "That folder does not exist.";
            return false;
        }

        if (!Repository.IsValid(folderPath))
        {
            context = null;
            error = "This folder is not a git repository. Run 'git init' in it yourself, then try again.";
            return false;
        }

        var created = new ProjectContext(folderPath);
        Directory.CreateDirectory(created.DataFolderPath);
        GitignoreManager.EnsureIgnored(folderPath, ".sain/");
        created.Presets.EnsureLoaded();

        context = created;
        error = null;
        return true;
    }

    public Repository OpenRepository() => new(RootPath);
}
