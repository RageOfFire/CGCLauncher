using System.Text.Json;

namespace CGCLauncher.Models;

public class VersionDetails
{
    /// <summary>Modern (1.13+) structured arguments. Null on older versions.</summary>
    public ArgumentsBlock? Arguments { get; set; }

    /// <summary>Legacy (pre-1.13) flat argument string. Null on newer versions.</summary>
    public string? MinecraftArguments { get; set; }

    public AssetIndexInfo AssetIndex { get; set; } = new();
    public string Assets { get; set; } = string.Empty;
    public DownloadsBlock Downloads { get; set; } = new();
    public string Id { get; set; } = string.Empty;
    public JavaVersionInfo? JavaVersion { get; set; }
    public List<Library> Libraries { get; set; } = new();
    public string MainClass { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

public class ArgumentsBlock
{
    // Each element is either a plain string or a {rules, value} object,
    // so we keep these as raw JsonElements and resolve them at launch time.
    public List<JsonElement> Game { get; set; } = new();
    public List<JsonElement> Jvm { get; set; } = new();
}

public class DownloadsBlock
{
    public DownloadEntry? Client { get; set; }
    public DownloadEntry? Server { get; set; }
}

public class DownloadEntry
{
    public string Sha1 { get; set; } = string.Empty;
    public long Size { get; set; }
    public string Url { get; set; } = string.Empty;
}

public class AssetIndexInfo : DownloadEntry
{
    public string Id { get; set; } = string.Empty;
    public long TotalSize { get; set; }
}

public class JavaVersionInfo
{
    public string Component { get; set; } = string.Empty;
    public int MajorVersion { get; set; }
}
