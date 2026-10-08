using ClaudeCodeTerminal.App.Core;
using ClaudeCodeTerminal.App.Diff;

namespace ClaudeCodeTerminal.App.UI;

public sealed class MainForm : Form
{
    private readonly TabControl _sessionTabs;
    private readonly Panel _emptyStatePanel;
    private readonly Panel _toolbar;
    private readonly ModernButton _newSessionButton;
    private readonly ToolStripMenuItem _newSessionMenuItem;

    private ProjectContext? _project;
    private ProjectFileWatcherService? _watcher;

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

        _newSessionButton = new ModernButton
        {
            Text = "+  New Session",
            AutoSize = false,
            Size = new Size(150, 30),
            Location = new Point(14, 9),
        };
        Theme.StyleButton(_newSessionButton, primary: true);
        _newSessionButton.Click += OnNewSessionClicked;

        _toolbar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 48,
            BackColor = Theme.PanelBackground,
            Visible = false,
        };
        _toolbar.Controls.Add(_newSessionButton);

        _sessionTabs = new TabControl
        {
            Dock = DockStyle.Fill,
            DrawMode = TabDrawMode.OwnerDrawFixed,
            ItemSize = new Size(150, 36),
            Padding = new Point(16, 8),
            BackColor = Theme.Background,
            Font = Theme.UiFont,
            Visible = false,
        };
        _sessionTabs.DrawItem += OnDrawTabItem;
        _sessionTabs.SelectedIndexChanged += OnActiveTabChanged;

        _emptyStatePanel = BuildEmptyStatePanel();

        Controls.Add(_sessionTabs);
        Controls.Add(_toolbar);
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

    private void OnDrawTabItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _sessionTabs.TabPages.Count)
            return;

        var page = _sessionTabs.TabPages[e.Index];
        var selected = e.Index == _sessionTabs.SelectedIndex;

        e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        using (var backBrush = new SolidBrush(selected ? Theme.ElevatedBackground : Theme.PanelBackground))
            e.Graphics.FillRectangle(backBrush, e.Bounds);

        if (selected)
        {
            const int indicatorWidth = 28;
            using var accentBrush = new SolidBrush(Theme.Accent);
            var indicatorRect = new Rectangle(e.Bounds.X + (e.Bounds.Width - indicatorWidth) / 2, e.Bounds.Top + 2, indicatorWidth, 3);
            e.Graphics.FillRectangle(accentBrush, indicatorRect);
        }

        using var textBrush = new SolidBrush(selected ? Theme.Text : Theme.SubtleText);
        var textFormat = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
        var textBounds = new Rectangle(e.Bounds.X, e.Bounds.Y + 3, e.Bounds.Width, e.Bounds.Height - 3);
        e.Graphics.DrawString(page.Text, _sessionTabs.Font, textBrush, textBounds, textFormat);
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
        _toolbar.Visible = true;
        _sessionTabs.Visible = true;
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
        tabControl.Terminal.Focus();
    }

    private void RemoveTab(TabPage page)
    {
        _sessionTabs.TabPages.Remove(page);
        page.Dispose();
    }

    private void OnActiveTabChanged(object? sender, EventArgs e)
    {
        if (!TryGetActiveTab(out var tab))
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

        var activePage = _sessionTabs.SelectedTab;

        foreach (TabPage page in _sessionTabs.TabPages)
        {
            if (page.Controls.Count == 0 || page.Controls[0] is not SessionTabControl tab)
                continue;

            if (page == activePage)
                tab.RefreshDiff();
            else
                tab.DiffEngine?.MarkStale();
        }
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
