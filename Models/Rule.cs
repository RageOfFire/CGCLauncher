namespace CGCLauncher.Models;

/// <summary>
/// A single rule used to gate libraries or launch arguments based on OS or feature flags.
/// </summary>
public class Rule
{
    public string Action { get; set; } = "allow"; // "allow" or "disallow"
    public OsRule? Os { get; set; }
    public Dictionary<string, bool>? Features { get; set; }
}

public class OsRule
{
    public string? Name { get; set; }   // windows / osx / linux
    public string? Arch { get; set; }   // x86 / x86_64 / arm64
    public string? Version { get; set; }
}

public class Library
{
    public string Name { get; set; } = string.Empty;
    public LibraryDownloads? Downloads { get; set; }

    /// <summary>Legacy native-classifier mapping, e.g. { "windows": "natives-windows" }</summary>
    public Dictionary<string, string>? Natives { get; set; }

    public List<Rule>? Rules { get; set; }
}

public class LibraryDownloads
{
    public Artifact? Artifact { get; set; }
    public Dictionary<string, Artifact>? Classifiers { get; set; }
}

public class Artifact
{
    public string Path { get; set; } = string.Empty;
    public string Sha1 { get; set; } = string.Empty;
    public long Size { get; set; }
    public string Url { get; set; } = string.Empty;
}
