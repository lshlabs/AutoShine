namespace AutoShine.Core;

public sealed record SimulatedDevice(string Id, string Name, bool Connected, bool Enabled, bool Muted);
public sealed record SimulationRequest(string Event, AppSettings Settings, List<SimulatedDevice> Devices, string?[] Defaults, AudioSessionSnapshot? Session);
public sealed record SimulationResult(List<SimulatedDevice> Devices, string?[] Defaults, AudioSessionSnapshot? Session, List<string> Events);

// This controller owns only in-memory devices. It cannot call Windows audio APIs.
public static class AudioSimulation
{
    public static SimulationResult Run(SimulationRequest request)
    {
        if (request.Event == "start") {
            var errors = SettingsValidator.Validate(request.Settings);
            if (errors.Count > 0) throw new IOException(string.Join(" ", errors));
        }
        if (request.Devices is null || request.Defaults is null || request.Devices.Count > 256 || request.Defaults.Length != 3 || request.Devices.Any(d => d is null || string.IsNullOrWhiteSpace(d.Id) || d.Name is null) || request.Devices.Select(d => d.Id).Distinct(StringComparer.OrdinalIgnoreCase).Count() != request.Devices.Count)
            throw new IOException("시뮬레이션 장치 상태가 올바르지 않습니다.");
        if (request.Session is { } saved && (saved.Defaults is null || saved.Defaults.Length != 3 || saved.Devices is null || saved.Devices.Any(d => d is null) || SettingsValidator.Validate(new() { Audio = saved.Policy }).Count > 0))
            throw new IOException("시뮬레이션 장치 상태가 올바르지 않습니다.");
        var audio = new VirtualAudio(request.Devices, request.Defaults);
        var engine = new AudioSession(audio);
        var session = request.Session;
        switch (request.Event)
        {
            case "start":
                if (session is not null) throw new IOException("시뮬레이션 세션이 이미 있습니다.");
                if (request.Settings.Audio.RemoteOutput is null) throw new IOException("원격 오디오 장치를 확인하세요.");
                session = engine.Capture(request.Settings.Audio); engine.Start(session); break;
            case "end":
                if (session is null) throw new IOException("시뮬레이션을 먼저 시작하세요.");
                engine.End(session); session = null; break;
            case "drop": case "reconnect":
                if (session is null) throw new IOException("시뮬레이션을 먼저 시작하세요.");
                audio.Events.Add("연결 끊김·재접속: 원격 설정 유지, 자동 종료 없음"); break;
            default: throw new IOException("알 수 없는 시뮬레이션 이벤트입니다.");
        }
        return new(audio.Values, audio.Defaults, session, audio.Events);
    }
    sealed class VirtualAudio(List<SimulatedDevice> devices, string?[] defaults) : IAudioControl
    {
        public List<SimulatedDevice> Values { get; } = devices.ToList();
        public string?[] Defaults { get; } = defaults.ToArray();
        public List<string> Events { get; } = [];
        public IReadOnlyList<AudioDevice> Devices() => Values.Select(d => new AudioDevice(d.Id, d.Name, !d.Connected ? DeviceState.Unplugged : d.Enabled ? DeviceState.Active : DeviceState.Disabled)).ToList();
        SimulatedDevice Find(string id) => Values.Single(d => d.Id == id && d.Connected);
        void Update(SimulatedDevice d) => Values[Values.FindIndex(x => x.Id == d.Id)] = d;
        public string? Default(int role) => Defaults[role];
        public bool Muted(string id) => Find(id).Muted;
        public void Mute(string id, bool value) { var d = Find(id); Update(d with { Muted = value }); Events.Add(d.Name + (value ? " · 음소거" : " · 음소거 해제")); }
        public void Enable(string id, bool value) { var d = Find(id); Update(d with { Enabled = value }); Events.Add(d.Name + (value ? " · 활성화" : " · 비활성화")); }
        public void SetDefault(string id, int role) { var d = Find(id); if (!d.Enabled) throw new IOException("비활성 장치를 기본 출력으로 지정할 수 없습니다."); Defaults[role] = id; Events.Add(d.Name + " · 기본 출력 역할 " + role); }
    }
}

