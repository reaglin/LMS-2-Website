using System.Globalization;
using System.Xml.Linq;

namespace Lms2Website.Core.Cartridge;

/// <summary>A web link or LTI launch read out of the cartridge.</summary>
public sealed class CcLink
{
    public string Title { get; init; } = string.Empty;
    public string Url { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public bool IsLti { get; init; }
}

/// <summary>An assignment or a discussion topic — both are "a title and a body of HTML".</summary>
public sealed class CcPrompt
{
    public string Title { get; init; } = string.Empty;
    public string BodyHtml { get; init; } = string.Empty;
    public decimal? Points { get; init; }
    public List<string> SubmissionFormats { get; } = new();
    public bool IsDiscussion { get; init; }
}

/// <summary>
/// Readers for the small XML resource types — web links (imswl), Basic LTI links, CC assignments
/// and discussion topics (imsdt). Namespace-agnostic; never throw on a missing optional part;
/// return null only when the document is not the expected kind at all.
///
/// Ported from PreseMaker's <c>CcResourceReaders</c>, with the bodies kept as HTML (this app
/// publishes them, it does not flatten them to text).
/// </summary>
public static class CcResourceReaders
{
    public static CcLink? ReadWebLink(string xml)
    {
        var root = Root(xml);
        if (root == null || root.Name.LocalName != "webLink") return null;
        return new CcLink
        {
            Title = Text(Child(root, "title")),
            Url   = Child(root, "url")?.Attribute("href")?.Value.Trim() ?? string.Empty
        };
    }

    public static CcLink? ReadLti(string xml)
    {
        var root = Root(xml);
        if (root == null || root.Name.LocalName != "cartridge_basiclti_link") return null;
        var launch = Text(Child(root, "secure_launch_url"));
        if (string.IsNullOrWhiteSpace(launch)) launch = Text(Child(root, "launch_url"));
        return new CcLink
        {
            Title       = Text(Child(root, "title")),
            Description = Text(Child(root, "description")),
            Url         = launch,
            IsLti       = true
        };
    }

    /// <summary>CC assignment (assignment_xmlv1p0). D2L puts the student-facing text in
    /// &lt;instructor_text&gt;, Canvas in &lt;text&gt; — take whichever is there, both if both.</summary>
    public static CcPrompt? ReadAssignment(string xml)
    {
        var root = Root(xml);
        if (root == null || root.Name.LocalName != "assignment") return null;

        var text  = Child(root, "text");
        var itext = Child(root, "instructor_text");
        var body  = string.Join("\n<hr>\n",
            new[] { text, itext }.Where(e => e != null && !string.IsNullOrWhiteSpace(e.Value))
                                 .Select(e => AsHtml(e!)));

        decimal? points = null;
        var gradable = Child(root, "gradable");
        if (gradable != null && decimal.TryParse(gradable.Attribute("points_possible")?.Value,
                NumberStyles.Any, CultureInfo.InvariantCulture, out var p))
            points = p;

        var prompt = new CcPrompt { Title = Text(Child(root, "title")), BodyHtml = body, Points = points };
        var formats = Child(root, "submission_formats")?.Elements()
            .Where(e => e.Name.LocalName == "format")
            .Select(e => e.Attribute("type")?.Value ?? string.Empty)
            .Where(t => t.Length > 0) ?? Enumerable.Empty<string>();
        prompt.SubmissionFormats.AddRange(formats);
        return prompt;
    }

    /// <summary>Discussion topic (imsdt).</summary>
    public static CcPrompt? ReadDiscussion(string xml)
    {
        var root = Root(xml);
        if (root == null || root.Name.LocalName != "topic") return null;
        var text = Child(root, "text");
        return new CcPrompt
        {
            Title        = Text(Child(root, "title")),
            BodyHtml     = text != null ? AsHtml(text) : string.Empty,
            IsDiscussion = true
        };
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static XElement? Root(string xml)
    {
        try { return XDocument.Parse(xml, LoadOptions.None).Root; }
        catch (System.Xml.XmlException) { return null; }
    }

    internal static XElement? Child(XElement parent, string localName) =>
        parent.Elements().FirstOrDefault(e => e.Name.LocalName == localName);

    private static string Text(XElement? e) => (e?.Value ?? string.Empty).Trim();

    /// <summary>
    /// A CC text element is entity-escaped HTML (texttype="text/html") or plain text. Return HTML
    /// either way, so every body can be treated the same downstream.
    /// </summary>
    internal static string AsHtml(XElement e)
    {
        var type = e.Attribute("texttype")?.Value ?? "text/html";
        var v = e.Value;   // XElement.Value un-escapes entities and unwraps CDATA
        return type.Equals("text/plain", StringComparison.OrdinalIgnoreCase)
            ? PlainToHtml(v)
            : v.Trim();
    }

    /// <summary>Wraps plain text in paragraphs, escaping it.</summary>
    internal static string PlainToHtml(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return string.Empty;
        var paragraphs = text.Replace("\r\n", "\n").Split("\n\n", StringSplitOptions.RemoveEmptyEntries);
        return string.Join("\n", paragraphs.Select(p =>
            "<p>" + System.Net.WebUtility.HtmlEncode(p.Trim()).Replace("\n", "<br>") + "</p>"));
    }
}
