namespace Lms2Website.Core.Publish;

/// <summary>
/// Wording the window and the publish log have to agree on, because getting it wrong would mislead
/// someone about who can read their course.
/// </summary>
public static class PublishWords
{
    /// <summary>
    /// What "private repository" really does. The plain reading of the words is wrong, and the
    /// mistake is the dangerous direction: GitHub's own documentation says "GitHub Pages sites are
    /// publicly available on the internet, even if the repository for the site is private". A
    /// private repository hides the files, not the website built from them. Restricting who may
    /// open a published site needs GitHub Enterprise Cloud, and on a free account a private
    /// repository cannot publish a site at all.
    /// </summary>
    public const string PrivateDoesNotMeanHidden =
        "This does not make the website private. A GitHub Pages site is public on the internet even when " +
        "its repository is private — the setting hides the files, not the site. On a free account a " +
        "private repository cannot publish a site at all, and restricting who may open a published site " +
        "needs GitHub Enterprise Cloud. If the course must not be public, do not publish it here.";
}

/// <summary>
/// The naming rule for a published course site: every repository is <c>L2W-&lt;course&gt;</c>.
///
/// It is a safety rule as much as a tidy one. Publishing is a force-push onto a branch, and the
/// repository name is proposed from the course title — so without a prefix a course called
/// "EGN3443" would aim straight at a hand-made <c>EGN3443</c> repository and overwrite it. With
/// the prefix the app can only ever land on a name a person would not have chosen by accident.
/// </summary>
public static class RepoName
{
    /// <summary>True when the name already carries the prefix, whatever its capitalisation.</summary>
    public static bool HasPrefix(string? name) =>
        (name ?? string.Empty).TrimStart().StartsWith(L2W.RepoPrefix, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Puts the prefix on a name. A name that already has it keeps the rest of its spelling and
    /// only has the prefix itself normalised ("l2w-egn3443" → "L2W-egn3443"), so the prefix is
    /// never doubled. Blank stays blank, so the caller's own validation still speaks first.
    /// </summary>
    public static string Apply(string? name)
    {
        var trimmed = (name ?? string.Empty).Trim();
        if (trimmed.Length == 0) return string.Empty;

        return HasPrefix(trimmed)
            ? L2W.RepoPrefix + trimmed[L2W.RepoPrefix.Length..]
            : L2W.RepoPrefix + trimmed;
    }
}
