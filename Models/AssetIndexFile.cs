namespace CGCLauncher.Models;

public class AssetIndexFile
{
    public Dictionary<string, AssetObject> Objects { get; set; } = new();
}

public class AssetObject
{
    public string Hash { get; set; } = string.Empty;
    public long Size { get; set; }
}
