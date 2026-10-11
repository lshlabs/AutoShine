using AutoShine.Core;

var passed = 0;
void Check(string name, Action test) { test(); passed++; Console.WriteLine($"PASS {name}"); }
void Assert(bool condition) { if (!condition) throw new Exception("Assertion failed"); }
void Invalid(AppSettings value) => Assert(SettingsValidator.Validate(value).Count > 0);
Check("independent start activation and sound states", () => {
    foreach(var enabled in new[]{true,false}) foreach(var muted in new[]{true,false}) {
        var audio=new FakeAudio();var engine=new AudioSession(audio);
        var policy=new AudioSettings {Rules=[new(){Device=new("speaker","speaker"),StartEnabled=enabled,StartMuted=muted}]};
        engine.Start(engine.Capture(policy));
        Assert(audio.Devices().Single(d=>d.Id=="speaker").State==(enabled?DeviceState.Active:DeviceState.Disabled));
        Assert(audio.Muted("speaker")==muted);
        engine.Protect(policy);Assert(audio.Muted("speaker")==muted);
    }
});
Check("safe default policy", () => { var s = new AppSettings(); Assert(SettingsValidator.Validate(s).Count == 0 && !s.Audio.ReapplyOnLocalReconnect && s.Audio.Restore == RestoreStrategy.PreviousState); });
Check("null settings rejected without crashing", () => Assert(SettingsValidator.Validate(null).Count > 0));
Check("previous default role cannot restore explicitly disabled output", () => { var audio = new FakeAudio(); audio.SetDefault("remote", 2); var engine = new AudioSession(audio); var saved = engine.Capture(new() { Rules = [new() { Device = new("remote", "remote"), EndEnabled = false, End = EndAction.Muted }] }); engine.End(saved); Assert(audio.Default(2) == "speaker" && audio.Devices().Single(d => d.Id == "remote").State == DeviceState.Disabled); });
Check("simulation rejects null devices before engine execution", () => { try { AudioSimulation.Run(new("drop", new(), [null!], [null, null, null], null)); throw new Exception("accepted malformed device"); } catch (IOException) { } });
Check("simulation rejects endpoint IDs differing only by case", () => { try { AudioSimulation.Run(new("drop", new(), [new("dac", "a", true, true, false), new("DAC", "b", true, true, false)], [null, null, null], null)); throw new Exception("accepted duplicate device"); } catch (IOException) { } });
Check("bare special hotkey validation ignores case", () => Assert(SettingsValidator.Validate(new() { Hotkey = new() { Key = "scrolllock", Control = false, Alt = false, Shift = false } }).Count == 0));
Check("protection enumerates once and still mutes every protected output", () => { var audio = new FakeAudio(); var engine = new AudioSession(audio); engine.Protect(new() { Rules = [new() { Device = new("dac", "dac"), Start = StartAction.Mute }, new() { Device = new("speaker", "speaker"), Start = StartAction.Disable }] }); Assert(audio.DeviceReads == 1 && audio.Muted("dac") && audio.Muted("speaker")); });
Check("future schema rejected", () => Invalid(new() { SchemaVersion = 99 }));
Check("missing section rejected", () => Invalid(new() { Audio = null! }));
Check("null hotkey rejected", () => Invalid(new() { Hotkey = new() { Key = null! } }));
Check("null display ID rejected", () => Invalid(new() { Display = new() { SunshineDeviceId = null! } }));
Check("malformed output binding rejected", () => Invalid(new() { Audio = new() { RemoteOutput = new(null!, "DAC") } }));
Check("null fallback binding rejected", () => Invalid(new() { Audio = new() { LocalOutputPriority = [null!] } }));
Check("invalid port rejected", () => Invalid(new() { Sunshine = new() { BasePort = 65535 } }));
Check("Windows ID cannot become Sunshine UUID", () => Invalid(new() { Display = new() { SunshineDeviceId = "DISPLAY\\MONITOR" } }));
Check("bare function key rejected", () => Invalid(new() { Hotkey = new() { Control = false, Alt = false, Shift = false } }));
Check("letter chord supported", () => Assert(SettingsValidator.Validate(new() { Hotkey = new() { Key = "K" } }).Count == 0));
Check("digit chord supported", () => Assert(SettingsValidator.Validate(new() { Hotkey = new() { Key = "7" } }).Count == 0));
Check("bare letter rejected", () => Invalid(new() { Hotkey = new() { Key = "K", Control = false, Alt = false, Shift = false } }));
Check("hotkey is optional by default", () => Assert(!new HotkeySettings().Enabled));
Check("hotkey choice survives JSON roundtrip", () => { var original = new AppSettings { Hotkey = new() { Enabled = true, Key = "K" } }; var json = System.Text.Json.JsonSerializer.Serialize(original, SettingsStore.JsonOptions); var restored = System.Text.Json.JsonSerializer.Deserialize<AppSettings>(json, SettingsStore.JsonOptions)!; Assert(restored.Hotkey.Enabled && restored.Hotkey.Key == "K"); });
var binding = new DeviceBinding("device-a", "Example DAC");
Check("duplicate rules rejected", () => Invalid(new() { Audio = new() { Rules = [new() { Device = binding }, new() { Device = binding }] } }));
Check("remote output cannot be blocked", () => Invalid(new() { Audio = new() { RemoteOutput = binding, Rules = [new() { Device = binding, Start = StartAction.Mute }] } }));
Check("fixed restore needs a target", () => Invalid(new() { Audio = new() { Restore = RestoreStrategy.FixedProfile } }));
Check("same name does not silently remap ID", () => Assert(DeviceResolver.Resolve(binding, [new("new-id", binding.Name, DeviceState.Active)]).Device is null));
Check("unplugged target does not resolve", () => Assert(DeviceResolver.Resolve(binding, [new(binding.Id, binding.Name, DeviceState.Unplugged)]).Device is null));
Check("disabled target remains identifiable", () => Assert(DeviceResolver.Resolve(binding, [new(binding.Id, binding.Name, DeviceState.Disabled)]).Device is not null));
Check("duplicate inventory fails closed", () => Assert(DeviceResolver.Resolve(binding, [new(binding.Id, binding.Name, DeviceState.Active), new(binding.Id, binding.Name, DeviceState.Active)]).Device is null));
Check("missing preferred output uses configured fallback", () => Assert(DeviceResolver.FirstAvailable([new("absent", "DAC"), binding], [new(binding.Id, binding.Name, DeviceState.Active)])?.Id == binding.Id));
Check("disconnect preserves remote session", () => Assert(SessionRules.Transition(SessionPhase.Remote, SessionSignal.Disconnect) == SessionPhase.Remote));
Check("resume preserves remote session", () => Assert(SessionRules.Transition(SessionPhase.Remote, SessionSignal.Resume) == SessionPhase.Remote));
Check("quit without session preserves local state", () => Assert(SessionRules.Transition(SessionPhase.Local, SessionSignal.End) == SessionPhase.Local));
Check("session end enters recovery", () => Assert(SessionRules.Transition(SessionPhase.Remote, SessionSignal.End) == SessionPhase.Recovering));
var speakerRule = new AudioRule { Device = new("speaker", "Speaker"), End = EndAction.SelectedOutputMutedElseUnmuted, EnableOnEnd = true };
Check("fallback speaker is muted when selected", () => Assert(AudioRecovery.EndMute(speakerRule, new("speaker", "Speaker", DeviceState.Active)) == true));
Check("speaker is unmuted when DAC is selected", () => Assert(AudioRecovery.EndMute(speakerRule, new("dac", "DAC", DeviceState.Active)) == false));
Check("no output candidate preserves conditional mute", () => Assert(AudioRecovery.EndMute(speakerRule, null) is null));
var directory = Path.Combine(Path.GetTempPath(), "autoshine-tests-" + Guid.NewGuid());
Directory.CreateDirectory(directory);
try
{
    var store = new SettingsStore(Path.Combine(directory, "settings.json"));
    Check("missing settings loads defaults", () => Assert(store.Load().SchemaVersion == 1));
    Check("settings save round trip", () => { store.Save(new() { Audio = new() { Rules = [new() { Device = binding }] } }); Assert(store.Load().Audio.Rules.Single().Device == binding); });
    Check("replacement backs up previous settings", () => { store.Save(new()); Assert(new SettingsStore(store.Path + ".bak").Load().Audio.Rules.Count == 1); });
    Check("invalid save leaves valid file intact", () => { var before = File.ReadAllText(store.Path); try { store.Save(new() { SchemaVersion = 99 }); throw new Exception("accepted invalid settings"); } catch (InvalidDataException) { } Assert(File.ReadAllText(store.Path) == before); });
    Check("corrupt JSON rejected", () => { File.WriteAllText(store.Path, "{bad}"); try { store.Load(); throw new Exception("accepted corrupt JSON"); } catch (System.Text.Json.JsonException) { } });
}
finally { Directory.Delete(directory, true); }
var policy = new AudioSettings { RemoteOutput = new("remote", "Virtual"), Restore = RestoreStrategy.FixedProfile,
 LocalOutputPriority = [new("dac", "DAC"), new("speaker", "Speaker")], Rules = [
 new() { Device = new("dac", "DAC"), Start = StartAction.Disable, End = EndAction.Muted, EnableOnEnd = true },
 new() { Device = new("speaker", "Speaker"), Start = StartAction.Disable, End = EndAction.SelectedOutputMutedElseUnmuted, EnableOnEnd = true }] };
