using ClaudeCodeTerminal.App.Diff;

namespace ClaudeCodeTerminal.App.UI;

public sealed class DiffPanelControl : UserControl
{
    private readonly SplitContainer _split;
    private readonly ListBox _fileList;
    private readonly RichTextBox _diffView;
    private readonly Font _diffFont;
    private IReadOnlyList<FileChange> _changes = [];
    private bool _userMovedSplitter;
    private bool _settingSplitterProgrammatically;

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
        _split.SplitterMoved += (_, _) =>
        {
            if (!_settingSplitterProgrammatically)
                _userMovedSplitter = true;
        };
        _split.Resize += OnSplitResize;

        _fileList = new ListBox
        {
            Dock = DockStyle.Fill,
            DrawMode = DrawMode.OwnerDrawFixed,
            ItemHeight = 24,
            BackColor = Theme.PanelBackground,
            ForeColor = Theme.Text,
            BorderStyle = BorderStyle.None,
            Font = Theme.UiFont,
        };
        _fileList.DrawItem += OnDrawFileListItem;
        _fileList.SelectedIndexChanged += (_, _) => ShowSelectedDiff();

        _diffFont = CreateDiffFont();
        _diffView = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            WordWrap = false,
            ScrollBars = RichTextBoxScrollBars.Both,
            BorderStyle = BorderStyle.None,
            BackColor = Theme.Background,
            ForeColor = Theme.Text,
            DetectUrls = false,
            Font = _diffFont,
        };
        Theme.ApplyDarkScrollBar(_diffView);

        _split.Panel1.Controls.Add(_fileList);
        _split.Panel1.Controls.Add(Theme.CreateSectionHeader("File changes"));
        _split.Panel2.Controls.Add(_diffView);
        _split.Panel2.Controls.Add(Theme.CreateSectionHeader("Git Diff"));

        Controls.Add(_split);
    }

    private static Font CreateDiffFont()
    {
        foreach (var family in new[] { "Cascadia Mono", "Consolas" })
        {
            if (FontFamily.Families.Any(f => f.Name.Equals(family, StringComparison.OrdinalIgnoreCase)))
                return new Font(family, 9f, FontStyle.Regular, GraphicsUnit.Point);
        }

        return new Font(FontFamily.GenericMonospace, 9f, FontStyle.Regular, GraphicsUnit.Point);
    }

    private void OnSplitResize(object? sender, EventArgs e)
    {
        if (_userMovedSplitter || _split.Width < 300)
            return;

        _settingSplitterProgrammatically = true;
        _split.SplitterDistance = Math.Clamp(220, 100, Math.Max(100, _split.Width - 100));
        _settingSplitterProgrammatically = false;
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
            _diffView.Clear();
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
        var markerBounds = new Rectangle(e.Bounds.X + 10, e.Bounds.Y, 16, e.Bounds.Height);
        e.Graphics.DrawString(marker, e.Font ?? Font, markerBrush, markerBounds, new StringFormat { LineAlignment = StringAlignment.Center });

        using var textBrush = new SolidBrush(Theme.Text);
        var textBounds = new Rectangle(e.Bounds.X + 26, e.Bounds.Y, e.Bounds.Width - 26, e.Bounds.Height);
        var pathFormat = new StringFormat
        {
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisPath,
            FormatFlags = StringFormatFlags.NoWrap,
        };
        e.Graphics.DrawString(change.Path, e.Font ?? Font, textBrush, textBounds, pathFormat);
    }

    private void ShowSelectedDiff()
    {
        var index = _fileList.SelectedIndex;
        var diffText = index >= 0 && index < _changes.Count ? _changes[index].DiffText : string.Empty;
        RenderDiff(diffText);
    }

    private void RenderDiff(string diffText)
    {
        _diffView.Clear();
        if (string.IsNullOrEmpty(diffText))
            return;

        var lines = diffText.Replace("\r\n", "\n").Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i];
            var isLast = i == lines.Length - 1;
            if (isLast && line.Length == 0)
                break;

            var (fg, bg) = ClassifyDiffLine(line);
            var start = _diffView.TextLength;
            _diffView.AppendText(line + "\n");
            _diffView.Select(start, line.Length + 1);
            _diffView.SelectionColor = fg;
            _diffView.SelectionBackColor = bg;
        }

        _diffView.Select(0, 0);
    }

    public static (Color Foreground, Color Background) ClassifyDiffLine(string line)
    {
        if (line.StartsWith("diff --git", StringComparison.Ordinal)
            || line.StartsWith("index ", StringComparison.Ordinal)
            || line.StartsWith("new file mode", StringComparison.Ordinal)
            || line.StartsWith("deleted file mode", StringComparison.Ordinal)
            || line.StartsWith("similarity index", StringComparison.Ordinal)
            || line.StartsWith("rename from", StringComparison.Ordinal)
            || line.StartsWith("rename to", StringComparison.Ordinal)
            || line.StartsWith("--- ", StringComparison.Ordinal)
            || line.StartsWith("+++ ", StringComparison.Ordinal))
            return (Theme.FaintText, Theme.Background);

        if (line.StartsWith("@@", StringComparison.Ordinal))
            return (Theme.DiffHunkHeader, Theme.DiffHunkBackground);

        if (line.StartsWith("+", StringComparison.Ordinal))
            return (Theme.Added, Theme.DiffAddedBackground);

        if (line.StartsWith("-", StringComparison.Ordinal))
            return (Theme.Deleted, Theme.DiffRemovedBackground);

        return (Theme.Text, Theme.Background);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _diffFont.Dispose();

        base.Dispose(disposing);
    }
}
