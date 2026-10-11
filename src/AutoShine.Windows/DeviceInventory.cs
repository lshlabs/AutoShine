using System.Runtime.InteropServices;
using AutoShine.Core;

namespace AutoShine.Windows;

public static class DeviceInventory
{
    public static IReadOnlyList<AudioDevice> AudioOutputs()
    {
        var enumerator = (IMMDeviceEnumerator)new MMDeviceEnumerator();
        IMMDeviceCollection? devices = null;
        var output = new List<AudioDevice>();
        try
        {
            Marshal.ThrowExceptionForHR(enumerator.EnumAudioEndpoints(0, 15, out devices));
            Marshal.ThrowExceptionForHR(devices.GetCount(out var count));
            for (uint i = 0; i < count; i++)
            {
                IMMDevice? device = null; IPropertyStore? properties = null;
                try
                {
                    Marshal.ThrowExceptionForHR(devices.Item(i, out device));
                    Marshal.ThrowExceptionForHR(device.GetId(out var id));
                    Marshal.ThrowExceptionForHR(device.GetState(out var state));
                    Marshal.ThrowExceptionForHR(device.OpenPropertyStore(0, out properties));
                    var nameKey = new PropertyKey { Format = new("a45c254e-df1c-4efd-8020-67d146a850e0"), Id = 14 };
                    Marshal.ThrowExceptionForHR(properties.GetValue(ref nameKey, out var value));
                    string name;
                    try { name = value.Type == 31 ? Marshal.PtrToStringUni(value.Pointer) ?? id : id; }
                    finally { PropVariantClear(ref value); }
                    output.Add(new(id, name, state switch { 1 => DeviceState.Active, 2 => DeviceState.Disabled, 8 => DeviceState.Unplugged, _ => DeviceState.Absent }));
                }
                finally { Release(properties); Release(device); }
            }
        }
        finally { Release(devices); Release(enumerator); }
        return output.OrderBy(d => !d.Present).ThenBy(d => d.Name).ToList();
    }

