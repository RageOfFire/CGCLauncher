using System.IO;
using System.Text.Json;
using CGCLauncher.Models;

namespace CGCLauncher.Services;

public class SettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "CGCLauncher", "settings.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
    };

    public LauncherSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath)) return new LauncherSettings();
            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<LauncherSettings>(json, JsonOpts)
                   ?? new LauncherSettings();
        }
        catch
        {
            return new LauncherSettings();
        }
    }

    public void Save(LauncherSettings settings)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
            var json = JsonSerializer.Serialize(settings, JsonOpts);
            File.WriteAllText(SettingsPath, json);
        }
        catch
        {
            // Non-fatal — settings just won't persist this session
        }
    }
}
