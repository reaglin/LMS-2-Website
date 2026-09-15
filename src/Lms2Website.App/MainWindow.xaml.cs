using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Navigation;
using Lms2Website.App.ViewModels;
using Lms2Website.App.Views;

namespace Lms2Website.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _model = new();

    public MainWindow()
    {
        InitializeComponent();
        DataContext = _model;
    }

    /// <summary>Reads a cartridge handed to the app on the command line or dropped on its icon.</summary>
    public Task OpenAsync(string cartridgePath) => _model.ReadAsync(cartridgePath);

    private void OnSettings(object sender, RoutedEventArgs e)
    {
        new TokenWindow { Owner = this }.ShowDialog();
        _model.RefreshTokenStatus();
    }

    private void OnHelp(object sender, RoutedEventArgs e) =>
        new HelpWindow { Owner = this }.ShowDialog();

    private void OnNavigate(object sender, RequestNavigateEventArgs e)
    {
        if (sender is Hyperlink link && link.NavigateUri != null)
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(link.NavigateUri.ToString())
            {
                UseShellExecute = true
            });
        }
        e.Handled = true;
    }

    // ── dropping a cartridge on the window ────────────────────────────────────

    private void OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = Cartridge(e) == null ? DragDropEffects.None : DragDropEffects.Copy;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        var path = Cartridge(e);
        if (path != null) _ = _model.ReadAsync(path);
        e.Handled = true;
    }

    private static string? Cartridge(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return null;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return null;
        return paths.FirstOrDefault(p =>
            Path.GetExtension(p).Equals(".imscc", StringComparison.OrdinalIgnoreCase) ||
            Path.GetExtension(p).Equals(".zip", StringComparison.OrdinalIgnoreCase));
    }
}
