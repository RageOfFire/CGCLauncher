using System.Security.Cryptography;
using System.Text;

namespace CGCLauncher.Utils;

public static class UuidUtils
{
    /// <summary>
    /// Generates a deterministic "offline player" UUID from a username, the same way
    /// the vanilla client itself does when no online session is present. This does NOT
    /// grant online/multiplayer access on servers that verify accounts - it only gives
    /// the local client a stable identity for offline/demo/LAN play.
    /// </summary>
    public static string OfflineUuid(string username)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes("OfflinePlayer:" + username));
        hash[6] = (byte)((hash[6] & 0x0F) | 0x30); // version 3
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80); // variant

        var hex = Convert.ToHexString(hash).ToLowerInvariant();
        return $"{hex[..8]}-{hex[8..12]}-{hex[12..16]}-{hex[16..20]}-{hex[20..32]}";
    }
}
