using ClaudeCodeTerminal.App.Core;
using ClaudeCodeTerminal.App.Diff;
using ClaudeCodeTerminal.App.Pty;
using ClaudeCodeTerminal.App.Terminal;

namespace ClaudeCodeTerminal.App.UI;

public sealed class SessionTabControl : UserControl
{
    public TerminalRenderControl Terminal { get; }
    public PtySession? Session { get; private set; }
    public TabDiffEngine? DiffEngine { get; private set; }
    public IReadOnlyList<FileChange> LastChanges { get; private set; } = Array.Empty<FileChange>();

    private TerminalGridSnapshot? _grid;

    public event EventHandler? SessionExited;

    public SessionTabControl()
    {
        Terminal = new TerminalRenderControl { Dock = DockStyle.Fill };
        Terminal.TerminalSizeChanged += OnTerminalSizeChanged;
        Controls.Add(Terminal);
    }

    public async Task StartAsync(Preset preset, ProjectContext project)
    {
        var (rows, cols) = Terminal.CurrentGridSize;

        _grid = new TerminalGridSnapshot(rows, cols);
        Terminal.AttachGrid(_grid);

        Session = await PtySession.StartAsync(preset.Executable, preset.Args, project.RootPath, rows, cols);
        _grid.SendData += (_, data) => Session.Write(data);
        Session.DataReceived += (_, data) => _grid.Feed(data.Span);
        Session.ProcessExited += OnProcessExited;

        DiffEngine = new TabDiffEngine(project);
        DiffEngine.BuildBaseline();
    }

    public void RefreshDiff()
    {
        if (DiffEngine is null)
            return;

        LastChanges = DiffEngine.Recompute();
    }

    private void OnTerminalSizeChanged(object? sender, (int Rows, int Cols) size)
    {
        Session?.Resize(size.Rows, size.Cols);
    }

    private void OnProcessExited(object? sender, int exitCode)
    {
        if (InvokeRequired)
            BeginInvoke((MethodInvoker)(() => SessionExited?.Invoke(this, EventArgs.Empty)));
        else
            SessionExited?.Invoke(this, EventArgs.Empty);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            if (Session is not null)
                Session.ProcessExited -= OnProcessExited;
            Session?.Dispose();
            DiffEngine?.Dispose();
        }

        base.Dispose(disposing);
    }
}
