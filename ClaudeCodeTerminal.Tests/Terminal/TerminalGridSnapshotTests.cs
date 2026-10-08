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

    [Fact]
    public void KeyPressed_EnterSendsCarriageReturnNotLineFeed()
    {
        var grid = new TerminalGridSnapshot(rows: 5, cols: 20);
        byte[]? sent = null;
        grid.SendData += (_, data) => sent = data;

        grid.KeyPressed("Enter", control: false, shift: false);

        Assert.Equal([0x0D], sent);
    }

    [Fact]
    public void Feed_ChangesAccumulateAcrossMultipleFeedCallsUntilCaptured()
    {
        var grid = new TerminalGridSnapshot(rows: 5, cols: 20);

        grid.Feed(Encoding.UTF8.GetBytes("hello"));
        grid.Feed(Encoding.UTF8.GetBytes("[0m"));

        var changed = grid.TryCapture(out var rows, out _);

        Assert.True(changed);
        var text = string.Concat(rows.SelectMany(r => r.Spans).Select(s => s.Text));
        Assert.Contains("hello", text);
    }

    [Fact]
    public void Feed_LineWiderThanEightyColumnsIsNotAutoWrapped()
    {
        var grid = new TerminalGridSnapshot(rows: 5, cols: 123);
        var longLine = new string('x', 100);

        grid.Feed(Encoding.UTF8.GetBytes(longLine));

        grid.TryCapture(out var rows, out _);
        var row0Text = string.Concat(rows[0].Spans.Select(s => s.Text)).TrimEnd();
        var row1Text = string.Concat(rows[1].Spans.Select(s => s.Text)).TrimEnd();
        Assert.Equal(longLine, row0Text);
        Assert.Equal(string.Empty, row1Text);
    }

    [Fact]
    public void Resize_WidensWrapBoundaryNotJustVisibleDimensions()
    {
        var grid = new TerminalGridSnapshot(rows: 5, cols: 20);
        grid.Resize(5, 123);
        var longLine = new string('y', 100);

        grid.Feed(Encoding.UTF8.GetBytes(longLine));

        grid.TryCapture(out var rows, out _);
        var row0Text = string.Concat(rows[0].Spans.Select(s => s.Text)).TrimEnd();
        Assert.Equal(longLine, row0Text);
    }

    [Fact]
    public void Feed_OscWindowTitleWithUnicodePayloadDoesNotLeakIntoScreen()
    {
        var grid = new TerminalGridSnapshot(rows: 5, cols: 40);

        // ESC ] 0 ; <sparkle U+2733, UTF-8 encodes to E2 9C B3 - the 9C byte collides with the
        // raw 8-bit String Terminator (ST) if the OSC payload isn't read UTF-8-aware> Title ESC \
        byte[] oscTitle =
        [
            0x1b, 0x5d, 0x30, 0x3b,
            0xe2, 0x9c, 0xb3, 0x20,
            0x54, 0x69, 0x74, 0x6c, 0x65,
            0x1b, 0x5c,
        ];

        grid.Feed(oscTitle);
        grid.Feed(Encoding.UTF8.GetBytes("Hello World"));

        grid.TryCapture(out var rows, out _);
        var row0Text = string.Concat(rows[0].Spans.Select(s => s.Text)).TrimEnd();
        Assert.Equal("Hello World", row0Text);
    }

    [Fact]
    public void Feed_OscTerminatedWithSevenBitStringTerminatorIsFullyConsumed()
    {
        var grid = new TerminalGridSnapshot(rows: 5, cols: 40);

        // ESC ] 0 ; title ESC \  -- the 7-bit form of the String Terminator, no accidental
        // byte collisions in this payload.
        byte[] oscTitle = [0x1b, 0x5d, 0x30, 0x3b, .."title"u8.ToArray(), 0x1b, 0x5c];

        grid.Feed(oscTitle);
        grid.Feed(Encoding.UTF8.GetBytes("after"));

        grid.TryCapture(out var rows, out _);
        var row0Text = string.Concat(rows[0].Spans.Select(s => s.Text)).TrimEnd();
        Assert.Equal("after", row0Text);
    }

    [Fact]
    public void Feed_CsiLessThanPrefixedSequenceDoesNotLeakTrailingCharacter()
    {
        var grid = new TerminalGridSnapshot(rows: 5, cols: 40);

        // CSI < u - Kitty keyboard protocol "pop keyboard enhancement flags", not implemented,
        // but must still be recognized as a complete, consumed sequence.
        byte[] kittyPop = [0x1b, 0x5b, 0x3c, 0x75];

        grid.Feed(kittyPop);
        grid.Feed(Encoding.UTF8.GetBytes("World"));

        grid.TryCapture(out var rows, out _);
        var row0Text = string.Concat(rows[0].Spans.Select(s => s.Text)).TrimEnd();
        Assert.Equal("World", row0Text);
    }
}
