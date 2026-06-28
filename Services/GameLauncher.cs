using System.Diagnostics;
using System.IO;
using System.Text.Json;
using CGCLauncher.Models;
using CGCLauncher.Utils;

namespace CGCLauncher.Services;

public class GameLauncher
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    /// <param name="accessToken">
    /// Pass-through credential for the session. With no real Microsoft auth wired up yet
    /// (that's milestone 2), the vanilla client itself will fall back to Demo Mode -
    /// this code never tries to fake or bypass that check.
    /// </param>
    public Process LaunchGame(
        VersionDetails details,
        string installDir,
        List<string> libraryJars,
        string clientJarPath,
        string nativesDir,
        string username,
        string uuid,
        string accessToken,
        string javaPath = "java")
    {
        var classpath = string.Join(Path.PathSeparator, libraryJars.Append(clientJarPath));

        var placeholders = new Dictionary<string, string>
        {
            ["auth_player_name"] = username,
            ["version_name"] = details.Id,
            ["game_directory"] = installDir,
            ["assets_root"] = Path.Combine(installDir, "assets"),
            ["assets_index_name"] = details.Assets,
            ["auth_uuid"] = uuid,
            ["auth_access_token"] = accessToken,
            ["user_type"] = "msa",
            ["version_type"] = details.Type,
            ["natives_directory"] = nativesDir,
            ["launcher_name"] = "CGCLauncher",
            ["launcher_version"] = "1.0.0",
            ["classpath"] = classpath,
        };

        var args = new List<string>();

        if (details.Arguments?.Jvm is { Count: > 0 } jvmArgs)
            args.AddRange(ResolveArgumentElements(jvmArgs, placeholders));
        else
            args.Add($"-Djava.library.path={nativesDir}");

        args.Add("-cp");
        args.Add(classpath);
        args.Add(details.MainClass);

        if (details.Arguments?.Game is { Count: > 0 } gameArgs)
        {
            args.AddRange(ResolveArgumentElements(gameArgs, placeholders));
        }
        else if (!string.IsNullOrEmpty(details.MinecraftArguments))
        {
            var legacy = details.MinecraftArguments;
            foreach (var (key, value) in placeholders)
                legacy = legacy.Replace($"${{{key}}}", value);
            args.AddRange(legacy.Split(' ', StringSplitOptions.RemoveEmptyEntries));
        }

        var psi = new ProcessStartInfo
        {
            FileName = javaPath,
            WorkingDirectory = installDir,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var a in args) psi.ArgumentList.Add(a);

        Directory.CreateDirectory(installDir);
        return Process.Start(psi) ?? throw new InvalidOperationException("Failed to start the Minecraft process.");
    }

    /// <summary>Resolves Mojang's mixed string/conditional-object argument arrays into a flat string list.</summary>
    private List<string> ResolveArgumentElements(List<JsonElement> elements, Dictionary<string, string> placeholders)
    {
        var result = new List<string>();

        foreach (var el in elements)
        {
            if (el.ValueKind == JsonValueKind.String)
            {
                result.Add(Substitute(el.GetString()!, placeholders));
                continue;
            }

            if (el.ValueKind != JsonValueKind.Object) continue;

            if (el.TryGetProperty("rules", out var rulesEl))
            {
                var rules = JsonSerializer.Deserialize<List<Rule>>(rulesEl.GetRawText(), JsonOpts);
                if (!OsUtils.EvaluateRules(rules)) continue;
            }

            if (!el.TryGetProperty("value", out var valueEl)) continue;

            if (valueEl.ValueKind == JsonValueKind.String)
            {
                result.Add(Substitute(valueEl.GetString()!, placeholders));
            }
            else if (valueEl.ValueKind == JsonValueKind.Array)
            {
                foreach (var v in valueEl.EnumerateArray())
                    result.Add(Substitute(v.GetString() ?? string.Empty, placeholders));
            }
        }

        return result;
    }

    private string Substitute(string template, Dictionary<string, string> placeholders)
    {
        foreach (var (key, value) in placeholders)
            template = template.Replace($"${{{key}}}", value);
        return template;
    }
}
