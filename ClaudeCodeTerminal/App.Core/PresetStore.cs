using System.Text.Json;

namespace ClaudeCodeTerminal.App.Core;

public sealed class PresetStore
{
    private const string BuiltInClaudeId = "builtin-claude";

    private readonly string _filePath;
    private List<Preset> _presets = [];

    public PresetStore(string dataFolderPath)
    {
        _filePath = Path.Combine(dataFolderPath, "presets.json");
    }

    public IReadOnlyList<Preset> Presets => _presets;

    public void EnsureLoaded()
    {
        if (!File.Exists(_filePath))
        {
            _presets = [BuiltInClaudePreset()];
            Save();
            return;
        }

        try
        {
            var json = File.ReadAllText(_filePath);
            var file = JsonSerializer.Deserialize<PresetsFile>(json) ?? new PresetsFile();
            _presets = file.Presets
                .Select(p => new Preset(p.Id, p.Name, p.Executable, p.Args, p.IsBuiltIn))
                .ToList();

            if (!_presets.Any(p => p.Id == BuiltInClaudeId))
                _presets.Insert(0, BuiltInClaudePreset());
        }
        catch (JsonException)
        {
            BackupCorruptFile();
            _presets = [BuiltInClaudePreset()];
            Save();
        }
    }

    public Preset AddPreset(string name, string executable, IReadOnlyList<string> args)
    {
        var preset = new Preset(Guid.NewGuid().ToString("N"), name, executable, args, IsBuiltIn: false);
        _presets.Add(preset);
        Save();
        return preset;
    }

    public void RemovePreset(string id)
    {
        var preset = _presets.FirstOrDefault(p => p.Id == id);
        if (preset is null || preset.IsBuiltIn)
            return;

        _presets.Remove(preset);
        Save();
    }

    private void BackupCorruptFile()
    {
        try
        {
            File.Copy(_filePath, _filePath + ".bak", overwrite: true);
        }
        catch (IOException)
        {
        }
    }

    private static Preset BuiltInClaudePreset() =>
        new(BuiltInClaudeId, "Claude Code", "claude", [], IsBuiltIn: true);

    private void Save()
    {
        var file = new PresetsFile
        {
            Version = 1,
            Presets = _presets
                .Select(p => new PresetDto
                {
                    Id = p.Id,
                    Name = p.Name,
                    Executable = p.Executable,
                    Args = p.Args.ToList(),
                    IsBuiltIn = p.IsBuiltIn,
                })
                .ToList(),
        };

        var json = JsonSerializer.Serialize(file, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_filePath, json);
    }

    private sealed class PresetsFile
    {
        public int Version { get; set; } = 1;
        public List<PresetDto> Presets { get; set; } = [];
    }

    private sealed class PresetDto
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Executable { get; set; } = "";
        public List<string> Args { get; set; } = [];
        public bool IsBuiltIn { get; set; }
    }
}
