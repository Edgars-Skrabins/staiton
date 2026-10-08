namespace ClaudeCodeTerminal.App.UI;

public static class Theme
{
    public static readonly Color Background = Color.FromArgb(0x1A, 0x1A, 0x1A);
    public static readonly Color PanelBackground = Color.FromArgb(0x20, 0x20, 0x20);
    public static readonly Color ElevatedBackground = Color.FromArgb(0x26, 0x26, 0x26);
    public static readonly Color Border = Color.FromArgb(0x33, 0x33, 0x33);

    public static readonly Color Text = Color.FromArgb(0xD4, 0xD4, 0xD4);
    public static readonly Color SubtleText = Color.FromArgb(0x8A, 0x8A, 0x8A);

    public static readonly Color Accent = Color.FromArgb(0x2E, 0xA0, 0x43);
    public static readonly Color AccentHover = Color.FromArgb(0x3F, 0xB9, 0x50);
    public static readonly Color AccentMuted = Color.FromArgb(0x1F, 0x3D, 0x28);

    public static readonly Color Added = Color.FromArgb(0x3F, 0xB9, 0x50);
    public static readonly Color Deleted = Color.FromArgb(0xE0, 0x6C, 0x6C);
    public static readonly Color Modified = Color.FromArgb(0xD7, 0xBA, 0x7D);

    public static void StyleListBox(ListBox listBox)
    {
        listBox.BackColor = PanelBackground;
        listBox.ForeColor = Text;
        listBox.BorderStyle = BorderStyle.None;
        listBox.DrawMode = DrawMode.OwnerDrawFixed;
        listBox.ItemHeight = 20;
        listBox.DrawItem += (sender, e) => DrawListItem(listBox, e, listBox.Items.Count > 0 ? listBox.Items[Math.Max(e.Index, 0)]?.ToString() ?? string.Empty : string.Empty, Text);
    }

    private static void DrawListItem(ListBox listBox, DrawItemEventArgs e, string text, Color textColor)
    {
        var selected = (e.State & DrawItemState.Selected) == DrawItemState.Selected;
        using (var backBrush = new SolidBrush(selected ? AccentMuted : PanelBackground))
            e.Graphics.FillRectangle(backBrush, e.Bounds);

        if (selected)
        {
            using var accentBrush = new SolidBrush(Accent);
            e.Graphics.FillRectangle(accentBrush, e.Bounds.X, e.Bounds.Y, 3, e.Bounds.Height);
        }

        if (e.Index >= 0)
        {
            using var textBrush = new SolidBrush(textColor);
            var textBounds = new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width - 8, e.Bounds.Height);
            e.Graphics.DrawString(text, listBox.Font, textBrush, textBounds, new StringFormat { LineAlignment = StringAlignment.Center });
        }
    }

    public static void StyleButton(Button button, bool primary = false)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = primary ? Accent : Border;
        button.FlatAppearance.BorderSize = 1;
        button.BackColor = primary ? AccentMuted : ElevatedBackground;
        button.ForeColor = Text;
        button.FlatAppearance.MouseOverBackColor = primary ? Accent : Border;
    }

    public static void StyleTextBox(TextBoxBase textBox)
    {
        textBox.BackColor = PanelBackground;
        textBox.ForeColor = Text;
        textBox.BorderStyle = BorderStyle.None;
    }

    public static void StyleSplitContainer(SplitContainer split)
    {
        split.BackColor = Border;
        split.Panel1.BackColor = Background;
        split.Panel2.BackColor = Background;
    }
}
