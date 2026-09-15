using System.Net;
using AngleSharp.Html.Parser;

namespace Lms2Website.Core.Site;

/// <summary>Small HTML helpers shared by the reader and the site builder.</summary>
public static class Html
{
    private static readonly HtmlParser Parser = new();

    /// <summary>Escapes text for insertion into markup.</summary>
    public static string Escape(string? text) => WebUtility.HtmlEncode(text ?? string.Empty);

    /// <summary>
    /// The visible text of an HTML fragment, whitespace collapsed — for titles, search text and
    /// the "is this choice the word true" test. Never throws: bad markup yields its own text.
    /// </summary>
    public static string ToPlainText(string? html)
    {
        if (string.IsNullOrWhiteSpace(html)) return string.Empty;
        string text;
        try
        {
            var doc = Parser.ParseDocument(html);
            text = doc.Body?.TextContent ?? string.Empty;
        }
        catch (InvalidOperationException) { text = html; }
        return Collapse(text);
    }

    public static string Collapse(string text) =>
        string.Join(" ", (text ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

    /// <summary>First <paramref name="max"/> characters of the text, cut at a word boundary.</summary>
    public static string Snippet(string? text, int max = 200)
    {
        var t = Collapse(text ?? string.Empty);
        if (t.Length <= max) return t;
        var cut = t.LastIndexOf(' ', Math.Min(max, t.Length - 1));
        return (cut > max / 2 ? t[..cut] : t[..max]).TrimEnd() + "…";
    }

    /// <summary>A size for people: "3.4 MB", "812 KB", "640 bytes".</summary>
    public static string FileSize(long bytes) => bytes switch
    {
        >= 1024L * 1024 * 1024 => $"{bytes / (1024.0 * 1024 * 1024):0.#} GB",
        >= 1024 * 1024         => $"{bytes / (1024.0 * 1024):0.#} MB",
        >= 1024                => $"{bytes / 1024.0:0.#} KB",
        _                      => $"{bytes} bytes"
    };
}
