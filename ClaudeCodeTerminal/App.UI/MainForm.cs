using ClaudeCodeTerminal.App.Core;
using ClaudeCodeTerminal.App.Diff;

namespace ClaudeCodeTerminal.App.UI;

public sealed class MainForm : Form
{
    private readonly TabControl _sessionTabs;
    private readonly DiffPanelControl _diffPanel;
    private readonly ToolStripMenuItem _newSessionMenuItem;
    private readonly SplitContainer _split;

    private ProjectContext? _project;
    private ProjectFileWatcherService? _watcher;

    public MainForm()
    {
        Text = "Claude Code Terminal";
        Width = 1200;
        Height = 800;
        StartPosition = FormStartPosition.CenterScreen;

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

        _split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
        };

        _sessionTabs = new TabControl { Dock = DockStyle.Fill };
        _sessionTabs.SelectedIndexChanged += OnActiveTabChanged;

        _diffPanel = new DiffPanelControl { Dock = DockStyle.Fill };

        _split.Panel1.Controls.Add(_sessionTabs);
        _split.Panel2.Controls.Add(_diffPanel);

        Controls.Add(_split);
        Controls.Add(menuStrip);
        MainMenuStrip = menuStrip;

        Shown += (_, _) => _split.SplitterDistance = Math.Max(200, ClientSize.Width - 420);
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
    }

    private void CloseProject()
    {
        _watcher?.Dispose();
        _watcher = null;

        foreach (var page in _sessionTabs.TabPages.Cast<TabPage>().ToArray())
        {
            _sessionTabs.TabPages.Remove(page);
            page.Dispose();
        }

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

        var tabControl = new SessionTabControl { Dock = DockStyle.Fill };
        var page = new TabPage(picker.SelectedPreset.Name);
        page.Controls.Add(tabControl);

        tabControl.SessionExited += (_, _) => RemoveTab(page);

        _sessionTabs.TabPages.Add(page);
        _sessionTabs.SelectedTab = page;

        await tabControl.StartAsync(picker.SelectedPreset, _project);
        tabControl.RefreshDiff();
        RefreshDiffPanelIfActive(page);
    }

    private void RemoveTab(TabPage page)
    {
        _sessionTabs.TabPages.Remove(page);
        page.Dispose();
    }

    private void OnActiveTabChanged(object? sender, EventArgs e)
    {
        if (!TryGetActiveTab(out var tab))
        {
            _diffPanel.ShowChanges([]);
            return;
        }

        if (tab.DiffEngine is { IsStale: true })
            tab.RefreshDiff();

        _diffPanel.ShowChanges(tab.LastChanges);
    }

    private void OnProjectFilesChanged(object? sender, EventArgs e)
    {
        if (InvokeRequired)
        {
            BeginInvoke((MethodInvoker)(() => OnProjectFilesChanged(sender, e)));
            return;
        }

        var activePage = _sessionTabs.SelectedTab;

        foreach (TabPage page in _sessionTabs.TabPages)
        {
            if (page.Controls.Count == 0 || page.Controls[0] is not SessionTabControl tab)
                continue;

            if (page == activePage)
            {
                tab.RefreshDiff();
                _diffPanel.ShowChanges(tab.LastChanges);
            }
            else
            {
                tab.DiffEngine?.MarkStale();
            }
        }
    }

    private void RefreshDiffPanelIfActive(TabPage page)
    {
        if (_sessionTabs.SelectedTab != page)
            return;

        if (page.Controls.Count == 0 || page.Controls[0] is not SessionTabControl tab)
            return;

        _diffPanel.ShowChanges(tab.LastChanges);
    }

    private bool TryGetActiveTab(out SessionTabControl tab)
    {
        var page = _sessionTabs.SelectedTab;
        if (page is not null && page.Controls.Count > 0 && page.Controls[0] is SessionTabControl found)
        {
            tab = found;
            return true;
        }

        tab = null!;
        return false;
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        CloseProject();
        base.OnFormClosed(e);
    }
}
