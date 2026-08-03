using System.Text.Json.Serialization;

namespace CGCLauncher.Models;

/// <summary>Response from login.live.com token endpoint.</summary>
public class MsaTokenResponse
{
    [JsonPropertyName("access_token")]
    public string AccessToken { get; set; } = string.Empty;

    [JsonPropertyName("refresh_token")]
    public string RefreshToken { get; set; } = string.Empty;

    [JsonPropertyName("expires_in")]
    public int ExpiresIn { get; set; }

    [JsonPropertyName("error")]
    public string? Error { get; set; }

    [JsonPropertyName("error_description")]
    public string? ErrorDescription { get; set; }
}

/// <summary>Persisted to disk so the player stays signed in between sessions.</summary>
public class StoredSession
{
    public string RefreshToken { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string Uuid { get; set; } = string.Empty;
    public DateTimeOffset SavedAt { get; set; }
}
