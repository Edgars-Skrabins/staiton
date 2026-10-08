using System.Runtime.InteropServices;

namespace ClaudeCodeTerminal.App.UI;

public static class Theme
{
    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int valueSize);

    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    private const int DwmwaUseImmersiveDarkMode = 20;
    private const uint SwpFrameChanged = 0x0020;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpNoZOrder = 0x0004;
    private const uint SwpNoActivate = 0x0010;

    public static void ApplyDarkTitleBar(Form form)
    {
        void Apply()
        {
            var useDark = 1;
            DwmSetWindowAttribute(form.Handle, DwmwaUseImmersiveDarkMode, ref useDark, sizeof(int));
            SetWindowPos(form.Handle, IntPtr.Zero, 0, 0, 0, 0, SwpFrameChanged | SwpNoMove | SwpNoSize | SwpNoZOrder | SwpNoActivate);
        }

        if (form.IsHandleCreated)
            Apply();
        else
            form.HandleCreated += (_, _) => Apply();

        form.Shown += (_, _) => Apply();
    }

    [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
    private static extern int SetWindowTheme(IntPtr hWnd, string pszSubAppName, string? pszSubIdList);

    public static void ApplyDarkScrollBar(Control control)
    {
        void Apply() => SetWindowTheme(control.Handle, "DarkMode_Explorer", null);

        if (control.IsHandleCreated)
            Apply();
        else
            control.HandleCreated += (_, _) => Apply();
    }

    public static readonly Color Background = Color.FromArgb(0x16, 0x17, 0x19);
    public static readonly Color PanelBackground = Color.FromArgb(0x1C, 0x1D, 0x20);
    public static readonly Color ElevatedBackground = Color.FromArgb(0x24, 0x26, 0x2A);
    public static readonly Color Border = Color.FromArgb(0x30, 0x32, 0x36);

    public static readonly Color Text = Color.FromArgb(0xE4, 0xE5, 0xE7);
    public static readonly Color SubtleText = Color.FromArgb(0x8D, 0x91, 0x97);
    public static readonly Color FaintText = Color.FromArgb(0x5C, 0x60, 0x66);

    public static readonly Color Accent = Color.FromArgb(0x2E, 0xA0, 0x43);
    public static readonly Color AccentHover = Color.FromArgb(0x37, 0xB5, 0x4E);
    public static readonly Color AccentMuted = Color.FromArgb(0x1C, 0x33, 0x22);

    public static readonly Color Added = Color.FromArgb(0x4E, 0xC9, 0x63);
    public static readonly Color Deleted = Color.FromArgb(0xF0, 0x7A, 0x7A);
    public static readonly Color Modified = Color.FromArgb(0xE0, 0xBB, 0x6E);

    public static readonly Color DiffAddedBackground = Color.FromArgb(0x17, 0x2A, 0x1B);
    public static readonly Color DiffRemovedBackground = Color.FromArgb(0x30, 0x1A, 0x1B);
    public static readonly Color DiffHunkHeader = Color.FromArgb(0x6E, 0xA8, 0xE0);
    public static readonly Color DiffHunkBackground = Color.FromArgb(0x1A, 0x22, 0x2C);

    private static Font? _uiFont;
    public static Font UiFont => _uiFont ??= CreateUiFont();

    private static Font? _uiFontBold;
    public static Font UiFontBold => _uiFontBold ??= new Font(UiFont, FontStyle.Bold);

    private static Font CreateUiFont()
    {
        foreach (var family in new[] { "Segoe UI Variable Text", "Segoe UI" })
        {
            if (FontFamily.Families.Any(f => f.Name.Equals(family, StringComparison.OrdinalIgnoreCase)))
                return new Font(family, 9.5f, FontStyle.Regular, GraphicsUnit.Point);
        }

        return new Font(FontFamily.GenericSansSerif, 9.5f, FontStyle.Regular, GraphicsUnit.Point);
    }

    public static void StyleListBox(ListBox listBox)
    {
        listBox.BackColor = PanelBackground;
        listBox.ForeColor = Text;
        listBox.BorderStyle = BorderStyle.None;
        listBox.Font = UiFont;
        listBox.DrawMode = DrawMode.OwnerDrawFixed;
        listBox.ItemHeight = 24;
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
            e.Graphics.FillRectangle(accentBrush, e.Bounds.X, e.Bounds.Y + 3, 3, e.Bounds.Height - 6);
        }

        if (e.Index >= 0)
        {
            using var textBrush = new SolidBrush(textColor);
            var textBounds = new Rectangle(e.Bounds.X + 12, e.Bounds.Y, e.Bounds.Width - 12, e.Bounds.Height);
            e.Graphics.DrawString(text, listBox.Font, textBrush, textBounds, new StringFormat { LineAlignment = StringAlignment.Center });
        }
    }

    public static void StyleButton(ModernButton button, bool primary = false)
    {
        button.Font = UiFont;
        button.FillColor = primary ? Accent : ElevatedBackground;
        button.HoverColor = primary ? AccentHover : Border;
        button.BorderColor = primary ? Color.Transparent : Border;
        button.ForeColor = primary ? Color.White : Text;
    }

    public static void StyleTextBox(TextBoxBase textBox)
    {
        textBox.BackColor = PanelBackground;
        textBox.ForeColor = Text;
        textBox.BorderStyle = BorderStyle.None;
        textBox.Font = UiFont;
    }

    public static void StyleSplitContainer(SplitContainer split)
    {
        split.BackColor = Border;
        split.Panel1.BackColor = Background;
        split.Panel2.BackColor = Background;
    }

    public static Panel CreateSectionHeader(string title)
    {
        var panel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 30,
            BackColor = PanelBackground,
        };

        var label = new Label
        {
            Text = title,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(12, 0, 0, 0),
            ForeColor = SubtleText,
            Font = UiFontBold,
            AutoEllipsis = true,
        };

        panel.Controls.Add(label);
        return panel;
    }

    public static void StyleMenuStrip(MenuStrip menuStrip)
    {
        menuStrip.Renderer = new ToolStripProfessionalRenderer(new DarkMenuColorTable());
        menuStrip.BackColor = PanelBackground;
        menuStrip.ForeColor = Text;
        menuStrip.Font = UiFont;
        menuStrip.Padding = new Padding(8, 4, 0, 4);

        foreach (ToolStripItem item in menuStrip.Items)
            StyleMenuItemRecursive(item);
    }

    private static void StyleMenuItemRecursive(ToolStripItem item)
    {
        item.ForeColor = Text;
        item.Font = UiFont;

        if (item is not ToolStripMenuItem menuItem)
            return;

        menuItem.DropDown.BackColor = ElevatedBackground;
        menuItem.DropDown.ForeColor = Text;
        menuItem.DropDown.Font = UiFont;
        menuItem.DropDown.Renderer = new ToolStripProfessionalRenderer(new DarkMenuColorTable());

        foreach (ToolStripItem child in menuItem.DropDownItems)
            StyleMenuItemRecursive(child);
    }
}
