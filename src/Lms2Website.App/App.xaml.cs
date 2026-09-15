using System.IO;
using System.Windows;
using System.Windows.Threading;
using Lms2Website.Core.Cartridge;
using Lms2Website.Core.Site;

namespace Lms2Website.App;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        // A headless conversion, for scripting and for hand-testing a build without clicking:
        //   LMS2Website.exe --convert <course.imscc> <output folder>
        if (e.Args.Length >= 3 && e.Args[0].Equals("--convert", StringComparison.OrdinalIgnoreCase))
        {
            Convert(e.Args[1], e.Args[2]);
            Shutdown(0);
            return;
        }

        // Writes the demo cartridge, for trying the app out without a real course export:
        //   LMS2Website.exe --write-sample <folder>
        if (e.Args.Length >= 2 && e.Args[0].Equals("--write-sample", StringComparison.OrdinalIgnoreCase))
        {
            Lms2Website.Core.Samples.SampleCartridge.Write(e.Args[1]);
            Shutdown(0);
            return;
        }

        DispatcherUnhandledException += OnUnhandledException;
        base.OnStartup(e);

        // Shown here rather than with StartupUri, so the --convert path above can exit first.
        var window = new MainWindow();
        MainWindow = window;
        window.Show();

        // A cartridge passed on the command line (or dropped on the .exe) opens straight away.
        if (e.Args.Length == 1 && File.Exists(e.Args[0]))
            _ = window.OpenAsync(e.Args[0]);
    }

    private static void Convert(string cartridge, string output)
    {
        try
        {
            var course = CartridgeReader.Read(cartridge);
            var result = SiteBuilder.Build(course, new SiteBuildOptions { OutputFolder = output });
            var report =
                $"{course.Title}\n{course.Summary()}\n" +
                $"{result.PagesWritten} pages, {result.FilesCopied} files in {result.Elapsed.TotalSeconds:0.0}s\n" +
                $"{result.IndexPath}\n" +
                string.Join('\n', result.Warnings.Select(w => "  ! " + w));
            File.WriteAllText(Path.Combine(output, "convert-log.txt"), report);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            Directory.CreateDirectory(output);
            File.WriteAllText(Path.Combine(output, "convert-log.txt"), "Failed: " + ex.Message);
        }
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        MessageBox.Show(
            e.Exception.Message + "\n\n" + e.Exception.GetType().Name,
            "LMS 2 Website ran into a problem", MessageBoxButton.OK, MessageBoxImage.Error);
        e.Handled = true;
    }
}
