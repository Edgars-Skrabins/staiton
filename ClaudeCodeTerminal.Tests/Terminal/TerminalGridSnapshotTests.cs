using System.Text;
using ClaudeCodeTerminal.App.Terminal;

namespace ClaudeCodeTerminal.Tests.Terminal;

public class TerminalGridSnapshotTests
{
    [Fact]
    public void Feed_PlainTextAppearsInFirstRow()
    {
        var grid = new TerminalGridSnapshot(rows: 5, cols: 20);

        grid.Feed(Encoding.UTF8.GetBytes("hello"));

        Assert.True(grid.TryCapture(out var rows, out _));
        var text = string.Concat(rows[0].Spans.Select(s => s.Text));
        Assert.StartsWith("hello", text);
    }

    [Fact]
    public void TryCapture_ReturnsFalseWhenNothingChangedSinceLastCapture()
    {
        var grid = new TerminalGridSnapshot(rows: 5, cols: 20);
        grid.Feed(Encoding.UTF8.GetBytes("hello"));
        grid.TryCapture(out _, out _);

        var changed = grid.TryCapture(out _, out _);

        Assert.False(changed);
    }

    [Fact]
    public void Feed_SgrColorIsReflectedAsWebColor()
    {
        var grid = new TerminalGridSnapshot(rows: 5, cols: 20);

        grid.Feed(Encoding.UTF8.GetBytes("[31mred text[0m"));

        grid.TryCapture(out var rows, out _);
        var coloredSpan = rows[0].Spans.First(s => s.Text.Contains("red"));
        Assert.NotNull(coloredSpan.ForgroundColor);
        Assert.StartsWith("#", coloredSpan.ForgroundColor);
    }

    [Fact]
    public void Feed_SequenceSplitAcrossTwoChunksStillParsesCorrectly()
    {
        var grid = new TerminalGridSnapshot(rows: 5, cols: 20);
        var bytes = Encoding.UTF8.GetBytes("[31mred[0m");
        var splitPoint = bytes.Length / 2;

        grid.Feed(bytes.AsSpan(0, splitPoint));
        grid.Feed(bytes.AsSpan(splitPoint));

        grid.TryCapture(out var rows, out _);
        var text = string.Concat(rows[0].Spans.Select(s => s.Text));
        Assert.Contains("red", text);
    }

    [Fact]
    public void Feed_MultiByteUtf8CharacterSplitAcrossChunksStillDecodesCorrectly()
    {
        var grid = new TerminalGridSnapshot(rows: 5, cols: 20);
        var bytes = Encoding.UTF8.GetBytes("héllo");
        var splitPoint = Array.IndexOf(bytes, bytes.First(b => (b & 0x80) != 0)) + 1;

        grid.Feed(bytes.AsSpan(0, splitPoint));
        grid.Feed(bytes.AsSpan(splitPoint));

        grid.TryCapture(out var rows, out _);
        var text = string.Concat(rows[0].Spans.Select(s => s.Text));
        Assert.StartsWith("héllo", text);
    }

    [Fact]
    public void KeyPressed_ArrowUpProducesEscapeSequenceViaSendData()
    {
        var grid = new TerminalGridSnapshot(rows: 5, cols: 20);
        byte[]? sent = null;
        grid.SendData += (_, data) => sent = data;

        grid.KeyPressed("Up", control: false, shift: false);

        Assert.NotNull(sent);
        Assert.Equal("[A", Encoding.ASCII.GetString(sent!));
    }

    [Fact]
    public void KeyPressed_CtrlCProducesControlByte()
    {
        var grid = new TerminalGridSnapshot(rows: 5, cols: 20);
        byte[]? sent = null;
        grid.SendData += (_, data) => sent = data;

        grid.KeyPressed("C", control: true, shift: false);

        Assert.Equal([0x03], sent);
    }

    [Fact]
    public void KeyPressed_PlainCharacterIsSentAsUtf8Bytes()
    {
        var grid = new TerminalGridSnapshot(rows: 5, cols: 20);
        byte[]? sent = null;
        grid.SendData += (_, data) => sent = data;

        grid.KeyPressed("a", control: false, shift: false);

        Assert.Equal("a"u8.ToArray(), sent);
    }

    [Fact]
    public void Resize_ChangesVisibleDimensions()
    {
        var grid = new TerminalGridSnapshot(rows: 5, cols: 20);

        grid.Resize(10, 40);
        grid.Feed(Encoding.UTF8.GetBytes("resized"));

        grid.TryCapture(out var rows, out _);
        Assert.Equal(10, rows.Count);
    }
}
