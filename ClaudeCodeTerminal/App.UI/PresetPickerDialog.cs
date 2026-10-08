using ClaudeCodeTerminal.App.Core;

namespace ClaudeCodeTerminal.App.UI;

public sealed class PresetPickerDialog : Form
{
    private readonly ListBox _list;

    public Preset? SelectedPreset { get; private set; }

    public PresetPickerDialog(IReadOnlyList<Preset> presets)
    {
        Text = "New Session";
        ClientSize = new Size(340, 260);
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        BackColor = Theme.Background;
        Font = Theme.UiFont;
        Theme.ApplyDarkTitleBar(this);

        _list = new ListBox { Dock = DockStyle.Fill };
        Theme.StyleListBox(_list);
        foreach (var preset in presets)
            _list.Items.Add(preset);
        if (presets.Count > 0)
            _list.SelectedIndex = 0;
        _list.DoubleClick += (_, _) => AcceptSelection();

        var okButton = new ModernButton { Text = "Start", DialogResult = DialogResult.OK, Size = new Size(90, 32), Margin = new Padding(6, 3, 0, 3) };
        var cancelButton = new ModernButton { Text = "Cancel", DialogResult = DialogResult.Cancel, Size = new Size(90, 32), Margin = new Padding(6, 3, 0, 3) };
        Theme.StyleButton(okButton, primary: true);
        Theme.StyleButton(cancelButton);
        okButton.Click += (_, _) => AcceptSelection();

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 52,
            Padding = new Padding(8, 10, 8, 8),
            BackColor = Theme.Background,
        };
        buttonPanel.Controls.Add(okButton);
        buttonPanel.Controls.Add(cancelButton);

        Controls.Add(_list);
        Controls.Add(buttonPanel);

        AcceptButton = okButton;
        CancelButton = cancelButton;
    }

    private void AcceptSelection()
    {
        SelectedPreset = _list.SelectedItem as Preset;
        DialogResult = DialogResult.OK;
    }
}
