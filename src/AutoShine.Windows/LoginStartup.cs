using Microsoft.Win32;

namespace AutoShine.Windows;

public static class LoginStartup
{
    const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    static string Executable => Path.Combine(AppContext.BaseDirectory, "AutoShine.exe");
    static bool Owned(object? value) => value is string command && command.Equals($"\"{Executable}\" --background", StringComparison.OrdinalIgnoreCase);
    public static bool MachineEnabled()
    {
        using var key = Registry.LocalMachine.OpenSubKey(RunKey);
        return Owned(key?.GetValue("AutoShine"));
    }
    public static bool Enabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return Owned(key?.GetValue("AutoShine")) || MachineEnabled();
    }
    public static void RemoveMachineEntry()
    {
        using var key = Registry.LocalMachine.OpenSubKey(RunKey, writable: true);
        if (Owned(key?.GetValue("AutoShine"))) key!.DeleteValue("AutoShine", false);
    }
    public static void SetUser(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKey, writable: true);
        if (enabled) {
            if (!File.Exists(Executable)) throw new IOException("AutoShine 실행 파일을 찾을 수 없습니다.");
            key.SetValue("AutoShine", $"\"{Executable}\" --background", RegistryValueKind.String);
        } else if (Owned(key.GetValue("AutoShine"))) key.DeleteValue("AutoShine", false);
    }
}
