using System.Security.Cryptography;
using System.Xml;
using System.Xml.Linq;

namespace AutoShine.Windows;

public sealed record VddPreset(int Width, int Height, List<int> RefreshRates, string Name = "");
public sealed record VddPresetSettings(string Fingerprint, List<int> GlobalRates, List<VddPreset> Presets);
public static class VddPresets
{
    public const string DefaultPath = @"C:\VirtualDisplayDriver\vdd_settings.xml";
    static XDocument Parse(byte[] bytes)
    {
        using var stream = new MemoryStream(bytes);
        using var reader = XmlReader.Create(stream, new() { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 1024 * 1024 });
        XDocument document;
        try { document = XDocument.Load(reader, LoadOptions.PreserveWhitespace); }
        catch (XmlException ex) { throw new IOException("Invalid VDD XML", ex); }
        if (document.Root?.Name != "vdd_settings" || document.Root.Elements("resolutions").Count() != 1) throw new IOException("Invalid VDD settings structure");
        return document;
    }
    const string NamePrefix = "AutoShine preset name: ";
    static string ReadName(XElement resolution)
    {
        var value = resolution.Nodes().OfType<XComment>().FirstOrDefault(c => c.Value.StartsWith(NamePrefix))?.Value[NamePrefix.Length..];
        if (value is null) return "";
        try { return System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(value)); }
        catch (FormatException) { return ""; }
    }
    public static VddPresetSettings Read(string path = DefaultPath)
    {
        var bytes = File.ReadAllBytes(path); var root = Parse(bytes).Root!;
        int Number(XElement node) => int.TryParse(node.Value, out var number) ? number : throw new IOException("Invalid VDD number");
        return new(Convert.ToHexString(SHA256.HashData(bytes)), root.Element("global")?.Elements("g_refresh_rate").Select(Number).ToList() ?? [],
            root.Element("resolutions")!.Elements("resolution").Select(r => new VddPreset(Number(r.Element("width") ?? throw new IOException("Missing VDD width")), Number(r.Element("height") ?? throw new IOException("Missing VDD height")), r.Elements("refresh_rate").Select(Number).ToList(), ReadName(r))).ToList());
    }
    public static void Save(VddPresetSettings settings, string path = DefaultPath)
    {
        bool Rates(List<int>? rates) => rates is not null && rates.Count <= 64 && rates.All(r => r is >= 1 and <= 1000) && rates.Distinct().Count() == rates.Count;
        if (!Rates(settings.GlobalRates) || settings.Presets is null || settings.Presets.Count is < 1 or > 128 || settings.Presets.Any(p => p is null || p.Name is null || p.Name.Length > 80 || p.Name.Any(char.IsControl) || p.Width is < 1 or > 16384 || p.Height is < 1 or > 16384 || !Rates(p.RefreshRates) || p.RefreshRates.Count == 0 && settings.GlobalRates.Count == 0) || settings.Presets.Select(p => (p.Width, p.Height)).Distinct().Count() != settings.Presets.Count)
            throw new IOException("Invalid or duplicate VDD preset");
        var bytes = File.ReadAllBytes(path);
        if (Convert.ToHexString(SHA256.HashData(bytes)) != settings.Fingerprint) throw new IOException("VDD settings changed; reload before saving");
        var document = Parse(bytes); var root = document.Root!;
        var global = root.Element("global");
        if (global is null) { global = new XElement("global"); root.Add(global); }
        global.Elements("g_refresh_rate").Remove();
        global.Add(settings.GlobalRates.Select(r => new XElement("g_refresh_rate", r)));
        var resolutions = root.Element("resolutions")!;
        resolutions.Elements("resolution").Remove();
        resolutions.Add(settings.Presets.Select(p => new XElement("resolution", string.IsNullOrWhiteSpace(p.Name) ? null : new XComment(NamePrefix + Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(p.Name.Trim()))), new XElement("width", p.Width), new XElement("height", p.Height), p.RefreshRates.Select(r => new XElement("refresh_rate", r)))));
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try { document.Save(temporary); File.Replace(temporary, path, path + ".autoshine.bak"); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
