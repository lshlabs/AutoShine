using System.Text.Json;
using System.Text.Json.Serialization;

namespace AutoShine.Core;

public sealed class SettingsStore(string path)
{
    public static JsonSerializerOptions JsonOptions { get; } = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) }
    };
    public string Path { get; } = System.IO.Path.GetFullPath(path);
    public AppSettings Load()
    {
        if (!File.Exists(Path)) return new();
        var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(Path), JsonOptions) ?? throw new InvalidDataException("설정이 비어 있습니다.");
        var errors = SettingsValidator.Validate(settings);
        if (errors.Count > 0) throw new InvalidDataException(string.Join(Environment.NewLine, errors));
        return settings;
    }
    public void Save(AppSettings value)
    {
        var errors = SettingsValidator.Validate(value);
        if (errors.Count > 0) throw new InvalidDataException(string.Join(Environment.NewLine, errors));
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        var temporary = Path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(value, JsonOptions));
            if (File.Exists(Path)) File.Replace(temporary, Path, Path + ".bak");
            else File.Move(temporary, Path);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
