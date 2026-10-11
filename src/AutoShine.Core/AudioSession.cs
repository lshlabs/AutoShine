namespace AutoShine.Core;

public interface IAudioControl
{
    IReadOnlyList<AudioDevice> Devices();
    string? Default(int role);
    bool Muted(string id);
    void Mute(string id, bool value);
    void Enable(string id, bool value);
    void SetDefault(string id, int role);
    void SettleDefaults(IReadOnlyList<string?> defaults, IReadOnlyList<string> protectedOutputs) { }
}
public sealed record EndpointSnapshot(string Id, bool Enabled, bool Muted);
public sealed record AudioSessionSnapshot(AudioSettings Policy, string?[] Defaults, List<EndpointSnapshot> Devices)
{
    public SunshineSettings? Connection { get; init; }
    public DisplaySettings? Display { get; init; }
}

// A session uses one frozen policy. Editing the UI cannot change recovery halfway through.
public sealed class AudioSession(IAudioControl audio)
{
    public AudioSessionSnapshot Capture(AudioSettings policy)
    {
        ValidateRequired(policy);
        var devices = audio.Devices();
        var defaults = Enumerable.Range(0, 3).Select(audio.Default).ToArray();
        var ids = policy.Rules.Select(r => r.Device.Id).Concat(policy.LocalOutputPriority.Select(d => d.Id)).Concat(defaults.OfType<string>()).Concat(policy.RemoteOutput is null ? [] : new[] { policy.RemoteOutput.Id }).Distinct(StringComparer.OrdinalIgnoreCase);
        return new(policy, defaults,
            ids.Select(id => devices.SingleOrDefault(d => d.Id == id && d.Present)).OfType<AudioDevice>()
            .Select(d => new EndpointSnapshot(d.Id, d.State == DeviceState.Active, audio.Muted(d.Id))).ToList());
    }
    public void Start(AudioSessionSnapshot session)
    {
        ValidateRequired(session.Policy);
        Protect(session.Policy);
        foreach (var rule in session.Policy.Rules)
            if (Present(rule.Device.Id)) {
                var enabled=rule.StartEnabled ?? (rule.Start == StartAction.Disable ? false : (bool?)null);
                if(enabled.HasValue) audio.Enable(rule.Device.Id, enabled.Value);
                if(rule.StartMuted.HasValue) audio.Mute(rule.Device.Id, rule.StartMuted.Value);
            }
        if (session.Policy.RemoteOutput is { } output)
        {
            audio.Enable(output.Id, true);
            audio.Mute(output.Id, false);
            for (var role = 0; role < 3; role++) audio.SetDefault(output.Id, role);
        }
    }
    public void Protect(AudioSettings policy)
    {
        var present = audio.Devices().Where(d => d.Present).Select(d => d.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in policy.Rules.Where(r => r.StartMuted == true || r.StartMuted is null && r.Start != StartAction.Unchanged))
            if (present.Contains(rule.Device.Id) && !audio.Muted(rule.Device.Id)) audio.Mute(rule.Device.Id, true);
    }
    public void End(AudioSessionSnapshot session)
    {
        var policy = session.Policy;
        // Preserve the state at entry, including temporary muting during recovery.
        var unchanged = policy.Rules.Where(r => r.End == EndAction.Unchanged && Present(r.Device.Id))
            .ToDictionary(r => r.Device.Id, r => audio.Muted(r.Device.Id));
        Protect(policy);
        var devices = audio.Devices();
        var selected = policy.Restore == RestoreStrategy.FixedProfile
            ? DeviceResolver.FirstAvailable(policy.LocalOutputPriority.Where(b => policy.Rules.All(r => r.Device.Id != b.Id || r.EndEnabled != false)), devices)
            : devices.FirstOrDefault(d => d.Present && d.Id == session.Defaults[0] && policy.Rules.All(r => r.Device.Id != d.Id || r.EndEnabled != false));
        // Mute before enabling: Windows may automatically select an enabled endpoint.
        foreach (var rule in policy.Rules)
        {
            if (!Present(rule.Device.Id)) { if (!rule.Optional) throw new IOException("Required output missing: " + rule.Device.Name); continue; }
            var before = session.Devices.FirstOrDefault(d => d.Id == rule.Device.Id);
            if (rule.EndEnabled == true || rule.EndEnabled is null && (rule.EnableOnEnd || rule.End == EndAction.PreviousState && before?.Enabled == true))
            { audio.Mute(rule.Device.Id, true); audio.Enable(rule.Device.Id, true); }
        }
        if (selected is not null)
        {
            audio.Mute(selected.Id, true); audio.Enable(selected.Id, true);
            string? RestoredTarget(int role)
            {
                if (policy.Restore != RestoreStrategy.PreviousState) return selected.Id;
                var target = session.Defaults[role];
                return target is not null && Present(target) && policy.Rules.All(r => !string.Equals(r.Device.Id, target, StringComparison.OrdinalIgnoreCase) || r.EndEnabled != false)
                    ? target : selected.Id;
            }
            for (var role = 0; role < 3; role++)
            {
                var target = RestoredTarget(role);
                if (target is not null && Present(target)) audio.SetDefault(target, role);
            }
            var defaults = Enumerable.Range(0, 3).Select(RestoredTarget).ToArray();
            audio.SettleDefaults(defaults, policy.Rules.Where(r => r.Start != StartAction.Unchanged || r.EnableOnEnd).Select(r => r.Device.Id).Append(selected.Id).Where(Present).Distinct().ToArray());
        }
        foreach (var rule in policy.Rules)
        {
            if (!Present(rule.Device.Id)) continue;
            var before = session.Devices.FirstOrDefault(d => d.Id == rule.Device.Id);
            var mute = AudioRecovery.EndMute(rule, selected);
            if (rule.End == EndAction.PreviousState) mute = before?.Muted;
            if (rule.End == EndAction.Unchanged) mute = unchanged.GetValueOrDefault(rule.Device.Id);
            if (mute.HasValue) audio.Mute(rule.Device.Id, mute.Value);
            if (rule.EndEnabled == false || rule.EndEnabled is null && rule.End == EndAction.PreviousState && before?.Enabled == false && !rule.EnableOnEnd && selected?.Id != rule.Device.Id) audio.Enable(rule.Device.Id, false);
        }
        if (selected is not null && policy.Rules.All(r => r.Device.Id != selected.Id))
        {
            var before = session.Devices.FirstOrDefault(d => d.Id == selected.Id);
            if (before is not null) audio.Mute(selected.Id, before.Muted);
        }
        if (policy.RemoteOutput is { } remote && policy.Rules.All(r => r.Device.Id != remote.Id))
        {
            var before = session.Devices.FirstOrDefault(d => d.Id == remote.Id);
            if (before is not null && Present(remote.Id)) { audio.Mute(remote.Id, before.Muted); audio.Enable(remote.Id, before.Enabled); }
        }
    }
    private bool Present(string id) => audio.Devices().Any(d => d.Id == id && d.Present);
    private void ValidateRequired(AudioSettings policy)
    {
        var present = audio.Devices().Where(d => d.Present).Select(d => d.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var rule in policy.Rules.Where(r => !r.Optional))
            if (!present.Contains(rule.Device.Id)) throw new IOException("Required output missing: " + rule.Device.Name);
        if (policy.RemoteOutput is { } remote && !present.Contains(remote.Id)) throw new IOException("Streaming output missing: " + remote.Name);
    }
}
