namespace Lms2Website.Core.Model;

/// <summary>What a cartridge item becomes on the website.</summary>
public enum ItemKind
{
    /// <summary>An HTML page from the cartridge — becomes a content page.</summary>
    Page = 0,
    /// <summary>Any other file (pptx, pdf, docx…) — becomes a download link, and a page when it can be embedded.</summary>
    File,
    /// <summary>A web link or LTI launch — becomes an outbound link.</summary>
    Link,
    /// <summary>A QTI assessment — becomes a read-only question page with the answer key.</summary>
    Quiz,
    /// <summary>A CC assignment — becomes a read-only page with the instructions.</summary>
    Assignment,
    /// <summary>A discussion topic — becomes a read-only page with the prompt.</summary>
    Discussion,
    /// <summary>Recognised, but nothing publishable came out of it (kept so the count adds up).</summary>
    Unsupported
}

public static class ItemKindInfo
{
    public static string Label(this ItemKind k) => k switch
    {
        ItemKind.Page        => "Page",
        ItemKind.File        => "File",
        ItemKind.Link        => "Link",
        ItemKind.Quiz        => "Quiz",
        ItemKind.Assignment  => "Assignment",
        ItemKind.Discussion  => "Discussion",
        _                    => "Not published"
    };

    /// <summary>Plural, for counts in the preview ("3 pages").</summary>
    public static string Plural(this ItemKind k) => k switch
    {
        ItemKind.Page        => "pages",
        ItemKind.File        => "files",
        ItemKind.Link        => "links",
        ItemKind.Quiz        => "quizzes",
        ItemKind.Assignment  => "assignments",
        ItemKind.Discussion  => "discussions",
        _                    => "not published"
    };
}

/// <summary>Which LMS wrote the cartridge. Affects quirk handling and the preview note only.</summary>
public enum CartridgeProducer
{
    Unknown = 0,
    Brightspace,
    Canvas,
    Moodle,
    Blackboard,
    Schoology,
    PreseMaker
}

/// <summary>
/// A file inside the cartridge that the site needs to carry: a page's image, a stylesheet,
/// an attached document. <see cref="SourceHref"/> is the zip-relative entry;
/// <see cref="SiteHref"/> is where it lands in the site (root-relative, e.g. "files/lecture.pptx").
/// </summary>
public sealed class SiteAsset
{
    public required string SourceHref { get; init; }
    public required string SiteHref   { get; init; }
    public long Bytes { get; set; }
}

/// <summary>One question of a quiz, read-only: the stem, its choices, and which are correct.</summary>
public sealed class QuizQuestion
{
    public string Title       { get; set; } = string.Empty;
    /// <summary>The stem, as HTML (the cartridge's own markup, cleaned).</summary>
    public string PromptHtml  { get; set; } = string.Empty;
    /// <summary>"Multiple choice", "True/False", "Multi-select", "Short answer", "Essay", "Matching".</summary>
    public string TypeLabel   { get; set; } = "Question";
    public List<QuizChoice> Choices { get; } = new();
    /// <summary>Accepted text answers (short answer) or the answer key (essay); empty when there are choices.</summary>
    public List<string> Answers { get; } = new();
    /// <summary>General feedback shown with the answer.</summary>
    public string Feedback { get; set; } = string.Empty;
    public decimal? Points { get; set; }
    /// <summary>True when nothing in the QTI marked an answer correct — the page says so rather than guessing.</summary>
    public bool HasNoMarkedAnswer => Choices.Count > 0 && !Choices.Exists(c => c.IsCorrect)
                                     || Choices.Count == 0 && Answers.Count == 0;
}

public sealed class QuizChoice
{
    public string TextHtml { get; set; } = string.Empty;
    public bool   IsCorrect { get; set; }
    public string Feedback { get; set; } = string.Empty;
}

/// <summary>A quiz as the website shows it: the questions, in order, with the key.</summary>
public sealed class QuizContent
{
    public string Title { get; set; } = string.Empty;
    /// <summary>The assessment's own instructions / description, as HTML.</summary>
    public string DescriptionHtml { get; set; } = string.Empty;
    public List<QuizQuestion> Questions { get; } = new();
    public bool IsQuestionBank { get; set; }
    public List<string> Warnings { get; } = new();
}

