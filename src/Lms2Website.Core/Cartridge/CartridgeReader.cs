using Lms2Website.Core.Model;
using Lms2Website.Core.Site;

namespace Lms2Website.Core.Cartridge;

/// <summary>
/// Reads one .imscc and returns the <see cref="CourseSite"/> the preview shows and the site
/// builder writes: the organization tree becomes sections and items, every resource is
/// classified, and every payload (page HTML, quiz, assignment, discussion, link) is parsed here
/// — so the preview never lies about what the site will contain.
///
/// Nothing is written to disk; <see cref="Site.SiteBuilder"/> does that, re-opening the package.
/// </summary>
public static class CartridgeReader
{
    /// <summary>Extensions that are worth showing on a page of their own rather than a bare download.</summary>
    private static readonly HashSet<string> EmbeddableExtensions =
        new(StringComparer.OrdinalIgnoreCase) { ".pdf", ".mp4", ".webm", ".mp3", ".m4a", ".ogg", ".png", ".jpg", ".jpeg", ".gif", ".svg", ".webp" };

    public static CourseSite Read(string imsccPath, IProgress<string>? progress = null)
    {
        using var pkg = new CcPackage(imsccPath);
        return Read(pkg, progress);
    }

    public static CourseSite Read(CcPackage pkg, IProgress<string>? progress = null)
    {
        var warnings = new List<string>();
        var manifest = CcManifestReader.Read(pkg.ReadText(pkg.ManifestHref), warnings);

        var course = new CourseSite
        {
            Producer      = pkg.DetectProducer(),
            SchemaVersion = manifest.SchemaVersion,
            SourcePath    = pkg.Path,
            SourceBytes   = pkg.PackageBytes,
            Title         = string.IsNullOrWhiteSpace(manifest.Title)
                                ? Path.GetFileNameWithoutExtension(pkg.Path)
                                : manifest.Title,
            Description   = manifest.Description
        };

        if (course.Producer == CartridgeProducer.Canvas)
        {
            var (title, description) = ReadCanvasCourseSettings(pkg);
            if (!string.IsNullOrWhiteSpace(title)) course.Title = title;
            if (!string.IsNullOrWhiteSpace(description) && string.IsNullOrWhiteSpace(course.Description))
                course.Description = description;
        }

        var byId = manifest.Resources
            .GroupBy(r => r.Identifier, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);

        // Items that a page pulls in as a dependency (a lecture pptx under its page) are shown
        // with that page, not a second time as a section item of their own.
        var dependencyIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var r in manifest.Resources)
            if (IsHtmlResource(r, pkg))
                foreach (var d in r.Dependencies) dependencyIds.Add(d);

        var moduleSlugs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var loose = new SiteModule { Title = "Course materials" };

        foreach (var top in manifest.Items)
        {
            if (top.IsLeaf)
            {
                AddItem(loose, top, string.Empty, pkg, byId, dependencyIds, warnings, progress);
                continue;
            }

            var module = new SiteModule { Title = Display(top.Title, "Section") };
            Walk(top, string.Empty, module, pkg, byId, dependencyIds, warnings, progress);
            if (module.Items.Count == 0) continue;
            module.Slug = Slug.Unique($"{course.Modules.Count + 1:00}-{Slug.From(module.Title, "section")}", moduleSlugs);
            course.Modules.Add(module);
        }

        if (loose.Items.Count > 0)
        {
            loose.Slug = Slug.Unique($"{course.Modules.Count + 1:00}-course-materials", moduleSlugs);
            course.Modules.Add(loose);
        }

        // No organization at all: publish every resource in one section, in manifest order.
        if (course.Modules.Count == 0 && manifest.Resources.Count > 0)
        {
            var all = new SiteModule { Title = "Course materials", Slug = "01-course-materials" };
            foreach (var r in manifest.Resources)
            {
                if (dependencyIds.Contains(r.Identifier)) continue;
                var item = BuildItem(r, Display(r.Title, Path.GetFileNameWithoutExtension(r.Href)), pkg, byId, warnings, progress);
                if (item != null) all.Items.Add(item);
            }
            if (all.Items.Count > 0) course.Modules.Add(all);
        }

