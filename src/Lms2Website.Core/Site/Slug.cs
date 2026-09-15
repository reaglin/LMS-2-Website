using System.Globalization;
using System.Text;

namespace Lms2Website.Core.Site;

/// <summary>
/// Turns a course, module or item title into a URL segment that is safe on GitHub Pages:
/// lower-case ASCII, words joined by hyphens, no punctuation, never empty, never a Windows
/// reserved name. Accented letters fold to their base letter; anything else is dropped.
/// </summary>
public static class Slug
{
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "con", "prn", "aux", "nul", "index",
        "com1", "com2", "com3", "com4", "com5", "com6", "com7", "com8", "com9",
        "lpt1", "lpt2", "lpt3", "lpt4", "lpt5", "lpt6", "lpt7", "lpt8", "lpt9"
    };

    public static string From(string? title, string fallback = "item", int maxLength = 60)
    {
        var text = (title ?? string.Empty).Trim();

        // D2L names a page "Overview.html"; the extension is not part of the title.
        foreach (var ext in new[] { ".html", ".htm" })
            if (text.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                text = text[..^ext.Length];

        var normalized = text.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        bool lastWasHyphen = true;   // leading hyphens never start the slug
        foreach (var ch in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsAsciiLetterOrDigit(ch))
            {
                sb.Append(char.ToLowerInvariant(ch));
                lastWasHyphen = false;
            }
            else if (!lastWasHyphen)
            {
                sb.Append('-');
                lastWasHyphen = true;
            }
        }

        var slug = sb.ToString().Trim('-');
        if (slug.Length > maxLength)
        {
            slug = slug[..maxLength];
            int cut = slug.LastIndexOf('-');
            if (cut > maxLength / 2) slug = slug[..cut];
            slug = slug.Trim('-');
        }
        if (slug.Length == 0) slug = fallback;
        if (Reserved.Contains(slug)) slug += "-page";
        return slug;
    }

    /// <summary>The slug, suffixed with -2, -3… until it is not already in <paramref name="used"/>.</summary>
    public static string Unique(string slug, HashSet<string> used)
    {
        if (used.Add(slug)) return slug;
        for (int i = 2; ; i++)
        {
            var candidate = $"{slug}-{i}";
            if (used.Add(candidate)) return candidate;
        }
    }

    /// <summary>A file name that keeps its extension but is safe in a URL ("Lecture 1.pptx" → "lecture-1.pptx").</summary>
    public static string FileName(string name, string fallback = "file")
    {
        var ext  = Path.GetExtension(name);
        var stem = Path.GetFileNameWithoutExtension(name);
        var safeExt = new string(ext.Where(c => char.IsAsciiLetterOrDigit(c) || c == '.').ToArray()).ToLowerInvariant();
        return From(stem, fallback) + safeExt;
    }
}
