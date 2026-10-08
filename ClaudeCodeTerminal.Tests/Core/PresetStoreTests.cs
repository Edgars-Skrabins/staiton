using ClaudeCodeTerminal.App.Core;
using ClaudeCodeTerminal.Tests.TestSupport;

namespace ClaudeCodeTerminal.Tests.Core;

public class PresetStoreTests
{
    [Fact]
    public void EnsureLoaded_CreatesBuiltInPresetWhenFileMissing()
    {
        using var temp = new TempDirectory();
        var store = new PresetStore(temp.Path);

        store.EnsureLoaded();

        var preset = Assert.Single(store.Presets);
        Assert.True(preset.IsBuiltIn);
        Assert.Equal("claude", preset.Executable);
        Assert.True(File.Exists(Path.Combine(temp.Path, "presets.json")));
    }

    [Fact]
    public void AddPreset_PersistsAcrossReload()
    {
        using var temp = new TempDirectory();
        var store = new PresetStore(temp.Path);
        store.EnsureLoaded();

        store.AddPreset("Claude Opus", "claude", ["--model", "opus"]);

        var reloaded = new PresetStore(temp.Path);
        reloaded.EnsureLoaded();

        Assert.Equal(2, reloaded.Presets.Count);
        Assert.Contains(reloaded.Presets, p => p.Name == "Claude Opus" && p.Args.SequenceEqual(["--model", "opus"]));
    }

    [Fact]
    public void RemovePreset_CannotRemoveBuiltIn()
    {
        using var temp = new TempDirectory();
        var store = new PresetStore(temp.Path);
        store.EnsureLoaded();
        var builtInId = store.Presets[0].Id;

        store.RemovePreset(builtInId);

        Assert.Single(store.Presets);
    }

    [Fact]
    public void RemovePreset_RemovesUserDefinedPreset()
    {
        using var temp = new TempDirectory();
        var store = new PresetStore(temp.Path);
        store.EnsureLoaded();
        var added = store.AddPreset("Custom", "custom.exe", []);

        store.RemovePreset(added.Id);

        Assert.Single(store.Presets);
    }

    [Fact]
    public void EnsureLoaded_RecoversFromCorruptFile()
    {
        using var temp = new TempDirectory();
        File.WriteAllText(Path.Combine(temp.Path, "presets.json"), "{ not valid json");

        var store = new PresetStore(temp.Path);
        store.EnsureLoaded();

        Assert.Single(store.Presets);
        Assert.True(File.Exists(Path.Combine(temp.Path, "presets.json.bak")));
    }
}
