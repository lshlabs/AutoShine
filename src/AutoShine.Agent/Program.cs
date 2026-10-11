using System.Diagnostics;
using System.IO.Pipes;
using System.Security.Principal;
using System.Text.Json;
using System.Runtime.InteropServices;
using AutoShine.Core;
using AutoShine.Windows;
using AutoShine.Sunshine;

namespace AutoShine.Agent;

internal static class Program
{
    internal static readonly string Root = ProductData.Root();
    internal static readonly string Pipe = "AutoShine-" + WindowsIdentity.GetCurrent().User!.Value;
    internal static readonly DiagnosticLog Log = new(Path.Combine(Root, "agent.log"));
    static Process? webProcess;
    [STAThread] static void Main(string[] args)
    {
        try
        {
            if (args.FirstOrDefault() == "--remove-machine-startup") { LoginStartup.RemoveMachineEntry(); return; }
            if (args.FirstOrDefault() == "--apply-vdd")
            {
                var settings = new SettingsStore(Path.Combine(Root, "settings.json")).Load();
                var server = SunshineInspector.InspectAsync(settings.Sunshine.ConfigPath, settings.Sunshine.BasePort).GetAwaiter().GetResult();
                if (!server.Reachable || server.AppId != 0 || File.Exists(Path.Combine(Root, "audio-session.json"))) throw new IOException("End the streaming session before editing VDD settings");
                VddPresets.Save(JsonSerializer.Deserialize<VddPresetSettings>(File.ReadAllText(args[1]), SettingsStore.JsonOptions) ?? throw new IOException("Invalid VDD preset"));
                File.WriteAllText(args[1] + ".result", "{}"); return;
            }
            if (args.FirstOrDefault() == "--check-uninstall")
            {
                var settings = new SettingsStore(Path.Combine(Root, "settings.json")).Load();
                if (File.Exists(Path.Combine(Root, "audio-session.json"))) throw new IOException("End the session before uninstalling.");
                var paths = new[] { settings.Sunshine.ConfigPath, SunshineInspector.DetectConfigPath() }.Where(p => !string.IsNullOrWhiteSpace(p)).Distinct(StringComparer.OrdinalIgnoreCase);
                foreach (var path in paths)
                {
                    if (!File.Exists(path)) throw new IOException("Cannot verify Sunshine configuration.");
                    if (SunshineInspector.ReadConfig(path!).GetValueOrDefault("global_prep_cmd", "").Contains("AutoShine.exe", StringComparison.OrdinalIgnoreCase))
                        throw new IOException("Unlink AutoShine before uninstalling.");
                }
                return;
            }
            if (args.FirstOrDefault() == "--apply")
            {
                var plan = JsonSerializer.Deserialize<ConfigurationPlan>(File.ReadAllText(args[1]), SettingsStore.JsonOptions) ?? throw new IOException("Invalid apply plan");
                var backup = SunshineConfiguration.Apply(plan);
                File.WriteAllText(args[1] + ".result", JsonSerializer.Serialize(new { backup, message = "Sunshine 설정을 적용했습니다. Sunshine을 다시 시작하면 연동됩니다." })); return;
            }
            if (args.FirstOrDefault() is "begin" or "end" or "status" or "quit")
            {
                using var client = new NamedPipeClientStream(".", Pipe, PipeDirection.InOut);
                client.Connect(5000); using var reader = new StreamReader(client); using var writer = new StreamWriter(client) { AutoFlush = true };
                writer.WriteLine(args[0]); var reply = reader.ReadLineAsync().WaitAsync(TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();
                if (reply != "OK") { if (args[0] == "begin") Console.Error.WriteLine(reply ?? "Agent did not reply"); throw new IOException(reply ?? "Agent did not reply"); } return;
            }
            using var mutex = new Mutex(true, "Local\\" + Pipe, out var first);
            if (!first) { if (!args.Contains("--background")) OpenWeb(); return; }
            using var installerMutex = new Mutex(true, "Local\\AutoShine.Installer");
            ApplicationConfiguration.Initialize();
            using var host = new AgentHost();
            if (!args.Contains("--background")) OpenWeb();
            Application.Run(host);
            if (webProcess is { HasExited: false }) webProcess.Kill();
        }
        catch (Exception ex) { Log.Write("launcher-failed", ex); Environment.ExitCode = 1; if (args.FirstOrDefault() is "--apply" or "--apply-vdd" && args.Length > 1) File.WriteAllText(args[1] + ".result", JsonSerializer.Serialize(new { error = ex.Message })); if (args.Length == 0) MessageBox.Show(ex.Message, "AutoShine"); }
    }
    internal static void OpenWeb()
    {
        var executable = Path.Combine(AppContext.BaseDirectory, "AutoShine.Web.exe");
        if (!File.Exists(executable)) throw new IOException("Web 실행 파일이 없습니다.");
        using var client = new HttpClient(new HttpClientHandler { UseProxy = false }) { Timeout = TimeSpan.FromSeconds(1) };
        try
        {
            var content = client.GetStringAsync("http://127.0.0.1:48191/api/runtime").GetAwaiter().GetResult();
            using var data = JsonDocument.Parse(content);
            if (!data.RootElement.TryGetProperty("product", out var product) || product.GetString() != "AutoShine") throw new IOException("48191 포트에 다른 서버가 실행 중입니다. 기존 서버를 종료하세요.");
            var process = Process.GetProcessById(data.RootElement.GetProperty("processId").GetInt32());
            if (!string.Equals(process.MainModule?.FileName, executable, StringComparison.OrdinalIgnoreCase)) throw new IOException("다른 위치의 설정 서버가 실행 중입니다. 먼저 종료하세요.");
            webProcess = process;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            webProcess = Process.Start(new ProcessStartInfo(executable, "--no-browser") { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = AppContext.BaseDirectory });
            var until = DateTime.UtcNow.AddSeconds(8); bool ready = false;
            while (DateTime.UtcNow < until)
            {
                if (webProcess is null || webProcess.HasExited) throw new IOException("설정 서버를 실행할 수 없습니다.");
                try { using var response = client.GetAsync("http://127.0.0.1:48191/").GetAwaiter().GetResult(); if (response.IsSuccessStatusCode) { ready = true; break; } } catch (HttpRequestException) { }
                Thread.Sleep(100);
            }
            if (!ready) throw new IOException("설정 서버 시작 시간이 초과되었습니다.");
        }
        Process.Start(new ProcessStartInfo("http://127.0.0.1:48191/") { UseShellExecute = true });
    }
}

internal sealed class AgentHost : Form
{
    static readonly JsonSerializerOptions PipeJson = new(SettingsStore.JsonOptions) { WriteIndented = false };
    readonly NotifyIcon tray;
    readonly CancellationTokenSource stop = new();
    readonly SemaphoreSlim gate = new(1);
    readonly SettingsStore store = new(Path.Combine(Program.Root, "settings.json"));
    readonly string sessionPath = Path.Combine(Program.Root, "audio-session.json");
    readonly string recoveryPath = Path.Combine(Program.Root, "last-recovery.json");
    AudioSessionSnapshot? session;
    string error = "";
    DateTime started;
    bool seenActive;
    int emptyCount;
    readonly System.Windows.Forms.Timer configurationTimer = new() { Interval = 2000 };
    HotkeySettings? registered;
    HashSet<string> localDevices = [];
    AudioSessionSnapshot? lastRecovery;
    DateTime nextInspection;
    public AgentHost()
    {
        Directory.CreateDirectory(Program.Root);
        if (File.Exists(sessionPath)) session = JsonSerializer.Deserialize<AudioSessionSnapshot>(File.ReadAllText(sessionPath), SettingsStore.JsonOptions) ?? throw new IOException("Invalid session journal");
        if (File.Exists(recoveryPath)) lastRecovery = JsonSerializer.Deserialize<AudioSessionSnapshot>(File.ReadAllText(recoveryPath), SettingsStore.JsonOptions) ?? throw new IOException("Invalid recovery journal");
        localDevices = DeviceInventory.AudioOutputs().Where(d => d.Present).Select(d => d.Id).ToHashSet();
        started = DateTime.UtcNow;
        Program.Log.Write(session is null ? "agent-start-local" : "agent-start-session-journal");
        var menu = new ContextMenuStrip();
        menu.Items.Add("설정 열기", null, (_, _) => Program.OpenWeb());
        menu.Items.Add("종료", null, (_, _) => { if (session is not null) { MessageBox.Show("원격 세션을 먼저 종료하세요."); return; } Close(); });
        tray = new NotifyIcon { Icon = SystemIcons.Application, Text = "AutoShine", Visible = true, ContextMenuStrip = menu };
        tray.DoubleClick += (_, _) => Program.OpenWeb();
        ShowInTaskbar = false; WindowState = FormWindowState.Minimized;
        configurationTimer.Tick += (_, _) => UpdateHotkey(); configurationTimer.Start();
        _ = Task.Run(Listen); _ = Task.Run(Poll); _ = Task.Run(ProtectLoop);
    }
    protected override void SetVisibleCore(bool value) => base.SetVisibleCore(false);
    protected override void OnFormClosed(FormClosedEventArgs e) { stop.Cancel(); configurationTimer.Dispose(); UnregisterHotKey(Handle, 0x6122); tray.Dispose(); base.OnFormClosed(e); }
    void UpdateHotkey()
    {
        try
        {
            var hotkey = store.Load().Hotkey;
            if (hotkey == registered) return;
            UnregisterHotKey(Handle, 0x6122); registered = null;
            if (!hotkey.Enabled) { registered = hotkey; return; }
            var key = hotkey.Key.ToUpperInvariant();
            uint code = key switch { "SCROLLLOCK" => 0x91, "PAUSE" => 0x13, _ => key.Length == 1 ? key[0] : (uint)(0x6F + int.Parse(key[1..])) };
            if (!RegisterHotKey(Handle, 0x6122, 0x4000u | (hotkey.Alt ? 1u : 0) | (hotkey.Control ? 2u : 0) | (hotkey.Shift ? 4u : 0), code)) { error = "종료 단축키를 다른 프로그램이 사용하고 있습니다."; return; }
            registered = hotkey;
        }
        catch (Exception ex) { error = ex.Message; }
    }
    protected override void WndProc(ref Message message)
    {
        if (message.Msg == 0x312 && message.WParam == 0x6122)
        { QuitFromHotkey(); return; }
        base.WndProc(ref message);
    }
    async void QuitFromHotkey() { try { await Execute("quit"); } catch (Exception ex) { tray.ShowBalloonTip(3000, "세션 종료 실패", ex.Message, ToolTipIcon.Warning); } }
    [DllImport("user32.dll", SetLastError = true)] static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint key);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr window, int id);
    AudioSession Engine(AudioSettings policy) => new(new AudioControl(policy.ControlToolPath));
    async Task Execute(string command, AudioSessionSnapshot? expectedSession = null)
    {
        await gate.WaitAsync(stop.Token);
        try
        {
            if (expectedSession is not null && !ReferenceEquals(session, expectedSession)) return;
            if (command == "quit")
            {
                var connection = SessionContext.Connection(session, store.Load());
                var server = await SunshineInspector.InspectAsync(connection.ConfigPath, connection.BasePort, preferConfiguredPort: session?.Connection is not null);
                if (!server.Reachable) throw new IOException("Sunshine 상태를 확인할 수 없습니다.");
                if (server.AppId == 0) return;
                if (session is not null) Engine(session.Policy).Protect(session.Policy);
                // Sunshine runs our end hook before replying; it must be able to acquire gate.
                gate.Release();
                try { await SunshineQuit.Quit(server.BasePort); }
                finally { await gate.WaitAsync(); }
                Program.Log.Write("quit-request-accepted"); return;
            }
            if (command == "begin")
            {
                if (session is null)
                {
                    var settings = store.Load();
                    if (string.IsNullOrWhiteSpace(settings.Audio.ControlToolPath)) throw new IOException("오디오 제어 도구 경로를 설정하세요.");
                    var server = await SunshineInspector.InspectAsync(settings.Sunshine.ConfigPath, settings.Sunshine.BasePort);
                    session = Engine(settings.Audio).Capture(settings.Audio) with { Connection = settings.Sunshine with { BasePort = server.BasePort }, Display = settings.Display };
                    Persist(); started = DateTime.UtcNow; seenActive = false;
                }
                Engine(session.Policy).Start(session);
            }
            else if (command == "end" && session is not null)
            {
                var connection = session.Connection ?? store.Load().Sunshine; var server = await SunshineInspector.InspectAsync(connection.ConfigPath, connection.BasePort, preferConfiguredPort: session.Connection is not null);
                if (!server.Reachable || server.AppId != 0) return; // undo can run before currentgame clears
                if (session.Display is { Policy: DisplayPolicy.EnsureOnlyDisplay } display && !string.IsNullOrWhiteSpace(display.SunshineDeviceId) && !DeviceInventory.Displays().Any(d => d.Enabled && !d.Virtual))
                    throw new IOException("Sunshine의 로컬 화면 복구를 기다리고 있습니다.");
                Engine(session.Policy).End(session);
                PersistSnapshot(recoveryPath, session);
                lastRecovery = session;
                File.Delete(sessionPath); session = null;
                localDevices = DeviceInventory.AudioOutputs().Where(d => d.Present).Select(d => d.Id).ToHashSet();
            }
            error = "";
            Program.Log.Write(command + "-completed");
        }
        catch (Exception ex) { error = ex.Message; Program.Log.Write(command + "-failed", ex); if (session is not null) { try { Engine(session.Policy).Protect(session.Policy); } catch { } } throw; }
        finally { gate.Release(); }
    }
    void Persist()
    {
        PersistSnapshot(sessionPath, session!);
    }
    static void PersistSnapshot(string path, AudioSessionSnapshot snapshot)
    {
        var temp = path + ".tmp"; File.WriteAllText(temp, JsonSerializer.Serialize(snapshot, SettingsStore.JsonOptions)); File.Move(temp, path, true);
    }
    async Task Poll()
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                if (session is not null)
                {
                    if (DateTime.UtcNow >= nextInspection)
                    {
                        nextInspection = DateTime.UtcNow.AddSeconds(2);
                        AudioSessionSnapshot? inspecting;
                        await gate.WaitAsync(stop.Token);
                        try { inspecting = session; if (inspecting is not null) Engine(inspecting.Policy).Start(inspecting); }
                        finally { gate.Release(); }
                        if (inspecting is null) continue;
                        var connection = inspecting.Connection ?? store.Load().Sunshine; var server = await SunshineInspector.InspectAsync(connection.ConfigPath, connection.BasePort, preferConfiguredPort: inspecting.Connection is not null);
                        if (!ReferenceEquals(session, inspecting)) continue;
                        if (server.Reachable && server.AppId > 0) { seenActive = true; emptyCount = 0; }
                        else if (server.Reachable && server.AppId == 0 && (seenActive || DateTime.UtcNow - started > TimeSpan.FromSeconds(30)))
                        { if (++emptyCount >= 2) await Execute("end", inspecting); }
                        else emptyCount = 0;
                    }
                }
                else
                {
                    var devices = DeviceInventory.AudioOutputs().Where(d => d.Present).Select(d => d.Id).ToHashSet();
                    var settings = store.Load();
                    if (lastRecovery is not null && settings.Audio.ReapplyOnLocalReconnect && settings.Audio.Restore == RestoreStrategy.FixedProfile && devices.Except(localDevices).Any())
                    {
                        var server = await SunshineInspector.InspectAsync(settings.Sunshine.ConfigPath, settings.Sunshine.BasePort);
                        if (server.Reachable && server.AppId == 0)
                        {
                            await gate.WaitAsync(stop.Token);
                            try { if (session is null) Engine(settings.Audio).End(lastRecovery with { Policy = settings.Audio }); }
                            finally { gate.Release(); }
                        }
                    }
                    localDevices = devices;
                }
            }
            catch (Exception ex) { error = ex.Message; Program.Log.Write("poll-failed", ex); }
            try { await Task.Delay(session is null ? 2000 : 250, stop.Token); } catch (OperationCanceledException) { break; }
        }
    }
    async Task ProtectLoop()
    {
        while (!stop.IsCancellationRequested)
        {
            try
            {
                await gate.WaitAsync(stop.Token);
                try { if (session is not null) Engine(session.Policy).Protect(session.Policy); }
                finally { gate.Release(); }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { error = ex.Message; Program.Log.Write("protection-failed", ex); }
            try { await Task.Delay(250, stop.Token); } catch (OperationCanceledException) { break; }
        }
    }
    async Task Listen()
    {
        while (!stop.IsCancellationRequested)
        {
            NamedPipeServerStream? pipe = null;
            try
            {
                pipe = new NamedPipeServerStream(Program.Pipe, PipeDirection.InOut, 8, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
                await pipe.WaitForConnectionAsync(stop.Token);
                _ = HandlePipe(pipe);
                pipe = null; // The handler owns this connection; keep accepting end/status.
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex) { error = ex.Message; try { await Task.Delay(500, stop.Token); } catch (OperationCanceledException) { break; } }
            finally { pipe?.Dispose(); }
        }
    }
    async Task HandlePipe(NamedPipeServerStream pipe)
    {
        using (pipe)
        try
        {
            using var reader = new StreamReader(pipe);
            using var writer = new StreamWriter(pipe) { AutoFlush = true };
            var command = await reader.ReadLineAsync(stop.Token).AsTask().WaitAsync(TimeSpan.FromSeconds(5));
            try
            {
                if (command == "hotkey-status") { await writer.WriteLineAsync(JsonSerializer.Serialize(registered, PipeJson)); return; }
                if (command == "ui-status") { await writer.WriteLineAsync(JsonSerializer.Serialize(new { activeSession = session is not null, hotkey = registered, error, settingsPath = store.Path }, PipeJson)); return; }
                if (command is not ("begin" or "end" or "status" or "quit")) throw new IOException("Unsupported command");
                if (command != "status") await Task.Run(() => Execute(command));
                await writer.WriteLineAsync("OK");
            }
            catch (Exception ex) { await writer.WriteLineAsync("ERROR " + ex.Message.Replace('\n', ' ').Replace('\r', ' ')); }
        }
        catch (OperationCanceledException) when (stop.IsCancellationRequested) { }
        catch (Exception ex) { Program.Log.Write("pipe-client-failed", ex); }
    }
}
