using System.Text;
using ClaudeCodeTerminal.App.Pty;

namespace ClaudeCodeTerminal.Tests.Pty;

public class PtySessionTests
{
    [Fact]
    public async Task StartAsync_SpawnsRealProcessAndReceivesOutput()
    {
        var received = new StringBuilder();
        var outputSeen = new TaskCompletionSource();
        var exited = new TaskCompletionSource<int>();

        using var session = await PtySession.StartAsync(
            "powershell.exe",
            ["-NoProfile", "-Command", "Write-Output hello-from-pty-test"],
            Path.GetTempPath(),
            rows: 24,
            cols: 80);

        session.DataReceived += (_, data) =>
        {
            string text;
            lock (received)
            {
                received.Append(Encoding.UTF8.GetString(data.Span));
                text = received.ToString();
            }

            if (text.Contains("hello-from-pty-test"))
                outputSeen.TrySetResult();
        };
        session.ProcessExited += (_, code) => exited.TrySetResult(code);
        session.BeginReading();

        var outputTask = await Task.WhenAny(outputSeen.Task, Task.Delay(TimeSpan.FromSeconds(15)));
        Assert.Same(outputSeen.Task, outputTask);

        var exitTask = await Task.WhenAny(exited.Task, Task.Delay(TimeSpan.FromSeconds(15)));
        Assert.Same(exited.Task, exitTask);
        Assert.Equal(0, await exited.Task);
    }

    [Fact]
    public async Task ProcessExited_ReplaysToLateSubscriberIfAlreadyExited()
    {
        using var session = await PtySession.StartAsync(
            "powershell.exe",
            ["-NoProfile", "-Command", "exit 0"],
            Path.GetTempPath(),
            rows: 24,
            cols: 80);

        session.BeginReading();

        var firstExit = new TaskCompletionSource<int>();
        session.ProcessExited += (_, code) => firstExit.TrySetResult(code);
        await Task.WhenAny(firstExit.Task, Task.Delay(TimeSpan.FromSeconds(15)));
        Assert.True(firstExit.Task.IsCompletedSuccessfully);

        var lateSubscriberExit = new TaskCompletionSource<int>();
        session.ProcessExited += (_, code) => lateSubscriberExit.TrySetResult(code);

        var completed = await Task.WhenAny(lateSubscriberExit.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(lateSubscriberExit.Task, completed);
        Assert.Equal(0, await lateSubscriberExit.Task);
    }
}
