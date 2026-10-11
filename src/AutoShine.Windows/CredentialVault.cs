using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace AutoShine.Windows;
public static class CredentialVault
{
    public static bool Exists(string target, bool migrate = true)
    {
        if (ExistsExact(target)) return true;
        var legacy = LegacyTarget(target);
        if (legacy is null || !ExistsExact(legacy)) return false;
        if (!migrate) return true;
        var credential = Read(legacy);
        Save(target, credential.Username, credential.Password);
        Delete(legacy);
        return true;
    }
    static string? LegacyTarget(string target) => target.StartsWith("AutoShine/", StringComparison.Ordinal)
        ? "SunshineCompanion/" + target["AutoShine/".Length..] : null;
    static bool ExistsExact(string target)
    {
        if (!CredRead(target, 1, 0, out var pointer))
        {
            if (Marshal.GetLastWin32Error() == 1168) return false;
            throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        CredFree(pointer); return true;
    }
    public static void Delete(string target)
    {
        if (!CredDelete(target, 1, 0) && Marshal.GetLastWin32Error() != 1168) throw new Win32Exception(Marshal.GetLastWin32Error());
        var legacy = LegacyTarget(target);
        if (legacy is not null && !CredDelete(legacy, 1, 0) && Marshal.GetLastWin32Error() != 1168) throw new Win32Exception(Marshal.GetLastWin32Error());
    }
    public static void Save(string target, string username, string password)
    {
        var bytes = Encoding.Unicode.GetBytes(password);
        var pointer = Marshal.AllocHGlobal(bytes.Length);
        try
        {
            Marshal.Copy(bytes, 0, pointer, bytes.Length);
            var credential = new Credential { Type = 1, TargetName = target, UserName = username, Persist = 2, CredentialBlobSize = (uint)bytes.Length, CredentialBlob = pointer };
            if (!CredWrite(ref credential, 0)) throw new Win32Exception(Marshal.GetLastWin32Error());
        }
        finally { Marshal.Copy(new byte[bytes.Length], 0, pointer, bytes.Length); Marshal.FreeHGlobal(pointer); Array.Clear(bytes); }
    }
    public static (string Username, string Password) Read(string target)
    {
        if (!Exists(target)) throw new IOException("Sunshine 인증을 먼저 연결하세요.");
        if (!CredRead(target, 1, 0, out var pointer)) throw new IOException("Sunshine 인증을 먼저 연결하세요.");
        try { var credential = Marshal.PtrToStructure<Credential>(pointer); return (credential.UserName ?? "", Marshal.PtrToStringUni(credential.CredentialBlob, (int)credential.CredentialBlobSize / 2) ?? ""); }
        finally { CredFree(pointer); }
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)] struct Credential
    {
        public uint Flags, Type; public string? TargetName, Comment; public long LastWritten; public uint CredentialBlobSize; public IntPtr CredentialBlob; public uint Persist, AttributeCount; public IntPtr Attributes; public string? TargetAlias, UserName;
    }
    [DllImport("advapi32.dll", EntryPoint = "CredWriteW", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool CredWrite(ref Credential credential, uint flags);
    [DllImport("advapi32.dll", EntryPoint = "CredReadW", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool CredRead(string target, uint type, uint flags, out IntPtr credential);
    [DllImport("advapi32.dll")] static extern void CredFree(IntPtr pointer);
    [DllImport("advapi32.dll", EntryPoint = "CredDeleteW", CharSet = CharSet.Unicode, SetLastError = true)] static extern bool CredDelete(string target, uint type, uint flags);
}
