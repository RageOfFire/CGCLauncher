namespace CGCLauncher.Models;

public enum VersionKind { Vanilla, OptiFine }

/// <summary>
/// A single row in the version combo box. Wraps either a vanilla manifest entry or an
/// OptiFine version so both can live in the same list without casting gymnastics.
/// </summary>
public class LauncherVersion
{
    public VersionKind Kind { get; init; }

    // Exactly one of these will be set, depending on Kind.
    public VersionManifestEntry? ManifestEntry { get; init; }
    public OptiFineVersion? OptiFine { get; init; }

    public string DisplayName => Kind == VersionKind.Vanilla
        ? ManifestEntry!.Id
        : OptiFine!.DisplayName;

    public override string ToString() => DisplayName;

    // Convenience factories
    public static LauncherVersion FromVanilla(VersionManifestEntry entry) => new()
    {
        Kind = VersionKind.Vanilla,
        ManifestEntry = entry
    };

    public static LauncherVersion FromOptiFine(OptiFineVersion of) => new()
    {
        Kind = VersionKind.OptiFine,
        OptiFine = of
    };
}
