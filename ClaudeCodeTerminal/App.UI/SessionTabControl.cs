using ClaudeCodeTerminal.App.Core;
using ClaudeCodeTerminal.App.Diff;
using ClaudeCodeTerminal.App.Pty;
using ClaudeCodeTerminal.App.Terminal;

namespace ClaudeCodeTerminal.App.UI;

public sealed class SessionTabControl : UserControl
{
    private readonly SplitContainer _outerSplit;
    private bool _splittersInitialized;

    public TerminalRenderControl Terminal { get; }
    public DiffPanelControl DiffPanel { get; }
    public PtySession? Session { get; private set; }
    public TabDiffEngine? DiffEngine { get; private set; }
    public IReadOnlyList<FileChange> LastChanges { get; private set; } = Array.Empty<FileChange>();

    private TerminalGridSnapshot? _grid;

    public event EventHandler? SessionExited;

    public SessionTabControl()
    {
        BackColor = Theme.Background;

        Terminal = new TerminalRenderControl { Dock = DockStyle.Fill };
        Terminal.TerminalSizeChanged += OnTerminalSizeChanged;

        DiffPanel = new DiffPanelControl { Dock = DockStyle.Fill };

        _outerSplit = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 1,
        };
        Theme.StyleSplitContainer(_outerSplit);
        _outerSplit.Panel1.Controls.Add(Terminal);
        _outerSplit.Panel2.Controls.Add(DiffPanel);

        Controls.Add(_outerSplit);

        Layout += OnFirstLayout;
    }

    private void OnFirstLayout(object? sender, LayoutEventArgs e)
    {
        if (_splittersInitialized || Width < 300)
            return;

        var target = (int)(Width * 0.42);
        _outerSplit.SplitterDistance = Math.Clamp(target, 100, Math.Max(100, Width - 100));
        _splittersInitialized = true;
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
        Session.BeginReading();

        DiffEngine = new TabDiffEngine(project);
        DiffEngine.BuildBaseline();
    }

    public void RefreshDiff()
    {
        if (DiffEngine is null)
            return;

        LastChanges = DiffEngine.Recompute();
        DiffPanel.ShowChanges(LastChanges);
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
