using System.Net.Http;
using System.Text.Json;
using CGCLauncher.Models;

namespace CGCLauncher.Services;

/// <summary>
/// Talks to Mojang's public metadata endpoints. These are unauthenticated, publicly
/// documented URLs - the same ones the official launcher and tools like Prism/MultiMC use
/// to know what versions exist and where to download them from.
/// </summary>
public class MojangApiService
{
    private static readonly HttpClient Http = new();
    private const string ManifestUrl = "https://launchermeta.mojang.com/mc/game/version_manifest_v2.json";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<VersionManifest> GetVersionManifestAsync()
    {
        var json = await Http.GetStringAsync(ManifestUrl);
        return JsonSerializer.Deserialize<VersionManifest>(json, JsonOpts)
               ?? throw new InvalidOperationException("Không thể phân tích tệp manifest phiên bản.");
    }

    public async Task<VersionDetails> GetVersionDetailsAsync(string versionJsonUrl)
    {
        var json = await Http.GetStringAsync(versionJsonUrl);
        return JsonSerializer.Deserialize<VersionDetails>(json, JsonOpts)
               ?? throw new InvalidOperationException("Không thể phân tích chi tiết phiên bản.");
    }
}
