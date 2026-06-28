using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text.Json;
using CGCLauncher.Models;
using CGCLauncher.Utils;

namespace CGCLauncher.Services;

public class DownloadService
{
    private static readonly HttpClient Http = new();
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    /// <summary>Fired with human-readable progress lines for the UI to display.</summary>
    public event Action<string>? Log;

    private void WriteLog(string msg) => Log?.Invoke(msg);

    private async Task DownloadFileAsync(string url, string destPath, long? expectedSize = null)
    {
        var dir = Path.GetDirectoryName(destPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        if (File.Exists(destPath) && (expectedSize == null || new FileInfo(destPath).Length == expectedSize))
            return; // already present, skip re-downloading

        WriteLog($"  downloading {Path.GetFileName(destPath)}");
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        await using var fs = new FileStream(destPath, FileMode.Create, FileAccess.Write);
        await response.Content.CopyToAsync(fs);
    }

    public async Task<string> DownloadClientJarAsync(VersionDetails details, string installDir)
    {
        var client = details.Downloads.Client
                     ?? throw new InvalidOperationException($"Version {details.Id} has no client download entry.");

        var jarPath = Path.Combine(installDir, "versions", details.Id, $"{details.Id}.jar");
        await DownloadFileAsync(client.Url, jarPath, client.Size);
        return jarPath;
    }

    /// <summary>Downloads all platform-applicable libraries and extracts any native (.dll/.so/.dylib) jars.</summary>
    public async Task<(List<string> ClasspathJars, string NativesDir)> DownloadLibrariesAsync(VersionDetails details, string installDir)
    {
        var librariesRoot = Path.Combine(installDir, "libraries");
        var nativesDir = Path.Combine(installDir, "versions", details.Id, "natives");
        Directory.CreateDirectory(nativesDir);

        var classpathJars = new List<string>();
        var nativeJarsToExtract = new List<string>();

        foreach (var lib in details.Libraries)
        {
            if (!OsUtils.EvaluateRules(lib.Rules)) continue;

            if (lib.Downloads?.Artifact is { } artifact)
            {
                var path = Path.Combine(librariesRoot, artifact.Path.Replace('/', Path.DirectorySeparatorChar));
                await DownloadFileAsync(artifact.Url, path, artifact.Size);
                classpathJars.Add(path);
            }

            // Native libraries are published under classifiers like "natives-windows".
            var classifierKey = lib.Natives != null && lib.Natives.TryGetValue(OsUtils.GetOsName(), out var key)
                ? key
                : null;

            if (classifierKey != null
                && lib.Downloads?.Classifiers != null
                && lib.Downloads.Classifiers.TryGetValue(classifierKey, out var nativeArtifact))
            {
                var nativeJarPath = Path.Combine(librariesRoot, nativeArtifact.Path.Replace('/', Path.DirectorySeparatorChar));
                await DownloadFileAsync(nativeArtifact.Url, nativeJarPath, nativeArtifact.Size);
                nativeJarsToExtract.Add(nativeJarPath);
            }
        }

        foreach (var jar in nativeJarsToExtract)
            ExtractNatives(jar, nativesDir);

        return (classpathJars, nativesDir);
    }

    private void ExtractNatives(string jarPath, string destDir)
    {
        using var archive = ZipFile.OpenRead(jarPath);
        foreach (var entry in archive.Entries)
        {
            if (string.IsNullOrEmpty(entry.Name) || entry.FullName.StartsWith("META-INF"))
                continue; // skip directories and jar metadata

            var destPath = Path.Combine(destDir, entry.Name);
            if (File.Exists(destPath)) continue;
            entry.ExtractToFile(destPath, overwrite: true);
        }
    }

    public async Task DownloadAssetsAsync(VersionDetails details, string installDir)
    {
        var assetIndexPath = Path.Combine(installDir, "assets", "indexes", $"{details.AssetIndex.Id}.json");
        await DownloadFileAsync(details.AssetIndex.Url, assetIndexPath, details.AssetIndex.Size);

        var json = await File.ReadAllTextAsync(assetIndexPath);
        var index = JsonSerializer.Deserialize<AssetIndexFile>(json, JsonOpts)
                    ?? throw new InvalidOperationException("Failed to parse asset index.");

        var objectsDir = Path.Combine(installDir, "assets", "objects");

        foreach (var asset in index.Objects.Values)
        {
            var prefix = asset.Hash[..2];
            var url = $"https://resources.download.minecraft.net/{prefix}/{asset.Hash}";
            var destPath = Path.Combine(objectsDir, prefix, asset.Hash);
            await DownloadFileAsync(url, destPath, asset.Size);
        }
    }
}
