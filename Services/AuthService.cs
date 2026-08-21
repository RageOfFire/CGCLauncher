using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using CGCLauncher.Models;

namespace CGCLauncher.Services;

/// <summary>
/// Microsoft → Xbox Live → XSTS → Minecraft auth chain.
///
/// Uses Microsoft's own Xbox app client ID (00000000402b5328) which is
/// already registered with Mojang — no Azure app registration required.
/// This is the same approach used by Prism Launcher, ATLauncher, GDLauncher.
/// </summary>
public class AuthService
{
    // Microsoft's Xbox application — recognised by Mojang's Minecraft auth server.
    // Players do NOT need to create an Azure app to use this.
    private const string ClientId    = "00000000402b5328";
    private const string RedirectUri = "https://login.live.com/oauth20_desktop.srf";
    private const string AuthUrl     = "https://login.live.com/oauth20_authorize.srf";
    private const string TokenUrl    = "https://login.live.com/oauth20_token.srf";

    private const string XblUrl      = "https://user.auth.xboxlive.com/user/authenticate";
    private const string XstsUrl     = "https://xsts.auth.xboxlive.com/xsts/authorize";
    private const string McAuthUrl   = "https://api.minecraftservices.com/authentication/login_with_xbox";
    private const string McProfileUrl= "https://api.minecraftservices.com/minecraft/profile";

    private static readonly HttpClient Http = new();
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive  = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private static readonly string SessionFile = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CGCLauncher", "auth", "session.json");

    // ── Silent sign-in (startup) ──────────────────────────────────────────────

    /// <summary>
    /// Tries to restore the session using a stored refresh token.
    /// Never opens a browser — returns null if no stored session or refresh fails.
    /// </summary>
    public async Task<LauncherSession?> TrySilentSignInAsync()
    {
        try
        {
            if (!File.Exists(SessionFile)) return null;

            var json    = await File.ReadAllTextAsync(SessionFile);
            var stored  = JsonSerializer.Deserialize<StoredSession>(json, JsonOpts);
            if (stored == null || string.IsNullOrEmpty(stored.RefreshToken)) return null;

            // Refresh the MS token silently
            var tokens = await RefreshTokensAsync(stored.RefreshToken);
            var session = await BuildSessionAsync(tokens);

            // Persist the new refresh token
            await SaveSessionAsync(session, tokens.RefreshToken);
            return session;
        }
        catch
        {
            return null; // Non-fatal — will fall back to interactive sign-in
        }
    }

    // ── Interactive sign-in (button click) ───────────────────────────────────

    /// <summary>
    /// Opens a browser window for Microsoft login, then chains through
    /// Xbox Live → XSTS → Minecraft → profile.
    /// </summary>
    public async Task<LauncherSession> SignInAsync(Window owner)
    {
        var loginUrl =
            $"{AuthUrl}?client_id={ClientId}" +
            $"&response_type=code" +
            $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
            $"&scope=XboxLive.signin%20offline_access" +
            $"&prompt=select_account";

        var loginWindow = new LoginWindow(loginUrl) { Owner = owner };
        var result = loginWindow.ShowDialog();

        if (result != true || string.IsNullOrEmpty(loginWindow.AuthCode))
            throw new AuthException("Việc đăng nhập đã bị hủy.");

        // Exchange the auth code for MS tokens
        var tokens  = await ExchangeCodeAsync(loginWindow.AuthCode);
        var session = await BuildSessionAsync(tokens);

        // Persist the refresh token for next startup
        await SaveSessionAsync(session, tokens.RefreshToken);
        return session;
    }

    // ── Sign out ──────────────────────────────────────────────────────────────

    public void SignOut()
    {
        if (File.Exists(SessionFile))
            File.Delete(SessionFile);
    }

    // ── MS OAuth token exchange ───────────────────────────────────────────────

