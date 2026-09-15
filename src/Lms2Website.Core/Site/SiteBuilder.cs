using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Lms2Website.Core.Cartridge;
using Lms2Website.Core.Model;

namespace Lms2Website.Core.Site;

public sealed class SiteBuildOptions
{
    /// <summary>Folder the site is written into. Its contents are replaced.</summary>
    public required string OutputFolder { get; init; }
    /// <summary>Line at the foot of every page; the default names the tool and the date.</summary>
    public string? FooterNote { get; init; }
}

public sealed class SiteBuildResult
{
    public string OutputFolder { get; init; } = string.Empty;
    public string IndexPath    { get; init; } = string.Empty;
    public int PagesWritten    { get; set; }
    public int FilesCopied     { get; set; }
    public long Bytes          { get; set; }
    public TimeSpan Elapsed    { get; set; }
    public List<string> Warnings { get; } = new();
}

/// <summary>
/// Writes a <see cref="CourseSite"/> to a folder of plain HTML: one page per item, a page per
/// section, a course home page, the shared stylesheet and script, and every file the cartridge
/// carried. Nothing in the output needs a server, so the same folder opens from disk and works
/// on GitHub Pages under a project path.
///
/// Layout:
/// <code>
///   index.html                  course home
///   assets/site.css, site.js, search-index.js
///   NN-section/index.html       one section
///   NN-section/item.html        one page, quiz, assignment, discussion or file
///   files/…                     everything the cartridge carried
///   .nojekyll                   so GitHub Pages serves the folder verbatim
/// </code>
/// </summary>
public static class SiteBuilder
{
    public static SiteBuildResult Build(
        CourseSite course,
        SiteBuildOptions options,
        IProgress<(int Percent, string Message)>? progress = null,
        CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var output = Path.GetFullPath(options.OutputFolder);
        var result = new SiteBuildResult
        {
            OutputFolder = output,
            IndexPath    = Path.Combine(output, "index.html")
        };

        PrepareFolder(output);
        Directory.CreateDirectory(Path.Combine(output, "assets"));

        using var pkg = new CcPackage(course.SourcePath);

        var template = new PageTemplate(course)
        {
            FooterNote = options.FooterNote ??
                $"Converted from an LMS export with LMS 2 Website on {DateTime.Now:d MMMM yyyy}."
        };

        // zip href → the page that publishes it, so a link between two cartridge pages survives.
        var pageByZipHref = new Dictionary<string, SiteItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in course.PublishedItems)
        {
            var canonical = pkg.Canonical(item.SourceHref);
            if (item.Kind == ItemKind.Page && canonical != null) pageByZipHref[canonical] = item;
        }

