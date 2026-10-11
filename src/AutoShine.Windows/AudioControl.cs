using System.Diagnostics;
using AutoShine.Core;

namespace AutoShine.Windows;

// Device visibility/default writes use the existing, user-supplied SoundVolumeView tool.
// It is not redistributed silently with the installer.
public sealed class AudioControl(string toolPath) : IAudioControl
{
    public IReadOnlyList<AudioDevice> Devices() => DeviceInventory.AudioOutputs();
    public string? Default(int role) => DeviceInventory.DefaultOutput(role);
    public bool Muted(string id) => DeviceInventory.EndpointMute(id);
    public void Mute(string id, bool value) => DeviceInventory.EndpointMute(id, value);
    public void Enable(string id, bool value)
    {
        if (Devices().Any(d => d.Id == id && d.State == (value ? DeviceState.Active : DeviceState.Disabled))) return;
        Run("/ShowDisabledDevices", "1", value ? "/Enable" : "/Disable", id);
        Verify(() => Devices().Any(d => d.Id == id && d.State == (value ? DeviceState.Active : DeviceState.Disabled)));
    }
    public void SetDefault(string id, int role)
    {
        if (Default(role) == id) return;
        Run("/SetDefault", id, role.ToString());
        Verify(() => Default(role) == id);
    }
    public void SettleDefaults(IReadOnlyList<string?> defaults, IReadOnlyList<string> protectedOutputs)
    {
        // Sunshine/Windows can reset defaults after display restoration. Restrict this
        // bounded settling period to the session-end transition; never enforce at idle.
        var started = DateTime.UtcNow; var stable = started;
        while (DateTime.UtcNow - started < TimeSpan.FromSeconds(12))
        {
            var present = Devices().Where(d => d.Present).Select(d => d.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var id in protectedOutputs)
                if (present.Contains(id) && !Muted(id)) Mute(id, true);
            for (var role = 0; role < 3; role++)
                if (defaults[role] is { } id && Default(role) != id) { SetDefault(id, role); stable = DateTime.UtcNow; }
            if (DateTime.UtcNow - started >= TimeSpan.FromSeconds(5) && DateTime.UtcNow - stable >= TimeSpan.FromSeconds(2)) return;
            Thread.Sleep(100);
        }
        throw new IOException("기본 출력 전환이 안정되지 않아 음소거 상태로 복구를 재시도합니다.");
    }
    private void Run(params string[] arguments)
    {
        if (!File.Exists(toolPath)) throw new IOException("SoundVolumeView 경로를 지정하세요: " + toolPath);
        var info = new ProcessStartInfo(Path.GetFullPath(toolPath)) { UseShellExecute = false, CreateNoWindow = true };
        foreach (var argument in arguments) info.ArgumentList.Add(argument);
        using var process = Process.Start(info) ?? throw new IOException("Audio tool failed to start");
        if (!process.WaitForExit(4000)) { process.Kill(true); throw new IOException("Audio tool timed out"); }
        if (process.ExitCode != 0) throw new IOException("Audio tool failed: " + process.ExitCode);
    }
    private static void Verify(Func<bool> condition)
    {
        var until = DateTime.UtcNow.AddSeconds(2);
        do { if (condition()) return; Thread.Sleep(50); } while (DateTime.UtcNow < until);
        throw new IOException("Windows audio change could not be verified");
    }
}
