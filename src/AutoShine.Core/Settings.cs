namespace AutoShine.Core;

public enum StartAction { Unchanged, Mute, Disable }
public enum EndAction { PreviousState, Unchanged, Muted, Unmuted, SelectedOutputMutedElseUnmuted }
public enum RestoreStrategy { PreviousState, FixedProfile }
public enum DisplayPolicy { VerifyOnly, EnsureActive, EnsurePrimary, EnsureOnlyDisplay }
public enum DeviceState { Active, Disabled, Absent, Unplugged }
public enum SessionSignal { Start, Disconnect, Resume, End }
public enum SessionPhase { Local, Preparing, Remote, Recovering }

public sealed record DeviceBinding(string Id, string Name);
public sealed record AudioRule
{
    public DeviceBinding Device { get; init; } = new("", "");
    public StartAction Start { get; init; } = StartAction.Unchanged;
    public bool? StartEnabled { get; init; }
    public bool? StartMuted { get; init; }
    public EndAction End { get; init; } = EndAction.PreviousState;
    public bool Optional { get; init; } = true;
    public bool EnableOnEnd { get; init; }
    // Null keeps the legacy activation policy. Explicit values are chosen in the GUI.
    public bool? EndEnabled { get; init; }
}
public sealed record AudioSettings
{
    public string ControlToolPath { get; init; } = "";
    public DeviceBinding? RemoteOutput { get; init; }
    public RestoreStrategy Restore { get; init; } = RestoreStrategy.PreviousState;
    public List<AudioRule> Rules { get; init; } = [];
    public List<DeviceBinding> LocalOutputPriority { get; init; } = [];
    public bool ReapplyOnLocalReconnect { get; init; }
}
public sealed record DisplaySettings
{
    public DeviceBinding? WindowsDevice { get; init; }
    // Sunshine's UUID and Windows' device path are different identity domains.
    public string SunshineDeviceId { get; init; } = "";
    public DisplayPolicy Policy { get; init; } = DisplayPolicy.EnsureOnlyDisplay;
    public bool UseClientResolution { get; init; } = true;
    public bool UseClientRefreshRate { get; init; } = true;
    public bool HoldOnDisconnect { get; init; } = true;
}
public sealed record SunshineSettings
{
    public string ConfigPath { get; init; } = "";
    public int BasePort { get; init; } = 47989;
}
public sealed record HotkeySettings
{
    public bool Enabled { get; init; } = false;
    public bool Control { get; init; } = true;
    public bool Alt { get; init; } = true;
    public bool Shift { get; init; } = true;
    public string Key { get; init; } = "F10";
}
public sealed record AppSettings
{
    public const int CurrentSchema = 1;
    public int SchemaVersion { get; init; } = CurrentSchema;
    public SunshineSettings Sunshine { get; init; } = new();
    public DisplaySettings Display { get; init; } = new();
    public AudioSettings Audio { get; init; } = new();
    public HotkeySettings Hotkey { get; init; } = new();
}
public sealed record AudioDevice(string Id, string Name, DeviceState State)
{
    public bool Present => State is DeviceState.Active or DeviceState.Disabled;
    public string Label => $"{Name} · {State switch { DeviceState.Active => "사용 가능", DeviceState.Disabled => "비활성", DeviceState.Unplugged => "연결 해제", _ => "없음" }}";
}
public sealed record DisplayDevice(string Id, string Name, bool Enabled, bool Virtual, int Width, int Height, int RefreshRate)
{
    public string Label => $"{Name} · {(Virtual ? "가상 화면" : "화면")} · {(Enabled ? $"{Width}×{Height} {RefreshRate}Hz" : "비활성")}";
}


