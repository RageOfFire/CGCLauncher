using System.Windows;
using System.Windows.Navigation;

namespace CGCLauncher.Services;

public partial class LoginWindow : Window
{
    private const string RedirectUri = "https://login.live.com/oauth20_desktop.srf";

    /// <summary>The auth code extracted from the redirect URL. Null if cancelled.</summary>
    public string? AuthCode { get; private set; }

    public LoginWindow(string startUrl)
    {
        InitializeComponent();
        // Suppress IE script error dialogs that can appear on some machines
        Browser.LoadCompleted += (_, _) => SuppressScriptErrors(Browser);
        Browser.Navigate(startUrl);
    }

    private void Browser_Navigating(object sender, NavigatingCancelEventArgs e)
    {
        var url = e.Uri?.ToString() ?? string.Empty;

        StatusText.Text = url.Length > 60 ? url[..60] + "…" : url;

        if (!url.StartsWith(RedirectUri, StringComparison.OrdinalIgnoreCase))
            return;

        // The auth code arrives as a query parameter in the redirect URL
        var uri = new Uri(url);
        var query = uri.Query.TrimStart('?');

        foreach (var part in query.Split('&'))
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2 && kv[0] == "code")
            {
                AuthCode = Uri.UnescapeDataString(kv[1]);
                e.Cancel = true;
                DialogResult = true;
                Close();
                return;
            }

            // Microsoft sends "error" in the redirect when the user cancels
            if (kv.Length == 2 && kv[0] == "error")
            {
                e.Cancel = true;
                DialogResult = false;
                Close();
                return;
            }
        }
    }

    /// <summary>Prevents the IE-based WebBrowser from showing script error pop-ups.</summary>
    private static void SuppressScriptErrors(System.Windows.Controls.WebBrowser wb)
    {
        var field = typeof(System.Windows.Controls.WebBrowser)
            .GetField("_axIWebBrowser2",
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);

        var activeX = field?.GetValue(wb);
        activeX?.GetType().InvokeMember("Silent",
            System.Reflection.BindingFlags.SetProperty, null, activeX,
            new object[] { true });
    }
}
