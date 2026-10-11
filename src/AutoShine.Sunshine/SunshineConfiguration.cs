using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AutoShine.Core;

namespace AutoShine.Sunshine;

public sealed record ConfigurationPlan(string Path, string Fingerprint, string Text, IReadOnlyDictionary<string, string> Changes);
public static class SunshineConfiguration
{
    public static bool HasAutoShineIntegration(string? path)
    {
        if (path is null || !File.Exists(path)) return false;
        var values = SunshineInspector.ReadConfig(path);
        return values.TryGetValue("global_prep_cmd", out var raw) &&
            (JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(raw) ?? []).Any(IsAutoShinePair);
    }
    public static ConfigurationPlan PlanDisplay(AppSettings settings) => Plan(settings, "", true);
    public static ConfigurationPlan Plan(AppSettings settings, string agentPath) => Plan(settings, agentPath, false);
    private static ConfigurationPlan Plan(AppSettings settings, string agentPath, bool displayOnly)
    {
        var errors = SettingsValidator.Validate(displayOnly ? settings with { Audio = new(), Hotkey = new() } : settings);
        if (errors.Count > 0) throw new InvalidDataException(string.Join(" ", errors));
        var path = string.IsNullOrWhiteSpace(settings.Sunshine.ConfigPath) ? SunshineInspector.DetectConfigPath() : settings.Sunshine.ConfigPath;
        if (path is null || !File.Exists(path)) throw new IOException("Sunshine 설정 파일이 없습니다.");
        if (!displayOnly && !File.Exists(agentPath)) throw new IOException("Agent를 먼저 설치하세요.");
        var bytes = File.ReadAllBytes(path); var original = Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF');
        var values = SunshineInspector.ReadConfig(path);
        var displayId = string.IsNullOrWhiteSpace(settings.Display.SunshineDeviceId) && values.TryGetValue("output_name", out var existingDisplay) ? existingDisplay : settings.Display.SunshineDeviceId;
        var prep = values.TryGetValue("global_prep_cmd", out var raw) ? JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(raw) ?? [] : [];
        // Refuse silent takeover of the independently installed automation.
        if (!displayOnly && prep.Any(p => p.Values.Any(v => v.ValueKind == JsonValueKind.String && v.GetString()!.Contains("SunshineAutomation.exe", StringComparison.OrdinalIgnoreCase))))
            throw new IOException("기존 SunshineAutomation 연동이 있습니다. 기존 Agent 중지와 연동 이전을 먼저 완료해야 합니다.");
        prep.RemoveAll(IsAutoShinePair);
        prep.Insert(0, new() { ["do"] = JsonSerializer.SerializeToElement($"\"{agentPath}\" begin"), ["undo"] = JsonSerializer.SerializeToElement($"\"{agentPath}\" end"), ["elevated"] = JsonSerializer.SerializeToElement(false) });
        var changes = new Dictionary<string, string>
        {
            ["global_prep_cmd"] = JsonSerializer.Serialize(prep),
            ["dd_config_revert_on_disconnect"] = settings.Display.HoldOnDisconnect ? "disabled" : "enabled"
        };
        if (!string.IsNullOrWhiteSpace(settings.Display.SunshineDeviceId)) changes["output_name"] = settings.Display.SunshineDeviceId;
        if (settings.Display.Policy != DisplayPolicy.VerifyOnly && Guid.TryParse(displayId, out _))
        {
            changes["dd_configuration_option"] = settings.Display.Policy switch { DisplayPolicy.EnsureActive => "ensure_active", DisplayPolicy.EnsurePrimary => "ensure_primary", _ => "ensure_only_display" };
            changes["dd_resolution_option"] = settings.Display.UseClientResolution ? "auto" : "disabled";
            changes["dd_refresh_rate_option"] = settings.Display.UseClientRefreshRate ? "auto" : "disabled";
        }
        if (displayOnly) { changes.Remove("global_prep_cmd"); changes.Remove("dd_config_revert_on_disconnect"); }
        var lines = Regex.Split(original, "\r?\n").ToList();
        foreach (var (key, value) in changes)
        {
            var indices = lines.Select((line, i) => (line, i)).Where(x => Regex.IsMatch(x.line, @"^\s*" + Regex.Escape(key) + @"\s*=", RegexOptions.IgnoreCase)).Select(x => x.i).ToList();
            if (indices.Count > 1) throw new IOException("중복 Sunshine 설정: " + key);
            if (indices.Count == 1) lines[indices[0]] = key + " = " + value; else lines.Add(key + " = " + value);
        }
        return new(System.IO.Path.GetFullPath(path), Convert.ToHexString(SHA256.HashData(bytes)), string.Join("\r\n", lines), changes);
    }
    public static string Apply(ConfigurationPlan plan)
    {
        if (Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(plan.Path))) != plan.Fingerprint) throw new IOException("Sunshine 설정이 변경되었습니다. 적용 내용을 다시 확인하세요.");
        var backup = plan.Path + ".autoshine-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N") + ".bak";
        var temp = plan.Path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { File.WriteAllText(temp, plan.Text, new UTF8Encoding(false)); File.Replace(temp, plan.Path, backup); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
        return backup;
    }
    static bool IsAutoShinePair(Dictionary<string, JsonElement> command) =>
        command.TryGetValue("do", out var begin) && command.TryGetValue("undo", out var end) &&
        begin.ValueKind == JsonValueKind.String && end.ValueKind == JsonValueKind.String &&
        Regex.IsMatch(begin.GetString()!, "^\\s*\"[^\"]*[\\\\/]AutoShine\\.exe\"\\s+begin\\s*$", RegexOptions.IgnoreCase) &&
        Regex.IsMatch(end.GetString()!, "^\\s*\"[^\"]*[\\\\/]AutoShine\\.exe\"\\s+end\\s*$", RegexOptions.IgnoreCase);

    public static ConfigurationPlan PlanUnlink(SunshineSettings settings)
    {
        var path = string.IsNullOrWhiteSpace(settings.ConfigPath) ? SunshineInspector.DetectConfigPath() : settings.ConfigPath;
        if (path is null || !File.Exists(path)) throw new IOException("Sunshine 설정 파일이 없습니다.");
        var bytes = File.ReadAllBytes(path);
        var lines = Regex.Split(Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF'), "\\r?\\n").ToList();
        var indices = lines.Select((line, i) => (line, i)).Where(x => Regex.IsMatch(x.line, @"^\s*global_prep_cmd\s*=", RegexOptions.IgnoreCase)).Select(x => x.i).ToList();
        if (indices.Count != 1) throw new IOException("준비 명령이 없거나 중복되어 있습니다. Sunshine 설정을 확인하세요.");
        var raw = lines[indices[0]][(lines[indices[0]].IndexOf('=') + 1)..].Trim();
        var prep = JsonSerializer.Deserialize<List<Dictionary<string, JsonElement>>>(raw) ?? [];
        if (prep.RemoveAll(IsAutoShinePair) == 0) throw new IOException("해제할 AutoShine 연동이 없습니다. 사용자 지정 명령은 자동 삭제하지 않습니다.");
        var value = JsonSerializer.Serialize(prep);
        lines[indices[0]] = "global_prep_cmd = " + value;
        return new(Path.GetFullPath(path), Convert.ToHexString(SHA256.HashData(bytes)), string.Join("\r\n", lines), new Dictionary<string, string> { ["global_prep_cmd"] = value });
    }
}
