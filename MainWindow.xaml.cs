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
    private readonly MojangApiService  _api        = new();
    private readonly DownloadService   _downloader = new();
    private readonly GameLauncher      _launcher   = new();
    private readonly ServerListService _serverList = new();
    private readonly OptiFineService   _optiFine   = new();
    private readonly AuthService       _auth       = new();

    private VersionManifest?      _manifest;
    private List<OptiFineVersion> _optiFineVersions = new();
    private LauncherSession?      _session;  // null = not signed in

    // TODO: replace with your real server address
    private static readonly DefaultServerEntry[] DefaultServers =
    {
        new("CrystalGemCraft", "mc.crystalgemcraft.com"),
    };

    public MainWindow()
    {
        InitializeComponent();

        InstallDirTextBox.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "CGCLauncher");

        _downloader.Log += msg => Dispatcher.Invoke(() => AppendLog(msg));

        Loaded += async (_, _) =>
        {
            await LoadAllVersionsAsync();
            await TrySilentSignInAsync();
        };
    }

    // ── Version loading ──────────────────────────────────────────────────────

    private async Task LoadAllVersionsAsync()
    {
        // Load vanilla manifest and OptiFine list in parallel
        AppendLog("Fetching version manifest...");

        var vanillaTask   = _api.GetVersionManifestAsync();
        var optifineTask  = TryLoadOptiFineAsync();

        await Task.WhenAll(vanillaTask, optifineTask);

        _manifest = vanillaTask.Result;
        AppendLog($"Loaded {_manifest.Versions.Count} vanilla versions, " +
                  $"{_optiFineVersions.Count} OptiFine versions.");

        RefreshVersionList();
    }

    private async Task TrySilentSignInAsync()
    {
        // Only reads from the token cache — never opens a browser
        _session = await _auth.TrySilentSignInAsync();
        if (_session != null)
        {
            SignInButton.Content      = _session.Username;
            AuthStatusText.Text       = $"Signed in as {_session.Username}";
            OfflineUsernamePanel.Visibility = Visibility.Collapsed;
            AppendLog($"Welcome back, {_session.Username}!");
        }
    }

    private async Task TryLoadOptiFineAsync()
    {
        try
        {
            _optiFineVersions = await _optiFine.GetVersionsAsync();
        }
        catch (Exception ex)
        {
            // Non-fatal — OptiFine site may be unreachable
            Dispatcher.Invoke(() => AppendLog($"Could not fetch OptiFine list: {ex.Message}"));
            _optiFineVersions = new List<OptiFineVersion>();
        }
    }

    private void RefreshVersionList()
    {
        if (_manifest == null) return;

        var showSnapshots = ShowSnapshotsCheckBox.IsChecked == true;
        var showOptiFine  = ShowOptiFineCheckBox.IsChecked  == true;

        var list = new List<LauncherVersion>();

        // Vanilla entries
        foreach (var v in _manifest.Versions)
        {
            if (v.Type == "release" || (showSnapshots && v.Type == "snapshot"))
                list.Add(LauncherVersion.FromVanilla(v));
        }

        // OptiFine entries appended below vanilla (only when checkbox is ticked)
        if (showOptiFine)
        {
            foreach (var of in _optiFineVersions)
                list.Add(LauncherVersion.FromOptiFine(of));
        }

        VersionComboBox.ItemsSource = list;
        VersionComboBox.SelectedIndex = list.Count > 0 ? 0 : -1;
    }

    private void FilterCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        // If OptiFine just got ticked and we have no versions yet, try fetching again
        if (sender == ShowOptiFineCheckBox
            && ShowOptiFineCheckBox.IsChecked == true
            && _optiFineVersions.Count == 0)
        {
            _ = Task.Run(async () =>
            {
                await TryLoadOptiFineAsync();
                Dispatcher.Invoke(RefreshVersionList);
            });
            return;
        }

        RefreshVersionList();
    }

    // ── Launch ───────────────────────────────────────────────────────────────

    private async void LaunchButton_Click(object sender, RoutedEventArgs e)
    {
        if (VersionComboBox.SelectedItem is not LauncherVersion selected)
        {
            AppendLog("Select a version first.");
            return;
        }

        if (selected.Kind == VersionKind.OptiFine)
        {
            AppendLog("OptiFine launching isn't supported yet — select a vanilla version for now.");
            AppendLog($"You can download OptiFine manually from: {selected.OptiFine!.DownloadUrl}");
            return;
        }

        var installDir  = InstallDirTextBox.Text;
        var username    = _session?.Username    ?? OfflineUsernameBox.Text.Trim().Replace(" ", "_");
        var uuid        = _session?.Uuid        ?? UuidUtils.OfflineUuid(username);
        var accessToken = _session?.MinecraftAccessToken ?? "0";

        if (string.IsNullOrWhiteSpace(username)) username = "Player";

        if (_session == null)
            AppendLog($"No Microsoft account — launching as \"{username}\" in Demo Mode.");

        LaunchButton.IsEnabled = false;
        try
        {
            _serverList.EnsureDefaultServers(installDir, DefaultServers);

            var manifestEntry = selected.ManifestEntry!;
            AppendLog($"Fetching version details for {manifestEntry.Id}...");
            var details = await _api.GetVersionDetailsAsync(manifestEntry.Url);

            AppendLog("Downloading client jar...");
            var clientJar = await _downloader.DownloadClientJarAsync(details, installDir);

            AppendLog("Downloading libraries and natives...");
            var (libs, nativesDir) = await _downloader.DownloadLibrariesAsync(details, installDir);

            AppendLog("Downloading assets (first run can take a while)...");
            await _downloader.DownloadAssetsAsync(details, installDir);

            AppendLog($"Launching as {username}...");
            var process = _launcher.LaunchGame(
                details, installDir, libs, clientJar, nativesDir,
                username, uuid, accessToken);

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

    // ── Auth ─────────────────────────────────────────────────────────────────

    private async void SignInButton_Click(object sender, RoutedEventArgs e)
    {
        if (_session != null)
        {
            var result = MessageBox.Show(
                $"Signed in as {_session.Username}.\n\nSign out?",
                "Account", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _auth.SignOut();
                _session = null;
                SignInButton.Content             = "Sign in with Microsoft";
                AuthStatusText.Text              = "Offline mode — sign in to unlock full game";
                OfflineUsernamePanel.Visibility  = Visibility.Visible;
                AppendLog("Signed out.");
            }
            return;
        }

        SignInButton.IsEnabled = false;
        SignInButton.Content   = "Signing in...";
        AppendLog("Opening Microsoft sign-in...");

        try
        {
            _session = await _auth.SignInAsync(this);

            SignInButton.Content             = _session.Username;
            AuthStatusText.Text              = $"Signed in as {_session.Username}";
            OfflineUsernamePanel.Visibility  = Visibility.Collapsed;
            AppendLog($"Signed in as {_session.Username} ({_session.Uuid})");
        }
        catch (AuthException ex)
        {
            AppendLog($"Sign-in failed: {ex.Message}");
            SignInButton.Content = "Sign in with Microsoft";
            MessageBox.Show(ex.Message, "Sign-in failed",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            AppendLog($"Unexpected sign-in error: {ex.Message}");
            SignInButton.Content = "Sign in with Microsoft";
        }
        finally
        {
            SignInButton.IsEnabled = true;
        }
    }

    // ── Title bar ────────────────────────────────────────────────────────────

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

    // ── Browse ───────────────────────────────────────────────────────────────

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Select install directory" };
        if (dialog.ShowDialog() == true)
            InstallDirTextBox.Text = dialog.FolderName;
    }

    // ── Log ──────────────────────────────────────────────────────────────────

    private void AppendLog(string message)
    {
        LogTextBox.AppendText(message + Environment.NewLine);
        LogTextBox.ScrollToEnd();
    }
}
