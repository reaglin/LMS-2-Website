using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace Lms2Website.Core.Site;

/// <summary>What the rewriter did to one page — the body to publish, plus what to tell the user.</summary>
public sealed class RewriteResult
{
    public string BodyHtml { get; set; } = string.Empty;
    /// <summary>References that pointed at something not in the cartridge (left in place).</summary>
    public List<string> BrokenReferences { get; } = new();
    /// <summary>Links back into the LMS — they will ask a visitor to sign in.</summary>
    public int LmsLinkCount { get; set; }
    /// <summary>Scripts removed from the page.</summary>
    public int ScriptsRemoved { get; set; }
}

/// <summary>
/// Turns one page of cartridge HTML into the body of a website page: drops the LMS's own
/// stylesheets and scripts, points every in-cartridge reference at the file's new home, marks
/// links that go back into the LMS, and hands back just the body markup for the template to wrap.
///
/// The page's own inline styles are kept — they are the author's formatting.
/// </summary>
public static class ContentRewriter
{
    private static readonly HtmlParser Parser = new();

    /// <summary>Elements and the attribute that carries a reference.</summary>
    private static readonly (string Selector, string Attr)[] References =
    [
        ("img[src]", "src"), ("img[srcset]", "srcset"),
        ("a[href]", "href"),
        ("audio[src]", "src"), ("video[src]", "src"), ("video[poster]", "poster"),
        ("source[src]", "src"), ("track[src]", "src"),
        ("iframe[src]", "src"), ("embed[src]", "src"), ("object[data]", "data")
    ];

