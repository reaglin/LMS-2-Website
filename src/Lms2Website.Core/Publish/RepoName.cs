namespace Lms2Website.Core.Publish;

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
