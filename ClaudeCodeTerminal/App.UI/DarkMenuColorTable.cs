namespace ClaudeCodeTerminal.App.UI;

public sealed class DarkMenuColorTable : ProfessionalColorTable
{
    public override Color ToolStripDropDownBackground => Theme.ElevatedBackground;
    public override Color MenuStripGradientBegin => Theme.PanelBackground;
    public override Color MenuStripGradientEnd => Theme.PanelBackground;
    public override Color MenuBorder => Theme.Border;
    public override Color MenuItemBorder => Theme.Accent;
    public override Color MenuItemSelected => Theme.AccentMuted;
    public override Color MenuItemSelectedGradientBegin => Theme.AccentMuted;
    public override Color MenuItemSelectedGradientEnd => Theme.AccentMuted;
    public override Color MenuItemPressedGradientBegin => Theme.ElevatedBackground;
    public override Color MenuItemPressedGradientEnd => Theme.ElevatedBackground;
    public override Color ImageMarginGradientBegin => Theme.ElevatedBackground;
    public override Color ImageMarginGradientMiddle => Theme.ElevatedBackground;
    public override Color ImageMarginGradientEnd => Theme.ElevatedBackground;
    public override Color SeparatorDark => Theme.Border;
    public override Color SeparatorLight => Theme.Border;
    public override Color ToolStripBorder => Theme.Border;
}