    private async Task<MsaTokenResponse> ExchangeCodeAsync(string code)
    {
        var body = new Dictionary<string, string>
        {
            ["client_id"]    = ClientId,
            ["code"]         = code,
            ["grant_type"]   = "authorization_code",
            ["redirect_uri"] = RedirectUri,
            ["scope"]        = "XboxLive.signin offline_access",
        };

        return await PostFormAsync(TokenUrl, body);
    }

    private async Task<MsaTokenResponse> RefreshTokensAsync(string refreshToken)
    {
        var body = new Dictionary<string, string>
        {
            ["client_id"]     = ClientId,
            ["refresh_token"] = refreshToken,
            ["grant_type"]    = "refresh_token",
            ["scope"]         = "XboxLive.signin offline_access",
        };

        return await PostFormAsync(TokenUrl, body);
    }

    private async Task<MsaTokenResponse> PostFormAsync(string url, Dictionary<string, string> fields)
    {
        using var content  = new FormUrlEncodedContent(fields);
        var response = await Http.PostAsync(url, content);
        var json     = await response.Content.ReadAsStringAsync();
        var tokens   = JsonSerializer.Deserialize<MsaTokenResponse>(json, JsonOpts)
                       ?? throw new AuthException("Không thể phân tích phản hồi mã thông báo MS.");

        if (!string.IsNullOrEmpty(tokens.Error))
            throw new AuthException($"Lỗi mã thông báo MS: {tokens.Error} — {tokens.ErrorDescription}");

        return tokens;
    }

    // ── Auth chain ────────────────────────────────────────────────────────────

    private async Task<LauncherSession> BuildSessionAsync(MsaTokenResponse tokens)
    {
        var xbl     = await GetXboxLiveTokenAsync(tokens.AccessToken);
        var xsts    = await GetXstsTokenAsync(xbl.Token);
        var mc      = await GetMinecraftTokenAsync(xbl.DisplayClaims.UserHash, xsts.Token);
        var profile = await GetProfileAsync(mc.AccessToken);

        return new LauncherSession
        {
            Username             = profile.Name,
            Uuid                 = FormatUuid(profile.Id),
            MinecraftAccessToken = mc.AccessToken,
            ExpiresAt            = DateTimeOffset.UtcNow.AddSeconds(mc.ExpiresIn),
        };
    }

    // ── Step 2: MS → Xbox Live ────────────────────────────────────────────────

    private async Task<XboxLiveResponse> GetXboxLiveTokenAsync(string msToken)
    {
        var body = new XboxLiveRequest
        {
            Properties = new XboxLiveProperties { RpsTicket = $"d={msToken}" }
        };

        using var req = BuildJsonRequest(HttpMethod.Post, XblUrl, body);
        req.Headers.Add("x-xbl-contract-version", "0");

        var resp = await Http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
        {
            var raw = await resp.Content.ReadAsStringAsync();
            throw new AuthException($"Xác thực Xbox Live thất bại ({(int)resp.StatusCode}): {raw}");
        }

        var json = await resp.Content.ReadAsStringAsync();
        var xbl  = JsonSerializer.Deserialize<XboxLiveResponse>(json, JsonOpts)
                   ?? throw new AuthException("Không thể phân tích phản hồi từ Xbox Live.");

        if (string.IsNullOrEmpty(xbl.Token))
            throw new AuthException("Xbox Live đã trả về một mã thông báo trống.");

        return xbl;
    }

    // ── Step 3: Xbox Live → XSTS ─────────────────────────────────────────────

