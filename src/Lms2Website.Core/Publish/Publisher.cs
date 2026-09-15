using Lms2Website.Core.Site;

namespace Lms2Website.Core.Publish;

public sealed class PublishRequest
{
    public required string SiteFolder { get; init; }
    /// <summary>GitHub account or organisation. Blank means "whoever the token belongs to".</summary>
    public string Owner { get; init; } = string.Empty;
    public required string Repository { get; init; }
    public string Branch { get; init; } = "main";
    public string CommitMessage { get; init; } = "Publish course website";
    public string Description { get; init; } = string.Empty;
    /// <summary>A private repository cannot serve GitHub Pages on a free plan.</summary>
    public bool Private { get; init; }
    /// <summary>Create the repository when it is not there yet (token publishing only).</summary>
    public bool CreateIfMissing { get; init; } = true;
    public bool EnablePages { get; init; } = true;
}

public sealed class PublishResult
{
    public string RepositoryUrl { get; set; } = string.Empty;
    public string PagesUrl { get; set; } = string.Empty;
    /// <summary>"Git push" or "GitHub API".</summary>
    public string Method { get; set; } = string.Empty;
    public bool RepositoryCreated { get; set; }
    public bool PagesEnabled { get; set; }
    /// <summary>What the user still has to do by hand, if anything.</summary>
    public List<string> NextSteps { get; } = new();
}

/// <summary>A thing worth knowing before a site is pushed anywhere.</summary>
public sealed record PublishWarning(string Message, bool Blocking);

/// <summary>
/// Puts a built site on GitHub Pages. Two routes, and the app prefers the first:
///
/// 1. <b>With a token</b> — the app finds or creates the repository, pushes the site (with Git
///    when it is installed, otherwise straight through the API), and switches Pages on. Nothing
///    to do on github.com.
/// 2. <b>Without a token</b> — a plain <c>git push</c> to a repository the user has already made,
///    authenticated by Git Credential Manager. Pages is then switched on by hand, and the result
///    says so.
/// </summary>
public static class Publisher
{
    /// <summary>GitHub rejects a file over 100 MB outright and complains over 50 MB.</summary>
    private const long HardFileLimit = 100L * 1024 * 1024;
    private const long SoftFileLimit = 50L * 1024 * 1024;
    /// <summary>GitHub Pages will not serve a site built from a repository over 1 GB.</summary>
    private const long SiteLimit = 1024L * 1024 * 1024;
    /// <summary>Above this, uploading file by file through the API is too slow to be sensible.</summary>
    private const long ApiUploadLimit = 40L * 1024 * 1024;

    /// <summary>What GitHub will object to, checked before anything is sent.</summary>
    public static IReadOnlyList<PublishWarning> Preflight(string siteFolder)
    {
        var warnings = new List<PublishWarning>();
        if (!Directory.Exists(siteFolder))
            return [new PublishWarning("The site folder does not exist — build the website first.", true)];

        long total = 0;
        foreach (var file in Directory.EnumerateFiles(siteFolder, "*", SearchOption.AllDirectories))
        {
            if (file.Replace('\\', '/').Contains("/.git/", StringComparison.Ordinal)) continue;
            var length = new FileInfo(file).Length;
            total += length;
            var name = Path.GetRelativePath(siteFolder, file);
            if (length > HardFileLimit)
                warnings.Add(new PublishWarning($"{name} is {Html.FileSize(length)} — GitHub refuses any file over 100 MB.", true));
            else if (length > SoftFileLimit)
                warnings.Add(new PublishWarning($"{name} is {Html.FileSize(length)} — GitHub warns above 50 MB but will take it.", false));
        }

        if (total > SiteLimit)
            warnings.Add(new PublishWarning(
                $"The site is {Html.FileSize(total)}. GitHub Pages does not serve sites built from a repository over 1 GB.", true));
        return warnings;
    }

    /// <summary>Total bytes of the site, ignoring .git.</summary>
    public static long SiteSize(string siteFolder) =>
        Directory.Exists(siteFolder)
            ? Directory.EnumerateFiles(siteFolder, "*", SearchOption.AllDirectories)
                       .Where(f => !f.Replace('\\', '/').Contains("/.git/", StringComparison.Ordinal))
                       .Sum(f => new FileInfo(f).Length)
            : 0;

    public static async Task<PublishResult> PublishAsync(
        PublishRequest request, string? token, Action<string> log,
        IProgress<(int Percent, string Message)>? progress = null, CancellationToken ct = default)
    {
        foreach (var blocker in Preflight(request.SiteFolder).Where(w => w.Blocking))
            throw new InvalidOperationException(blocker.Message);

        return string.IsNullOrWhiteSpace(token)
            ? await PublishWithGitOnlyAsync(request, log, ct)
            : await PublishWithTokenAsync(request, token, log, progress, ct);
    }

    // ── with a token ──────────────────────────────────────────────────────────

