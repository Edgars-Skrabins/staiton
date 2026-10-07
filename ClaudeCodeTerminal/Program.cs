using ClaudeCodeTerminal.App.UI;

namespace ClaudeCodeTerminal;

static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}