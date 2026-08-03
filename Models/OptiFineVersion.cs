namespace CGCLauncher.Models;

public class OptiFineVersion
{
    /// <summary>e.g. "OptiFine_1.20.4_HD_U_I7.jar"</summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>e.g. "1.20.4"</summary>
    public string MinecraftVersion { get; set; } = string.Empty;

    /// <summary>e.g. "HD_U_I7"</summary>
    public string OptiFineId { get; set; } = string.Empty;

    /// <summary>Direct jar download via OptiFine's adloadx endpoint.</summary>
    public string DownloadUrl =>
        $"https://optifine.net/adloadx?f={Uri.EscapeDataString(FileName)}";

    public string DisplayName => $"{MinecraftVersion}  +  OptiFine {OptiFineId.Replace("_", " ")}";

    public override string ToString() => DisplayName;
}