        var copied = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);   // zip href → site href
        var usedFileNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        string? Resolve(string zipHref)
        {
            var canonical = pkg.Canonical(zipHref);
            if (canonical == null) return null;
            if (pageByZipHref.TryGetValue(canonical, out var page)) return page.SiteHref;
            if (copied.TryGetValue(canonical, out var already)) return already;

            var name = Slug.Unique(Slug.FileName(Path.GetFileName(canonical.TrimEnd('/')), "file"), usedFileNames);
            var siteHref = "files/" + name;
            var destination = Path.Combine(output, "files", name);
            pkg.ExtractTo(canonical, destination);
            copied[canonical] = siteHref;
            result.FilesCopied++;
            result.Bytes += new FileInfo(destination).Length;
            return siteHref;
        }

        var search = new List<SearchEntry>();
        var modules = course.Modules.Where(m => m.Include).ToList();
        int done = 0, total = Math.Max(1, course.PublishedCount + modules.Count + 1);

        foreach (var module in modules)
        {
            ct.ThrowIfCancellationRequested();
            var moduleDir = Path.Combine(output, module.Slug);
            Directory.CreateDirectory(moduleDir);

            foreach (var item in module.Published)
            {
                ct.ThrowIfCancellationRequested();
                progress?.Report((100 * done++ / total, item.Title));
                if (item.Kind == ItemKind.Link) continue;   // no page of its own

                var body = RenderItem(item, module, course, Resolve, result, search);
                var html = template.Render(item.Title, depth: 1, body, item, module);
                File.WriteAllText(Path.Combine(moduleDir, item.Slug), html, new UTF8Encoding(false));
                result.PagesWritten++;
            }

            progress?.Report((100 * done++ / total, module.Title));
            var moduleHtml = template.Render(module.Title, depth: 1, RenderModuleIndex(module), null, module);
            File.WriteAllText(Path.Combine(moduleDir, "index.html"), moduleHtml, new UTF8Encoding(false));
            result.PagesWritten++;
            search.Add(new SearchEntry(module.Slug + "/index.html", module.Title, "Section", string.Empty, module.Summary()));
        }

        progress?.Report((100 * done / total, "Course home"));
        File.WriteAllText(result.IndexPath,
            template.Render(course.Title, depth: 0, RenderHome(course)), new UTF8Encoding(false));
        result.PagesWritten++;

        File.WriteAllText(Path.Combine(output, SiteAssets.StylesheetFileName.Replace('/', Path.DirectorySeparatorChar)),
            SiteAssets.Stylesheet, new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(output, SiteAssets.ScriptFileName.Replace('/', Path.DirectorySeparatorChar)),
            SiteAssets.Script, new UTF8Encoding(false));
        File.WriteAllText(Path.Combine(output, SiteAssets.SearchIndexFileName.Replace('/', Path.DirectorySeparatorChar)),
            "window.SITE_SEARCH = " + JsonSerializer.Serialize(search, SearchJson) + ";\n", new UTF8Encoding(false));

        // GitHub Pages runs Jekyll by default, which hides folders that start with an underscore.
        File.WriteAllText(Path.Combine(output, ".nojekyll"), string.Empty);
        File.WriteAllText(Path.Combine(output, "README.md"), Readme(course, result), new UTF8Encoding(false));

        result.Warnings.AddRange(course.Warnings);
        result.Elapsed = stopwatch.Elapsed;
        progress?.Report((100, "Done"));
        return result;
    }

    // ── item pages ────────────────────────────────────────────────────────────

    private static string RenderItem(
        SiteItem item, SiteModule module, CourseSite course,
        Func<string, string?> resolve, SiteBuildResult result, List<SearchEntry> search)
    {
        var sb = new StringBuilder(4096);
        sb.Append("<span class=\"badge ").Append(item.Kind.ToString().ToLowerInvariant()).Append("\">")
          .Append(Html.Escape(item.Kind.Label())).Append("</span>\n");
        sb.Append("<h1>").Append(Html.Escape(item.Title)).Append("</h1>\n");

        string searchBody;
        switch (item.Kind)
        {
            case ItemKind.Quiz:
                searchBody = RenderQuiz(item, module, sb, resolve, result);
                break;
            case ItemKind.File:
                searchBody = RenderFile(item, sb, resolve, result);
                break;
            default:
                searchBody = RenderBody(item, module, sb, resolve, result);
                break;
        }

        AppendAttachments(item, sb, resolve, result, skipPrimary: item.Kind == ItemKind.File);
        search.Add(new SearchEntry(item.SiteHref, item.Title, item.Kind.Label(), module.Title, Html.Snippet(searchBody, 800)));
        return sb.ToString();
    }

    private static string RenderBody(
        SiteItem item, SiteModule module, StringBuilder sb,
        Func<string, string?> resolve, SiteBuildResult result)
    {
        if (item.Points is { } points)
            sb.Append("<p class=\"lede\">").Append(Points(points));
        if (item.Points is not null && item.SubmissionFormats.Count > 0)
            sb.Append(" · submitted as ").Append(Html.Escape(string.Join(", ", item.SubmissionFormats)));
        if (item.Points is not null) sb.Append("</p>\n");

        if (item.Kind is ItemKind.Assignment or ItemKind.Discussion)
            sb.Append("<div class=\"note\">This is a copy of the ")
              .Append(item.Kind == ItemKind.Discussion ? "discussion prompt" : "assignment")
              .Append(" for reading. Work is still handed in through the course in the LMS.</div>\n");

        var rewritten = ContentRewriter.Clean(item.BodyHtml, item.SourceHref, module.Slug + "/", resolve, item.Title);
        sb.Append("<div class=\"content\">\n").Append(rewritten.BodyHtml).Append("\n</div>\n");
        Record(rewritten, item, result);
        return Html.ToPlainText(rewritten.BodyHtml);
    }

    private static string RenderFile(SiteItem item, StringBuilder sb, Func<string, string?> resolve, SiteBuildResult result)
    {
        var asset = item.Attachments.FirstOrDefault();
        var siteHref = asset == null ? null : resolve(asset.SourceHref);
        if (siteHref == null)
        {
            sb.Append("<div class=\"note warn\">This file is named in the course but is not in the export.</div>\n");
            result.Warnings.Add($"\"{item.Title}\": the file is not in the cartridge.");
            return item.Title;
        }

        var name = Path.GetFileName(asset!.SourceHref);
        var href = ContentRewriter.Relative(item.SiteHref[..(item.SiteHref.LastIndexOf('/') + 1)], siteHref);
        sb.Append("<p class=\"lede\">").Append(Html.Escape(name)).Append(" · ").Append(Html.FileSize(asset.Bytes)).Append("</p>\n");
        sb.Append("<p><a href=\"").Append(href).Append("\" download>Download this file</a></p>\n");

        var extension = Path.GetExtension(name).ToLowerInvariant();
        if (extension == ".pdf")
            sb.Append("<iframe class=\"embed\" src=\"").Append(href).Append("\" title=\"")
              .Append(Html.Escape(item.Title)).Append("\"></iframe>\n");
        else if (extension is ".png" or ".jpg" or ".jpeg" or ".gif" or ".svg" or ".webp")
            sb.Append("<p><img src=\"").Append(href).Append("\" alt=\"").Append(Html.Escape(item.Title)).Append("\"></p>\n");
        else if (extension is ".mp4" or ".webm")
            sb.Append("<video class=\"embed\" controls src=\"").Append(href).Append("\"></video>\n");
        else if (extension is ".mp3" or ".m4a" or ".ogg")
            sb.Append("<p><audio controls src=\"").Append(href).Append("\"></audio></p>\n");

        return item.Title + " " + name;
    }

    private static string RenderQuiz(
        SiteItem item, SiteModule module, StringBuilder sb,
        Func<string, string?> resolve, SiteBuildResult result)
    {
        var quiz = item.Quiz!;
        var text = new StringBuilder();
        var missingImages = 0;

        // Question HTML carries images and links of its own, and they move with the site.
        string Fix(string html)
        {
            if (string.IsNullOrWhiteSpace(html)) return string.Empty;
            var rewritten = ContentRewriter.Clean(html, item.SourceHref, module.Slug + "/", resolve);
            missingImages += rewritten.BrokenReferences.Count;
            return rewritten.BodyHtml;
        }

        sb.Append("<div class=\"note\">These are the questions as the course exported them, with the answers the cartridge records. It is a copy for reading — nothing here is graded.</div>\n");
        if (!string.IsNullOrWhiteSpace(quiz.DescriptionHtml))
            sb.Append("<div class=\"content\">").Append(Fix(quiz.DescriptionHtml)).Append("</div>\n");

        int n = 0;
        foreach (var q in quiz.Questions)
        {
            n++;
            sb.Append("<section class=\"question\">\n<div class=\"q-head\"><strong>Question ").Append(n).Append("</strong>");
            sb.Append(" · ").Append(Html.Escape(q.TypeLabel));
            if (q.Points is { } pts) sb.Append(" · ").Append(Points(pts));
            sb.Append("</div>\n");
            sb.Append("<div class=\"content\">").Append(Fix(q.PromptHtml)).Append("</div>\n");
            text.Append(Html.ToPlainText(q.PromptHtml)).Append(' ');

            if (q.Choices.Count > 0)
            {
                sb.Append("<ol class=\"choices\">\n");
                foreach (var choice in q.Choices)
                {
                    sb.Append(choice.IsCorrect ? "<li class=\"correct\">" : "<li>").Append(Fix(choice.TextHtml));
                    if (choice.IsCorrect) sb.Append("<span class=\"mark\" title=\"Correct answer\">✓</span>");
                    if (!string.IsNullOrWhiteSpace(choice.Feedback))
                        sb.Append("<div class=\"feedback\">").Append(Fix(choice.Feedback)).Append("</div>");
                    sb.Append("</li>\n");
                }
                sb.Append("</ol>\n");
            }
            else if (q.Answers.Count > 0)
            {
                sb.Append("<p class=\"answer\"><strong>Answer:</strong> ")
                  .Append(Html.Escape(string.Join("; ", q.Answers.Select(Html.ToPlainText)))).Append("</p>\n");
            }

            if (q.HasNoMarkedAnswer && q.Answers.Count == 0)
                sb.Append("<p class=\"answer\">The export does not record an answer for this question.</p>\n");
            if (!string.IsNullOrWhiteSpace(q.Feedback))
                sb.Append("<div class=\"feedback\">").Append(Fix(q.Feedback)).Append("</div>\n");
            sb.Append("</section>\n");
        }

        if (quiz.Questions.Count == 0)
            sb.Append("<div class=\"note warn\">No questions could be read out of this quiz.</div>\n");
        if (missingImages > 0)
            result.Warnings.Add($"Quiz \"{item.Title}\": {missingImages} image(s) the questions use are not in the export — the page says so where they were.");

        return text.ToString();
    }

    private static void AppendAttachments(
        SiteItem item, StringBuilder sb, Func<string, string?> resolve, SiteBuildResult result, bool skipPrimary)
    {
        var attachments = skipPrimary ? item.Attachments.Skip(1).ToList() : item.Attachments;
        if (attachments.Count == 0) return;

        var fromDir = item.SiteHref[..(item.SiteHref.LastIndexOf('/') + 1)];
        var rows = new List<string>();
        foreach (var asset in attachments)
        {
            var siteHref = resolve(asset.SourceHref);
            if (siteHref == null)
            {
                result.Warnings.Add($"\"{item.Title}\": attachment {asset.SourceHref} is not in the cartridge.");
                continue;
            }
            rows.Add($"<li><a href=\"{ContentRewriter.Relative(fromDir, siteHref)}\" download>" +
                     $"{Html.Escape(Path.GetFileName(asset.SourceHref))}</a> " +
                     $"<span class=\"size\">{Html.FileSize(asset.Bytes)}</span></li>");
        }
        if (rows.Count == 0) return;

        sb.Append("<h2>Files</h2>\n<ul class=\"files\">\n").Append(string.Join('\n', rows)).Append("\n</ul>\n");
    }

    private static void Record(RewriteResult rewritten, SiteItem item, SiteBuildResult result)
    {
        foreach (var broken in rewritten.BrokenReferences.Distinct())
            result.Warnings.Add($"\"{item.Title}\": the link to {broken} is not in the cartridge — left as it was.");
        if (rewritten.LmsLinkCount > 0)
            result.Warnings.Add($"\"{item.Title}\": {rewritten.LmsLinkCount} link(s) point back into the LMS and will ask a visitor to sign in.");
    }

    // ── index pages ───────────────────────────────────────────────────────────

    private static string RenderModuleIndex(SiteModule module)
    {
        var sb = new StringBuilder(2048);
        sb.Append("<h1>").Append(Html.Escape(module.Title)).Append("</h1>\n");
        var summary = module.Summary();
        if (summary.Length > 0) sb.Append("<p class=\"lede\">").Append(Html.Escape(summary)).Append("</p>\n");

        string? section = null;
        sb.Append("<ul class=\"items-list\">\n");
        foreach (var item in module.Published)
        {
            if (item.Section != section)
            {
                section = item.Section;
                if (!string.IsNullOrEmpty(section))
                    sb.Append("</ul>\n<h2 class=\"section-head\">").Append(Html.Escape(section)).Append("</h2>\n<ul class=\"items-list\">\n");
            }
            // The section index sits beside its items, so an item is just its file name.
            var href = item.Kind == ItemKind.Link ? Html.Escape(item.Url) : Uri.EscapeDataString(item.Slug);
            sb.Append("<li><a href=\"").Append(href).Append('"');
            if (item.Kind == ItemKind.Link) sb.Append(" target=\"_blank\" rel=\"noopener\"");
            sb.Append("><span class=\"t\">").Append(Html.Escape(item.Title)).Append("</span>");
            sb.Append("<span class=\"m\">").Append(Html.Escape(item.Describe())).Append("</span></a></li>\n");
        }
        sb.Append("</ul>\n");
        return sb.ToString();
    }

    private static string RenderHome(CourseSite course)
    {
        var sb = new StringBuilder(2048);
        sb.Append("<h1>").Append(Html.Escape(course.Title)).Append("</h1>\n");
        if (!string.IsNullOrWhiteSpace(course.Description))
            sb.Append("<div class=\"content\">").Append(course.Description).Append("</div>\n");
        sb.Append("<p class=\"lede\">").Append(Html.Escape(course.Summary())).Append("</p>\n");

        sb.Append("<ul class=\"cards\">\n");
        foreach (var module in course.Modules.Where(m => m.Include))
        {
            sb.Append("<li class=\"card\"><a href=\"").Append(ContentRewriter.EscapePath(module.Slug)).Append("/index.html\">")
              .Append(Html.Escape(module.Title)).Append("</a>");
            var summary = module.Summary();
            if (summary.Length > 0) sb.Append("<p>").Append(Html.Escape(summary)).Append("</p>");
            sb.Append("</li>\n");
        }
        sb.Append("</ul>\n");
        return sb.ToString();
    }

    // ── plumbing ──────────────────────────────────────────────────────────────

    /// <summary>"1 point", "2.5 points".</summary>
    private static string Points(decimal points) => $"{points:0.##} point{(points == 1m ? "" : "s")}";

    private sealed record SearchEntry(string u, string t, string k, string s, string b);

    private static readonly JsonSerializerOptions SearchJson = new()
    {
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>
    /// Empties the output folder, keeping a .git directory so a published site can be re-built in
    /// place without losing its history.
    /// </summary>
    private static void PrepareFolder(string output)
    {
        Directory.CreateDirectory(output);
        foreach (var dir in Directory.GetDirectories(output))
        {
            if (Path.GetFileName(dir).Equals(".git", StringComparison.OrdinalIgnoreCase)) continue;
            Directory.Delete(dir, recursive: true);
        }
        foreach (var file in Directory.GetFiles(output))
            File.Delete(file);
    }

    private static string Readme(CourseSite course, SiteBuildResult result) =>
        $"""
        # {course.Title}

        A static website built from `{Path.GetFileName(course.SourcePath)}`
        ({course.Producer} export) by **LMS 2 Website** on {DateTime.Now:d MMMM yyyy}.

        - Open `index.html` to read it locally — no server needed.
        - On GitHub Pages, serve this folder from the branch root.
        - {result.PagesWritten} pages, {result.FilesCopied} files.

        Re-running the conversion replaces every file here except `.git`.
        """;
}