    /// <param name="html">The page as it is in the cartridge (a whole document, or a fragment).</param>
    /// <param name="pageZipHref">Where the page lives in the zip — references resolve against it.</param>
    /// <param name="pageSiteDir">The page's folder in the site ("01-week-1/"), for relative links.</param>
    /// <param name="resolve">
    /// Given a zip-relative href, returns the site-root-relative URL the file will have
    /// (copying it into the site if need be), or null when the cartridge has no such file.
    /// </param>
    public static RewriteResult Clean(string html, string pageZipHref, string pageSiteDir, Func<string, string?> resolve,
                                      string? pageTitle = null)
    {
        var result = new RewriteResult();
        if (string.IsNullOrWhiteSpace(html)) return result;

        IDocument doc;
        try { doc = Parser.ParseDocument(WrapFragment(html)); }
        catch (InvalidOperationException)
        {
            result.BodyHtml = html;
            return result;
        }

        foreach (var script in doc.QuerySelectorAll("script").ToList())
        {
            script.Remove();
            result.ScriptsRemoved++;
        }
        // The LMS's stylesheets are not in the cartridge and would fight the site's own.
        foreach (var link in doc.QuerySelectorAll("link").ToList()) link.Remove();
        foreach (var el in doc.QuerySelectorAll("*").ToList())
            foreach (var attr in el.Attributes.Where(a => a.Name.StartsWith("on", StringComparison.OrdinalIgnoreCase)).ToList())
                el.RemoveAttribute(attr.Name);

        foreach (var (selector, attrName) in References)
        {
            foreach (var el in doc.QuerySelectorAll(selector))
            {
                var raw = el.GetAttribute(attrName);
                if (string.IsNullOrWhiteSpace(raw)) continue;

                if (IsAbsolute(raw))
                {
                    if (el.LocalName == "a" && IsLmsUrl(raw))
                    {
                        el.SetAttribute("class", string.Join(' ', new[] { el.GetAttribute("class"), "lms-link" }
                            .Where(s => !string.IsNullOrWhiteSpace(s))));
                        el.SetAttribute("title", "This link goes back to the course in the LMS and will ask for a sign-in.");
                        result.LmsLinkCount++;
                    }
                    continue;
                }
                if (raw.StartsWith('#') || raw.StartsWith("data:", StringComparison.OrdinalIgnoreCase)) continue;

                var (path, suffix) = SplitSuffix(raw);
                var zipHref = ResolveAgainstPage(pageZipHref, path);
                var siteHref = resolve(zipHref);
                if (siteHref == null)
                {
                    result.BrokenReferences.Add(raw);
                    if (el.LocalName == "img")
                    {
                        // A broken image icon tells the reader nothing; say what happened instead.
                        // (D2L, for one, leaves the images used inside quiz questions out of the export.)
                        var note = doc.CreateElement("span");
                        note.SetAttribute("class", "missing-image");
                        note.TextContent = string.IsNullOrWhiteSpace(el.GetAttribute("alt"))
                            ? "[image not included in the export]"
                            : $"[image not included in the export: {el.GetAttribute("alt")}]";
                        el.Replace(note);
                    }
                    else if (el.LocalName == "a")
                    {
                        el.SetAttribute("class", string.Join(' ', new[] { el.GetAttribute("class"), "broken-link" }
                            .Where(s => !string.IsNullOrWhiteSpace(s))));
                    }
                    continue;
                }

                el.SetAttribute(attrName, Relative(pageSiteDir, siteHref) + suffix);
                if (el.LocalName == "a" && !siteHref.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
                    el.SetAttribute("download", string.Empty);
            }
        }

        if (pageTitle != null) RemoveRepeatedTitle(doc, pageTitle);

        result.BodyHtml = (doc.Body?.InnerHtml ?? html).Trim();
        return result;
    }

    /// <summary>
    /// An LMS page nearly always opens with its own title, which the site already shows as the
    /// page heading. Drop that first heading when it says the same thing — but only then.
    /// </summary>
    private static void RemoveRepeatedTitle(IDocument doc, string pageTitle)
    {
        var first = doc.Body?.Children.FirstOrDefault();
        if (first == null) return;

        bool isHeading = first.LocalName is "h1" or "h2" or "h3" or "h4";
        // D2L also writes the title as a bold paragraph rather than a heading.
        bool isBoldParagraph = first.LocalName is "p" or "div" &&
                               first.QuerySelectorAll("strong, b").Any() &&
                               first.QuerySelectorAll("a, img").Length == 0;
        if (!isHeading && !isBoldParagraph) return;

        if (Comparable(first.TextContent) == Comparable(pageTitle)) first.Remove();
    }

    /// <summary>Letters and digits only, lower-case — so punctuation and spacing cannot disagree.</summary>
    private static string Comparable(string text) =>
        new(text.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    /// <summary>A reference the cartridge cannot own: another site, a mail link, a protocol handler.</summary>
    private static bool IsAbsolute(string value)
    {
        var v = value.Trim();
        if (v.StartsWith("//", StringComparison.Ordinal)) return true;
        if (v.StartsWith('/')) return true;   // site-absolute: meaningless inside a cartridge
        int colon = v.IndexOf(':');
        if (colon <= 0) return false;
        var scheme = v[..colon];
        return !(scheme.Length == 1 && char.IsAsciiLetter(scheme[0]));   // "C:\..." is a path, not a scheme
    }

    /// <summary>True for a URL that points back into a learning management system.</summary>
    internal static bool IsLmsUrl(string url) =>
        url.Contains("/d2l/", StringComparison.OrdinalIgnoreCase) ||
        url.Contains("quickLink", StringComparison.OrdinalIgnoreCase) ||
        url.Contains(".instructure.com/courses", StringComparison.OrdinalIgnoreCase) ||
        url.Contains("/webapps/blackboard/", StringComparison.OrdinalIgnoreCase) ||
        url.Contains("/course/view.php", StringComparison.OrdinalIgnoreCase);

    /// <summary>Resolves a page-relative reference to a zip-relative href.</summary>
    internal static string ResolveAgainstPage(string pageZipHref, string reference)
    {
        var href = reference.Replace('\\', '/');
        // Canvas writes "$IMS-CC-FILEBASE$/folder/file.png" for anything in web_resources.
        const string fileBase = "$IMS-CC-FILEBASE$";
        if (href.StartsWith(fileBase, StringComparison.OrdinalIgnoreCase))
            return "web_resources/" + href[fileBase.Length..].TrimStart('/');

        try { href = Uri.UnescapeDataString(href); } catch (UriFormatException) { /* use it as written */ }
        var dir = pageZipHref.Contains('/') ? pageZipHref[..(pageZipHref.LastIndexOf('/') + 1)] : string.Empty;
        return Cartridge.CcPackage.Combine(dir, href);
    }

    /// <summary>"../files/x.png" for a file at "files/x.png" seen from the folder "01-week/".</summary>
    internal static string Relative(string fromDir, string rootRelative)
    {
        var from = fromDir.Trim('/');
        int depth = from.Length == 0 ? 0 : from.Split('/').Length;
        var prefix = string.Concat(Enumerable.Repeat("../", depth));
        return prefix + EscapePath(rootRelative);
    }

    /// <summary>Percent-escapes each segment of a site path, leaving the separators alone.</summary>
    internal static string EscapePath(string path) =>
        string.Join('/', path.Split('/').Select(Uri.EscapeDataString));

    private static (string Path, string Suffix) SplitSuffix(string href)
    {
        int q = href.IndexOfAny(['?', '#']);
        return q < 0 ? (href, string.Empty) : (href[..q], href[q..]);
    }

    /// <summary>A body fragment parses fine on its own; a whole document keeps its head out of the way.</summary>
    private static string WrapFragment(string html) =>
        html.Contains("<html", StringComparison.OrdinalIgnoreCase) ? html : "<!DOCTYPE html><html><body>" + html + "</body></html>";
}
