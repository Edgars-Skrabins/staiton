namespace ClaudeCodeTerminal.App.Core;

public sealed record Preset(string Id, string Name, string Executable, IReadOnlyList<string> Args, bool IsBuiltIn)
{
    public override string ToString() => Name;
}
