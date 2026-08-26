using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using CGCLauncher.Models;

namespace CGCLauncher.Services;

/// <summary>
/// Downloads and caches Mojang's own JRE bundles — exactly the same Java
/// builds the official Minecraft launcher ships.  Players never need to
/// install Java themselves; the launcher fetches the correct version
/// automatically based on what the chosen Minecraft version requires.
/// </summary>
public class JavaRuntimeService
{
    // Mojang's public Java runtime index — lists every component (java-runtime-gamma,
    // jre-legacy, etc.) for every supported platform.
    private const string AllRuntimesUrl =
        "https://launchermeta.mojang.com/v1/products/java-runtime/" +
        "2ec0cc96c44e5a76b9c8b7c39df7210883d12871/all.json";

    private static readonly HttpClient Http = new();
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public event Action<string>? Log;
    private void WriteLog(string msg) => Log?.Invoke(msg);

    // ── Public entry point ────────────────────────────────────────────────────

    /// <summary>
    /// Ensures the JRE required by <paramref name="details"/> is present on disk
    /// and returns the full path to java.exe (or "java" on Linux/macOS).
    ///
    /// If the runtime folder already exists the download is skipped entirely.
    /// </summary>
    public async Task<string> EnsureJavaAsync(VersionDetails details, string installDir)
    {
        // Each Minecraft version declares which Java component it needs:
        //   jre-legacy        → Java 8  (MC < 1.17)
        //   java-runtime-alpha → Java 16 (MC 1.17)
        //   java-runtime-beta  → Java 17 (MC 1.17.1 transition)
        //   java-runtime-gamma → Java 17 (MC 1.18 – 1.20)
        //   java-runtime-delta → Java 21 (MC 1.21+)
        var component = details.JavaVersion?.Component ?? "java-runtime-gamma";

        var runtimeDir = Path.Combine(installDir, "runtime", component);
        var javaExe    = GetJavaExePath(runtimeDir);

        if (File.Exists(javaExe))
        {
            WriteLog($"  Java runtime pronto: {component} ({javaExe})");
            return javaExe;
        }

        WriteLog($"  Đang tải Java runtime ({component}) từ Mojang...");

        // Fetch all.json — the index of every available runtime
        var allJson = await Http.GetStringAsync(AllRuntimesUrl);

        // The outer two keys are platform and component — both dynamic strings,
        // so we parse as nested dictionaries.
        var allRuntimes = JsonSerializer.Deserialize<
            Dictionary<string, Dictionary<string, List<JavaRuntimeEntry>>>>(allJson, JsonOpts)
            ?? throw new InvalidOperationException("Không thể phân tích Java runtime manifest.");

        var platformKey = GetPlatformKey();

        if (!allRuntimes.TryGetValue(platformKey, out var platformRuntimes)
            || !platformRuntimes.TryGetValue(component, out var entries)
            || entries.Count == 0)
        {
            throw new InvalidOperationException(
                $"Không tìm thấy Java runtime '{component}' cho nền tảng '{platformKey}'. " +
                "Hãy cài đặt Java thủ công từ adoptium.net.");
        }

        var entry = entries[0];
        WriteLog($"  Phiên bản Java: {entry.Version.Name}");

        // Download the per-component file manifest
        var fileManifestJson = await Http.GetStringAsync(entry.Manifest.Url);
        var fileManifest = JsonSerializer.Deserialize<JavaFileManifest>(fileManifestJson, JsonOpts)
                           ?? throw new InvalidOperationException("Không thể phân tích file manifest.");

        // Download every file listed in the manifest
        var total   = fileManifest.Files.Count(f => f.Value.Type == "file");
        var current = 0;

        foreach (var (relativePath, fileEntry) in fileManifest.Files)
        {
            var localPath = Path.Combine(runtimeDir,
                relativePath.Replace('/', Path.DirectorySeparatorChar));

            switch (fileEntry.Type)
            {
                case "directory":
                    Directory.CreateDirectory(localPath);
                    break;

                case "file":
                    if (fileEntry.Downloads == null) break;
                    await DownloadFileAsync(fileEntry.Downloads.Raw, localPath);
                    current++;
                    if (current % 50 == 0 || current == total)
                        WriteLog($"  Java: {current}/{total} file đã tải...");
                    break;

                case "link":
                    // Symlinks — only relevant on Linux/macOS; skip on Windows
                    if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
                        && fileEntry.Target != null)
                    {
                        var dir = Path.GetDirectoryName(localPath);
                        if (dir != null) Directory.CreateDirectory(dir);
                        if (!File.Exists(localPath))
                            File.CreateSymbolicLink(localPath, fileEntry.Target);
                    }
                    break;
            }
        }

        WriteLog($"  Java runtime đã sẵn sàng: {entry.Version.Name}");
        return javaExe;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    /// <summary>Returns the full path to the java executable for the current OS.</summary>
    private static string GetJavaExePath(string runtimeDir)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return Path.Combine(runtimeDir, "bin", "java.exe");
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return Path.Combine(runtimeDir, "jre.bundle", "Contents", "Home", "bin", "java");
        return Path.Combine(runtimeDir, "bin", "java");
    }

    /// <summary>
    /// Maps the current OS + architecture to the platform key used in Mojang's all.json.
    /// </summary>
    private static string GetPlatformKey()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return RuntimeInformation.ProcessArchitecture == Architecture.X86
                ? "windows-x86" : "windows-x64";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return RuntimeInformation.ProcessArchitecture == Architecture.Arm64
                ? "mac-os-arm64" : "mac-os";

        // Linux
        return RuntimeInformation.ProcessArchitecture == Architecture.X86
            ? "linux-i386" : "linux";
    }

    private async Task DownloadFileAsync(JavaFileArtifact artifact, string destPath)
    {
        var dir = Path.GetDirectoryName(destPath);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        // Skip if already downloaded with the right size
        if (File.Exists(destPath) && new FileInfo(destPath).Length == artifact.Size)
            return;

        using var response = await Http.GetAsync(
            artifact.Url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        await using var fs = new FileStream(destPath, FileMode.Create, FileAccess.Write);
        await response.Content.CopyToAsync(fs);
    }
}
