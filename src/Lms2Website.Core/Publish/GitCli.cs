using System.Diagnostics;
using System.Text;

namespace Lms2Website.Core.Publish;

/// <summary>
/// Drives the <c>git</c> command line over the site folder. A publish is always a clean overwrite
/// of one branch — the folder is rebuilt from the cartridge every time — so the sequence is
/// init → remote → add → commit → branch → push --force.
///
/// Authentication is either a token put on the remote URL for the one push (never written into
/// the repository's config) or, with no token, whatever Git Credential Manager already knows.
///
/// Ported from PreseMaker's <c>WebsiteGitHubPublishService</c>, including its fix for the deadlock
/// when Git floods stderr with line-ending warnings.
/// </summary>
public static class GitCli
{
    public static async Task<(bool Installed, string Version)> CheckInstalledAsync(CancellationToken ct = default)
    {
        try
        {
            var (code, stdout, _) = await RunAsync("--version", Path.GetTempPath(), ct);
            return code == 0 && stdout.Length > 0 ? (true, stdout.Trim()) : (false, string.Empty);
        }
        catch (InvalidOperationException) { return (false, string.Empty); }
    }

    /// <summary>True when the branch already has commits on the remote — i.e. a force-push replaces something.</summary>
    public static async Task<bool> RemoteHasBranchAsync(string repoUrl, string branch, CancellationToken ct = default)
    {
        try
        {
            var (code, stdout, _) = await RunAsync($"ls-remote \"{repoUrl}\" \"refs/heads/{branch}\"", Path.GetTempPath(), ct);
            return code == 0 && !string.IsNullOrWhiteSpace(stdout);
        }
        catch (InvalidOperationException) { return false; }
    }

    /// <summary>
    /// Commits the folder and force-pushes it to <paramref name="branch"/>.
    /// <paramref name="pushUrl"/> may carry a token; it is used for this push only and is never
    /// logged — <paramref name="log"/> sees the repository URL with any credentials removed.
    /// </summary>
    public static async Task PublishAsync(
        string siteFolder, string pushUrl, string branch, string commitMessage,
        Action<string> log, CancellationToken ct = default)
    {
        var safeBranch = string.IsNullOrWhiteSpace(branch) ? "main" : branch.Trim();
        var shown = Redact(pushUrl);

        log("$ git init");
        await CheckedAsync("init", siteFolder, log, ct);

        // Commits are made with an identity that belongs to this app when the machine has none set.
        var (nameCode, _, _) = await RunAsync("config user.name", siteFolder, ct);
        if (nameCode != 0)
        {
            await RunAsync("config user.name \"LMS 2 Website\"", siteFolder, ct);
            await RunAsync("config user.email \"lms2website@localhost\"", siteFolder, ct);
        }

        await RunAsync("remote remove origin", siteFolder, ct);   // may not exist; ignore
        log($"$ git remote add origin {shown}");
        await CheckedAsync($"remote add origin {pushUrl}", siteFolder, log, ct, redact: true);

        log("$ git add .");
        // core.autocrlf=false: LF is right for the web, and it avoids a warning per file.
        await CheckedAsync("-c core.autocrlf=false add .", siteFolder, log, ct);

        log($"$ git commit -m \"{commitMessage}\"");
        var (commitCode, commitOut, commitErr) = await RunAsync($"commit -m \"{Escape(commitMessage)}\"", siteFolder, ct);
        var commitText = $"{commitOut}\n{commitErr}".Trim();
        if (commitCode != 0)
        {
            bool nothingToDo = commitText.Contains("nothing to commit", StringComparison.OrdinalIgnoreCase) ||
                               commitText.Contains("nothing added to commit", StringComparison.OrdinalIgnoreCase);
            if (nothingToDo) log("Nothing changed since the last publish — pushing the existing commit.");
            else throw new InvalidOperationException($"git commit failed (exit {commitCode}).\n{commitText}");
        }
        else if (commitText.Length > 0) log(commitText);

        log($"$ git branch -M {safeBranch}");
        await CheckedAsync($"branch -M {safeBranch}", siteFolder, log, ct);

        log($"$ git push -u origin {safeBranch} --force");
        await StreamedAsync($"push -u origin {safeBranch} --force", siteFolder, log, ct);

        // Leave no token behind in .git/config.
        await RunAsync("remote remove origin", siteFolder, ct);
        await RunAsync($"remote add origin {Redact(pushUrl)}", siteFolder, ct);
    }

