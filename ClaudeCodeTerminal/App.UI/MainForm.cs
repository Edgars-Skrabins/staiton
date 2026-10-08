using ClaudeCodeTerminal.App.Core;
using ClaudeCodeTerminal.App.Diff;

namespace ClaudeCodeTerminal.App.UI;

public sealed class MainForm : Form
{
    private readonly TabStrip _tabStrip;
    private readonly Panel _tabContentHost;
    private readonly Panel _emptyStatePanel;
    private readonly ToolStripMenuItem _newSessionMenuItem;
    private readonly Dictionary<TabStripItem, SessionTabControl> _sessions = [];

    private ProjectContext? _project;
    private ProjectFileWatcherService? _watcher;
    private int _sessionCounter;

    public MainForm()
    {
        Text = "Claude Code Terminal";
        Width = 1280;
        Height = 820;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Theme.Background;
        Theme.ApplyDarkTitleBar(this);

        var openProjectMenuItem = new ToolStripMenuItem("Open Project...", null, OnOpenProjectClicked);
        _newSessionMenuItem = new ToolStripMenuItem("New Session", null, OnNewSessionClicked) { Enabled = false };
        var exitMenuItem = new ToolStripMenuItem("Exit", null, (_, _) => Close());

        var fileMenu = new ToolStripMenuItem("File");
        fileMenu.DropDownItems.Add(openProjectMenuItem);
        fileMenu.DropDownItems.Add(_newSessionMenuItem);
        fileMenu.DropDownItems.Add(new ToolStripSeparator());
        fileMenu.DropDownItems.Add(exitMenuItem);

        var menuStrip = new MenuStrip();
        menuStrip.Items.Add(fileMenu);
        Theme.StyleMenuStrip(menuStrip);

        _tabStrip = new TabStrip { Visible = false };
        _tabStrip.AddTabRequested += OnNewSessionClicked;
        _tabStrip.TabActivated += OnTabActivated;
        _tabStrip.TabCloseRequested += (_, item) => CloseSession(item);

        _tabContentHost = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.Background,
            Visible = false,
        };

        _emptyStatePanel = BuildEmptyStatePanel();

        Controls.Add(_tabContentHost);
        Controls.Add(_tabStrip);
        Controls.Add(_emptyStatePanel);
        Controls.Add(menuStrip);
        MainMenuStrip = menuStrip;
    }

    private Panel BuildEmptyStatePanel()
    {
        var panel = new Panel { Dock = DockStyle.Fill, BackColor = Theme.Background };

        var titleLabel = new Label
        {
            Text = "Claude Code Terminal",
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Theme.Text,
            Font = new Font(Theme.UiFont.FontFamily, 18f, FontStyle.Bold),
            Size = new Size(420, 40),
            Anchor = AnchorStyles.None,
        };

        var openButton = new ModernButton
        {
            Text = "Open Project",
            AutoSize = false,
            Size = new Size(170, 38),
            Font = Theme.UiFont,
            Anchor = AnchorStyles.None,
        };
        Theme.StyleButton(openButton, primary: true);
        openButton.Click += OnOpenProjectClicked;

        var hintLabel = new Label
        {
            Text = "Select a folder that's already a git repository to get started.",
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            ForeColor = Theme.SubtleText,
            Font = Theme.UiFont,
            Size = new Size(380, 24),
            Anchor = AnchorStyles.None,
        };

        panel.Controls.Add(titleLabel);
        panel.Controls.Add(openButton);
        panel.Controls.Add(hintLabel);

        panel.Layout += (_, _) =>
        {
            titleLabel.Location = new Point((panel.Width - titleLabel.Width) / 2, (panel.Height - openButton.Height) / 2 - 56);
            openButton.Location = new Point((panel.Width - openButton.Width) / 2, (panel.Height - openButton.Height) / 2);
            hintLabel.Location = new Point((panel.Width - hintLabel.Width) / 2, openButton.Bottom + 14);
        };

        return panel;
    }

    private void OnOpenProjectClicked(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "Select a project folder (it must already be a git repository)",
        };

        if (dialog.ShowDialog(this) != DialogResult.OK)
            return;

        if (!ProjectContext.TryOpen(dialog.SelectedPath, out var context, out var error))
        {
            MessageBox.Show(this, error, "Cannot open project", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        CloseProject();

        _project = context;
        _watcher = new ProjectFileWatcherService(_project!);
        _watcher.Changed += OnProjectFilesChanged;
        _watcher.Overflowed += OnProjectFilesChanged;

        Text = $"Claude Code Terminal - {_project!.RootPath}";
        _newSessionMenuItem.Enabled = true;

        _emptyStatePanel.Visible = false;
        _tabStrip.Visible = true;
        _tabContentHost.Visible = true;
    }

    private void CloseProject()
    {
        _watcher?.Dispose();
        _watcher = null;

        foreach (var item in _tabStrip.Items.ToArray())
            CloseSession(item);

        _sessionCounter = 0;
        _project = null;
        _newSessionMenuItem.Enabled = false;
    }

    private async void OnNewSessionClicked(object? sender, EventArgs e)
    {
        if (_project is null)
            return;

        using var picker = new PresetPickerDialog(_project.Presets.Presets);
        if (picker.ShowDialog(this) != DialogResult.OK || picker.SelectedPreset is null)
            return;

        _sessionCounter++;
        var tabControl = new SessionTabControl { Dock = DockStyle.Fill, Visible = false };
        _tabContentHost.Controls.Add(tabControl);

        var item = _tabStrip.AddTab($"Session {_sessionCounter}");
        _sessions[item] = tabControl;
        tabControl.SessionExited += (_, _) => CloseSession(item);

        _tabStrip.Activate(item);

        await tabControl.StartAsync(picker.SelectedPreset, _project);
        tabControl.RefreshDiff();
        tabControl.Terminal.Focus();
    }

    private void CloseSession(TabStripItem item)
    {
        if (!_sessions.Remove(item, out var tabControl))
            return;

        _tabStrip.RemoveTab(item);
        _tabContentHost.Controls.Remove(tabControl);
        tabControl.Dispose();
    }

    private void OnTabActivated(object? sender, TabStripItem item)
    {
        foreach (var (otherItem, otherTab) in _sessions)
            otherTab.Visible = otherItem == item;

        if (!_sessions.TryGetValue(item, out var tab))
            return;

        if (tab.DiffEngine is { IsStale: true })
            tab.RefreshDiff();

        tab.Terminal.Focus();
    }

    private void OnProjectFilesChanged(object? sender, EventArgs e)
    {
        if (InvokeRequired)
        {
            BeginInvoke((MethodInvoker)(() => OnProjectFilesChanged(sender, e)));
            return;
        }

        foreach (var tab in _sessions.Values)
        {
            if (tab.Visible)
                tab.RefreshDiff();
            else
                tab.DiffEngine?.MarkStale();
        }
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        CloseProject();
        base.OnFormClosed(e);
    }
}
