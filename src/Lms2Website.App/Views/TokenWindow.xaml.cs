using System.Net.Http;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Navigation;
using Lms2Website.Core.Publish;

namespace Lms2Website.App.Views;

public partial class TokenWindow : Window
{
    public TokenWindow()
    {
        InitializeComponent();
        var token = TokenStore.Load();
        Status.Text = token == null
            ? "No token is saved on this machine."
            : $"A token is saved ({TokenStore.Mask(token)}). Paste a new one to replace it.";
    }

    private async void OnSave(object sender, RoutedEventArgs e)
    {
        var token = TokenBox.Password.Trim();
        if (token.Length == 0)
        {
            Status.Text = "Paste a token first.";
            return;
        }

        IsEnabled = false;
        Status.Text = "Asking GitHub who this token belongs to…";
        try
        {
            using var api = new GitHubApi(token);
            var user = await api.GetUserAsync();
            TokenStore.Save(token);
            Status.Text = $"Saved. GitHub knows this token as {user.Login}" +
                          (string.IsNullOrWhiteSpace(user.Name) ? "." : $" ({user.Name}).");
            TokenBox.Clear();
        }
        catch (GitHubException ex)
        {
            Status.Text = ex.Message;
        }
        catch (HttpRequestException ex)
        {
            Status.Text = "GitHub could not be reached: " + ex.Message;
        }
        finally
        {
            IsEnabled = true;
        }
    }

    private void OnRemove(object sender, RoutedEventArgs e)
    {
        TokenStore.Clear();
        TokenBox.Clear();
        Status.Text = "The saved token has been deleted from this machine.";
    }

    private void OnClose(object sender, RoutedEventArgs e) => Close();

    private void OnNavigate(object sender, RequestNavigateEventArgs e)
    {
        if (sender is Hyperlink link && link.NavigateUri != null)
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(link.NavigateUri.ToString())
            {
                UseShellExecute = true
            });
        e.Handled = true;
    }
}