    private static async Task<PublishResult> PublishWithTokenAsync(
        PublishRequest request, string token, Action<string> log,
        IProgress<(int Percent, string Message)>? progress, CancellationToken ct)
    {
        var result = new PublishResult();
        using var api = new GitHubApi(token);

        progress?.Report((2, "Checking the token"));
        var user = await api.GetUserAsync(ct);
        var owner = string.IsNullOrWhiteSpace(request.Owner) ? user.Login : request.Owner.Trim();
        log($"Signed in to GitHub as {user.Login}.");

        progress?.Report((5, "Looking for the repository"));
        var repo = await api.FindRepoAsync(owner, request.Repository, ct);
        if (repo == null)
        {
            if (!request.CreateIfMissing)
                throw new InvalidOperationException($"{owner}/{request.Repository} does not exist.");
            if (!owner.Equals(user.Login, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException(
                    $"{owner}/{request.Repository} does not exist. The app can only create repositories under your own account ({user.Login}) — " +
                    "make it on github.com first, then publish again.");
            log($"Creating {owner}/{request.Repository}…");
            repo = await api.CreateRepoAsync(request.Repository, Describe(request.Description), request.Private, ct);
            result.RepositoryCreated = true;
        }
        result.RepositoryUrl = $"https://github.com/{owner}/{repo.Name}";

        // Mark it as a generated site. Worth saying out loud but never worth failing a publish.
        try
        {
            await api.EnsureTopicAsync(owner, repo.Name, L2W.Topic, ct);
        }
        catch (GitHubException ex)
        {
            log($"Could not put the \"{L2W.Topic}\" topic on the repository: {ex.Message}");
        }

        var (gitInstalled, version) = await GitCli.CheckInstalledAsync(ct);
        var size = SiteSize(request.SiteFolder);

        if (gitInstalled)
        {
            log($"Pushing with {version}.");
            result.Method = "Git push";
            progress?.Report((10, "Pushing to GitHub"));
            await GitCli.PublishAsync(request.SiteFolder, GitCli.WithToken(repo.CloneUrl, token),
                request.Branch, request.CommitMessage, log, ct);
        }
        else if (size <= ApiUploadLimit)
        {
            log("Git is not installed — uploading through the GitHub API instead.");
            result.Method = "GitHub API";
            await api.UploadFolderAsync(owner, repo.Name, request.Branch, request.SiteFolder,
                request.CommitMessage, progress, ct);
        }
        else
        {
            throw new InvalidOperationException(
                $"The site is {Html.FileSize(size)}, which is too much to upload file by file, and Git is not installed. " +
                "Install Git for Windows (https://git-scm.com/download/win) and publish again.");
        }

        if (request.EnablePages)
        {
            progress?.Report((97, "Switching GitHub Pages on"));
            try
            {
                var pages = await api.EnablePagesAsync(owner, repo.Name, request.Branch, ct);
                result.PagesUrl = pages.Url;
                result.PagesEnabled = true;
                log($"GitHub Pages is serving {request.Branch} at {pages.Url} (status: {pages.Status}).");
                result.NextSteps.Add("The first build takes a minute or two — the address 404s until it finishes.");
            }
            catch (GitHubException ex)
            {
                // The site is pushed; only the switch failed. Say so rather than failing the publish.
                result.PagesUrl = $"https://{owner}.github.io/{repo.Name}/";
                log("The files are on GitHub, but Pages could not be switched on: " + ex.Message);
                result.NextSteps.Add($"Switch Pages on by hand: {result.RepositoryUrl}/settings/pages → " +
                                     $"Source \"Deploy from a branch\" → {request.Branch} / (root).");
            }
        }

        if (request.Private)
            result.NextSteps.Add("The repository is private — GitHub Pages only serves private repositories on a paid plan.");

        progress?.Report((100, "Published"));
        return result;
    }

    /// <summary>The repository description always says what made it.</summary>
    private static string Describe(string description)
    {
        var built = $"Built with {L2W.Product}.";
        var text = (description ?? string.Empty).Trim();
        if (text.Length == 0) return $"A course website. {built}";
        return text.Contains(L2W.Product, StringComparison.OrdinalIgnoreCase)
            ? text
            : $"{text.TrimEnd('.', ' ')}. {built}";
    }

    // ── without a token ───────────────────────────────────────────────────────

    private static async Task<PublishResult> PublishWithGitOnlyAsync(PublishRequest request, Action<string> log, CancellationToken ct)
    {
        var (installed, version) = await GitCli.CheckInstalledAsync(ct);
        if (!installed)
            throw new InvalidOperationException(
                "Publishing needs either a GitHub token or Git for Windows. Add a token in Settings, " +
                "or install Git from https://git-scm.com/download/win.");

        var owner = request.Owner.Trim();
        if (owner.Length == 0)
            throw new InvalidOperationException("Without a token the app cannot work out the account — fill in the GitHub user or organisation.");

        var repoUrl = $"https://github.com/{owner}/{request.Repository}.git";
        log($"Pushing with {version} (Git Credential Manager will ask for a sign-in if it needs one).");

        var result = new PublishResult
        {
            RepositoryUrl = $"https://github.com/{owner}/{request.Repository}",
            Method = "Git push",
            PagesUrl = $"https://{owner}.github.io/{request.Repository}/"
        };

        await GitCli.PublishAsync(request.SiteFolder, repoUrl, request.Branch, request.CommitMessage, log, ct);

        result.NextSteps.Add($"Switch GitHub Pages on: {result.RepositoryUrl}/settings/pages → " +
                             $"Source \"Deploy from a branch\" → {request.Branch} / (root). " +
                             "Add a token in Settings and the app will do this for you next time.");
        return result;
    }
}
