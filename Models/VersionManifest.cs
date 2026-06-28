namespace CGCLauncher.Models;

public class VersionManifest
{
    public LatestVersions Latest { get; set; } = new();
    public List<VersionManifestEntry> Versions { get; set; } = new();
}

public class LatestVersions
{
    public string Release { get; set; } = string.Empty;
    public string Snapshot { get; set; } = string.Empty;
}

/// <summary>
/// One entry in the manifest. Doesn't contain the full version data -
/// Url points to a per-version JSON with libraries/downloads/etc.
/// </summary>
public class VersionManifestEntry
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // release, snapshot, old_beta, old_alpha
    public string Url { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public string ReleaseTime { get; set; } = string.Empty;

    public override string ToString() => Id;
}
