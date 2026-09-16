using System.Windows;

namespace Lms2Website.App.Views;

/// <summary>
/// What an .imscc is and how to get one out of the LMS, for somebody who has been handed this app
/// and has never heard of Common Cartridge. It says plainly where the app has and has not been
/// tested, because a confident instruction that does not match the screen is worse than a hedge.
/// </summary>
public partial class CartridgeWindow : Window
{
    public CartridgeWindow() => InitializeComponent();

    private void OnClose(object sender, RoutedEventArgs e) => Close();
}
