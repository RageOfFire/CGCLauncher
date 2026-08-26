using System.Text.Json.Serialization;

namespace CGCLauncher.Models;

// ── all.json — top-level manifest listing every component per platform ─────
// Structure: { "windows-x64": { "java-runtime-gamma": [ { manifest, version } ] } }
// We deserialise the outer two levels as nested dictionaries since the keys
// (platform names, component names) are dynamic strings.

public class JavaRuntimeEntry
{
    [JsonPropertyName("manifest")]
    public JavaRuntimeManifestRef Manifest { get; set; } = new();

    [JsonPropertyName("version")]
    public JavaRuntimeVersion Version { get; set; } = new();
}

public class JavaRuntimeManifestRef
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("sha1")]
    public string Sha1 { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }
}

public class JavaRuntimeVersion
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

// ── Per-component file manifest ────────────────────────────────────────────
// Downloaded from JavaRuntimeManifestRef.Url — lists every file to install.

public class JavaFileManifest
{
    [JsonPropertyName("files")]
    public Dictionary<string, JavaFileEntry> Files { get; set; } = new();
}

public class JavaFileEntry
{
    /// <summary>"file", "directory", or "link"</summary>
    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("downloads")]
    public JavaFileDownloads? Downloads { get; set; }

    /// <summary>True for executables like java.exe — we set the execute bit on non-Windows.</summary>
    [JsonPropertyName("executable")]
    public bool Executable { get; set; }

    /// <summary>For symlinks — the path the link should point to.</summary>
    [JsonPropertyName("target")]
    public string? Target { get; set; }
}

public class JavaFileDownloads
{
    [JsonPropertyName("raw")]
    public JavaFileArtifact Raw { get; set; } = new();
}

public class JavaFileArtifact
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("sha1")]
    public string Sha1 { get; set; } = string.Empty;

    [JsonPropertyName("size")]
    public long Size { get; set; }
}
