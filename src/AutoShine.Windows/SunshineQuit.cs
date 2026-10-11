using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AutoShine.Windows;
public static class SunshineQuit
{
    public static async Task<string> CertificateFingerprint(int port)
    {
        // Only used to display the local certificate for explicit pairing, never to send credentials.
        string? pin = null;
        using var handler = new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false,
            ServerCertificateCustomValidationCallback = (_, certificate, _, _) => { pin = certificate is null ? null : Convert.ToHexString(SHA256.HashData(certificate.RawData)); return certificate is not null; } };
        using var client = new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(5) };
        using var response = await client.GetAsync($"https://localhost:{port + 1}/");
        return pin ?? throw new IOException("Sunshine 인증서를 확인할 수 없습니다.");
    }
    public static async Task Pair(int port, string username, string password, string pin)
    {
        if (username.Contains(':') || string.IsNullOrWhiteSpace(username) || password.Length == 0) throw new IOException("계정 정보를 확인하세요.");
        using var client = Client(pin);
        Authorize(client, username, password);
        using var response = await client.GetAsync($"https://localhost:{port + 1}/api/config");
        response.EnsureSuccessStatusCode();
        using var data = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        if (data.RootElement.TryGetProperty("status", out var status) && status.ValueKind == JsonValueKind.False) throw new IOException("Sunshine 인증이 거부되었습니다.");
        CredentialVault.Save("AutoShine/Sunshine/" + port, username, password);
        CredentialVault.Save("AutoShine/Certificate/" + port, "SHA256", pin);
    }
    public static async Task Quit(int port)
    {
        var credential = CredentialVault.Read("AutoShine/Sunshine/" + port);
        var pin = CredentialVault.Read("AutoShine/Certificate/" + port).Password;
        using var client = Client(pin); Authorize(client, credential.Username, credential.Password);
        using var response = await client.PostAsync($"https://localhost:{port + 1}/api/apps/close", new StringContent("", Encoding.UTF8, "application/json"));
        response.EnsureSuccessStatusCode();
        using var data = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        if (!data.RootElement.TryGetProperty("status", out var status) || status.ValueKind != JsonValueKind.True) throw new IOException("Sunshine이 종료 요청을 거부했습니다.");
    }
    private static HttpClient Client(string pin)
    {
        if (pin.Length != 64 || !pin.All(Uri.IsHexDigit)) throw new IOException("Invalid certificate fingerprint");
        return new(new HttpClientHandler { UseProxy = false, AllowAutoRedirect = false,
            ServerCertificateCustomValidationCallback = (_, certificate, _, _) => certificate is not null && string.Equals(Convert.ToHexString(SHA256.HashData(certificate.RawData)), pin, StringComparison.OrdinalIgnoreCase) }) { Timeout = TimeSpan.FromSeconds(8) };
    }
    private static void Authorize(HttpClient client, string username, string password) => client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(username + ":" + password)));
}
