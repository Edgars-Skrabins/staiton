using System.Drawing;
using System.Runtime.InteropServices;
using ClaudeCodeTerminal.App.Terminal;

namespace ClaudeCodeTerminal.Tests.Terminal;

public class TerminalRenderControlTests
{
    [DllImport("user32.dll")]
    private static extern nint SendMessage(nint hWnd, int msg, nint wParam, nint lParam);

    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONUP = 0x0202;

    [Fact]
    public void RegainsFocusOnMouseClickAfterFocusMovedToAnotherControl()
    {
        bool? focusedAfterClick = null;

        var thread = new Thread(() =>
        {
            using var form = new Form { Width = 400, Height = 300, StartPosition = FormStartPosition.Manual, Location = new Point(-2000, -2000) };
            using var terminal = new TerminalRenderControl { Dock = DockStyle.Top, Height = 150 };
            using var otherControl = new TextBox { Dock = DockStyle.Bottom };
            form.Controls.Add(terminal);
            form.Controls.Add(otherControl);
            form.Show();

            terminal.Focus();
            otherControl.Focus();

            var lParam = (nint)((50 << 16) | 50);
            SendMessage(terminal.Handle, WM_LBUTTONDOWN, (nint)1, lParam);
            SendMessage(terminal.Handle, WM_LBUTTONUP, (nint)0, lParam);

            focusedAfterClick = terminal.Focused;
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        Assert.True(focusedAfterClick);
    }
}
