using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Collections.Concurrent;
using AutoShine.Core;
using AutoShine.Windows;
using AutoShine.Sunshine;

var portArg = Array.IndexOf(args, "--port");
var port = portArg >= 0 && portArg + 1 < args.Length ? int.Parse(args[portArg + 1]) : 48191;
if (port is < 1024 or > 65535) throw new ArgumentException("Invalid localhost port");
var origin = $"http://127.0.0.1:{port}";
var builder = WebApplication.CreateBuilder(new WebApplicationOptions { Args = args, ContentRootPath = AppContext.BaseDirectory });
builder.WebHost.UseUrls(origin);
builder.WebHost.ConfigureKestrel(options => options.Limits.MaxRequestBodySize = 1024 * 1024);
builder.Services.ConfigureHttpJsonOptions(options => {
    options.SerializerOptions.PropertyNamingPolicy = SettingsStore.JsonOptions.PropertyNamingPolicy;
    foreach (var converter in SettingsStore.JsonOptions.Converters) options.SerializerOptions.Converters.Add(converter);
});
var app = builder.Build();
var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
var settingsArg = Array.IndexOf(args, "--settings-path");
if (settingsArg >= 0 && settingsArg + 1 >= args.Length) throw new ArgumentException("Missing settings path");
var settingsPath = settingsArg >= 0 ? Path.GetFullPath(args[settingsArg + 1]) : Path.Combine(ProductData.Root(), "settings.json");
var isolatedPreview = settingsArg >= 0;
var store = new SettingsStore(settingsPath);
var gate = new object();
var plans = new ConcurrentDictionary<string, ConfigurationPlan>();
app.Use(async (context, next) => {
    if (context.Request.Host.Host != "127.0.0.1" || context.Request.Host.Port != port) { context.Response.StatusCode = 403; return; }
    context.Response.Headers["Cache-Control"] = "no-store";
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["Content-Security-Policy"] = "default-src 'self'; script-src 'self'; style-src 'self'; font-src 'self'; img-src 'self' data:; connect-src 'self'; frame-ancestors 'none'; base-uri 'none'; form-action 'self'";
    if (context.Request.Path.StartsWithSegments("/api") && context.Request.Method != "GET") {
        if (context.Request.Headers.Origin != origin || context.Request.Headers["X-AutoShine-Token"] != token) { context.Response.StatusCode = 403; return; }
        if (isolatedPreview && context.Request.Path.Value is "/api/sunshine/apply" or "/api/sunshine/pair" or "/api/sunshine/unpair") {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsJsonAsync(new { error = "화면 검증 서버에서는 Sunshine 설정·인증을 변경하지 않습니다. 설치본에서 검증하세요." }); return;
        }
    }
    try { await next(); }
    catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or UnauthorizedAccessException or ArgumentException or BadHttpRequestException or HttpRequestException or System.ComponentModel.Win32Exception) {
        context.Response.StatusCode = 400;
        await context.Response.WriteAsJsonAsync(new { error = ex.Message });
    }
});
app.UseDefaultFiles(); app.UseStaticFiles();
app.MapGet("/api/vdd/presets", () => Results.Ok(VddPresets.Read()));
app.MapPost("/api/vdd/presets", async (VddPresetSettings presets) => {
    if (isolatedPreview) return Results.BadRequest(new { error = "화면 검증 서버에서는 VDD 설정을 변경하지 않습니다." });
    var settings = store.Load();
    var server = await SunshineInspector.InspectAsync(settings.Sunshine.ConfigPath, settings.Sunshine.BasePort);
    if (!server.Reachable || server.AppId != 0 || File.Exists(Path.Combine(Path.GetDirectoryName(settingsPath)!, "audio-session.json"))) return Results.BadRequest(new { error = "스트리밍을 종료한 뒤 저장하세요." });
    try { VddPresets.Save(presets); }
    catch (UnauthorizedAccessException) {
        var file = Path.Combine(Path.GetDirectoryName(settingsPath)!, "vdd-" + Guid.NewGuid().ToString("N") + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!); File.WriteAllText(file, JsonSerializer.Serialize(presets, SettingsStore.JsonOptions));
        try {
            var info = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "AutoShine.exe")) { UseShellExecute = true, Verb = "runas" };
            info.ArgumentList.Add("--apply-vdd"); info.ArgumentList.Add(file);
            using var process = Process.Start(info) ?? throw new IOException("VDD settings helper could not start");
            await process.WaitForExitAsync();
            if (process.ExitCode != 0) return Results.BadRequest(JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(file + ".result")));
        } finally { File.Delete(file); File.Delete(file + ".result"); }
    }
    return Results.Ok(new { settings = VddPresets.Read(), message = "VDD 설정 파일을 저장했습니다. 드라이버에 반영됐는지 확인하세요." });
});
app.MapGet("/api/runtime", () => Results.Ok(new { product = "AutoShine", processId = Environment.ProcessId }));
app.MapGet("/api/startup", () => Results.Ok(new { enabled = LoginStartup.Enabled() }));
app.MapPost("/api/startup", async (StartupRequest request) => {
    if (isolatedPreview) return Results.BadRequest(new { error = "검증 서버에서는 시작프로그램 설정을 변경하지 않습니다." });
    if (LoginStartup.MachineEnabled()) {
        using var process = Process.Start(new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "AutoShine.exe"), "--remove-machine-startup") { UseShellExecute = true, Verb = "runas" }) ?? throw new IOException("시작프로그램 설정을 변경할 수 없습니다.");
        await process.WaitForExitAsync();
        if (process.ExitCode != 0 || LoginStartup.MachineEnabled()) throw new IOException("시작프로그램 설정을 변경할 수 없습니다.");
    }
    LoginStartup.SetUser(request.Enabled);
    return Results.Ok(new { enabled = LoginStartup.Enabled() });
});
app.MapGet("/api/agent/status", async () => {
    try {
        using var pipe = new System.IO.Pipes.NamedPipeClientStream(".", "AutoShine-" + System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value, System.IO.Pipes.PipeDirection.InOut, System.IO.Pipes.PipeOptions.Asynchronous);
        await pipe.ConnectAsync(500);
        var line = await PipeMessages.ExchangeAsync(pipe, "ui-status", TimeSpan.FromSeconds(1));
        if (line is null || !line.StartsWith('{')) return Results.Ok(new { reachable = true, message = "이전 Agent입니다. 상세 상태 확인에는 업데이트가 필요합니다." });
        return Results.Ok(new { reachable = true, state = JsonSerializer.Deserialize<JsonElement>(line) });
    } catch (Exception ex) when (ex is IOException or TimeoutException or OperationCanceledException or JsonException) {
        return Results.Ok(new { reachable = false, message = "Agent 상태를 확인할 수 없습니다." });
    }
});
app.MapPost("/api/audio/tool/check", (ToolRequest request) => {
    if (string.IsNullOrWhiteSpace(request.Path) || !Path.IsPathFullyQualified(request.Path) || !File.Exists(request.Path) || !string.Equals(Path.GetFileName(request.Path), "SoundVolumeView.exe", StringComparison.OrdinalIgnoreCase))
        return Results.BadRequest(new { error = "존재하는 SoundVolumeView.exe의 전체 경로를 지정하세요." });
    return Results.Ok(new { message = "실행 파일을 확인했습니다. 실제 장치 제어 검증은 스트리밍 실기 검증에서 진행합니다." });
});
app.MapGet("/api/audio/tool", () => {
    return Results.Ok(new { path = FindAudioTool(), message = "SoundVolumeView는 오디오 장치 활성화와 기본 출력 변경에 사용하는 별도 도구입니다. 설치 파일에는 포함되지 않습니다." });
});
app.MapGet("/api/environment", async () => {
    AppSettings settings; string? settingsError = null;
    try { lock (gate) settings = store.Load(); } catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException or UnauthorizedAccessException) { settings = new(); settingsError = ex.Message; }
    var devices = await Task.Run(() => new { displays = DeviceInventory.Displays(), audio = DeviceInventory.AudioOutputs() });
    var sunshine = await SunshineInspector.InspectAsync(settings.Sunshine.ConfigPath, settings.Sunshine.BasePort);
    var integrated = SunshineConfiguration.HasAutoShineIntegration(sunshine.ConfigPath);
    var paired = CredentialVault.Exists("AutoShine/Sunshine/" + sunshine.BasePort, migrate: !isolatedPreview) && CredentialVault.Exists("AutoShine/Certificate/" + sunshine.BasePort, migrate: !isolatedPreview);
    var audioTool = string.IsNullOrWhiteSpace(settings.Audio.ControlToolPath) ? FindAudioTool() : settings.Audio.ControlToolPath;
    return Results.Ok(new { product = "AutoShine", processId = Environment.ProcessId, token, settings, settingsError, settingsPath, devices.displays, devices.audio, sunshine, sunshineConfigExists = File.Exists(sunshine.ConfigPath), sunshineDisplays = SunshineInspector.ReadDisplays(sunshine.ConfigPath), integrated, paired, sunshineUsername = paired ? CredentialVault.ReadUsername("AutoShine/Sunshine/" + sunshine.BasePort) : null, vddInstalled = devices.displays.Any(d => d.Virtual), soundVolumeViewInstalled = File.Exists(audioTool) && string.Equals(Path.GetFileName(audioTool), "SoundVolumeView.exe", StringComparison.OrdinalIgnoreCase), isolatedPreview, version = "1.0.0" });
});
app.MapPost("/api/settings", (AppSettings settings) => { lock (gate) store.Save(settings); return Results.Ok(new { message = "설정을 저장했습니다. 실행 중인 세션에는 시작 당시 설정을 유지합니다." }); });
app.MapPost("/api/sunshine/display-plan", (AppSettings settings) => {
    var errors = SettingsValidator.Validate(settings with { Audio = new(), Hotkey = new() });
    if (errors.Count > 0) throw new InvalidDataException(string.Join(" ", errors));
    var available = SunshineInspector.ReadDisplays(string.IsNullOrWhiteSpace(settings.Sunshine.ConfigPath) ? SunshineInspector.DetectConfigPath() : settings.Sunshine.ConfigPath);
    if (!available.Any(d => d.Id.Equals(settings.Display.SunshineDeviceId, StringComparison.OrdinalIgnoreCase))) throw new IOException("디스플레이 목록에서 사용할 화면을 선택하세요.");
    var plan = SunshineConfiguration.PlanDisplay(settings);
    var id = Guid.NewGuid().ToString("N"); plans.Clear(); plans[id] = plan;
    return Results.Ok(new { id, message = "선택한 디스플레이를 Sunshine에 적용합니다. 적용 후 Sunshine을 재시작하세요." });
});
app.MapPost("/api/sunshine/plan", (AppSettings settings) => {
    if (settings.Audio?.RemoteOutput is null) throw new IOException("스트리밍 출력 장치를 먼저 선택하세요.");
    if (string.IsNullOrWhiteSpace(settings.Audio.ControlToolPath) || !File.Exists(settings.Audio.ControlToolPath)) throw new IOException("설정에서 SoundVolumeView 실행 파일을 선택하고 저장하세요.");
    var plan = SunshineConfiguration.Plan(settings, Path.Combine(AppContext.BaseDirectory, "AutoShine.exe"));
    var id = Guid.NewGuid().ToString("N"); plans.Clear(); plans[id] = plan;
    return Results.Ok(new { id, plan.Path, plan.Changes, message = "적용 전에 Sunshine 세션을 종료하세요. 적용 후 Sunshine 재시작이 필요합니다." });
});
app.MapPost("/api/sunshine/apply", async (ApplyRequest request) => {
    if (!plans.TryRemove(request.Id, out var plan)) return Results.BadRequest(new { error = "적용 내용을 다시 확인하세요." });
    var config = SunshineInspector.ReadConfig(plan.Path);
    var server = await SunshineInspector.InspectAsync(plan.Path, config.TryGetValue("port", out var value) && int.TryParse(value, out var configuredPort) ? configuredPort : 47989);
    if (!server.Reachable || server.AppId != 0) return Results.BadRequest(new { error = "Sunshine이 연결된 로컬 상태에서만 적용할 수 있습니다." });
    var file = Path.Combine(Path.GetDirectoryName(settingsPath)!, "apply-" + Guid.NewGuid().ToString("N") + ".json");
    Directory.CreateDirectory(Path.GetDirectoryName(file)!); File.WriteAllText(file, JsonSerializer.Serialize(plan, SettingsStore.JsonOptions));
    try {
        var info = new ProcessStartInfo(Path.Combine(AppContext.BaseDirectory, "AutoShine.exe")) { UseShellExecute = true, Verb = "runas", WorkingDirectory = AppContext.BaseDirectory };
        info.ArgumentList.Add("--apply"); info.ArgumentList.Add(file);
        using var process = Process.Start(info) ?? throw new IOException("설정 적용기를 실행할 수 없습니다.");
        await process.WaitForExitAsync();
        if (!File.Exists(file + ".result")) throw new IOException("설정 적용 결과를 확인할 수 없습니다.");
        var result = JsonSerializer.Deserialize<JsonElement>(File.ReadAllText(file + ".result"));
        return process.ExitCode == 0 ? Results.Ok(result) : Results.BadRequest(result);
    } finally { File.Delete(file); File.Delete(file + ".result"); }
});
app.MapPost("/api/sunshine/unlink-plan", (SunshineSettings settings) => {
    var plan = SunshineConfiguration.PlanUnlink(settings);
    var id = Guid.NewGuid().ToString("N"); plans.Clear(); plans[id] = plan;
    return Results.Ok(new { id, plan.Path, plan.Changes, message = "AutoShine 준비 명령만 해제합니다. 화면 설정과 다른 준비 명령은 유지합니다. 세션 종료 후 적용하고 Sunshine을 재시작하세요." });
});
app.MapGet("/api/diagnostics", () => {
    var root = Path.GetDirectoryName(settingsPath)!;
    var logs = new[] { "agent.log.previous", "agent.log" }.Where(name => File.Exists(Path.Combine(root, name))).SelectMany(name => File.ReadLines(Path.Combine(root, name))).TakeLast(200).ToArray();
    return Results.File(JsonSerializer.SerializeToUtf8Bytes(new { version = "1.0.0", utc = DateTime.UtcNow, activeJournal = File.Exists(Path.Combine(root, "audio-session.json")), recoveryJournal = File.Exists(Path.Combine(root, "last-recovery.json")), events = logs }), "application/json", "autoshine-diagnostics.json");
});
app.MapPost("/api/files/pick/{kind}", async (string kind, FilePickerRequest request) => Results.Ok(new { path = await AutoShine.Web.FilePicker.Pick(kind, request.Path) }));
app.MapPost("/api/sunshine/certificate", async (SunshineSettings settings) => Results.Ok(new { pin = await SunshineQuit.CertificateFingerprint(settings.BasePort) }));
app.MapPost("/api/sunshine/unpair", (SunshineSettings settings) => {
    if (settings.BasePort is < 1024 or > 65514) throw new IOException("Sunshine 포트를 확인하세요.");
    CredentialVault.Delete("AutoShine/Sunshine/" + settings.BasePort);
    CredentialVault.Delete("AutoShine/Certificate/" + settings.BasePort);
    return Results.Ok(new { message = "선택한 포트의 AutoShine 종료 인증을 삭제했습니다." });
});
app.MapPost("/api/sunshine/pair", async (PairRequest request) => {
    if (request.Port is < 1024 or > 65514) throw new IOException("Sunshine 포트를 확인하세요.");
    var pin = request.Pin;
    if (string.IsNullOrEmpty(pin)) {
        var target = "AutoShine/Certificate/" + request.Port;
        if (CredentialVault.Exists(target)) pin = CredentialVault.Read(target).Password;
        else return Results.Ok(new { needsConfirmation = true, pin = await SunshineQuit.CertificateFingerprint(request.Port), message = "처음 연결하는 이 PC의 Sunshine 서버를 확인하세요." });
    }
    await SunshineQuit.Pair(request.Port, request.Username, request.Password, pin);
    return Results.Ok(new { message = "Sunshine 종료 기능을 연결했습니다. 인증정보는 Windows 자격 증명 관리자에 저장됩니다." });
});
app.MapPost("/api/settings/validate", (AppSettings settings) => {
    var errors = SettingsValidator.Validate(settings);
    return errors.Count == 0 ? Results.Ok(settings) : Results.BadRequest(new { error = string.Join(" ", errors) });
});
app.MapPost("/api/simulation", (SimulationRequest request) => Results.Ok(AudioSimulation.Run(request)));
app.MapPost("/api/hotkey/check", async (HotkeySettings hotkey) => {
    var validation = SettingsValidator.Validate(new() { Hotkey = hotkey });
    if (validation.Count > 0) return Results.BadRequest(new { error = string.Join(" ", validation) });
    if (hotkey.Enabled) {
        try {
            var pipeName = "AutoShine-" + System.Security.Principal.WindowsIdentity.GetCurrent().User!.Value;
            using var pipe = new System.IO.Pipes.NamedPipeClientStream(".", pipeName, System.IO.Pipes.PipeDirection.InOut, System.IO.Pipes.PipeOptions.Asynchronous);
            await pipe.ConnectAsync(500);
            var line = await PipeMessages.ExchangeAsync(pipe, "hotkey-status", TimeSpan.FromSeconds(1));
            if (line is not null && JsonSerializer.Deserialize<HotkeySettings>(line, SettingsStore.JsonOptions) == hotkey) return Results.Ok(new { message = "현재 Agent에 등록되어 사용 중인 조합입니다." });
        } catch (Exception ex) when (ex is IOException or TimeoutException or OperationCanceledException or JsonException) { }
    }
    return Results.Ok(new { message = HotkeyProbe.Check(IntPtr.Zero, hotkey) });
});
app.Lifetime.ApplicationStarted.Register(() => {
    Console.WriteLine($"AutoShine: {origin}");
    if (!args.Contains("--no-browser")) Process.Start(new ProcessStartInfo(origin) { UseShellExecute = true });
});
await app.RunAsync();
static string? FindAudioTool() {
    var candidates = new List<string> { Path.Combine(AppContext.BaseDirectory, "tools", "SoundVolumeView.exe"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "SoundVolumeView", "SoundVolumeView.exe"),
        Path.Combine(Path.GetPathRoot(Environment.SystemDirectory)!, "SunshineAudio", "SoundVolumeView", "SoundVolumeView.exe") };
    candidates.AddRange((Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator).Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => Path.Combine(p.Trim('"'), "SoundVolumeView.exe")));
    return candidates.FirstOrDefault(File.Exists);
}
sealed record ApplyRequest(string Id);
sealed record PairRequest(int Port, string Username, string Password, string? Pin);
sealed record FilePickerRequest(string? Path);

sealed record ToolRequest(string Path);
sealed record StartupRequest(bool Enabled);

