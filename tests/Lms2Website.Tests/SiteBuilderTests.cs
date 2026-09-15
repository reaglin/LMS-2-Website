using Lms2Website.Core.Cartridge;
using Lms2Website.Core.Model;
using Lms2Website.Core.Site;

namespace Lms2Website.Tests;

public class SiteBuilderTests : IDisposable
{
    private readonly TestCartridge _cartridge = TestCartridge.Create();
    private readonly string _output = Path.Combine(Path.GetTempPath(), "lms2web-site-" + Guid.NewGuid().ToString("N")[..8]);

    private (CourseSite Course, SiteBuildResult Result) BuildSite()
    {
        var course = CartridgeReader.Read(_cartridge.Path);
        var result = SiteBuilder.Build(course, new SiteBuildOptions { OutputFolder = _output });
        return (course, result);
    }

    private string Read(string relative) => File.ReadAllText(Path.Combine(_output, relative.Replace('/', Path.DirectorySeparatorChar)));

    public void Dispose()
    {
        _cartridge.Dispose();
        try { Directory.Delete(_output, recursive: true); } catch (DirectoryNotFoundException) { /* nothing written */ }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void WritesAPageForEveryItemPlusTheIndexes()
    {
        var (course, result) = BuildSite();

        // 5 item pages (2 pages, quiz, assignment, discussion) + 2 section pages + the home page.
        Assert.Equal(8, result.PagesWritten);
        Assert.True(File.Exists(Path.Combine(_output, "index.html")));
        Assert.True(File.Exists(Path.Combine(_output, ".nojekyll")));
        Assert.True(File.Exists(Path.Combine(_output, "assets", "site.css")));
        Assert.True(File.Exists(Path.Combine(_output, "assets", "site.js")));
        foreach (var item in course.PublishedItems.Where(i => i.Kind != ItemKind.Link))
            Assert.True(File.Exists(Path.Combine(_output, item.SiteHref.Replace('/', Path.DirectorySeparatorChar))),
                $"{item.SiteHref} was not written");
    }

    [Fact]
    public void PageKeepsItsContentButNotTheLmsScriptOrStylesheet()
    {
        BuildSite();
        var html = Read("01-week-1-getting-started/overview.html");

        Assert.Contains("Welcome", html, StringComparison.Ordinal);
        Assert.DoesNotContain("alert('tracking')", html, StringComparison.Ordinal);
        Assert.DoesNotContain("brightspace.com", html, StringComparison.Ordinal);
        Assert.Contains("assets/site.css", html, StringComparison.Ordinal);
    }

    [Fact]
    public void LinkBetweenTwoCartridgePagesPointsAtTheNewPage()
    {
        BuildSite();
        var html = Read("01-week-1-getting-started/overview.html");
        Assert.Contains("../02-week-2-measurement/lecture-notes.html", html, StringComparison.Ordinal);
    }

    [Fact]
    public void LinkBackIntoTheLmsIsMarked()
    {
        var (_, result) = BuildSite();
        var html = Read("01-week-1-getting-started/overview.html");

        Assert.Contains("lms-link", html, StringComparison.Ordinal);
        Assert.Contains(result.Warnings, w => w.Contains("point back into the LMS", StringComparison.Ordinal));
    }

    [Fact]
    public void ImageIsCopiedAndRepointed()
    {
        var (_, result) = BuildSite();
        var html = Read("02-week-2-measurement/lecture-notes.html");

        Assert.Contains("../files/diagram.png", html, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(_output, "files", "diagram.png")));
        Assert.True(result.FilesCopied >= 2);   // the diagram and the deck
    }

    [Fact]
    public void MissingReferenceIsReportedAndLeftAlone()
    {
        var (_, result) = BuildSite();
        Assert.Contains(result.Warnings, w => w.Contains("missing-file.pdf", StringComparison.Ordinal));
    }

    [Fact]
    public void AttachmentIsCopiedWithADownloadLink()
    {
        BuildSite();
        var html = Read("02-week-2-measurement/lecture-notes.html");

        Assert.Contains("../files/lecture-1.pptx", html, StringComparison.Ordinal);
        Assert.Contains("download", html, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Combine(_output, "files", "lecture-1.pptx")));
    }

    [Fact]
    public void QuizPageShowsTheQuestionsAndMarksTheAnswer()
    {
        BuildSite();
        var html = Read("02-week-2-measurement/week-2-quiz.html");

        Assert.Contains("Question 1", html, StringComparison.Ordinal);
        Assert.Contains("<li class=\"correct\">sample", html, StringComparison.Ordinal);
        Assert.Contains("<strong>Answer:</strong> 4", html, StringComparison.Ordinal);
        Assert.Contains("nothing here is graded", html, StringComparison.Ordinal);
        Assert.Contains("2 points", html, StringComparison.Ordinal);
    }

    [Fact]
    public void QuizQuestionHtmlGoesThroughTheRewriterToo()
    {
        BuildSite();
        var html = Read("02-week-2-measurement/week-2-quiz.html");

        // The demo quiz's stem keeps its markup, and nothing in it is left pointing into the zip.
        Assert.Contains("<b>population</b>", html, StringComparison.Ordinal);
        Assert.DoesNotContain("src=\"untitled", html, StringComparison.Ordinal);
    }

    [Fact]
    public void AssignmentPageSaysWorkIsStillHandedInInTheLms()
    {
        BuildSite();
        var html = Read("02-week-2-measurement/assignment-1.html");

        Assert.Contains("25 points", html, StringComparison.Ordinal);
        Assert.Contains("handed in through the course in the LMS", html, StringComparison.Ordinal);
        Assert.Contains("<strong>report</strong>", html, StringComparison.Ordinal);
    }

    [Fact]
    public void SectionIndexListsItsItemsAndTheWebLinkGoesStraightOut()
    {
        BuildSite();
        var html = Read("01-week-1-getting-started/index.html");

        Assert.Contains("href=\"overview.html\"", html, StringComparison.Ordinal);
        Assert.Contains("https://example.edu/course", html, StringComparison.Ordinal);
        Assert.Contains("target=\"_blank\"", html, StringComparison.Ordinal);
    }

    [Fact]
    public void HomePageListsEverySection()
    {
        BuildSite();
        var html = Read("index.html");

        Assert.Contains("Week 1: Getting Started", html, StringComparison.Ordinal);
        Assert.Contains("Week 2 / Measurement", html, StringComparison.Ordinal);
        Assert.Contains("02-week-2-measurement/index.html", html, StringComparison.Ordinal);
    }

    [Fact]
    public void SearchIndexCoversEveryPage()
    {
        BuildSite();
        var js = Read("assets/search-index.js");

        Assert.StartsWith("window.SITE_SEARCH = [", js, StringComparison.Ordinal);
        Assert.Contains("Week 2 Quiz", js, StringComparison.Ordinal);
        Assert.Contains("Introduce yourself", js, StringComparison.Ordinal);   // body text is indexed
    }

    [Fact]
    public void RebuildingReplacesTheSiteButKeepsGit()
    {
        BuildSite();
        Directory.CreateDirectory(Path.Combine(_output, ".git"));
        File.WriteAllText(Path.Combine(_output, ".git", "HEAD"), "ref: refs/heads/main");
        File.WriteAllText(Path.Combine(_output, "stale.html"), "old");

        BuildSite();

        Assert.False(File.Exists(Path.Combine(_output, "stale.html")));
        Assert.True(File.Exists(Path.Combine(_output, ".git", "HEAD")));
    }
}
