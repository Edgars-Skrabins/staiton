namespace ClaudeCodeTerminal.App.Core;

public static class GitignoreManager
{
    public static void EnsureIgnored(string projectRoot, string entry)
    {
        var gitignorePath = Path.Combine(projectRoot, ".gitignore");

        var existingContent = File.Exists(gitignorePath)
            ? File.ReadAllText(gitignorePath)
            : string.Empty;

        var lineEnding = existingContent.Contains("\r\n") ? "\r\n" : "\n";
        var normalizedEntry = entry.Trim().TrimStart('/').TrimEnd('/');

        var alreadyPresent = existingContent
            .Replace("\r\n", "\n")
            .Split('\n')
            .Any(line => line.Trim().TrimStart('/').TrimEnd('/') == normalizedEntry);

        if (alreadyPresent)
            return;

        var needsLeadingNewline =
            existingContent.Length > 0
            && !existingContent.EndsWith("\n", StringComparison.Ordinal)
            && !existingContent.EndsWith("\r\n", StringComparison.Ordinal);

        var toAppend = (needsLeadingNewline ? lineEnding : string.Empty) + entry + lineEnding;

        File.AppendAllText(gitignorePath, toAppend);
    }
}
