using System.Text;
using Lms2Website.Core.Model;

namespace Lms2Website.Core.Site;

/// <summary>
/// The shell every generated page shares: a header with the course name and the search box, the
/// section menu, the content, and the previous/next pager. One template, written by hand, so the
/// site has no build step and no dependencies.
/// </summary>
public sealed class PageTemplate
{
    private readonly CourseSite _course;
    private readonly List<SiteItem> _order;

    public PageTemplate(CourseSite course)
    {
        _course = course;
        // A web link leaves the site, so it is in the menus but never a previous/next target.
        _order  = course.Modules.Where(m => m.Include).SelectMany(m => m.Published)
                        .Where(i => i.Kind != ItemKind.Link).ToList();
    }

    /// <summary>The footer line at the bottom of every page.</summary>
    public string FooterNote { get; init; } =
        $"Converted from an LMS export with LMS 2 Website on {DateTime.Now:d MMMM yyyy}.";

    /// <param name="title">Page title (the &lt;h1&gt; is part of <paramref name="content"/>).</param>
    /// <param name="depth">0 for a page at the site root, 1 for a page inside a section folder.</param>
    /// <param name="current">The item this page shows, so the menu can mark it — null on index pages.</param>
    /// <param name="currentModule">The section this page belongs to — null on the course home page.</param>
    public string Render(string title, int depth, string content, SiteItem? current = null, SiteModule? currentModule = null)
    {
        var root = string.Concat(Enumerable.Repeat("../", depth));
        var sb = new StringBuilder(8192);

        sb.Append("<!DOCTYPE html>\n<html lang=\"en\" data-root=\"").Append(root).Append("\">\n<head>\n");
        sb.Append("<meta charset=\"utf-8\">\n<meta name=\"viewport\" content=\"width=device-width, initial-scale=1\">\n");
        // The mark that says this page was generated, not written by hand.
        sb.Append("<meta name=\"generator\" content=\"").Append(Html.Escape(L2W.Generator)).Append("\">\n");
        sb.Append("<title>").Append(Html.Escape(title == _course.Title ? title : $"{title} · {_course.Title}")).Append("</title>\n");
        sb.Append("<link rel=\"stylesheet\" href=\"").Append(root).Append(SiteAssets.StylesheetFileName).Append("\">\n");
        sb.Append("</head>\n<body>\n");
        sb.Append("<a class=\"skip\" href=\"#main\">Skip to content</a>\n");

        // ── header ──
        sb.Append("<header class=\"topbar\">\n");
        sb.Append("<button class=\"nav-toggle\" type=\"button\" aria-expanded=\"false\" aria-controls=\"sidebar\">Sections</button>\n");
        sb.Append("<a class=\"brand\" href=\"").Append(root).Append("index.html\">").Append(Html.Escape(_course.Title)).Append("</a>\n");
        sb.Append("<form class=\"search\" role=\"search\" onsubmit=\"return false\">");
        sb.Append("<input id=\"q\" type=\"search\" placeholder=\"Search this course\" aria-label=\"Search this course\" autocomplete=\"off\">");
        sb.Append("<div id=\"results\" class=\"results\" hidden></div></form>\n");
        sb.Append("</header>\n");

        sb.Append("<div class=\"layout\">\n");
        AppendSidebar(sb, root, current, currentModule);
        sb.Append("<main id=\"main\">\n");
        if (currentModule != null)
        {
            sb.Append("<nav class=\"crumbs\" aria-label=\"Breadcrumb\"><a href=\"").Append(root).Append("index.html\">Course home</a> › ");
            if (current == null)
                sb.Append(Html.Escape(currentModule.Title));
            else
                sb.Append("<a href=\"").Append(root).Append(ContentRewriter.EscapePath(currentModule.Slug)).Append("/index.html\">")
                  .Append(Html.Escape(currentModule.Title)).Append("</a>");
            sb.Append("</nav>\n");
        }
        sb.Append(content);
        if (current != null) AppendPager(sb, root, current);
        sb.Append("\n</main>\n</div>\n");

        sb.Append("<footer class=\"site-foot\">").Append(Html.Escape(FooterNote)).Append("</footer>\n");
        sb.Append("<script src=\"").Append(root).Append(SiteAssets.SearchIndexFileName).Append("\"></script>\n");
        sb.Append("<script src=\"").Append(root).Append(SiteAssets.ScriptFileName).Append("\"></script>\n");
        sb.Append("</body>\n</html>\n");
        return sb.ToString();
    }

    private void AppendSidebar(StringBuilder sb, string root, SiteItem? current, SiteModule? currentModule)
    {
        sb.Append("<nav id=\"sidebar\" class=\"sidebar\" aria-label=\"Course sections\">\n<ol>\n");
        foreach (var module in _course.Modules.Where(m => m.Include))
        {
            bool here = ReferenceEquals(module, currentModule);
            sb.Append("<li><a class=\"section-title\" href=\"").Append(root)
              .Append(ContentRewriter.EscapePath(module.Slug)).Append("/index.html\"");
            if (here && current == null) sb.Append(" aria-current=\"page\"");
            sb.Append('>').Append(Html.Escape(module.Title)).Append("</a>\n");

            if (here)
            {
                sb.Append("<ol class=\"items\">\n");
                foreach (var item in module.Published)
                {
                    sb.Append("<li><a href=\"").Append(ItemHref(item, root)).Append('"');
                    if (item.Kind == ItemKind.Link) sb.Append(" class=\"ext\" target=\"_blank\" rel=\"noopener\"");
                    if (ReferenceEquals(item, current)) sb.Append(" aria-current=\"page\"");
                    sb.Append('>').Append(Html.Escape(item.Title)).Append("</a></li>\n");
                }
                sb.Append("</ol>\n");
            }
            sb.Append("</li>\n");
        }
        sb.Append("</ol>\n</nav>\n");
    }

    private void AppendPager(StringBuilder sb, string root, SiteItem current)
    {
        int i = _order.IndexOf(current);
        if (i < 0) return;
        var prev = i > 0 ? _order[i - 1] : null;
        var next = i < _order.Count - 1 ? _order[i + 1] : null;
        if (prev == null && next == null) return;

        sb.Append("\n<nav class=\"pager\" aria-label=\"Previous and next\">\n");
        sb.Append(prev != null
            ? $"<a href=\"{ItemHref(prev, root)}\">← {Html.Escape(prev.Title)}</a>"
            : "<span></span>");
        sb.Append('\n');
        sb.Append(next != null
            ? $"<a href=\"{ItemHref(next, root)}\">{Html.Escape(next.Title)} →</a>"
            : "<span></span>");
        sb.Append("\n</nav>\n");
    }

    /// <summary>
    /// The href to reach an item from a page <paramref name="root"/> levels down: its page inside
    /// the site, or — for a web link, which has no page of its own — the address it points at.
    /// </summary>
    public static string ItemHref(SiteItem item, string root) =>
        item.Kind == ItemKind.Link
            ? Html.Escape(item.Url)
            : root + ContentRewriter.EscapePath(item.SiteHref);
}
