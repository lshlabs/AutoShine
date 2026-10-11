using System.Net;
using System.Text.RegularExpressions;
using System.Xml;
using System.Text.Json;
using Microsoft.Win32;

namespace AutoShine.Sunshine;

public sealed record SunshineSnapshot(string? ConfigPath, string DeviceId, int BasePort, bool Reachable, int? AppId, string Message);

public sealed record SunshineDisplay(string Id, string Name, bool Virtual, string DisplayName = "");

public static partial class SunshineInspector
{
    static readonly HttpClient Client = new(new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(3) };
    public static IReadOnlyList<SunshineDisplay> ReadDisplays(string? configPath)
    {
        if (configPath is null || !File.Exists(configPath)) return [];
        var configured = ReadConfig(configPath).GetValueOrDefault("log_path", "sunshine.log");
        var path = Path.IsPathFullyQualified(configured) ? configured : Path.Combine(Path.GetDirectoryName(configPath)!, configured);
        if (!File.Exists(path)) return [];
        try {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            stream.Seek(Math.Max(0, stream.Length - 8 * 1024 * 1024), SeekOrigin.Begin);
            using var reader = new StreamReader(stream);
            return ParseDisplays(reader.ReadToEnd());
        } catch (IOException) { return []; } catch (UnauthorizedAccessException) { return []; }
    }
    public static IReadOnlyList<SunshineDisplay> ParseDisplays(string log)
    {
        var matches = Regex.Matches(log, @"Currently available (?:display )?devices:\s*(\[)");
        for (var m = matches.Count - 1; m >= 0; m--) {
            var start = matches[m].Groups[1].Index;
            try {
                var bytes = System.Text.Encoding.UTF8.GetBytes(log[start..]);
                var reader = new Utf8JsonReader(bytes);
                using var document = JsonDocument.ParseValue(ref reader);
                var result = new List<SunshineDisplay>();
                foreach (var item in document.RootElement.EnumerateArray()) {
                    if (!item.TryGetProperty("device_id", out var id) || !Guid.TryParse(id.GetString(), out _)) continue;
                    var name = item.TryGetProperty("friendly_name", out var friendly) ? friendly.GetString() : null;
                    if (string.IsNullOrWhiteSpace(name)) name = item.TryGetProperty("display_name", out var display) ? display.GetString() : null;
                    var virtualDisplay = Regex.IsMatch(name ?? "", @"\b(VDD|IDD|Virtual[ -]?Display)\b", RegexOptions.IgnoreCase);
                    if (item.TryGetProperty("edid", out var edid) && edid.ValueKind == JsonValueKind.Object && edid.TryGetProperty("manufacturer_id", out var manufacturer) && edid.TryGetProperty("product_code", out var product))
                        virtualDisplay |= string.Equals(manufacturer.GetString(), "MTT", StringComparison.OrdinalIgnoreCase) && string.Equals(product.GetString(), "1337", StringComparison.OrdinalIgnoreCase);
                    if (!string.IsNullOrWhiteSpace(name)) result.Add(new(id.GetString()!, name, virtualDisplay, item.TryGetProperty("display_name", out var nativeName) ? nativeName.GetString() ?? "" : ""));
                }
                if (result.Count > 0) return result.DistinctBy(d => d.Id).ToList();
            } catch (JsonException) { }
        }
        return [];
    }
    public static string? DetectConfigPath()
    {
        var candidates = new List<string>();
        using (var service = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\SunshineService"))
        {
            var image = Convert.ToString(service?.GetValue("ImagePath")) ?? "";
            var match = ExecutablePath().Match(image);
            if (match.Success)
            {
                var executable = Environment.ExpandEnvironmentVariables(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value);
                var directory = Path.GetDirectoryName(executable)!;
                candidates.Add(Path.GetFullPath(Path.Combine(directory, "..", "config", "sunshine.conf")));
                candidates.Add(Path.Combine(directory, "config", "sunshine.conf"));
            }
        }
        candidates.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Sunshine", "config", "sunshine.conf"));
        return candidates.FirstOrDefault(File.Exists);
    }
    public static IReadOnlyDictionary<string, string> ReadConfig(string path)
    {
        var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in File.ReadLines(path))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;
            var equals = trimmed.IndexOf('=');
            if (equals <= 0) continue;
            values[trimmed[..equals].Trim()] = trimmed[(equals + 1)..].Trim();
        }
        return values;
    }
    public static async Task<SunshineSnapshot> InspectAsync(string? configuredPath, int configuredPort, CancellationToken cancellationToken = default, bool preferConfiguredPort = false)
    {
        var path = string.IsNullOrWhiteSpace(configuredPath) ? DetectConfigPath() : configuredPath;
        var port = configuredPort;
        var id = "";
        if (path is not null && File.Exists(path))
        {
            var config = ReadConfig(path);
            id = config.GetValueOrDefault("output_name", "");
            if (!preferConfiguredPort && config.TryGetValue("port", out var value) && int.TryParse(value, out var candidate) && candidate is >= 1024 and <= 65514) port = candidate;
        }
        try
        {
            var response = await Client.GetStringAsync($"http://127.0.0.1:{port}/serverinfo", cancellationToken);
            var document = new XmlDocument { XmlResolver = null };
            using var reader = XmlReader.Create(new StringReader(response), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            document.Load(reader);
            if (document.DocumentElement?.GetAttribute("status_code") != "200" || !int.TryParse(document.SelectSingleNode("/root/currentgame")?.InnerText, out var appId))
                return new(path, id, port, false, null, "Sunshine 응답을 확인할 수 없습니다.");
            return new(path, id, port, true, appId, appId > 0 ? "원격 세션이 있습니다." : "연결됨 · 실행 중인 세션 없음");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { return new(path, id, port, false, null, "Sunshine 응답 시간이 초과되었습니다."); }
        catch (HttpRequestException) { return new(path, id, port, false, null, "Sunshine에 연결할 수 없습니다."); }
        catch (XmlException) { return new(path, id, port, false, null, "Sunshine 응답을 확인할 수 없습니다."); }
    }
    [GeneratedRegex("^\\s*(?:\"([^\"]+\\.exe)\"|([^\"]+?\\.exe))(?:\\s|$)", RegexOptions.IgnoreCase)]
    private static partial Regex ExecutablePath();
}
