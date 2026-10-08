using ClaudeCodeTerminal.App.UI;

namespace ClaudeCodeTerminal.Tests.UI;

public class DiffPanelControlTests
{
    [Theory]
    [InlineData("diff --git a/file.txt b/file.txt")]
    [InlineData("index 0000000..1add51e")]
    [InlineData("new file mode 100644")]
    [InlineData("deleted file mode 100644")]
    [InlineData("--- a/file.txt")]
    [InlineData("+++ b/file.txt")]
    public void ClassifyDiffLine_MetadataLinesAreFaint(string line)
    {
        var (foreground, _) = DiffPanelControl.ClassifyDiffLine(line);
        Assert.Equal(Theme.FaintText, foreground);
    }

    [Fact]
    public void ClassifyDiffLine_HunkHeaderUsesHunkColors()
    {
        var (foreground, background) = DiffPanelControl.ClassifyDiffLine("@@ -1,3 +1,4 @@");
        Assert.Equal(Theme.DiffHunkHeader, foreground);
        Assert.Equal(Theme.DiffHunkBackground, background);
    }

    [Fact]
    public void ClassifyDiffLine_AddedLineUsesAddedColors()
    {
        var (foreground, background) = DiffPanelControl.ClassifyDiffLine("+new content");
        Assert.Equal(Theme.Added, foreground);
        Assert.Equal(Theme.DiffAddedBackground, background);
    }

    [Fact]
    public void ClassifyDiffLine_RemovedLineUsesRemovedColors()
    {
        var (foreground, background) = DiffPanelControl.ClassifyDiffLine("-old content");
        Assert.Equal(Theme.Deleted, foreground);
        Assert.Equal(Theme.DiffRemovedBackground, background);
    }

    [Fact]
    public void ClassifyDiffLine_ContextLineUsesPlainColors()
    {
        var (foreground, background) = DiffPanelControl.ClassifyDiffLine(" unchanged line");
        Assert.Equal(Theme.Text, foreground);
        Assert.Equal(Theme.Background, background);
    }

    [Fact]
    public void ClassifyDiffLine_PlusPlusPlusHeaderIsFaintNotAdded()
    {
        var (foreground, _) = DiffPanelControl.ClassifyDiffLine("+++ b/new-file.txt");
        Assert.Equal(Theme.FaintText, foreground);
        Assert.NotEqual(Theme.Added, foreground);
    }

    [Fact]
    public void ClassifyDiffLine_MinusMinusMinusHeaderIsFaintNotDeleted()
    {
        var (foreground, _) = DiffPanelControl.ClassifyDiffLine("--- a/old-file.txt");
        Assert.Equal(Theme.FaintText, foreground);
        Assert.NotEqual(Theme.Deleted, foreground);
    }
}
