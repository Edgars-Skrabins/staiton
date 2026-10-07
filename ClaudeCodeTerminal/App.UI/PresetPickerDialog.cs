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

        _list = new ListBox
        {
            Dock = DockStyle.Fill,
            DisplayMember = nameof(Preset.Name),
        };
        foreach (var preset in presets)
            _list.Items.Add(preset);
        if (presets.Count > 0)
            _list.SelectedIndex = 0;
        _list.DoubleClick += (_, _) => AcceptSelection();

        var okButton = new Button { Text = "Start", DialogResult = DialogResult.OK, AutoSize = true };
        var cancelButton = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        okButton.Click += (_, _) => AcceptSelection();

        var buttonPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            FlowDirection = FlowDirection.RightToLeft,
            Height = 44,
            Padding = new Padding(8),
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
