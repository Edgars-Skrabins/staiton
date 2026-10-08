using ClaudeCodeTerminal.App.UI;

namespace ClaudeCodeTerminal;

static class Program
{
    [STAThread]
    static void Main()
    {
        Application.SetColorMode(SystemColorMode.Dark);
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}