namespace AutoShine.Core;

public static class PipeMessages
{
    public static async Task<string?> ExchangeAsync(Stream pipe, string command, TimeSpan timeout)
    {
        // The caller owns the pipe; disposing either text wrapper must not close it.
        using var writer = new StreamWriter(pipe, leaveOpen: true) { AutoFlush = true };
        using var reader = new StreamReader(pipe, leaveOpen: true);
        await writer.WriteLineAsync(command);
        return await reader.ReadLineAsync().WaitAsync(timeout);
    }
}
