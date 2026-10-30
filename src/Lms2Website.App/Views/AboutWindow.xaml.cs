using System.Diagnostics;
using System.Reflection;
using System.Windows;
using System.Windows.Navigation;

namespace Lms2Website.App.Views;

/// <summary>
/// Who made the program, what it is for, and which version this is. The version is read from the
/// assembly (set once, by <c>Lms2WebsiteVersion</c> in Directory.Build.props), never typed here.
/// </summary>
public partial class AboutWindow : Window
{
    public AboutWindow()
    {
        InitializeComponent();
        VersionText.Text = "Version " + AppVersion;
    }

    /// <summary>Major.minor.build of this assembly, e.g. 0.1.0.</summary>
    public static string AppVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "unknown";

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    /// <summary>Opens a link in the browser; if there is no browser, the address stays on screen to copy.</summary>
    private void OnNavigate(object sender, RequestNavigateEventArgs e)
    {
        try { Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true }); }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { }
        e.Handled = true;
    }
}
