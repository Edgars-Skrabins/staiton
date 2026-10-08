using VtNetCore.VirtualTerminal;
using VtNetCore.VirtualTerminal.Layout;

namespace ClaudeCodeTerminal.App.Terminal;

public sealed class TerminalRenderControl : Control
{
    private static readonly string[] PreferredFontFamilies = ["Cascadia Mono", "Consolas"];

    private TerminalGridSnapshot? _grid;
    private Font _font;
    private SizeF _cellSize;
    private readonly System.Windows.Forms.Timer _repaintTimer;
    private readonly System.Windows.Forms.Timer _resizeDebounceTimer;
    private IReadOnlyList<LayoutRow> _lastRows = Array.Empty<LayoutRow>();
    private TerminalCursorState? _lastCursor;

    public event EventHandler<(int Rows, int Cols)>? TerminalSizeChanged;

    public TerminalRenderControl()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.UserPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.Selectable,
            true);
        DoubleBuffered = true;
        BackColor = Color.FromArgb(0x1A, 0x1A, 0x1A);
        ForeColor = Color.FromArgb(0xD4, 0xD4, 0xD4);
        TabStop = true;

        _font = CreateTerminalFont();
        MeasureCellSize();

        _repaintTimer = new System.Windows.Forms.Timer { Interval = 33 };
        _repaintTimer.Tick += (_, _) => RepaintIfChanged();
        _repaintTimer.Start();

        _resizeDebounceTimer = new System.Windows.Forms.Timer { Interval = 120 };
        _resizeDebounceTimer.Tick += (_, _) =>
        {
            _resizeDebounceTimer.Stop();
            RecalculateGridSize();
        };
    }

    public (int Rows, int Cols) CurrentGridSize
    {
        get
        {
            if (_cellSize.Width <= 0 || _cellSize.Height <= 0)
                return (24, 80);

            var cols = Math.Max(1, (int)(ClientSize.Width / _cellSize.Width));
            var rows = Math.Max(1, (int)(ClientSize.Height / _cellSize.Height));
            return (rows, cols);
        }
    }

    public void AttachGrid(TerminalGridSnapshot grid)
    {
        _grid = grid;
        Invalidate();
    }

    private static Font CreateTerminalFont()
    {
        foreach (var family in PreferredFontFamilies)
        {
            if (FontFamily.Families.Any(f => f.Name.Equals(family, StringComparison.OrdinalIgnoreCase)))
                return new Font(family, 10f, FontStyle.Regular, GraphicsUnit.Point);
        }

        return new Font(FontFamily.GenericMonospace, 10f, FontStyle.Regular, GraphicsUnit.Point);
    }

    private void MeasureCellSize()
    {
        using var g = CreateGraphics();
        var size = g.MeasureString("M", _font, int.MaxValue, StringFormat.GenericTypographic);
        _cellSize = new SizeF(MathF.Ceiling(size.Width), MathF.Ceiling(_font.GetHeight(g)));
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        _resizeDebounceTimer.Stop();
        _resizeDebounceTimer.Start();
    }

    protected override void OnDpiChangedAfterParent(EventArgs e)
    {
        base.OnDpiChangedAfterParent(e);
        _font.Dispose();
        _font = CreateTerminalFont();
        MeasureCellSize();
        RecalculateGridSize();
        Invalidate();
    }

    private void RecalculateGridSize()
    {
        var (rows, cols) = CurrentGridSize;
        _grid?.Resize(rows, cols);
        TerminalSizeChanged?.Invoke(this, (rows, cols));
    }

    private void RepaintIfChanged()
    {
        if (_grid is null)
            return;

        if (_grid.TryCapture(out var rows, out var cursor))
        {
            _lastRows = rows;
            _lastCursor = cursor;
            Invalidate();
        }
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.Clear(BackColor);

        for (var rowIndex = 0; rowIndex < _lastRows.Count; rowIndex++)
        {
            var row = _lastRows[rowIndex];
            var x = 0f;
            var y = rowIndex * _cellSize.Height;

            foreach (var span in row.Spans)
            {
                if (string.IsNullOrEmpty(span.Text))
                    continue;

                var width = span.Text.Length * _cellSize.Width;
                var fg = ParseColor(span.ForgroundColor, ForeColor);
                var bg = ParseColor(span.BackgroundColor, BackColor);

                using (var bgBrush = new SolidBrush(bg))
                    g.FillRectangle(bgBrush, x, y, width, _cellSize.Height);

                if (!span.Hidden)
                {
                    var style = FontStyle.Regular;
                    if (span.Bold) style |= FontStyle.Bold;
                    if (span.Italic) style |= FontStyle.Italic;
                    if (span.Underline) style |= FontStyle.Underline;

                    var font = _font;
                    if (style != FontStyle.Regular)
                        font = new Font(_font, style);

                    using var fgBrush = new SolidBrush(fg);
                    g.DrawString(span.Text, font, fgBrush, x, y, StringFormat.GenericTypographic);

                    if (font != _font)
                        font.Dispose();
                }

                x += width;
            }
        }

        if (_lastCursor is { ShowCursor: true } cursor)
        {
            var cx = cursor.CurrentColumn * _cellSize.Width;
            var cy = cursor.CurrentRow * _cellSize.Height;
            using var cursorBrush = new SolidBrush(Color.FromArgb(120, Color.White));
            g.FillRectangle(cursorBrush, cx, cy, _cellSize.Width, _cellSize.Height);
        }
    }

    private static Color ParseColor(string? webColor, Color fallback)
    {
        if (string.IsNullOrEmpty(webColor))
            return fallback;

        try
        {
            return ColorTranslator.FromHtml(webColor);
        }
        catch (Exception)
        {
            return fallback;
        }
    }

    protected override bool IsInputKey(Keys keyData) => true;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (_grid is null)
            return;

        var keyName = MapKeyCode(e.KeyCode, e.Control);
        if (keyName is null)
            return;

        _grid.KeyPressed(keyName, e.Control, e.Shift);
        e.Handled = true;
        e.SuppressKeyPress = true;
    }

    protected override void OnKeyPress(KeyPressEventArgs e)
    {
        base.OnKeyPress(e);
        if (_grid is null || char.IsControl(e.KeyChar))
            return;

        _grid.KeyPressed(e.KeyChar.ToString(), control: false, shift: false);
        e.Handled = true;
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (!Focused)
            Focus();
    }

    private static string? MapKeyCode(Keys keyCode, bool controlPressed)
    {
        return keyCode switch
        {
            Keys.Up => "Up",
            Keys.Down => "Down",
            Keys.Left => "Left",
            Keys.Right => "Right",
            Keys.Home => "Home",
            Keys.End => "End",
            Keys.Insert => "Insert",
            Keys.Delete => "Delete",
            Keys.PageUp => "PageUp",
            Keys.PageDown => "PageDown",
            Keys.Back => "Back",
            Keys.Tab => "Tab",
            Keys.Enter => "Enter",
            Keys.Escape => "Escape",
            >= Keys.F1 and <= Keys.F12 => "F" + (keyCode - Keys.F1 + 1),
            >= Keys.A and <= Keys.Z when controlPressed => keyCode.ToString(),
            _ => null,
        };
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _repaintTimer.Stop();
            _repaintTimer.Dispose();
            _resizeDebounceTimer.Stop();
            _resizeDebounceTimer.Dispose();
            _font.Dispose();
        }

        base.Dispose(disposing);
    }
}
