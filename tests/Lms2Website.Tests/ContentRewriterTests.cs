using Lms2Website.Core.Site;

namespace Lms2Website.Tests;

public class ContentRewriterTests
{
    /// <summary>Stands in for the site builder: anything under "ok/" exists, nothing else does.</summary>
    private static string? Resolve(string zipHref) =>
        zipHref.StartsWith("ok/", StringComparison.Ordinal) ? "files/" + Path.GetFileName(zipHref) : null;

    private static RewriteResult Clean(string html, string pageHref = "ok/page.html", string? title = null) =>
        ContentRewriter.Clean(html, pageHref, "01-week/", Resolve, title);

    [Fact]
    public void ScriptsAndStylesheetsGo()
    {
        var result = Clean("<html><head><link rel='stylesheet' href='https://lms/x.css'></head>" +
                           "<body><p onclick='steal()'>Hello</p><script>steal()</script></body></html>");

        Assert.Contains("Hello", result.BodyHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("steal", result.BodyHtml, StringComparison.Ordinal);
        Assert.DoesNotContain("stylesheet", result.BodyHtml, StringComparison.Ordinal);
        Assert.Equal(1, result.ScriptsRemoved);
    }

    [Fact]
    public void ReferencesThatResolveAreRepointedRelativeToThePage()
    {
        var result = Clean("<p><img src='pic.png'></p>");
        Assert.Contains("../files/pic.png", result.BodyHtml, StringComparison.Ordinal);
        Assert.Empty(result.BrokenReferences);
    }

    [Fact]
    public void AMissingImageBecomesASentence()
    {
        var result = Clean("<p><img src='../gone/pic.png' alt='A Venn diagram'></p>");

        Assert.DoesNotContain("<img", result.BodyHtml, StringComparison.Ordinal);
        Assert.Contains("image not included in the export: A Venn diagram", result.BodyHtml, StringComparison.Ordinal);
        Assert.Single(result.BrokenReferences);
    }

    [Fact]
    public void AMissingLinkIsKeptButMarked()
    {
        var result = Clean("<p><a href='../gone/handout.pdf'>Handout</a></p>");

        Assert.Contains("broken-link", result.BodyHtml, StringComparison.Ordinal);
        Assert.Contains("Handout", result.BodyHtml, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://class.example.edu/d2l/le/content/1/Home")]
    [InlineData("https://class.example.edu/d2l/common/dialogs/quickLink/quickLink.d2l?ou=123")]
    [InlineData("https://school.instructure.com/courses/42/pages/week-1")]
    public void LinksBackIntoTheLmsAreMarked(string url)
    {
        var result = Clean($"<p><a href='{url}'>Go</a></p>");
        Assert.Contains("lms-link", result.BodyHtml, StringComparison.Ordinal);
        Assert.Equal(1, result.LmsLinkCount);
    }

    [Fact]
    public void AnOrdinaryOutsideLinkIsLeftAlone()
    {
        var result = Clean("<p><a href='https://en.wikipedia.org/wiki/Median'>Median</a></p>");
        Assert.DoesNotContain("lms-link", result.BodyHtml, StringComparison.Ordinal);
        Assert.Equal(0, result.LmsLinkCount);
    }

    [Fact]
    public void CanvasFileBaseTokenResolvesToWebResources()
    {
        Assert.Equal("web_resources/img/x.png",
            ContentRewriter.ResolveAgainstPage("wiki_content/page.html", "$IMS-CC-FILEBASE$/img/x.png"));
    }

    [Fact]
    public void ReferencesResolveRelativeToThePagesFolder()
    {
        Assert.Equal("content/i2/pic.png",
            ContentRewriter.ResolveAgainstPage("content/i1/page.html", "../i2/pic.png"));
        Assert.Equal("content/i1/sub/pic.png",
            ContentRewriter.ResolveAgainstPage("content/i1/page.html", "sub/pic.png"));
    }

    [Fact]
    public void ARepeatedTitleAtTheTopOfThePageGoes()
    {
        var result = Clean("<h1>Measurement Basics</h1><p>Body.</p>", title: "Measurement Basics");
        Assert.DoesNotContain("<h1>", result.BodyHtml, StringComparison.Ordinal);
        Assert.Contains("Body.", result.BodyHtml, StringComparison.Ordinal);
    }

    [Fact]
    public void ABoldParagraphRepeatingTheTitleGoesToo()
    {
        var result = Clean("<p><strong>Module 1 &mdash; Basics</strong></p><p>Body.</p>", title: "Module 1 — Basics");
        Assert.DoesNotContain("Module 1", result.BodyHtml, StringComparison.Ordinal);
    }

    [Fact]
    public void AHeadingThatSaysSomethingElseStays()
    {
        var result = Clean("<h2>Why measure?</h2><p>Body.</p>", title: "Measurement Basics");
        Assert.Contains("<h2>Why measure?</h2>", result.BodyHtml, StringComparison.Ordinal);
    }

    [Fact]
    public void AFirstHeadingThatCarriesALinkIsNeverDropped()
    {
        var result = Clean("<p><strong><a href='https://x.test'>Measurement Basics</a></strong></p>",
                           title: "Measurement Basics");
        Assert.Contains("x.test", result.BodyHtml, StringComparison.Ordinal);
    }

    [Fact]
    public void RelativePathsClimbOutOfTheSectionFolder()
    {
        Assert.Equal("../files/x.png", ContentRewriter.Relative("01-week/", "files/x.png"));
        Assert.Equal("files/x.png", ContentRewriter.Relative("", "files/x.png"));
    }

    [Fact]
    public void SpacesInASitePathAreEscaped() =>
        Assert.Equal("01-week/a%20file.pptx", ContentRewriter.EscapePath("01-week/a file.pptx"));
}
