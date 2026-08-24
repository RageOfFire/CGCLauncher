using System.Net.Http;
using System.Text.Json;
using CGCLauncher.Models;

namespace CGCLauncher.Services;

public class MojangApiService
{
    private static readonly HttpClient Http = new();
    private const string ManifestUrl =
        "https://launchermeta.mojang.com/mc/game/version_manifest_v2.json";

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public async Task<VersionManifest> GetVersionManifestAsync()
    {
        var json = await Http.GetStringAsync(ManifestUrl);
        return JsonSerializer.Deserialize<VersionManifest>(json, JsonOpts)
               ?? throw new InvalidOperationException("Không thể phân tích manifest.");
    }

    public async Task<VersionDetails> GetVersionDetailsAsync(string versionJsonUrl)
    {
        var json = await Http.GetStringAsync(versionJsonUrl);
        return JsonSerializer.Deserialize<VersionDetails>(json, JsonOpts)
               ?? throw new InvalidOperationException("Không thể phân tích chi tiết phiên bản.");
    }
}