    private async Task<XstsResponse> GetXstsTokenAsync(string xblToken)
    {
        var body = new XstsRequest
        {
            Properties = new XstsProperties { UserTokens = new List<string> { xblToken } }
        };

        using var req = BuildJsonRequest(HttpMethod.Post, XstsUrl, body);
        req.Headers.Add("x-xbl-contract-version", "1");

        var resp = await Http.SendAsync(req);
        if (!resp.IsSuccessStatusCode)
        {
            var raw = await resp.Content.ReadAsStringAsync();
            XstsError? err = null;
            try { err = JsonSerializer.Deserialize<XstsError>(raw, JsonOpts); } catch { }

            var detail = err?.XErr switch
            {
                2148916233 => "Không có hồ sơ Xbox. Truy cập xbox.com và tạo một hồ sơ.",
                2148916235 => "Xbox Live không khả dụng trong khu vực của bạn.",
                2148916236 or 2148916237 => "Cần sự đồng ý của người lớn trong cài đặt Xbox.",
                2148916238 => "Tài khoản trẻ em — phụ huynh phải thêm vào nhóm gia đình.",
                _          => $"Lỗi XSTS {err?.XErr} ({(int)resp.StatusCode})."
            };

            throw new AuthException($"Xác thực Xbox thất bại: {detail}");
        }

        var json = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<XstsResponse>(json, JsonOpts)
               ?? throw new AuthException("Không thể phân tích phản hồi XSTS.");
    }

    // ── Step 4: XSTS → Minecraft token ───────────────────────────────────────

    private async Task<MinecraftAuthResponse> GetMinecraftTokenAsync(string userHash, string xstsToken)
    {
        var body = new MinecraftAuthRequest
        {
            IdentityToken = $"XBL3.0 x={userHash};{xstsToken}"
        };

        using var req  = BuildJsonRequest(HttpMethod.Post, McAuthUrl, body);
        var resp = await Http.SendAsync(req);

        if (!resp.IsSuccessStatusCode)
        {
            var raw = await resp.Content.ReadAsStringAsync();
            throw new AuthException($"Xác thực Minecraft thất bại ({(int)resp.StatusCode}): {raw}");
        }

        var json = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<MinecraftAuthResponse>(json, JsonOpts)
               ?? throw new AuthException("Không thể phân tích phản hồi xác thực Minecraft.");
    }

    // ── Step 5: Profile (also proves ownership) ───────────────────────────────

    private async Task<MinecraftProfile> GetProfileAsync(string mcToken)
    {
        using var req = new HttpRequestMessage(HttpMethod.Get, McProfileUrl);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", mcToken);
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        var resp = await Http.SendAsync(req);

        if (resp.StatusCode == System.Net.HttpStatusCode.NotFound)
            throw new AuthException(
                "Tài khoản này không sở hữu Minecraft Java Edition.\n" +
                "Vui lòng mua nó tại minecraft.net trước.");

        if (!resp.IsSuccessStatusCode)
        {
            var raw = await resp.Content.ReadAsStringAsync();
            throw new AuthException($"Không thể lấy thông tin hồ sơ ({(int)resp.StatusCode}): {raw}");
        }

        var json = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<MinecraftProfile>(json, JsonOpts)
               ?? throw new AuthException("Không thể phân tích thông tin hồ sơ Minecraft.");
    }

    // ── Session persistence ───────────────────────────────────────────────────

    private async Task SaveSessionAsync(LauncherSession session, string refreshToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SessionFile)!);
        var stored = new StoredSession
        {
            RefreshToken = refreshToken,
            Username     = session.Username,
            Uuid         = session.Uuid,
            SavedAt      = DateTimeOffset.UtcNow,
        };
        var json = JsonSerializer.Serialize(stored, JsonOpts);
        await File.WriteAllTextAsync(SessionFile, json);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private HttpRequestMessage BuildJsonRequest(HttpMethod method, string url, object body)
    {
        var json    = JsonSerializer.Serialize(body, JsonOpts);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var req     = new HttpRequestMessage(method, url) { Content = content };
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        return req;
    }

    private static string FormatUuid(string raw)
    {
        if (raw.Length != 32) return raw;
        return $"{raw[..8]}-{raw[8..12]}-{raw[12..16]}-{raw[16..20]}-{raw[20..]}";
    }
}

internal class XstsError
{
    [JsonPropertyName("XErr")]
    public long XErr { get; set; }
    [JsonPropertyName("Message")]
    public string? Message { get; set; }
}

public class AuthException : Exception
{
    public AuthException(string message) : base(message) { }
}
