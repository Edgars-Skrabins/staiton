using ClaudeCodeTerminal.App.Diff;

namespace ClaudeCodeTerminal.App.UI;

public sealed class DiffPanelControl : UserControl
{
    private readonly ListBox _fileList;
    private readonly TextBox _diffView;
    private IReadOnlyList<FileChange> _changes = [];

    public DiffPanelControl()
    {
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 150,
        };

        _fileList = new ListBox { Dock = DockStyle.Fill };
        _fileList.SelectedIndexChanged += (_, _) => ShowSelectedDiff();

        _diffView = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Both,
            WordWrap = false,
            Font = new Font(FontFamily.GenericMonospace, 9f),
        };

        split.Panel1.Controls.Add(_fileList);
        split.Panel2.Controls.Add(_diffView);

        Controls.Add(split);
    }

    public void ShowChanges(IReadOnlyList<FileChange> changes)
    {
        _changes = changes;

        _fileList.BeginUpdate();
        _fileList.Items.Clear();
        foreach (var change in changes)
            _fileList.Items.Add(FormatListEntry(change));
        _fileList.EndUpdate();

        if (_fileList.Items.Count > 0)
            _fileList.SelectedIndex = 0;
        else
            _diffView.Text = string.Empty;
    }

    private void ShowSelectedDiff()
    {
        var index = _fileList.SelectedIndex;
        _diffView.Text = index >= 0 && index < _changes.Count
            ? _changes[index].DiffText
            : string.Empty;
    }

    private static string FormatListEntry(FileChange change)
    {
        var marker = change.Kind switch
        {
            FileChangeKind.Added => "[+]",
            FileChangeKind.Deleted => "[-]",
            _ => "[~]",
        };
        return $"{marker} {change.Path}";
    }
}