/// <summary>
/// One publishable thing in a module: a page, a file, a link, a quiz, an assignment or a
/// discussion. The builder turns each into exactly one URL under the module folder
/// (a File item links to its download; everything else gets an HTML page).
/// </summary>
public sealed class SiteItem
{
    public ItemKind Kind { get; set; } = ItemKind.Page;
    public string Title { get; set; } = string.Empty;
    /// <summary>File name of this item's page inside the module folder ("overview-of-topics.html").</summary>
    public string Slug  { get; set; } = string.Empty;
    /// <summary>Manifest resource identifier (blank for a synthetic item).</summary>
    public string ResourceId { get; set; } = string.Empty;
    /// <summary>Sub-folder heading inside the module, "" when the item sits directly in the module.</summary>
    public string Section { get; set; } = string.Empty;

    /// <summary>Zip-relative href of the item's primary file.</summary>
    public string SourceHref { get; set; } = string.Empty;
    /// <summary>Page / assignment / discussion body, as HTML taken from the cartridge (not yet rewritten).</summary>
    public string BodyHtml { get; set; } = string.Empty;
    /// <summary>Outbound URL for <see cref="ItemKind.Link"/>.</summary>
    public string Url { get; set; } = string.Empty;
    /// <summary>Points possible, when the cartridge states them.</summary>
    public decimal? Points { get; set; }
    /// <summary>Accepted submission formats for an assignment.</summary>
    public List<string> SubmissionFormats { get; } = new();

    public QuizContent? Quiz { get; set; }

    /// <summary>Downloadable files that belong to this item (the file itself, or a page's attachments).</summary>
    public List<SiteAsset> Attachments { get; } = new();

    /// <summary>Notes shown in the preview ("12 questions", "3.4 MB", "link is external").</summary>
    public List<string> Notes { get; } = new();

    /// <summary>User choice in the preview — unchecked items are left out of the site.</summary>
    public bool Include { get; set; } = true;

    /// <summary>Site-root-relative URL of this item, filled in by the builder.</summary>
    public string SiteHref { get; set; } = string.Empty;

    public string Describe()
    {
        var parts = new List<string> { Kind.Label() };
        parts.AddRange(Notes);
        return string.Join(" · ", parts);
    }
}

/// <summary>A top-level folder of the cartridge organization — one section of the website.</summary>
public sealed class SiteModule
{
    public string Title { get; set; } = string.Empty;
    /// <summary>Folder name in the site ("02-week-1-welcome").</summary>
    public string Slug  { get; set; } = string.Empty;
    public List<SiteItem> Items { get; } = new();
    public bool Include { get; set; } = true;

    public IEnumerable<SiteItem> Published =>
        Items.Where(i => i.Include && i.Kind != ItemKind.Unsupported);

    public string Summary()
    {
        var groups = Published.GroupBy(i => i.Kind)
                              .OrderByDescending(g => g.Count())
                              .Select(g => $"{g.Count()} {(g.Count() == 1 ? g.Key.Label().ToLowerInvariant() : g.Key.Plural())}");
        return string.Join(", ", groups);
    }
}

/// <summary>
/// The whole course, read out of one cartridge: what the site builder writes and what the
/// preview shows. Nothing here knows about files on disk — <c>SiteBuilder</c> does that.
/// </summary>
public sealed class CourseSite
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public CartridgeProducer Producer { get; set; }
    public string SchemaVersion { get; set; } = string.Empty;
    /// <summary>Path of the .imscc the course was read from.</summary>
    public string SourcePath { get; set; } = string.Empty;
    public long SourceBytes { get; set; }

    public List<SiteModule> Modules { get; } = new();
    public List<string> Warnings { get; } = new();

    public IEnumerable<SiteItem> AllItems => Modules.SelectMany(m => m.Items);

    public IEnumerable<SiteItem> PublishedItems =>
        Modules.Where(m => m.Include).SelectMany(m => m.Published);

    public int PublishedCount => PublishedItems.Count();

    public string Summary()
    {
        var mods = Modules.Count(m => m.Include);
        var groups = PublishedItems.GroupBy(i => i.Kind)
                                   .OrderByDescending(g => g.Count())
                                   .Select(g => $"{g.Count()} {(g.Count() == 1 ? g.Key.Label().ToLowerInvariant() : g.Key.Plural())}");
        return $"{mods} section{(mods == 1 ? "" : "s")} · " + string.Join(" · ", groups);
    }
}
