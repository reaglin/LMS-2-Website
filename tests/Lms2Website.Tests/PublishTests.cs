using System.Diagnostics;
using Lms2Website.Core.Publish;

namespace Lms2Website.Tests;

public class PublishTests
{
    [Fact]
    public void TokenIsSplicedIntoThePushUrlAndTakenBackOutForLogs()
    {
        const string url = "https://github.com/reaglin/egn3443.git";
        var push = GitCli.WithToken(url, "ghp_secretsecret");

        Assert.Equal("https://x-access-token:ghp_secretsecret@github.com/reaglin/egn3443.git", push);
        Assert.Equal(url, GitCli.Redact(push));
        Assert.DoesNotContain("ghp_", GitCli.Redact(push), StringComparison.Ordinal);
    }

    [Fact]
    public void AnSshUrlIsLeftAlone() =>
        Assert.Equal("git@github.com:reaglin/x.git", GitCli.WithToken("git@github.com:reaglin/x.git", "ghp_x"));

    [Fact]
    public void MaskShowsEnoughToRecogniseAndNoMore()
    {
        Assert.Equal("ghp_…cdef", TokenStore.Mask("ghp_0123456789abcdef"));
        Assert.Equal("••••", TokenStore.Mask("abcd"));
    }

    [Fact]
    public void PreflightStopsAMissingFolder()
    {
        var warning = Assert.Single(Publisher.Preflight(Path.Combine(Path.GetTempPath(), "no-such-site-" + Guid.NewGuid())));
        Assert.True(warning.Blocking);
    }

    [Fact]
    public void PreflightPassesAnOrdinarySite()
    {
        var folder = Temp();
        try
        {
            File.WriteAllText(Path.Combine(folder, "index.html"), "<p>hello</p>");
            Assert.Empty(Publisher.Preflight(folder));
            Assert.Equal(12, Publisher.SiteSize(folder));
        }
        finally { Directory.Delete(folder, true); }
    }

    [Fact]
    public void SiteSizeIgnoresTheGitFolder()
    {
        var folder = Temp();
        try
        {
            File.WriteAllText(Path.Combine(folder, "index.html"), "12345");
            Directory.CreateDirectory(Path.Combine(folder, ".git"));
            File.WriteAllText(Path.Combine(folder, ".git", "big"), new string('x', 5000));
            Assert.Equal(5, Publisher.SiteSize(folder));
        }
        finally { Directory.Delete(folder, true); }
    }

    /// <summary>
    /// The whole git route — init, commit, force-push — against a bare repository on this disk.
    /// It exercises everything except GitHub itself, and needs no network and no account.
    /// Skipped when git is not installed.
    /// </summary>
    [Fact]
    public async Task PushesTheSiteToARepository()
    {
        var (installed, _) = await GitCli.CheckInstalledAsync();
        if (!installed) return;

        var root = Temp();
        var site = Path.Combine(root, "site");
        var remote = Path.Combine(root, "remote.git");
        Directory.CreateDirectory(site);
        Directory.CreateDirectory(remote);

        try
        {
            File.WriteAllText(Path.Combine(site, "index.html"), "<h1>Course</h1>");
            Directory.CreateDirectory(Path.Combine(site, "assets"));
            File.WriteAllText(Path.Combine(site, "assets", "site.css"), "body{}");
            Run("init --bare -b main", remote);

            var log = new List<string>();
            await GitCli.PublishAsync(site, remote.Replace('\\', '/'), "main", "First publish", log.Add);

            var files = Run("ls-tree -r --name-only main", remote);
            Assert.Contains("index.html", files, StringComparison.Ordinal);
            Assert.Contains("assets/site.css", files, StringComparison.Ordinal);
            Assert.Contains(log, l => l.Contains("git push", StringComparison.Ordinal));

            // A second publish of changed content replaces the branch rather than failing.
            File.WriteAllText(Path.Combine(site, "index.html"), "<h1>Course, again</h1>");
            await GitCli.PublishAsync(site, remote.Replace('\\', '/'), "main", "Second publish", log.Add);
            Assert.Contains("Second publish", Run("log -1 --pretty=%s main", remote), StringComparison.Ordinal);
        }
        finally
        {
            Force(root);
        }
    }

    private static string Temp()
    {
        var folder = Path.Combine(Path.GetTempPath(), "lms2web-pub-" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(folder);
        return folder;
    }

    private static string Run(string args, string workingDir)
    {
        using var process = Process.Start(new ProcessStartInfo("git", args)
        {
            WorkingDirectory = workingDir,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        })!;
        var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
        process.WaitForExit();
        return output;
    }

    /// <summary>Git marks objects read-only, so a plain recursive delete fails on Windows.</summary>
    private static void Force(string folder)
    {
        foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
            File.SetAttributes(file, FileAttributes.Normal);
        try { Directory.Delete(folder, true); } catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
