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
    private readonly AuthService       _auth       = new();
    private readonly SettingsService   _settings   = new();

    private VersionManifest?     _manifest;
    private LauncherSession?     _session;
    private LauncherSettings     _savedSettings = new();

    private static readonly DefaultServerEntry[] DefaultServers =
    {
        new("CrystalGemCraft", "mc.crystalgemcraft.com"),
    };

    public MainWindow()
    {
        InitializeComponent();

        _downloader.Log += msg => Dispatcher.Invoke(() => AppendLog(msg));

        Loaded += async (_, _) =>
        {
            LoadSettings();
            await LoadVersionsAsync();
            await TrySilentSignInAsync();
        };
    }

    // ── Settings ──────────────────────────────────────────────────────────────

    private void LoadSettings()
    {
        _savedSettings = _settings.Load();

        InstallDirTextBox.Text = !string.IsNullOrEmpty(_savedSettings.InstallDir)
            ? _savedSettings.InstallDir
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "CGCLauncher");

        ShowSnapshotsCheckBox.IsChecked  = _savedSettings.ShowSnapshots;
        HideLauncherCheckBox.IsChecked   = _savedSettings.HideLauncherOnLaunch;

        if (!string.IsNullOrEmpty(_savedSettings.OfflineUsername))
            OfflineUsernameBox.Text = _savedSettings.OfflineUsername;
    }

    private void SaveSettings(string? lastVersionId = null)
    {
        _savedSettings.InstallDir           = InstallDirTextBox.Text;
        _savedSettings.ShowSnapshots        = ShowSnapshotsCheckBox.IsChecked == true;
        _savedSettings.HideLauncherOnLaunch = HideLauncherCheckBox.IsChecked  == true;
        _savedSettings.OfflineUsername      = OfflineUsernameBox.Text.Trim();

        if (lastVersionId != null)
            _savedSettings.LastVersionId = lastVersionId;

        _settings.Save(_savedSettings);
    }

    // ── Version loading ───────────────────────────────────────────────────────

    private async Task LoadVersionsAsync()
    {
        try
        {
            AppendLog("Đang tải danh sách phiên bản...");
            _manifest = await _api.GetVersionManifestAsync();
            AppendLog($"Đã tải {_manifest.Versions.Count} phiên bản.");
            RefreshVersionList();

            if (!string.IsNullOrEmpty(_savedSettings.LastVersionId))
                RestoreLastVersion(_savedSettings.LastVersionId);
        }
        catch (Exception ex)
        {
            AppendLog($"Không thể tải danh sách phiên bản: {ex.Message}");
        }
    }

    private void RefreshVersionList()
    {
        if (_manifest == null) return;

        var showSnapshots = ShowSnapshotsCheckBox.IsChecked == true;
        var list = _manifest.Versions
            .Where(v => v.Type == "release" || (showSnapshots && v.Type == "snapshot"))
            .ToList();

        VersionComboBox.ItemsSource   = list;
        VersionComboBox.SelectedIndex = list.Count > 0 ? 0 : -1;
    }

    private void RestoreLastVersion(string versionId)
    {
        if (VersionComboBox.ItemsSource is not List<VersionManifestEntry> list) return;

        var match = list.FirstOrDefault(v =>
            v.Id.Equals(versionId, StringComparison.OrdinalIgnoreCase));

        if (match != null)
            VersionComboBox.SelectedItem = match;
    }

    private void FilterCheckBox_Changed(object sender, RoutedEventArgs e)
    {
        RefreshVersionList();
        SaveSettings();
    }

    private void HideLauncherCheckBox_Changed(object sender, RoutedEventArgs e) => SaveSettings();

    // ── Auth ──────────────────────────────────────────────────────────────────

    private async Task TrySilentSignInAsync()
    {
        _session = await _auth.TrySilentSignInAsync();
        if (_session != null)
        {
            SignInButton.Content            = _session.Username;
            AuthStatusText.Text             = $"Đã đăng nhập với {_session.Username}";
            OfflineUsernamePanel.Visibility = Visibility.Collapsed;
            AppendLog($"Chào mừng trở lại, {_session.Username}!");
        }
    }

    private async void SignInButton_Click(object sender, RoutedEventArgs e)
    {
        if (_session != null)
        {
            var result = MessageBox.Show(
                $"Đã đăng nhập với {_session.Username}.\n\nĐăng xuất?",
                "Tài khoản", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                _auth.SignOut();
                _session = null;
                SignInButton.Content            = "Đăng nhập bằng Microsoft";
                AuthStatusText.Text             = "Chế độ ngoại tuyến";
                OfflineUsernamePanel.Visibility = Visibility.Visible;
                AppendLog("Đã đăng xuất.");
            }
            return;
        }

        SignInButton.IsEnabled = false;
        SignInButton.Content   = "Đang đăng nhập...";
        AppendLog("Đang mở trang đăng nhập Microsoft...");

        try
        {
            _session = await _auth.SignInAsync(this);
            SignInButton.Content            = _session.Username;
            AuthStatusText.Text             = $"Đã đăng nhập với {_session.Username}";
            OfflineUsernamePanel.Visibility = Visibility.Collapsed;
            AppendLog($"Đã đăng nhập với {_session.Username}");
        }
        catch (AuthException ex)
        {
            AppendLog($"Đăng nhập thất bại: {ex.Message}");
            SignInButton.Content = "Đăng nhập bằng Microsoft";
            MessageBox.Show(ex.Message, "Đăng nhập thất bại",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        catch (Exception ex)
        {
            AppendLog($"Lỗi đăng nhập: {ex.Message}");
            SignInButton.Content = "Đăng nhập bằng Microsoft";
        }
        finally
        {
            SignInButton.IsEnabled = true;
        }
    }

    // ── Launch ────────────────────────────────────────────────────────────────

    private async void LaunchButton_Click(object sender, RoutedEventArgs e)
    {
        if (VersionComboBox.SelectedItem is not VersionManifestEntry selected)
        {
            AppendLog("Hãy chọn phiên bản trước.");
            return;
        }

        var installDir  = InstallDirTextBox.Text;
        var username    = _session?.Username ?? OfflineUsernameBox.Text.Trim().Replace(" ", "_");
        var uuid        = _session?.Uuid     ?? UuidUtils.OfflineUuid(username);
        var accessToken = _session?.MinecraftAccessToken ?? "0";

        if (string.IsNullOrWhiteSpace(username)) username = "Steve";

        if (_session == null)
            AppendLog($"Không có tài khoản — khởi chạy với tên \"{username}\" (chế độ ngoại tuyến).");

        LaunchButton.IsEnabled = false;

        try
        {
            _serverList.EnsureDefaultServers(installDir, DefaultServers);

            AppendLog($"Đang tải chi tiết phiên bản {selected.Id}...");
            var details = await _api.GetVersionDetailsAsync(selected.Url);

            AppendLog("Đang tải client jar...");
            var clientJar = await _downloader.DownloadClientJarAsync(details, installDir);

            AppendLog("Đang tải thư viện và native...");
            var (libs, nativesDir) = await _downloader.DownloadLibrariesAsync(details, installDir);

            AppendLog("Đang tải tài nguyên (lần đầu có thể mất vài phút)...");
            await _downloader.DownloadAssetsAsync(details, installDir);

            AppendLog($"Đang khởi chạy {selected.Id} với tên {username}...");
            var process = _launcher.LaunchGame(
                details, installDir, libs, clientJar, nativesDir,
                username, uuid, accessToken);

            SaveSettings(selected.Id);
            PipeOutput(process);
        }
        catch (Exception ex)
        {
            AppendLog($"Khởi chạy thất bại: {ex.Message}");
        }
        finally
        {
            LaunchButton.IsEnabled = true;
        }
    }

    private void PipeOutput(System.Diagnostics.Process process)
    {
        bool shouldHide = HideLauncherCheckBox.IsChecked == true;
        if (shouldHide)
            _ = Task.Delay(2000).ContinueWith(_ => Dispatcher.Invoke(Hide));

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

        _ = Task.Run(async () =>
        {
            await process.WaitForExitAsync();
            Dispatcher.Invoke(() =>
            {
                if (shouldHide)
                {
                    Show();
                    WindowState = WindowState.Normal;
                    Activate();
                }
                AppendLog("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
                AppendLog("Minecraft đã đóng.");
            });
        });
    }

    // ── Title bar ─────────────────────────────────────────────────────────────

    private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal : WindowState.Maximized;
            return;
        }
        if (e.ButtonState == MouseButtonState.Pressed) DragMove();
    }

    private void MinimizeButton_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState.Minimized;

    private void MaximizeButton_Click(object sender, RoutedEventArgs e) =>
        WindowState = WindowState == WindowState.Maximized
            ? WindowState.Normal : WindowState.Maximized;

    private void CloseButton_Click(object sender, RoutedEventArgs e)
    {
        SaveSettings();
        Close();
    }

    // ── Browse ────────────────────────────────────────────────────────────────

    private void BrowseButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
            { Title = "Chọn thư mục cài đặt" };
        if (dialog.ShowDialog() == true)
        {
            InstallDirTextBox.Text = dialog.FolderName;
            SaveSettings();
        }
    }

    // ── Log ───────────────────────────────────────────────────────────────────

    private void AppendLog(string message)
    {
        LogTextBox.AppendText(message + Environment.NewLine);
        LogTextBox.ScrollToEnd();
    }
}
