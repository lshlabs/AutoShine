namespace AutoShine.Core;

public static class AudioRecovery
{
    public static bool? EndMute(AudioRule rule, AudioDevice? selectedOutput) => rule.End switch
    {
        EndAction.Muted => true,
        EndAction.Unmuted => false,
        EndAction.SelectedOutputMutedElseUnmuted => selectedOutput is null ? null : string.Equals(rule.Device.Id, selectedOutput.Id, StringComparison.OrdinalIgnoreCase),
        _ => null
    };
}