Check("session disables physical only after muting", () => { var fake = new FakeAudio(); var engine = new AudioSession(fake); engine.Start(engine.Capture(policy)); Assert(fake.Events.IndexOf("mute:speaker:True") < fake.Events.IndexOf("enable:speaker:False")); Assert(fake.Default(0) == "remote"); });
Check("DAC recovery unmutes alternate speaker after default switch", () => { var fake = new FakeAudio(); var engine = new AudioSession(fake); var snap = engine.Capture(policy); engine.Start(snap); fake.Events.Clear(); engine.End(snap); Assert(fake.Default(0) == "dac" && fake.Muted("dac") && !fake.Muted("speaker")); Assert(fake.Events.IndexOf("default:dac:2") < fake.Events.IndexOf("mute:speaker:False")); });
Check("DAC unplugged during stream selects muted speaker", () => { var fake = new FakeAudio(); var engine = new AudioSession(fake); var snap = engine.Capture(policy); engine.Start(snap); fake.Remove("dac"); engine.End(snap); Assert(fake.Default(0) == "speaker" && fake.Muted("speaker")); });
Check("all recovery candidates absent retains existing output", () => { var fake = new FakeAudio(); var engine = new AudioSession(fake); var snap = engine.Capture(policy); engine.Start(snap); fake.Remove("dac"); fake.Remove("speaker"); engine.End(snap); Assert(fake.Default(0) == "remote"); });
Check("required missing output fails before audio changes", () => { var fake = new FakeAudio(); fake.Remove("dac"); var engine = new AudioSession(fake); try { engine.Capture(policy with { Rules = [policy.Rules[0] with { Optional = false }] }); throw new Exception("accepted missing device"); } catch (IOException) { } Assert(fake.Events.Count == 0); });
Check("previous default without device rule recovers original mute", () => { var fake = new FakeAudio(); var engine = new AudioSession(fake); var snap = engine.Capture(new() { RemoteOutput = policy.RemoteOutput }); engine.Start(snap); engine.End(snap); Assert(fake.Default(0) == "speaker" && !fake.Muted("speaker")); });
Check("replugged remote device is protected", () => { var fake = new FakeAudio(); var engine = new AudioSession(fake); var snap = engine.Capture(policy); engine.Start(snap); fake.Mute("speaker", false); engine.Protect(policy); Assert(fake.Muted("speaker")); });
Check("unchanged retains end-entry mute after protection and selection", () => {
    var fake = new FakeAudio(); var engine = new AudioSession(fake);
    var snap = engine.Capture(policy with { Rules = [policy.Rules[0], policy.Rules[1] with { End = EndAction.Unchanged }] });
    engine.Start(snap); engine.End(snap); Assert(fake.Muted("speaker"));
});
Check("unchanged restores unmuted entry after temporary recovery muting", () => {
    var fake = new FakeAudio(); var engine = new AudioSession(fake);
    var snap = engine.Capture(new() { Rules = [new() { Device = new("speaker", "Speaker"), End = EndAction.Unchanged, EnableOnEnd = true }] });
    engine.End(snap); Assert(!fake.Muted("speaker"));
});
Check("previous mute restores capture state rather than end-entry state", () => {
    var fake = new FakeAudio(); var engine = new AudioSession(fake);
    var snap = engine.Capture(policy with { Rules = [policy.Rules[0], policy.Rules[1] with { End = EndAction.PreviousState }] });
    engine.Start(snap); engine.End(snap); Assert(!fake.Muted("speaker"));
});
Check("active connection remains frozen across settings edits and journal roundtrip", () => {
    var original = new SunshineSettings { BasePort = 48000, ConfigPath = "original.conf" };
    var snap = new AudioSession(new FakeAudio()).Capture(new()) with { Connection = original };
    var restored = System.Text.Json.JsonSerializer.Deserialize<AudioSessionSnapshot>(System.Text.Json.JsonSerializer.Serialize(snap, SettingsStore.JsonOptions), SettingsStore.JsonOptions)!;
    Assert(SessionContext.Connection(restored, new()) == original);
    Assert(SessionContext.Connection(null, new()).BasePort == 47989);
});
Check("diagnostic log omits exception secrets and suppresses repeated errors", () => {
    var root = Path.Combine(Path.GetTempPath(), "autoshine-log-" + Guid.NewGuid());
    try {
        var path = Path.Combine(root, "agent.log"); var log = new DiagnosticLog(path);
        log.Write("test-failed", new IOException("secret-password"));
        log.Write("test-failed", new IOException("another-secret"));
        Assert(File.ReadAllLines(path).Length == 1 && !File.ReadAllText(path).Contains("secret") && File.ReadAllText(path).Contains("IOException"));
        File.WriteAllText(path, new string('x', 1024 * 1024 + 1)); log.Write("test-completed");
        Assert(File.Exists(path + ".previous") && new FileInfo(path).Length < 1024);
    } finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
});
Check("pipe response cleanup keeps caller-owned connection usable", () => {
    var name = "autoshine-test-" + Guid.NewGuid().ToString("N");
    using var server = new System.IO.Pipes.NamedPipeServerStream(name, System.IO.Pipes.PipeDirection.InOut, 1, System.IO.Pipes.PipeTransmissionMode.Byte, System.IO.Pipes.PipeOptions.Asynchronous);
    var serve = Task.Run(async () => {
        await server.WaitForConnectionAsync();
        using var reader = new StreamReader(server, leaveOpen: true);
        using var writer = new StreamWriter(server, leaveOpen: true) { AutoFlush = true };
        for (int i = 0; i < 2; i++) { Assert(await reader.ReadLineAsync() == "hotkey-status"); await writer.WriteLineAsync("OK"); }
    });
    using var client = new System.IO.Pipes.NamedPipeClientStream(".", name, System.IO.Pipes.PipeDirection.InOut, System.IO.Pipes.PipeOptions.Asynchronous);
    client.Connect(2000);
    for (int i = 0; i < 2; i++) Assert(PipeMessages.ExchangeAsync(client, "hotkey-status", TimeSpan.FromSeconds(2)).GetAwaiter().GetResult() == "OK");
    serve.WaitAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult();
});
Check("explicit disabled preferred output falls back without re-enabling it", () => {
    var fake = new FakeAudio(); var engine = new AudioSession(fake);
    var config = policy with { Rules = [policy.Rules[0] with { EndEnabled = false }, policy.Rules[1] with { EndEnabled = true }] };
    var snap = engine.Capture(config); engine.Start(snap); fake.Events.Clear(); engine.End(snap);
    Assert(fake.Default(0) == "speaker" && fake.Devices().Single(d => d.Id == "dac").State == DeviceState.Disabled);
    Assert(!fake.Events.Contains("enable:dac:True"));
});
Check("explicit activation restores a disabled device and its requested mute", () => {
    var fake = new FakeAudio(); fake.Enable("dac", false); var engine = new AudioSession(fake);
    var snap = engine.Capture(new() { Rules = [new() { Device = new("dac", "DAC"), EndEnabled = true, End = EndAction.Unmuted }] });
    engine.End(snap); Assert(fake.Devices().Single(d => d.Id == "dac").State == DeviceState.Active && !fake.Muted("dac"));
});
Check("simulation disconnect makes no audio changes and end uses frozen policy", () => {
    var values = new List<SimulatedDevice> {new("dac", "DAC", true, true, false),new("speaker", "Speaker", true, true, false),new("remote", "Virtual", true, true, false)};
    var settings = new AppSettings {Audio=policy};
    var start = AudioSimulation.Run(new("start",settings,values,["speaker","speaker","speaker"],null));
    var drop = AudioSimulation.Run(new("drop",new(),start.Devices,start.Defaults,start.Session));
    Assert(drop.Devices.SequenceEqual(start.Devices) && drop.Defaults.SequenceEqual(start.Defaults) && drop.Session is not null);
    var end = AudioSimulation.Run(new("end",new(),drop.Devices,drop.Defaults,drop.Session));
    Assert(end.Session is null && end.Defaults[0] == "dac" && end.Devices.Single(d=>d.Id=="dac").Muted && !end.Devices.Single(d=>d.Id=="speaker").Muted);
});
Check("simulation absent remote fails before changing in-memory input", () => {
    var values = new List<SimulatedDevice> {new("speaker", "Speaker", true, true, false),new("remote", "Virtual", false, true, false)};
    try { AudioSimulation.Run(new("start",new(){Audio=policy},values,["speaker","speaker","speaker"],null));throw new Exception("accepted absent remote"); } catch(IOException) { }
    Assert(!values[0].Muted && values[0].Enabled);
});
Console.WriteLine($"{passed} tests passed");


sealed class FakeAudio : IAudioControl
{
 readonly Dictionary<string, (bool Enabled, bool Muted)> devices = new() { ["dac"] = (true,false), ["speaker"] = (true,false), ["remote"] = (true,false) };
 readonly string?[] defaults = ["speaker","speaker","speaker"];
 public List<string> Events { get; } = [];
 public int DeviceReads { get; private set; }
 public IReadOnlyList<AudioDevice> Devices() { DeviceReads++; return devices.Select(d => new AudioDevice(d.Key,d.Key,d.Value.Enabled ? DeviceState.Active : DeviceState.Disabled)).ToList(); }
 public void Remove(string id) => devices.Remove(id);
 public string? Default(int role) => defaults[role];
 public bool Muted(string id) => devices[id].Muted;
 public void Mute(string id, bool value) { Events.Add($"mute:{id}:{value}"); devices[id]=(devices[id].Enabled,value); }
 public void Enable(string id, bool value) { Events.Add($"enable:{id}:{value}"); devices[id]=(value,devices[id].Muted); }
 public void SetDefault(string id, int role) { Events.Add($"default:{id}:{role}"); defaults[role]=id; }
}