    public static IReadOnlyList<DisplayDevice> Displays()
    {
        var result = new List<DisplayDevice>();
        for (uint index = 0; ; index++)
        {
            var adapter = new DisplayNative { Size = Marshal.SizeOf<DisplayNative>() };
            if (!EnumDisplayDevices(null, index, ref adapter, 0)) break;
            if ((adapter.Flags & 8) != 0) continue; // Legacy mirroring adapters are not physical targets.
            var monitor = new DisplayNative { Size = Marshal.SizeOf<DisplayNative>() };
            var hasMonitor = EnumDisplayDevices(adapter.Name, 0, ref monitor, 0);
            var identity = hasMonitor && !string.IsNullOrEmpty(monitor.Id) ? monitor.Id : adapter.Key ?? "";
            if (identity.Length == 0) continue;
            var mode = new Mode { Size = (short)Marshal.SizeOf<Mode>() };
            var enabled = (adapter.Flags & 1) != 0 && EnumDisplaySettings(adapter.Name, -1, ref mode);
            var virtualDisplay = (monitor.Id ?? "").Contains("MTT1337", StringComparison.OrdinalIgnoreCase) || (adapter.Description ?? "").Contains("Idd", StringComparison.OrdinalIgnoreCase) || (monitor.Description ?? "").Contains("VDD", StringComparison.OrdinalIgnoreCase);
            // An unused GPU output is not a monitor. Keep inactive virtual targets.
            if (!hasMonitor && !virtualDisplay) continue;
            result.Add(new(identity, virtualDisplay ? "가상 디스플레이 · " + adapter.Name : adapter.Name + " · " + monitor.Description, enabled, virtualDisplay, mode.Width, mode.Height, mode.Hz));
        }
        return result.OrderByDescending(d => d.Virtual).ToList();
    }
    public static string? DefaultOutput(int role)
    {
        var en = (IMMDeviceEnumerator)new MMDeviceEnumerator(); IMMDevice? d = null;
        try { if (en.GetDefaultAudioEndpoint(0, role, out d) < 0) return null; Marshal.ThrowExceptionForHR(d.GetId(out var id)); return id; }
        finally { Release(d); Release(en); }
    }
    public static bool EndpointMute(string id, bool? set = null)
    {
        var en = (IMMDeviceEnumerator)new MMDeviceEnumerator(); IMMDevice? d = null; object? instance = null;
        try {
            Marshal.ThrowExceptionForHR(en.GetDevice(id, out d)); var iid = typeof(IAudioEndpointVolume).GUID;
            Marshal.ThrowExceptionForHR(d.Activate(ref iid, 23, IntPtr.Zero, out instance));
            var volume = (IAudioEndpointVolume)instance;
            if (set.HasValue) { var context = Guid.Empty; Marshal.ThrowExceptionForHR(volume.SetMute(set.Value, ref context)); }
            Marshal.ThrowExceptionForHR(volume.GetMute(out var muted));
            if (set.HasValue && muted != set.Value) throw new IOException("Mute verification failed");
            return muted;
        } finally { Release(instance); Release(d); Release(en); }
    }
    [ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] private interface IAudioEndpointVolume
    {
        [PreserveSig] int RegisterControlChangeNotify(IntPtr callback);
        [PreserveSig] int UnregisterControlChangeNotify(IntPtr callback);
        [PreserveSig] int GetChannelCount(out uint count);
        [PreserveSig] int SetMasterVolumeLevel(float level, ref Guid context);
        [PreserveSig] int SetMasterVolumeLevelScalar(float level, ref Guid context);
        [PreserveSig] int GetMasterVolumeLevel(out float level);
        [PreserveSig] int GetMasterVolumeLevelScalar(out float level);
        [PreserveSig] int SetChannelVolumeLevel(uint channel, float level, ref Guid context);
        [PreserveSig] int SetChannelVolumeLevelScalar(uint channel, float level, ref Guid context);
        [PreserveSig] int GetChannelVolumeLevel(uint channel, out float level);
        [PreserveSig] int GetChannelVolumeLevelScalar(uint channel, out float level);
        [PreserveSig] int SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, ref Guid context);
        [PreserveSig] int GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }
    private static void Release(object? value) { if (value is not null) Marshal.ReleaseComObject(value); }
    [ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] private class MMDeviceEnumerator { }
    [ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] private interface IMMDeviceEnumerator
    {
        [PreserveSig] int EnumAudioEndpoints(int flow, int mask, out IMMDeviceCollection collection);
        [PreserveSig] int GetDefaultAudioEndpoint(int flow, int role, out IMMDevice device);
        [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string id, out IMMDevice device);
    }
    [ComImport, Guid("0BD7A1BE-7A1A-44DB-8397-CC5392387B5E"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] private interface IMMDeviceCollection
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int Item(uint index, out IMMDevice device);
    }
    [ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] private interface IMMDevice
    {
        [PreserveSig] int Activate(ref Guid id, int context, IntPtr parameters, [MarshalAs(UnmanagedType.IUnknown)] out object instance);
        [PreserveSig] int OpenPropertyStore(int access, out IPropertyStore properties);
        [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string id);
        [PreserveSig] int GetState(out int state);
    }
    [ComImport, Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)] private interface IPropertyStore
    {
        [PreserveSig] int GetCount(out uint count);
        [PreserveSig] int GetAt(uint index, out PropertyKey key);
        [PreserveSig] int GetValue(ref PropertyKey key, out PropVariant value);
    }
    [StructLayout(LayoutKind.Sequential)] private struct PropertyKey { public Guid Format; public uint Id; }
    [StructLayout(LayoutKind.Explicit, Size = 24)] private struct PropVariant { [FieldOffset(0)] public ushort Type; [FieldOffset(8)] public IntPtr Pointer; }
    [DllImport("ole32.dll")] private static extern int PropVariantClear(ref PropVariant value);
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct DisplayNative
    {
        public int Size;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Description;
        public int Flags;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Id;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string Key;
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] private struct Mode
    {
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Name;
        public short Spec, Version, Size, Extra;
        public int Fields, X, Y, Orientation, Fixed;
        public short Color, Duplex, Yres, TT, Collate;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string Form;
        public short LogPixels;
        public int Bpp, Width, Height, Flags, Hz, ICMMethod, ICMIntent, Media, Dither, Reserved1, Reserved2, PanW, PanH;
    }
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool EnumDisplayDevices(string? device, uint index, ref DisplayNative display, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool EnumDisplaySettings(string device, int index, ref Mode mode);
}
