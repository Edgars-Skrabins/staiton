using ClaudeCodeTerminal.App.Core;
using ClaudeCodeTerminal.Tests.TestSupport;

namespace ClaudeCodeTerminal.Tests.Core;

public class GitignoreManagerTests
{
    [Fact]
    public void CreatesGitignoreWhenMissing()
    {
        using var temp = new TempDirectory();

        GitignoreManager.EnsureIgnored(temp.Path, ".sain/");

        var content = File.ReadAllText(Path.Combine(temp.Path, ".gitignore"));
        Assert.Contains(".sain/", content);
    }

    [Fact]
    public void AppendsToExistingGitignoreWithoutTrailingNewline()
    {
        using var temp = new TempDirectory();
        var gitignorePath = Path.Combine(temp.Path, ".gitignore");
        File.WriteAllText(gitignorePath, "bin/\nobj/");

        GitignoreManager.EnsureIgnored(temp.Path, ".sain/");

        var lines = File.ReadAllText(gitignorePath).Replace("\r\n", "\n").Split('\n');
        Assert.Contains("bin/", lines);
        Assert.Contains("obj/", lines);
        Assert.Contains(".sain/", lines);
    }

    [Fact]
    public void DoesNotDuplicateExistingEntry()
    {
        using var temp = new TempDirectory();
        var gitignorePath = Path.Combine(temp.Path, ".gitignore");
        File.WriteAllText(gitignorePath, ".sain/\n");

        GitignoreManager.EnsureIgnored(temp.Path, ".sain/");

        var occurrences = File.ReadAllText(gitignorePath).Split(".sain").Length - 1;
        Assert.Equal(1, occurrences);
    }

    [Fact]
    public void RecognizesEntryRegardlessOfSlashVariant()
    {
        using var temp = new TempDirectory();
        var gitignorePath = Path.Combine(temp.Path, ".gitignore");
        File.WriteAllText(gitignorePath, "/.sain\n");

        GitignoreManager.EnsureIgnored(temp.Path, ".sain/");

        var nonEmptyLines = File.ReadAllText(gitignorePath)
            .Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Single(nonEmptyLines);
    }
}
