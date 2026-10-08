using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace ClaudeCodeTerminal.App.UI;

public sealed class TabStripItem : Panel
{
    private const int ClosePadding = 8;
    private const int CloseButtonSize = 16;

    private bool _isActive;
    private bool _hoveringClose;
    private TextBox? _renameBox;

    public string SessionName { get; private set; }

    public event EventHandler? Activated;
    public event EventHandler? CloseRequested;
    public event EventHandler<string>? Renamed;

    public TabStripItem(string initialName)
    {
        SessionName = initialName;

        SetStyle(
            ControlStyles.AllPaintingInWmPaint
            | ControlStyles.UserPaint
            | ControlStyles.OptimizedDoubleBuffer
            | ControlStyles.ResizeRedraw
            | ControlStyles.Selectable,
            true);
        DoubleBuffered = true;
        Size = new Size(170, 38);
        Font = Theme.UiFont;
        Cursor = Cursors.Hand;
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public bool IsActive
    {
        get => _isActive;
        set
        {
            if (_isActive == value)
                return;

            _isActive = value;
            Invalidate();
        }
    }

    private Rectangle CloseButtonBounds =>
        new(Width - CloseButtonSize - ClosePadding, (Height - CloseButtonSize) / 2, CloseButtonSize, CloseButtonSize);

    protected override void OnPaintBackground(PaintEventArgs pevent)
    {
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        using (var backBrush = new SolidBrush(_isActive ? Theme.ElevatedBackground : Theme.PanelBackground))
            g.FillRectangle(backBrush, ClientRectangle);

        if (_isActive)
        {
            const int indicatorWidth = 28;
            using var accentBrush = new SolidBrush(Theme.Accent);
            g.FillRectangle(accentBrush, (Width - indicatorWidth) / 2, 2, indicatorWidth, 3);
        }

        if (_renameBox is null)
        {
            using var textBrush = new SolidBrush(_isActive ? Theme.Text : Theme.SubtleText);
            var textBounds = new Rectangle(12, 3, Width - CloseButtonSize - ClosePadding - 16, Height - 3);
            var format = new StringFormat
            {
                Alignment = StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter,
                FormatFlags = StringFormatFlags.NoWrap,
            };
            g.DrawString(SessionName, Font, textBrush, textBounds, format);
        }

        var closeBounds = CloseButtonBounds;
        if (_hoveringClose)
        {
            using var closeBg = new SolidBrush(Theme.Border);
            g.FillEllipse(closeBg, closeBounds);
        }

        using var closePen = new Pen(_hoveringClose ? Theme.Text : Theme.SubtleText, 1.4f);
        const int inset = 5;
        g.DrawLine(closePen, closeBounds.Left + inset, closeBounds.Top + inset, closeBounds.Right - inset, closeBounds.Bottom - inset);
        g.DrawLine(closePen, closeBounds.Right - inset, closeBounds.Top + inset, closeBounds.Left + inset, closeBounds.Bottom - inset);
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var hovering = CloseButtonBounds.Contains(e.Location);
        if (hovering != _hoveringClose)
        {
            _hoveringClose = hovering;
            Invalidate();
        }
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        if (_hoveringClose)
        {
            _hoveringClose = false;
            Invalidate();
        }
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left)
            return;

        if (CloseButtonBounds.Contains(e.Location))
        {
            CloseRequested?.Invoke(this, EventArgs.Empty);
            return;
        }

        Activated?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnMouseDoubleClick(MouseEventArgs e)
    {
        base.OnMouseDoubleClick(e);
        if (!CloseButtonBounds.Contains(e.Location))
            BeginRename();
    }

    private void BeginRename()
    {
        if (_renameBox is not null)
            return;

        var box = new TextBox
        {
            Text = SessionName,
            Font = Font,
            BorderStyle = BorderStyle.None,
            BackColor = Theme.ElevatedBackground,
            ForeColor = Theme.Text,
            Location = new Point(10, (Height - 20) / 2),
            Width = Width - CloseButtonSize - ClosePadding - 20,
        };
        box.KeyDown += (_, args) =>
        {
            if (args.KeyCode == Keys.Enter)
            {
                args.SuppressKeyPress = true;
                CommitRename(box.Text);
            }
            else if (args.KeyCode == Keys.Escape)
            {
                args.SuppressKeyPress = true;
                CancelRename();
            }
        };
        box.LostFocus += (_, _) => CommitRename(box.Text);

        Controls.Add(box);
        _renameBox = box;
        Invalidate();
        box.Focus();
        box.SelectAll();
    }

    private void CommitRename(string newName)
    {
        if (_renameBox is null)
            return;

        var box = _renameBox;
        _renameBox = null;
        Controls.Remove(box);
        box.Dispose();

        var trimmed = newName.Trim();
        if (trimmed.Length > 0 && trimmed != SessionName)
        {
            SessionName = trimmed;
            Renamed?.Invoke(this, SessionName);
        }

        Invalidate();
    }

    private void CancelRename()
    {
        if (_renameBox is null)
            return;

        var box = _renameBox;
        _renameBox = null;
        Controls.Remove(box);
        box.Dispose();
        Invalidate();
    }
}
