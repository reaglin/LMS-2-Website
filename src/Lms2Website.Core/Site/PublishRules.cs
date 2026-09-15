using System.Globalization;

namespace Lms2Website.Core.Site;

/// <summary>
/// Which of a course's files are built but <b>not</b> published. A course with lecture decks is
/// mostly PowerPoint — 134 MB of it for EGN3443 — and a public repository is not always the right
/// home for that, so the site on disk stays whole and the push is trimmed.
///
/// Two kinds of rule, and they are not equally expressible as a <c>.gitignore</c>:
/// a type rule is a pattern (<c>*.pptx</c>), but "over 25 MB" is not something git can match on,
/// so the size rule is written out as one line per file. <see cref="SiteBuilder"/> writes that
/// file, and marks every excluded item on the page it belongs to, so a published site never
/// carries a download link that leads nowhere.
///
/// The default is <see cref="None"/>: nothing is excluded unless it is asked for.
/// </summary>
public sealed class PublishRules
{
    /// <summary>Files larger than this are built but not published. Zero means no size rule.</summary>
    public long MaxBytes { get; init; }

    /// <summary>Extensions that are built but not published, each stored as ".pptx".</summary>
    public IReadOnlyList<string> Extensions { get; init; } = [];

    /// <summary>Publish everything.</summary>
    public static PublishRules None { get; } = new();

    public bool Any => MaxBytes > 0 || Extensions.Count > 0;

    /// <summary>
    /// Why this file is not published, phrased for the page it appears on — or null to publish it.
    /// The size rule is tested first, because it is the one a reader is least likely to guess.
    /// </summary>
    public string? ExcludeReason(string fileName, long bytes)
    {
        if (MaxBytes > 0 && bytes > MaxBytes)
            return $"over the {Megabytes(MaxBytes)} limit set for this site";

        var extension = Path.GetExtension(fileName);
        if (extension.Length > 0 && Extensions.Any(e => e.Equals(extension, StringComparison.OrdinalIgnoreCase)))
            return $"{extension.ToLowerInvariant()} files are not published from this site";

        return null;
    }

    /// <summary>
    /// Reads the extensions a person typed: "pptx, .zip  mp4" all mean the same thing. Anything
    /// that is not a plain extension is dropped rather than guessed at.
    /// </summary>
    public static IReadOnlyList<string> ParseExtensions(string? text)
    {
        if (string.IsNullOrWhiteSpace(text)) return [];

        var found = new List<string>();
        foreach (var piece in text.Split([',', ';', ' ', '\t', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries))
        {
            var extension = piece.Trim().TrimStart('*');
            if (!extension.StartsWith('.')) extension = "." + extension;
            extension = extension.ToLowerInvariant();

            if (extension.Length < 2) continue;
            if (extension.AsSpan(1).ContainsAny(Path.GetInvalidFileNameChars())) continue;
            if (extension.AsSpan(1).Contains('.')) continue;
            if (!found.Contains(extension, StringComparer.OrdinalIgnoreCase)) found.Add(extension);
        }
        return found;
    }

    /// <summary>"25 MB", for the sentence on the page and the header of the .gitignore.</summary>
    public static string Megabytes(long bytes) =>
        (bytes / (1024d * 1024d)).ToString("0.##", CultureInfo.InvariantCulture) + " MB";

    /// <summary>A one-line description of the rules, for the log and the .gitignore header.</summary>
    public string Describe()
    {
        var parts = new List<string>();
        if (MaxBytes > 0) parts.Add($"nothing over {Megabytes(MaxBytes)}");
        if (Extensions.Count > 0) parts.Add("no " + string.Join(", ", Extensions));
        return parts.Count == 0 ? "everything is published" : string.Join("; ", parts);
    }
}
