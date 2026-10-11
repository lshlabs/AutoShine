using System.Text.Json;

namespace AutoShine.Core;

// Fixed event names and exception types only; settings and credentials stay out.
public sealed class DiagnosticLog(string path)
{
    readonly object gate = new();
    string? last;
    DateTime lastAt;
    public void Write(string operation, Exception? failure = null)
    {
        lock (gate)
        {
            var signature = operation + ":" + failure?.GetType().Name;
            if (signature == last && DateTime.UtcNow - lastAt < TimeSpan.FromMinutes(1)) return;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                if (File.Exists(path) && new FileInfo(path).Length > 1024 * 1024) File.Move(path, path + ".previous", true);
                File.AppendAllText(path, JsonSerializer.Serialize(new { utc = DateTime.UtcNow, operation, errorType = failure?.GetType().Name }) + Environment.NewLine);
                last = signature; lastAt = DateTime.UtcNow;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
        }
    }
}
