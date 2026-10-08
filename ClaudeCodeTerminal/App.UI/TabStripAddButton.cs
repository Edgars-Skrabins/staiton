using System.Drawing.Drawing2D;

namespace ClaudeCodeTerminal.App.UI;

public sealed class TabStripAddButton : Panel
{
    private bool _hovering;

    public event EventHandler? Activated;

    public TabStripAddButton()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.UserPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.Selectable,
            true);
        DoubleBuffered = true;
        Size = new Size(38, 38);
        Cursor = Cursors.Hand;
    }

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        _hovering = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        _hovering = false;
        Invalidate();
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button == MouseButtons.Left)
            Activated?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using (var bg = new SolidBrush(Theme.PanelBackground))
            g.FillRectangle(bg, ClientRectangle);

        var circleRect = new Rectangle((Width - 24) / 2, (Height - 24) / 2, 24, 24);
        if (_hovering)
        {
            using var hoverBrush = new SolidBrush(Theme.ElevatedBackground);
            g.FillEllipse(hoverBrush, circleRect);
        }

        using var pen = new Pen(_hovering ? Theme.Text : Theme.SubtleText, 1.6f);
        var cx = circleRect.Left + circleRect.Width / 2;
        var cy = circleRect.Top + circleRect.Height / 2;
        const int half = 6;
        g.DrawLine(pen, cx - half, cy, cx + half, cy);
        g.DrawLine(pen, cx, cy - half, cx, cy + half);
    }
}
