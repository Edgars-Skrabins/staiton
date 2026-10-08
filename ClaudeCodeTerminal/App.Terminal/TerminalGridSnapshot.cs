using VtNetCore.VirtualTerminal;
using VtNetCore.VirtualTerminal.Layout;
using VtNetCore.XTermParser;

namespace ClaudeCodeTerminal.App.Terminal;

public sealed class TerminalGridSnapshot
{
    private readonly object _gate = new();
    private readonly VirtualTerminalController _controller;
    private readonly DataConsumer _consumer;
    private bool _hasPendingChanges;

    public event EventHandler<byte[]>? SendData;

    public TerminalGridSnapshot(int rows, int cols)
    {
        _controller = new VirtualTerminalController();
        _controller.ResizeView(cols, rows);
        _controller.SendData += (_, e) => SendData?.Invoke(this, e.Data);
        _consumer = new DataConsumer(_controller);
    }

    public void Feed(ReadOnlySpan<byte> data)
    {
        lock (_gate)
        {
            _consumer.Push(data.ToArray());
            if (_controller.Changed)
                _hasPendingChanges = true;
        }
    }

    public void Resize(int rows, int cols)
    {
        lock (_gate)
        {
            _controller.ResizeView(cols, rows);
        }
    }

    public bool TryCapture(out IReadOnlyList<LayoutRow> rows, out TerminalCursorState cursor)
    {
        lock (_gate)
        {
            if (!_hasPendingChanges)
            {
                rows = Array.Empty<LayoutRow>();
                cursor = _controller.CursorState.Clone();
                return false;
            }

            rows = _controller.GetPageSpans(_controller.ViewPort.TopRow, _controller.VisibleRows);
            cursor = _controller.CursorState.Clone();
            _controller.ClearChanges();
            _hasPendingChanges = false;
            return true;
        }
    }

    public bool BracketedPasteMode
    {
        get { lock (_gate) { return _controller.BracketedPasteMode; } }
    }

    public bool MouseTrackingEnabled
    {
        get
        {
            lock (_gate)
            {
                return _controller.CellMotionMouseTracking
                    || _controller.UseAllMouseTracking
                    || _controller.X10SendMouseXYOnButton
                    || _controller.X11SendMouseXYOnButton;
            }
        }
    }

    public void KeyPressed(string key, bool control, bool shift)
    {
        lock (_gate)
        {
            _controller.KeyPressed(key, control, shift);
        }
    }

    public void MousePress(int x, int y, int buttonNumber, bool control, bool shift)
    {
        lock (_gate)
        {
            _controller.MousePress(x, y, buttonNumber, control, shift);
        }
    }

    public void MouseRelease(int x, int y, bool control, bool shift)
    {
        lock (_gate)
        {
            _controller.MouseRelease(x, y, control, shift);
        }
    }

    public void MouseMove(int x, int y, int buttonNumber, bool control, bool shift)
    {
        lock (_gate)
        {
            _controller.MouseMove(x, y, buttonNumber, control, shift);
        }
    }
}