    /// <summary>https://github.com/owner/repo.git with a token spliced in for one push.</summary>
    public static string WithToken(string repoUrl, string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return repoUrl;
        const string prefix = "https://";
        if (!repoUrl.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)) return repoUrl;
        return prefix + "x-access-token:" + token.Trim() + "@" + repoUrl[prefix.Length..];
    }

    /// <summary>The same URL without any credentials, for logs and for .git/config.</summary>
    public static string Redact(string url)
    {
        int at = url.IndexOf('@');
        int scheme = url.IndexOf("://", StringComparison.Ordinal);
        if (at < 0 || scheme < 0 || at < scheme) return url;
        return url[..(scheme + 3)] + url[(at + 1)..];
    }

    // ── process plumbing ──────────────────────────────────────────────────────

    private static string Escape(string message) => message.Replace("\"", "\\\"");

    private static async Task CheckedAsync(string args, string workingDir, Action<string> log, CancellationToken ct, bool redact = false)
    {
        var (code, stdout, stderr) = await RunAsync(args, workingDir, ct);
        var output = $"{stdout}\n{stderr}".Trim();
        if (output.Length > 0) log(redact ? Redact(output) : output);
        if (code != 0)
            throw new InvalidOperationException($"git {(redact ? Redact(args) : args)} failed (exit {code}).\n{(redact ? Redact(output) : output)}");
    }

    private static async Task<(int Code, string Stdout, string Stderr)> RunAsync(string args, string workingDir, CancellationToken ct)
    {
        using var process = Start(args, workingDir);
        // Read both pipes at once: git can fill stderr and block before stdout is closed.
        var stdout = process.StandardOutput.ReadToEndAsync(ct);
        var stderr = process.StandardError.ReadToEndAsync(ct);
        await Task.WhenAll(stdout, stderr);
        await process.WaitForExitAsync(ct);
        return (process.ExitCode, await stdout, await stderr);
    }

    private static async Task StreamedAsync(string args, string workingDir, Action<string> log, CancellationToken ct)
    {
        using var process = Start(args, workingDir);
        var all = new StringBuilder();

        var stderrPump = Task.Run(async () =>
        {
            var buffer = new char[512];
            var line = new StringBuilder();
            int read;
            while ((read = await process.StandardError.ReadAsync(buffer, ct)) > 0)
            {
                for (int i = 0; i < read; i++)
                {
                    char c = buffer[i];
                    all.Append(c);
                    if (c is '\r' or '\n')
                    {
                        var text = line.ToString().Trim();
                        line.Clear();
                        if (text.Length > 0) log(Redact(text));
                    }
                    else line.Append(c);
                }
            }
            var last = line.ToString().Trim();
            if (last.Length > 0) log(Redact(last));
        }, ct);

        var stdout = process.StandardOutput.ReadToEndAsync(ct);
        await process.WaitForExitAsync(ct);
        await stderrPump;

        var text = (await stdout).Trim();
        if (text.Length > 0) log(Redact(text));

        if (process.ExitCode != 0)
            throw new InvalidOperationException($"git push failed (exit {process.ExitCode}).\n{Redact(all.ToString())}".Trim());
    }

    private static Process Start(string args, string workingDir)
    {
        var info = new ProcessStartInfo("git", args)
        {
            WorkingDirectory       = workingDir,
            UseShellExecute        = false,
            RedirectStandardOutput = true,
            RedirectStandardError  = true,
            CreateNoWindow         = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding  = Encoding.UTF8
        };
        // Never let git stop for a console prompt; the app asks instead.
        info.Environment["GIT_TERMINAL_PROMPT"] = "0";

        try
        {
            return Process.Start(info)
                   ?? throw new InvalidOperationException("Git did not start.");
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            throw new InvalidOperationException(
                "Git is not installed, or not on the PATH. Install Git for Windows from " +
                "https://git-scm.com/download/win, or publish with a GitHub token instead.\n" + ex.Message, ex);
        }
    }
}
