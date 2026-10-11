namespace AutoShine.Core;

public static class SettingsValidator
{
    private static readonly HashSet<string> HotkeyKeys = new(StringComparer.OrdinalIgnoreCase)
    { "F1","F2","F3","F4","F5","F6","F7","F8","F9","F10","F11","F12","ScrollLock","Pause" };
    public static bool IsHotkeyKey(string? key) => key is not null && (HotkeyKeys.Contains(key) || key.Length == 1 && char.IsAsciiLetterOrDigit(key[0]));

    public static IReadOnlyList<string> Validate(AppSettings? value)
    {
        var errors = new List<string>();
        if (value is null) { errors.Add("필수 설정 항목이 없습니다."); return errors; }
        if (value.SchemaVersion != AppSettings.CurrentSchema) errors.Add("지원하지 않는 설정 버전입니다.");
        if (value.Sunshine is null || value.Display is null || value.Audio is null || value.Hotkey is null)
        { errors.Add("필수 설정 항목이 없습니다."); return errors; }
        if (value.Sunshine.ConfigPath is null || value.Display.SunshineDeviceId is null || value.Audio.ControlToolPath is null) errors.Add("문자열 설정이 누락되었습니다.");
        if (value.Sunshine.BasePort is < 1024 or > 65514) errors.Add("Sunshine 포트는 1024~65514 범위여야 합니다.");
        if (!Enum.IsDefined(value.Display.Policy) || !Enum.IsDefined(value.Audio.Restore)) errors.Add("알 수 없는 정책입니다.");
        if (!string.IsNullOrEmpty(value.Display.SunshineDeviceId) && !Guid.TryParse(value.Display.SunshineDeviceId, out _)) errors.Add("Sunshine 화면 ID 형식이 올바르지 않습니다.");
        if (!IsHotkeyKey(value.Hotkey.Key)) errors.Add("지원하지 않는 핫키입니다.");
        if (!string.Equals(value.Hotkey.Key, "ScrollLock", StringComparison.OrdinalIgnoreCase) && !string.Equals(value.Hotkey.Key, "Pause", StringComparison.OrdinalIgnoreCase) && !value.Hotkey.Control && !value.Hotkey.Alt && !value.Hotkey.Shift) errors.Add("문자·숫자·F키에는 하나 이상의 조합키가 필요합니다.");
        if (value.Audio.Rules is null || value.Audio.LocalOutputPriority is null) { errors.Add("오디오 목록이 없습니다."); return errors; }
        var bindings = value.Audio.LocalOutputPriority.Cast<DeviceBinding?>().Concat([value.Audio.RemoteOutput, value.Display.WindowsDevice]);
        if (value.Audio.LocalOutputPriority.Any(b => b is null) || bindings.Any(b => b is not null && (string.IsNullOrWhiteSpace(b.Id) || b.Name is null))) errors.Add("장치 식별자 또는 이름이 누락되었습니다.");
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (value.Audio.LocalOutputPriority.Where(b => b is not null).Select(b => b.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != value.Audio.LocalOutputPriority.Count) errors.Add("기본 출력 후보가 중복되어 있습니다.");
        foreach (var rule in value.Audio.Rules)
        {
            if (rule is null || rule.Device is null || string.IsNullOrWhiteSpace(rule.Device.Id) || rule.Device.Name is null) { errors.Add("오디오 규칙의 장치가 없습니다."); continue; }
            if (!ids.Add(rule.Device.Id)) errors.Add("같은 오디오 장치에 중복 규칙이 있습니다.");
            if (!Enum.IsDefined(rule.Start) || !Enum.IsDefined(rule.End)) errors.Add("알 수 없는 오디오 동작입니다.");
            if (string.Equals(value.Audio.RemoteOutput?.Id, rule.Device.Id, StringComparison.OrdinalIgnoreCase) && (rule.Start != StartAction.Unchanged || rule.StartEnabled == false || rule.StartMuted == true))
                errors.Add("원격 출력으로 사용할 장치를 시작 시 차단할 수 없습니다.");
        }
        if (value.Audio.Restore == RestoreStrategy.FixedProfile && value.Audio.LocalOutputPriority.Count == 0)
            errors.Add("지정 프로필 복구에는 로컬 기본 출력 후보가 필요합니다.");
        return errors;
    }
}

public sealed record DeviceResolution(AudioDevice? Device, string? Issue);
public static class DeviceResolver
{
    public static DeviceResolution Resolve(DeviceBinding binding, IEnumerable<AudioDevice> devices)
    {
        var list = devices.Where(d => d.Present).ToList();
        var exact = list.Where(d => d.Id.Equals(binding.Id, StringComparison.OrdinalIgnoreCase)).ToList();
        if (exact.Count == 1) return new(exact[0], null);
        if (exact.Count > 1) return new(null, "장치 ID가 중복되어 다시 선택해야 합니다.");
        var names = list.Count(d => d.Name.Equals(binding.Name, StringComparison.OrdinalIgnoreCase));
        return new(null, names > 0 ? "장치 ID가 변경되었습니다. 이름 후보를 확인하고 다시 연결하세요." : "장치가 연결되어 있지 않습니다.");
    }
    public static AudioDevice? FirstAvailable(IEnumerable<DeviceBinding> priority, IEnumerable<AudioDevice> devices)
    {
        var list = devices.ToList();
        return priority.Select(binding => Resolve(binding, list).Device).FirstOrDefault(device => device is not null);
    }
}

public static class SessionRules
{
    public static SessionPhase Transition(SessionPhase phase, SessionSignal signal) => signal switch
    {
        SessionSignal.Start when phase == SessionPhase.Local => SessionPhase.Preparing,
        SessionSignal.Disconnect or SessionSignal.Resume when phase == SessionPhase.Remote => SessionPhase.Remote,
        SessionSignal.End when phase is SessionPhase.Remote or SessionPhase.Preparing => SessionPhase.Recovering,
        _ => phase
    };
}
