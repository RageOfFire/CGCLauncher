namespace CGCLauncher.Models;

/// <summary>
/// Saved to disk so the launcher remembers the player's last choices
/// between sessions — no more picking the version and folder every time.
/// </summary>
public class LauncherSettings
{
    /// <summary>Version ID of the last successfully launched version, e.g. "1.20.4"</summary>
    public string LastVersionId { get; set; } = string.Empty;

    /// <summary>Install directory the player last used.</summary>
    public string InstallDir { get; set; } = string.Empty;

    /// <summary>Whether the Snapshot checkbox was ticked.</summary>
    public bool ShowSnapshots { get; set; } = false;

    /// <summary>Offline username entered by the player.</summary>
    public string OfflineUsername { get; set; } = "Steve";

    /// <summary>Hide the launcher window while Minecraft is running.</summary>
    public bool HideLauncherOnLaunch { get; set; } = false;
}
