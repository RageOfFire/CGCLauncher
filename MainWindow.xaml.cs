using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CGCLauncher.Models;
using CGCLauncher.Services;
using CGCLauncher.Utils;

namespace CGCLauncher;

public partial class MainWindow : Window
{
    private readonly MojangApiService _api = new();
    private readonly DownloadService _downloader = new();
    private readonly GameLauncher _launcher = new();
    private readonly ServerListService _serverList = new();
    private VersionManifest? _manifest;

    // TODO: set this to your actual server, e.g. new("Crystal GemCraft", "play.crystalgemcraft.net")
    private static readonly DefaultServerEntry[] DefaultServers =
    {
        new("Crystal GemCraft", "CHANGE_ME.example.net"),
    };

    public MainWindow()
    {
        InitializeComponent();

        InstallDirTextBox.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CGCLauncher");

        _downloader.Log += msg => Dispatcher.Invoke(() => AppendLog(msg));

        Loaded += async (_, _) => await LoadVersionsAsync();
    }

    private async Task LoadVersionsAsync()
    {
        try
        {
            AppendLog("Fetching version manifest...");
            _manifest = await _api.GetVersionManifestAsync();
            RefreshVersionList();
            AppendLog($"Loaded {_manifest.Versions.Count} versions.");
        }
        catch (Exception ex)
        {
            AppendLog($"Failed to load versions: {ex.Message}");
        }
    }

    private void RefreshVersionList()
    {
        if (_manifest == null) return;

        var showSnapshots = ShowSnapshotsCheckBox.IsChecked == true;
        var list = _manifest.Versions.Where(v => showSnapshots || v.Type == "release").ToList();

        VersionComboBox.ItemsSource = list;
        VersionComboBox.SelectedIndex = list.Count > 0 ? 0 : -1;
    }

    private void ShowSnapshotsCheckBox_Changed(object sender, RoutedEventArgs e) => RefreshVersionList();

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
            return;
        }
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;

    private void MaximizeButton_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;

    private void CloseButton_Click(object sender, RoutedEventArgs e) => Close();

    private void SignInButton_Click(object sender, RoutedEventArgs e) =>
        AppendLog("Microsoft sign-in isn't wired up yet - coming in milestone 2.");

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Select install directory" };
        if (dialog.ShowDialog() == true)
            InstallDirTextBox.Text = dialog.FolderName;
    }

    private async void LaunchButton_Click(object sender, RoutedEventArgs e)
    {
        if (VersionComboBox.SelectedItem is not VersionManifestEntry selected)
        {
            AppendLog("Select a version first.");
            return;
        }

        var username = string.IsNullOrWhiteSpace(UsernameTextBox.Text) ? "Player" : UsernameTextBox.Text.Trim();
        var installDir = InstallDirTextBox.Text;

        LaunchButton.IsEnabled = false;
        try
        {
            _serverList.EnsureDefaultServers(installDir, DefaultServers);

            AppendLog($"Fetching version details for {selected.Id}...");
            var details = await _api.GetVersionDetailsAsync(selected.Url);

            AppendLog("Downloading client jar...");
            var clientJar = await _downloader.DownloadClientJarAsync(details, installDir);

            AppendLog("Downloading libraries and natives...");
            var (libs, nativesDir) = await _downloader.DownloadLibrariesAsync(details, installDir);

            AppendLog("Downloading assets (first run can take a while)...");
            await _downloader.DownloadAssetsAsync(details, installDir);

            var uuid = UuidUtils.OfflineUuid(username);
            AppendLog("Launching Minecraft. No Microsoft account is linked yet, so the game will start in Demo Mode until auth (milestone 2) is added.");

            var process = _launcher.LaunchGame(
                details, installDir, libs, clientJar, nativesDir,
                username, uuid, accessToken: "0");

            PipeOutput(process);
        }
        catch (Exception ex)
        {
            AppendLog($"Launch failed: {ex.Message}");
        }
        finally
        {
            LaunchButton.IsEnabled = true;
        }
    }

    private void PipeOutput(System.Diagnostics.Process process)
    {
        _ = Task.Run(async () =>
        {
            string? line;
            while ((line = await process.StandardOutput.ReadLineAsync()) != null)
                Dispatcher.Invoke(() => AppendLog(line));
        });
        _ = Task.Run(async () =>
        {
            string? line;
            while ((line = await process.StandardError.ReadLineAsync()) != null)
                Dispatcher.Invoke(() => AppendLog(line));
        });
    }

    private void AppendLog(string message)
    {
        LogTextBox.AppendText(message + Environment.NewLine);
        LogTextBox.ScrollToEnd();
    }
}
