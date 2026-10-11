using AutoShine.Core;
using AutoShine.Sunshine;
using AutoShine.Windows;

int passed = 0;
void Assert(bool value) { if (!value) throw new Exception("Assertion failed"); }
void Check(string name, Action test) { test(); passed++; Console.WriteLine("PASS " + name); }
var root = Path.Combine(Path.GetTempPath(), "autoshine-integration-" + Guid.NewGuid()); Directory.CreateDirectory(root);
try
{
    var path = Path.Combine(root, "sunshine.conf"); var agent = Path.Combine(root, "AutoShine.exe"); File.WriteAllText(agent, "test fixture");
    var settings = new AppSettings { Sunshine = new() { ConfigPath = path } };
    Check("preserve unrelated options, comments and preparation commands", () => {
        File.WriteAllText(path, "# user's setting\nlocale = ko\ndd_hdr_option = disabled\nglobal_prep_cmd = [{\"do\":\"echo user\",\"undo\":\"\"}]\n");
        var plan = SunshineConfiguration.Plan(settings, agent);
        Assert(plan.Text.Contains("locale = ko") && plan.Text.Contains("# user's setting") && plan.Text.Contains("echo user"));
        Assert(!plan.Changes.ContainsKey("dd_configuration_option"));
        var backup = SunshineConfiguration.Apply(plan); Assert(File.Exists(backup) && File.ReadAllText(backup).Contains("echo user"));
    });
    Check("reapply does not duplicate own hooks", () => { var plan = SunshineConfiguration.Plan(settings, agent); var commands = System.Text.Json.JsonDocument.Parse(plan.Changes["global_prep_cmd"]); Assert(commands.RootElement.GetArrayLength() == 2); });
    Check("unlink custom path preserves user hooks and display policy", () => {
        File.AppendAllText(path, "dd_configuration_option = ensure_primary\n");
        var unlink = SunshineConfiguration.PlanUnlink(settings.Sunshine);
        Assert(unlink.Changes.Count == 1 && unlink.Text.Contains("echo user") && unlink.Text.Contains("dd_configuration_option = ensure_primary"));
        Assert(!unlink.Text.Contains("AutoShine.exe")); SunshineConfiguration.Apply(unlink);
    });
    Check("unlink refuses user custom autoshine command", () => {
        File.WriteAllText(path, "global_prep_cmd = [{\"do\":\"echo AutoShine.exe\",\"undo\":\"\"}]");
        try { SunshineConfiguration.PlanUnlink(settings.Sunshine); throw new Exception("custom command removed"); } catch (IOException) { }
    });
    Check("changed configuration rejects stale apply", () => { var plan = SunshineConfiguration.Plan(settings, agent); File.AppendAllText(path, "locale = en\n"); try { SunshineConfiguration.Apply(plan); throw new Exception("stale plan accepted"); } catch (IOException) { } });
    Check("legacy automation refuses silent takeover", () => { File.WriteAllText(path, "global_prep_cmd = [{\"do\":\"SunshineAutomation.exe begin\"}]"); try { SunshineConfiguration.Plan(settings, agent); throw new Exception("legacy hook accepted"); } catch (IOException) { } });
    Check("display inventory uses latest complete Sunshine snapshot", () => {
        var id="{11111111-2222-3333-4444-555555555555}";
        var json=System.Text.Json.JsonSerializer.Serialize(new[]{new {device_id=id,friendly_name="Virtual display",display_name=@"\\.\DISPLAY1",info=(object?)null}});
        var devices=SunshineInspector.ParseDisplays("Currently available devices:\n"+json+"\nCurrently available devices:\n[");
        Assert(devices.Count==1 && devices[0].Id==id && devices[0].Name=="Virtual display" && devices[0].DisplayName==@"\\.\DISPLAY1" && devices[0].Virtual);
        Assert(SunshineInspector.ParseDisplays("no inventory").Count==0);
    });
    Check("display-only plan leaves automation commands untouched", () => {
        File.WriteAllText(path,"global_prep_cmd = [{\"do\":\"SunshineAutomation.exe begin\"}]\nlocale = ko");
        var plan=SunshineConfiguration.PlanDisplay(settings with {Display=new(){SunshineDeviceId="{11111111-2222-3333-4444-555555555555}"}});
        Assert(!plan.Changes.ContainsKey("global_prep_cmd") && !plan.Changes.ContainsKey("dd_config_revert_on_disconnect") && plan.Text.Contains("SunshineAutomation.exe begin"));
        Assert(plan.Changes["output_name"]=="{11111111-2222-3333-4444-555555555555}");
    });
    Check("duplicate managed config refuses ambiguous rewrite", () => { File.WriteAllText(path, "dd_config_revert_on_disconnect = enabled\ndd_config_revert_on_disconnect = disabled"); try { SunshineConfiguration.Plan(settings, agent); throw new Exception("duplicate accepted"); } catch (IOException) { } });
    Check("display UUID applies without Windows device-path mapping", () => { File.WriteAllText(path, "locale = ko"); var plan = SunshineConfiguration.Plan(settings with { Display = new() { SunshineDeviceId = "{11111111-2222-3333-4444-555555555555}" } }, agent); Assert(plan.Changes["dd_configuration_option"] == "ensure_only_display" && plan.Changes["dd_resolution_option"] == "auto"); });
    Check("existing Sunshine UUID supports policy updates without remapping", () => { File.WriteAllText(path, "output_name = {11111111-2222-3333-4444-555555555555}"); var plan = SunshineConfiguration.Plan(settings, agent); Assert(plan.Changes["dd_configuration_option"] == "ensure_only_display" && !plan.Changes.ContainsKey("output_name")); });
    Check("product rename moves existing user data without changing contents", () => { var local = Path.Combine(root, "rename-data"); var old = Path.Combine(local, "SunshineCompanion"); Directory.CreateDirectory(old); File.WriteAllText(Path.Combine(old, "settings.json"), "preserved"); var renamed = ProductData.ResolveRoot(local); Assert(File.ReadAllText(Path.Combine(renamed, "settings.json")) == "preserved" && !Directory.Exists(old)); Assert(ProductData.ResolveRoot(local) == renamed); });
    Check("product rename preserves an existing destination", () => { var local = Path.Combine(root, "existing-data"); Directory.CreateDirectory(Path.Combine(local, "AutoShine")); Directory.CreateDirectory(Path.Combine(local, "SunshineCompanion")); File.WriteAllText(Path.Combine(local, "AutoShine", "settings.json"), "current"); Assert(File.ReadAllText(Path.Combine(ProductData.ResolveRoot(local), "settings.json")) == "current" && Directory.Exists(Path.Combine(local, "SunshineCompanion"))); });
    Check("VDD presets preserve other settings and backup", () => {
        var vdd = Path.Combine(root, "vdd.xml");
        var xml = "<vdd_settings><monitors><count>2</count></monitors><gpu><friendlyname>default</friendlyname></gpu><global><!--keep--><g_refresh_rate>60</g_refresh_rate></global><resolutions><resolution><width>1920</width><height>1080</height><refresh_rate>30</refresh_rate></resolution></resolutions><options><HardwareCursor>true</HardwareCursor></options></vdd_settings>";
        File.WriteAllText(vdd, xml);
        var original = VddPresets.Read(vdd);
        VddPresets.Save(original with { GlobalRates = [60, 120], Presets = [new(2560, 1440, [90], "게임 -- & <화면> 🎮")] }, vdd);
        var saved = VddPresets.Read(vdd);
        Assert(saved.Presets.Single().Name == "게임 -- & <화면> 🎮");
        Assert(saved.Presets.Single().Width == 2560 && saved.GlobalRates.SequenceEqual(new[] {60, 120}) && saved.Presets[0].RefreshRates.SequenceEqual(new[] {90}));
        Assert(File.ReadAllText(vdd).Contains("<!--keep-->") && File.ReadAllText(vdd).Contains("<count>2</count>") && File.ReadAllText(vdd).Contains("<HardwareCursor>true</HardwareCursor>") && File.ReadAllText(vdd + ".autoshine.bak") == xml);
        try { VddPresets.Save(original, vdd); throw new Exception("stale VDD accepted"); } catch (IOException) { }
        var bytes = File.ReadAllBytes(vdd);
        try { VddPresets.Save(saved with {Presets = [new(0, 1080, [60])]}, vdd); throw new Exception("invalid VDD accepted"); } catch (IOException) { }
        Assert(bytes.SequenceEqual(File.ReadAllBytes(vdd)));
    });
    Check("VDD rejects XML external entities", () => {
        var vdd = Path.Combine(root, "bad-vdd.xml");
        File.WriteAllText(vdd, "<!DOCTYPE vdd_settings [<!ENTITY secret SYSTEM 'file:///C:/private'>]><vdd_settings><resolutions>&secret;</resolutions></vdd_settings>");
        try { VddPresets.Read(vdd); throw new Exception("DTD accepted"); } catch (IOException) { }
    });
    Check("display inventory excludes unattached GPU output placeholders", () => {
        var displays=DeviceInventory.Displays();
        Assert(displays.All(d=>d.Virtual || d.Enabled || !string.IsNullOrWhiteSpace(d.Name.Split(" · ").Last())));
    });
    Check("Windows device and volume reads are non-mutating", () => { var devices = DeviceInventory.AudioOutputs(); Assert(devices.Count > 0); foreach (var device in devices.Where(d => d.State == DeviceState.Active)) _ = DeviceInventory.EndpointMute(device.Id); for (int role = 0; role < 3; role++) _ = DeviceInventory.DefaultOutput(role); });
}
finally { Directory.Delete(root, true); }
Console.WriteLine($"{passed} integration tests passed");