        AssignSlugs(course);
        course.Warnings.AddRange(warnings);
        return course;
    }

    // ── walking the organization ──────────────────────────────────────────────

    private static void Walk(
        CcOrgItem folder, string section, SiteModule module, CcPackage pkg,
        Dictionary<string, CcResource> byId, HashSet<string> dependencyIds,
        List<string> warnings, IProgress<string>? progress)
    {
        foreach (var child in folder.Children)
        {
            if (child.IsLeaf)
            {
                AddItem(module, child, section, pkg, byId, dependencyIds, warnings, progress);
            }
            else
            {
                var deeper = string.IsNullOrEmpty(section)
                    ? Display(child.Title, string.Empty)
                    : $"{section} › {Display(child.Title, string.Empty)}";
                Walk(child, deeper.Trim(' ', '›'), module, pkg, byId, dependencyIds, warnings, progress);
            }
        }
    }

    private static void AddItem(
        SiteModule module, CcOrgItem leaf, string section, CcPackage pkg,
        Dictionary<string, CcResource> byId, HashSet<string> dependencyIds,
        List<string> warnings, IProgress<string>? progress)
    {
        if (leaf.IdentifierRef == null || !byId.TryGetValue(leaf.IdentifierRef, out var resource)) return;
        if (dependencyIds.Contains(resource.Identifier)) return;

        var item = BuildItem(resource, Display(leaf.Title, resource.Title), pkg, byId, warnings, progress);
        if (item == null) return;
        item.Section = section;
        module.Items.Add(item);
    }

    // ── one resource → one item ───────────────────────────────────────────────

    private static SiteItem? BuildItem(
        CcResource resource, string title, CcPackage pkg,
        Dictionary<string, CcResource> byId, List<string> warnings, IProgress<string>? progress)
    {
        var item = new SiteItem
        {
            Title      = string.IsNullOrWhiteSpace(title) ? "Untitled" : title,
            ResourceId = resource.Identifier,
            SourceHref = resource.Href
        };
        progress?.Report(item.Title);

        var type = resource.Type ?? string.Empty;

        if (type.StartsWith("imsqti", StringComparison.OrdinalIgnoreCase))
            return ReadQuiz(item, resource, pkg, warnings);

        if (type.StartsWith("assignment", StringComparison.OrdinalIgnoreCase))
            return ReadPrompt(item, resource, pkg, warnings, discussion: false);

        if (type.StartsWith("imsdt", StringComparison.OrdinalIgnoreCase))
            return ReadPrompt(item, resource, pkg, warnings, discussion: true);

        if (type.StartsWith("imswl", StringComparison.OrdinalIgnoreCase))
            return ReadLink(item, resource, pkg, warnings, lti: false);

        if (type.StartsWith("imsbasiclti", StringComparison.OrdinalIgnoreCase))
            return ReadLink(item, resource, pkg, warnings, lti: true);

        if (type.StartsWith("webcontent", StringComparison.OrdinalIgnoreCase) ||
            type.StartsWith("associatedcontent", StringComparison.OrdinalIgnoreCase) ||
            type.Length == 0)
            return ReadWebContent(item, resource, pkg, byId, warnings);

        item.Kind = ItemKind.Unsupported;
        item.Notes.Add($"cartridge type {type}");
        warnings.Add($"\"{item.Title}\" is a {type} resource, which has no website equivalent — left out.");
        return item;
    }

    private static SiteItem ReadWebContent(
        SiteItem item, CcResource resource, CcPackage pkg,
        Dictionary<string, CcResource> byId, List<string> warnings)
    {
        if (!pkg.Exists(resource.Href))
        {
            item.Kind = ItemKind.Unsupported;
            item.Notes.Add("file missing from the cartridge");
            warnings.Add($"\"{item.Title}\": {resource.Href} is named in the manifest but is not in the package.");
            return item;
        }

        if (IsHtmlResource(resource, pkg))
        {
            item.Kind = ItemKind.Page;
            item.BodyHtml = pkg.ReadText(resource.Href);

            // Files the page declares as dependencies, plus other files listed on the resource.
            foreach (var depId in resource.Dependencies)
            {
                if (!byId.TryGetValue(depId, out var dep)) continue;
                foreach (var f in dep.Files) AddAttachment(item, f, pkg);
            }
            foreach (var f in resource.Files.Skip(1)) AddAttachment(item, f, pkg);

            if (item.Attachments.Count > 0)
                item.Notes.Add($"{item.Attachments.Count} attachment{(item.Attachments.Count == 1 ? "" : "s")}");
            return item;
        }

        item.Kind = ItemKind.File;
        var bytes = pkg.SizeOf(resource.Href);
        AddAttachment(item, resource.Href, pkg);
        item.Notes.Add(Path.GetExtension(resource.Href).TrimStart('.').ToUpperInvariant());
        item.Notes.Add(Html.FileSize(bytes));
        return item;
    }

    private static SiteItem ReadQuiz(SiteItem item, CcResource resource, CcPackage pkg, List<string> warnings)
    {
        item.Kind = ItemKind.Quiz;
        var href = resource.Files.FirstOrDefault(f => f.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)) ?? resource.Href;
        if (!pkg.Exists(href))
        {
            item.Kind = ItemKind.Unsupported;
            item.Notes.Add("quiz file missing");
            warnings.Add($"Quiz \"{item.Title}\": {href} is not in the package.");
            return item;
        }

        item.Quiz = QtiReader.Read(pkg.ReadText(href), item.Title);
        if (!string.IsNullOrWhiteSpace(item.Quiz.Title)) item.Title = item.Quiz.Title;
        item.Notes.Add($"{item.Quiz.Questions.Count} question{(item.Quiz.Questions.Count == 1 ? "" : "s")}");
        if (item.Quiz.IsQuestionBank) item.Notes.Add("question bank");
        foreach (var w in item.Quiz.Warnings) warnings.Add($"Quiz \"{item.Title}\": {w}");
        return item;
    }

    private static SiteItem ReadPrompt(SiteItem item, CcResource resource, CcPackage pkg, List<string> warnings, bool discussion)
    {
        var href = resource.Files.FirstOrDefault(f => f.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)) ?? resource.Href;
        if (!pkg.Exists(href))
        {
            item.Kind = ItemKind.Unsupported;
            warnings.Add($"\"{item.Title}\": {href} is not in the package.");
            return item;
        }

        var xml = pkg.ReadText(href);
        var prompt = discussion ? CcResourceReaders.ReadDiscussion(xml) : CcResourceReaders.ReadAssignment(xml);
        if (prompt == null)
        {
            item.Kind = ItemKind.Unsupported;
            item.Notes.Add("could not be read");
            warnings.Add($"\"{item.Title}\": the {(discussion ? "discussion" : "assignment")} XML was not in the expected shape.");
            return item;
        }

        item.Kind = discussion ? ItemKind.Discussion : ItemKind.Assignment;
        if (!string.IsNullOrWhiteSpace(prompt.Title)) item.Title = prompt.Title;
        item.BodyHtml = prompt.BodyHtml;
        item.Points = prompt.Points;
        item.SubmissionFormats.AddRange(prompt.SubmissionFormats);
        if (prompt.Points is { } pts) item.Notes.Add($"{pts:0.##} points");

        // Attached files (a rubric, a starter document) listed beside the XML.
        foreach (var f in resource.Files.Where(f => !f.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)))
            AddAttachment(item, f, pkg);
        return item;
    }

    private static SiteItem ReadLink(SiteItem item, CcResource resource, CcPackage pkg, List<string> warnings, bool lti)
    {
        var href = resource.Files.FirstOrDefault(f => f.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)) ?? resource.Href;
        if (!pkg.Exists(href))
        {
            item.Kind = ItemKind.Unsupported;
            warnings.Add($"\"{item.Title}\": {href} is not in the package.");
            return item;
        }

        var xml = pkg.ReadText(href);
        var link = lti ? CcResourceReaders.ReadLti(xml) : CcResourceReaders.ReadWebLink(xml);
        if (link == null || string.IsNullOrWhiteSpace(link.Url))
        {
            item.Kind = ItemKind.Unsupported;
            item.Notes.Add("no address");
            warnings.Add($"\"{item.Title}\": the link resource has no URL.");
            return item;
        }

        item.Kind = ItemKind.Link;
        if (!string.IsNullOrWhiteSpace(link.Title)) item.Title = link.Title;
        item.Url = link.Url;
        item.BodyHtml = link.Description;
        if (link.IsLti) item.Notes.Add("LTI tool — will need a sign-in");
        return item;
    }

    private static void AddAttachment(SiteItem item, string href, CcPackage pkg)
    {
        var canonical = pkg.Canonical(href);
        if (canonical == null) return;
        if (canonical.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
            canonical.EndsWith(".htm", StringComparison.OrdinalIgnoreCase)) return;
        if (item.Attachments.Any(a => a.SourceHref == canonical)) return;

        item.Attachments.Add(new SiteAsset
        {
            SourceHref = canonical,
            SiteHref   = string.Empty,   // the builder assigns this
            Bytes      = pkg.SizeOf(canonical)
        });
    }

    // ── slugs ─────────────────────────────────────────────────────────────────

    private static void AssignSlugs(CourseSite course)
    {
        foreach (var module in course.Modules)
        {
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "index" };
            foreach (var item in module.Items)
            {
                if (item.Kind == ItemKind.Unsupported) continue;
                if (item.Kind == ItemKind.Link)
                {
                    // A link has no page of its own — the menus point straight at the address.
                    item.SiteHref = item.Url;
                    continue;
                }
                var slug = Slug.Unique(Slug.From(item.Title, "item"), used);
                item.Slug = slug + ".html";
                item.SiteHref = $"{module.Slug}/{item.Slug}";
            }
        }
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static bool IsHtmlResource(CcResource resource, CcPackage pkg)
    {
        if (!resource.Type.StartsWith("webcontent", StringComparison.OrdinalIgnoreCase) &&
            resource.Type.Length != 0) return false;
        var href = pkg.Canonical(resource.Href) ?? resource.Href;
        // D2L appends ";Display Name.html" to an entry, so test the whole string, not Path.GetExtension.
        return href.EndsWith(".html", StringComparison.OrdinalIgnoreCase) ||
               href.EndsWith(".htm", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>The title to show: the org item's, else the resource's, with a trailing ".html" dropped.</summary>
    private static string Display(string? title, string? fallback)
    {
        var t = (title ?? string.Empty).Trim();
        if (t.Length == 0) t = (fallback ?? string.Empty).Trim();
        foreach (var ext in new[] { ".html", ".htm" })
            if (t.EndsWith(ext, StringComparison.OrdinalIgnoreCase))
                t = t[..^ext.Length].Trim();
        return t;
    }

    /// <summary>Canvas keeps the course title and description in course_settings/course_settings.xml.</summary>
    private static (string Title, string Description) ReadCanvasCourseSettings(CcPackage pkg)
    {
        const string href = "course_settings/course_settings.xml";
        if (!pkg.Exists(href)) return (string.Empty, string.Empty);
        try
        {
            var doc = System.Xml.Linq.XDocument.Parse(pkg.ReadText(href));
            string Value(string name) => doc.Descendants()
                .FirstOrDefault(e => e.Name.LocalName == name)?.Value.Trim() ?? string.Empty;
            var title = Value("title");
            if (title.Length == 0) title = Value("course_name");
            return (title, Value("public_description"));
        }
        catch (System.Xml.XmlException) { return (string.Empty, string.Empty); }
    }

    /// <summary>True when a file can be shown in the browser rather than only downloaded.</summary>
    public static bool CanEmbed(string fileName) =>
        EmbeddableExtensions.Contains(Path.GetExtension(fileName));
}
