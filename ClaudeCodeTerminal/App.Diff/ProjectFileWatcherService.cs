using ClaudeCodeTerminal.App.Core;
using LibGit2Sharp;

namespace ClaudeCodeTerminal.App.Diff;

public sealed class ProjectFileWatcherService : IDisposable
{
    private readonly FileSystemWatcher _watcher;
    private readonly System.Threading.Timer _debounceTimer;
    private readonly Repository _ignoreCheckRepository;
    private readonly string _gitDirPrefix;
    private readonly string _dataDirPrefix;
    private volatile bool _pendingOverflow;

    public event EventHandler? Changed;
    public event EventHandler? Overflowed;

    public ProjectFileWatcherService(ProjectContext project)
    {
        _ignoreCheckRepository = project.OpenRepository();
        _gitDirPrefix = Path.Combine(project.RootPath, ".git") + Path.DirectorySeparatorChar;
        _dataDirPrefix = project.DataFolderPath + Path.DirectorySeparatorChar;

        _debounceTimer = new System.Threading.Timer(OnDebounceElapsed, null, Timeout.Infinite, Timeout.Infinite);

        _watcher = new FileSystemWatcher(project.RootPath)
        {
            IncludeSubdirectories = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
            InternalBufferSize = 64 * 1024,
        };

        _watcher.Changed += OnFsEvent;
        _watcher.Created += OnFsEvent;
        _watcher.Deleted += OnFsEvent;
        _watcher.Renamed += OnFsEvent;
        _watcher.Error += OnError;

        _watcher.EnableRaisingEvents = true;
    }

    private void OnFsEvent(object sender, FileSystemEventArgs e)
    {
        if (ShouldIgnore(e.FullPath))
            return;

        _debounceTimer.Change(200, Timeout.Infinite);
    }

    private void OnError(object sender, ErrorEventArgs e)
    {
        _pendingOverflow = true;
        _debounceTimer.Change(200, Timeout.Infinite);
    }

    private bool ShouldIgnore(string fullPath)
    {
        if (fullPath.StartsWith(_gitDirPrefix, StringComparison.OrdinalIgnoreCase))
            return true;

        if (fullPath.StartsWith(_dataDirPrefix, StringComparison.OrdinalIgnoreCase))
            return true;

        try
        {
            var relative = Path.GetRelativePath(_ignoreCheckRepository.Info.WorkingDirectory, fullPath);
            return _ignoreCheckRepository.Ignore.IsPathIgnored(relative);
        }
        catch (LibGit2SharpException)
        {
            return false;
        }
    }

    private void OnDebounceElapsed(object? state)
    {
        var overflowed = _pendingOverflow;
        _pendingOverflow = false;

        if (overflowed)
            Overflowed?.Invoke(this, EventArgs.Empty);
        else
            Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Dispose()
    {
        _watcher.Dispose();
        _debounceTimer.Dispose();
        _ignoreCheckRepository.Dispose();
    }
}
