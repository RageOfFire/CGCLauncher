using System.Text.Json.Serialization;

namespace CGCLauncher.Models;

// ── Xbox Live request (PascalCase keys — Xbox API is case-sensitive) ──────

public class XboxLiveRequest
{
    [JsonPropertyName("Properties")]
    public XboxLiveProperties Properties { get; set; } = new();
    [JsonPropertyName("RelyingParty")]
    public string RelyingParty { get; set; } = "http://auth.xboxlive.com";
    [JsonPropertyName("TokenType")]
    public string TokenType { get; set; } = "JWT";
}

public class XboxLiveProperties
{
    [JsonPropertyName("AuthMethod")]
    public string AuthMethod { get; set; } = "RPS";
    [JsonPropertyName("SiteName")]
    public string SiteName { get; set; } = "user.auth.xboxlive.com";
    [JsonPropertyName("RpsTicket")]
    public string RpsTicket { get; set; } = string.Empty; // "d=<MS access token>"
}

// ── Xbox Live response (PascalCase keys) ─────────────────────────────────

public class XboxLiveResponse
{
    [JsonPropertyName("Token")]
    public string Token { get; set; } = string.Empty;
    [JsonPropertyName("DisplayClaims")]
    public XboxDisplayClaims DisplayClaims { get; set; } = new();
}

public class XboxDisplayClaims
{
    // Xbox returns lowercase "xui" containing array of { "uhs": "..." }
    [JsonPropertyName("xui")]
    public List<Dictionary<string, string>> Xui { get; set; } = new();

    public string UserHash => Xui.Count > 0 && Xui[0].TryGetValue("uhs", out var uhs)
        ? uhs : string.Empty;
}

// ── XSTS request (PascalCase keys) ───────────────────────────────────────

public class XstsRequest
{
    [JsonPropertyName("Properties")]
    public XstsProperties Properties { get; set; } = new();
    [JsonPropertyName("RelyingParty")]
    public string RelyingParty { get; set; } = "rp://api.minecraftservices.com/";
    [JsonPropertyName("TokenType")]
    public string TokenType { get; set; } = "JWT";
}

public class XstsProperties
{
    [JsonPropertyName("SandboxId")]
    public string SandboxId { get; set; } = "RETAIL";
    [JsonPropertyName("UserTokens")]
    public List<string> UserTokens { get; set; } = new();
}

public class XstsResponse
{
    [JsonPropertyName("Token")]
    public string Token { get; set; } = string.Empty;
    [JsonPropertyName("DisplayClaims")]
    public XboxDisplayClaims DisplayClaims { get; set; } = new();
}

// ── Minecraft auth request (camelCase) ───────────────────────────────────

public class MinecraftAuthRequest
{
    // Minecraft services API expects camelCase here
    [JsonPropertyName("identityToken")]
    public string IdentityToken { get; set; } = string.Empty;
}

// ── Minecraft auth response (snake_case) ─────────────────────────────────

public class MinecraftAuthResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;
    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }
}

// ── Minecraft profile (lowercase) ────────────────────────────────────────

public class MinecraftProfile
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;   // UUID without dashes
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty; // player username
}

// ── Entitlement check (lowercase) ────────────────────────────────────────

public class MinecraftEntitlements
{
    [JsonPropertyName("items")]
    public List<MinecraftItem> Items { get; set; } = new();

    public bool OwnsMinecraft => Items.Any(i =>
        i.Name == "product_minecraft" || i.Name == "game_minecraft");
}

public class MinecraftItem
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}

// ── Cached session ────────────────────────────────────────────────────────

public class LauncherSession
{
    public string Username { get; set; } = string.Empty;
    public string Uuid { get; set; } = string.Empty;
    public string MinecraftAccessToken { get; set; } = string.Empty;
    public DateTimeOffset ExpiresAt { get; set; }
    public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAt;
}
