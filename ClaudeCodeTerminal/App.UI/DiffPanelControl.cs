using ClaudeCodeTerminal.App.Diff;

namespace ClaudeCodeTerminal.App.UI;

public sealed class DiffPanelControl : UserControl
{
    private readonly SplitContainer _split;
    private readonly ListBox _fileList;
    private readonly TextBox _diffView;
    private IReadOnlyList<FileChange> _changes = [];
    private bool _splitterInitialized;

    public DiffPanelControl()
    {
        BackColor = Theme.Background;

        _split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterWidth = 1,
        };
        Theme.StyleSplitContainer(_split);
        Layout += OnFirstLayout;

        _fileList = new ListBox
        {
            Dock = DockStyle.Fill,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 22,
            BackColor = Theme.PanelBackground,
            ForeColor = Theme.Text,
            BorderStyle = BorderStyle.None,
        };
        _fileList.DrawItem += OnDrawFileListItem;
        _fileList.SelectedIndexChanged += (_, _) => ShowSelectedDiff();

        _diffView = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            BorderStyle = BorderStyle.None,
            BackColor = Theme.Background,
            ForeColor = Theme.Text,
            Font = new Font(FontFamily.GenericMonospace, 9f),
        };

        _split.Panel1.Controls.Add(_fileList);
        _split.Panel2.Controls.Add(_diffView);

        Controls.Add(_split);
    }

    private void OnFirstLayout(object? sender, LayoutEventArgs e)
    {
        if (_splitterInitialized || Width < 300)
            return;

        _split.SplitterDistance = Math.Clamp(220, 100, Math.Max(100, Width - 100));
        _splitterInitialized = true;
    }

    public void ShowChanges(IReadOnlyList<FileChange> changes)
    {
        _changes = changes;

        _fileList.BeginUpdate();
        _fileList.Items.Clear();
        foreach (var change in changes)
            _fileList.Items.Add(change.Path);
        _fileList.EndUpdate();

        if (_fileList.Items.Count > 0)
            _fileList.SelectedIndex = 0;
        else
            _diffView.Text = string.Empty;
    }

    private void OnDrawFileListItem(object? sender, DrawItemEventArgs e)
    {
        if (e.Index < 0 || e.Index >= _changes.Count)
        {
            e.DrawBackground();
            return;
        }

        var change = _changes[e.Index];
        var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;

        using (var backBrush = new SolidBrush(selected ? Theme.AccentMuted : Theme.PanelBackground))
            e.Graphics.FillRectangle(backBrush, e.Bounds);

        if (selected)
        {
            using var accentBrush = new SolidBrush(Theme.Accent);
            e.Graphics.FillRectangle(accentBrush, e.Bounds.X, e.Bounds.Y, 3, e.Bounds.Height);
        }

        var markerColor = change.Kind switch
        {
            FileChangeKind.Added => Theme.Added,
            FileChangeKind.Deleted => Theme.Deleted,
            _ => Theme.Modified,
        };
        var marker = change.Kind switch
        {
            FileChangeKind.Added => "+",
            FileChangeKind.Deleted => "-",
            _ => "~",
        };

        using var markerBrush = new SolidBrush(markerColor);
        var markerBounds = new Rectangle(e.Bounds.X + 8, e.Bounds.Y, 16, e.Bounds.Height);
        e.Graphics.DrawString(marker, e.Font ?? Font, markerBrush, markerBounds, new StringFormat { LineAlignment = StringAlignment.Center });

        using var textBrush = new SolidBrush(Theme.Text);
        var textBounds = new Rectangle(e.Bounds.X + 24, e.Bounds.Y, e.Bounds.Width - 24, e.Bounds.Height);
        e.Graphics.DrawString(change.Path, e.Font ?? Font, textBrush, textBounds, new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisPath });
    }

    private void ShowSelectedDiff()
    {
        var index = _fileList.SelectedIndex;
        _diffView.Text = index >= 0 && index < _changes.Count
            ? _changes[index].DiffText
            : string.Empty;
    }
}
