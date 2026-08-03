using System.Net.Http;
using System.Text.RegularExpressions;
using CGCLauncher.Models;

namespace CGCLauncher.Services;

/// <summary>
/// Scrapes optifine.net/downloads to build a list of available OptiFine versions.
/// OptiFine has no public API, so we parse jar filenames out of the download links.
/// The page contains hrefs like:  adloadx?f=OptiFine_1.20.4_HD_U_I7.jar
/// </summary>
public class OptiFineService
{
    private static readonly HttpClient Http = new();
    private const string DownloadsUrl = "https://optifine.net/downloads";

    // Matches filenames like: OptiFine_1.20.4_HD_U_I7.jar
    private static readonly Regex FilePattern = new(
        @"OptiFine_(?<mc>[\d.]+(?:_\w+)?)_(?<id>HD_U_\w+)\.jar",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private List<OptiFineVersion>? _cache;

    public async Task<List<OptiFineVersion>> GetVersionsAsync(bool forceRefresh = false)
    {
        if (_cache != null && !forceRefresh) return _cache;

        var html = await Http.GetStringAsync(DownloadsUrl);
        var versions = new List<OptiFineVersion>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (Match m in FilePattern.Matches(html))
        {
            var fileName = m.Value;
            if (!seen.Add(fileName)) continue;

            versions.Add(new OptiFineVersion
            {
                FileName = fileName,
                MinecraftVersion = m.Groups["mc"].Value,
                OptiFineId = m.Groups["id"].Value,
            });
        }

        // Newest first: sort by MC version desc, then OptiFine ID desc
        versions.Sort((a, b) =>
        {
            int mc = CompareVersionStrings(b.MinecraftVersion, a.MinecraftVersion);
            return mc != 0 ? mc : string.Compare(b.OptiFineId, a.OptiFineId, StringComparison.OrdinalIgnoreCase);
        });

        _cache = versions;
        return versions;
    }

    private static int CompareVersionStrings(string a, string b)
    {
        // Split on dots/underscores and compare each numeric segment
        var aParts = SplitVersion(a);
        var bParts = SplitVersion(b);
        for (int i = 0; i < Math.Max(aParts.Length, bParts.Length); i++)
        {
            var av = i < aParts.Length ? aParts[i] : 0;
            var bv = i < bParts.Length ? bParts[i] : 0;
            if (av != bv) return av.CompareTo(bv);
        }
        return 0;
    }

    private static int[] SplitVersion(string v) =>
        v.Split(new[] { '.', '_' }, StringSplitOptions.RemoveEmptyEntries)
         .Select(p => int.TryParse(p, out var n) ? n : 0)
         .ToArray();
}
