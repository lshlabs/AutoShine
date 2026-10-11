using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace AutoShine.Web;

internal static class FilePicker
{
    private static readonly SemaphoreSlim Gate = new(1);
    [DllImport("user32.dll")] private static extern nint GetForegroundWindow();
    [DllImport("user32.dll")] private static extern bool SetForegroundWindow(nint window);
    private sealed record WindowOwner(nint Handle) : IWin32Window;

    public static async Task<string?> Pick(string kind, string? currentPath)
    {
        if (kind is not ("tool" or "sunshine")) throw new IOException("지원하지 않는 파일 종류입니다.");
        var foreground = GetForegroundWindow();
        await Gate.WaitAsync();
        try {
            var completion = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
            var thread = new Thread(() => {
                try {
                    // A visible, transparent owner keeps the dialog in front of the browser.
                    // The selected file remains at its original location; no file is copied.
                    using var owner = new Form {
                        ShowInTaskbar = false, TopMost = true, Opacity = 0,
                        FormBorderStyle = FormBorderStyle.None,
                        StartPosition = FormStartPosition.Manual,
                        Size = new System.Drawing.Size(1, 1)
                    };
                    var bounds = Screen.FromHandle(foreground).WorkingArea;
                    owner.Location = new(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2);
                    if (foreground != 0) owner.Show(new WindowOwner(foreground)); else owner.Show();
                    owner.Activate();
                    SetForegroundWindow(owner.Handle);
                    using var dialog = new OpenFileDialog {
                        Title = kind == "tool" ? "SoundVolumeView 실행 파일 선택" : "Sunshine 설정 파일 선택",
                        Filter = kind == "tool" ? "SoundVolumeView|SoundVolumeView.exe" : "Sunshine 설정|sunshine.conf",
                        CheckFileExists = true, Multiselect = false, RestoreDirectory = true,
                        InitialDirectory = !string.IsNullOrWhiteSpace(currentPath) && Directory.Exists(Path.GetDirectoryName(currentPath)) ? Path.GetDirectoryName(currentPath) : null
                    };
                    var path = dialog.ShowDialog(owner) == DialogResult.OK ? Path.GetFullPath(dialog.FileName) : null;
                    owner.Close();
                    if (foreground != 0) SetForegroundWindow(foreground);
                    completion.SetResult(path);
                } catch (Exception ex) { completion.SetException(ex); }
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();
            return await completion.Task;
        } finally { Gate.Release(); }
    }
}
