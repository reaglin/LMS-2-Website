using System.Diagnostics;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Navigation;

namespace Lms2Website.App.Views;

/// <summary>
/// Publishing explained for somebody who has never used GitHub — which is most of the people this
/// app is for. It assumes nothing: what a repository is, what Pages is, what the address will be,
/// why the repository has to be public, and what "private" really does.
/// </summary>
public partial class HelpWindow : Window
{
    public HelpWindow() => InitializeComponent();

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnNavigate(object sender, RequestNavigateEventArgs e)
    {
        if (sender is Hyperlink link && link.NavigateUri != null)
            Process.Start(new ProcessStartInfo(link.NavigateUri.ToString()) { UseShellExecute = true });
        e.Handled = true;
    }
}
