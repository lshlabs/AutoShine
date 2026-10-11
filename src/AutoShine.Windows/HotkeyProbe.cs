using System.ComponentModel;
using System.Runtime.InteropServices;
using AutoShine.Core;

namespace AutoShine.Windows;
public static class HotkeyProbe
{
    public static string Check(IntPtr handle, HotkeySettings settings)
    {
        if (!settings.Enabled) return "종료 단축키 사용이 꺼져 있습니다.";
        var validation = SettingsValidator.Validate(new() { Hotkey = settings });
        if (validation.Count > 0) return string.Join(" ", validation);
        var name = settings.Key.ToUpperInvariant();
        uint key = name switch { "SCROLLLOCK" => 0x91, "PAUSE" => 0x13, _ => name.Length == 1 ? name[0] : (uint)(0x6F + int.Parse(name[1..])) };
        uint modifiers = 0x4000u | (settings.Alt ? 1u : 0) | (settings.Control ? 2u : 0) | (settings.Shift ? 4u : 0);
        const int id = 0x6121;
        if (!RegisterHotKey(handle, id, modifiers, key))
        {
            var error = Marshal.GetLastWin32Error();
            return error == 1409 ? "이미 다른 프로그램이 사용하고 있습니다. 기존 등록은 변경하지 않았습니다." : "등록할 수 없습니다: " + new Win32Exception(error).Message;
        }
        UnregisterHotKey(handle, id);
        return "사용 가능한 조합입니다. 확인용 등록은 해제했습니다.";
    }
    [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr handle, int id, uint modifiers, uint key);
    [DllImport("user32.dll", SetLastError = true)] private static extern bool UnregisterHotKey(IntPtr handle, int id);
}

