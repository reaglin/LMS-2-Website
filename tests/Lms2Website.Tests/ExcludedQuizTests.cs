using Lms2Website.Core.Cartridge;
using Lms2Website.Core.Model;
using Lms2Website.Core.Site;

namespace Lms2Website.Tests;

/// <summary>
/// A quiz carries its answer key, so "not published" has to mean "not written". These tests check
/// there is nothing left anywhere — no page, no link, no search entry, no file on disk — and that
/// the caller's course object is handed back unchanged.
/// </summary>
public class ExcludedQuizTests : IDisposable
{
    private readonly TestCartridge _cartridge = TestCartridge.Create();
    private readonly string _output = Path.Combine(Path.GetTempPath(), "lms2web-quiz-" + Guid.NewGuid().ToString("N")[..8]);

    private SiteBuildResult Build(CourseSite course, bool excludeQuizzes) =>
        SiteBuilder.Build(course, new SiteBuildOptions
        {
            OutputFolder = _output,
            Rules = new PublishRules { ExcludeQuizzes = excludeQuizzes }
        });

    private string[] EveryFile() => Directory.GetFiles(_output, "*", SearchOption.AllDirectories);

    public void Dispose()
    {
        _cartridge.Dispose();
        try { Directory.Delete(_output, recursive: true); } catch (DirectoryNotFoundException) { }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void TheQuizPageIsNotWrittenAtAll()
    {
        var kept = Build(CartridgeReader.Read(_cartridge.Path), excludeQuizzes: false);
        var quizPages = EveryFile().Count(f => Path.GetFileName(f).Contains("quiz", StringComparison.OrdinalIgnoreCase));
        Assert.True(quizPages > 0, "the sample course has a quiz to leave out");

        var result = Build(CartridgeReader.Read(_cartridge.Path), excludeQuizzes: true);

        Assert.Equal(1, result.QuizzesLeftOut);
        Assert.Equal(kept.PagesWritten - 1, result.PagesWritten);
        Assert.DoesNotContain(EveryFile(), f => Path.GetFileName(f).Contains("quiz", StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>The answer key must not survive anywhere in the folder, under any name.</summary>
    [Fact]
    public void NoAnswerTextIsLeftAnywhereInTheFolder()
    {
        var course = CartridgeReader.Read(_cartridge.Path);
        var quiz = course.AllItems.First(i => i.Kind == ItemKind.Quiz);
        var title = quiz.Title;

        Build(course, excludeQuizzes: true);

        foreach (var file in EveryFile().Where(f => !f.EndsWith(".pptx", StringComparison.OrdinalIgnoreCase)))
        {
            var text = File.ReadAllText(file);
            Assert.DoesNotContain(title, text, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("Answer:", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void NothingLinksToTheQuizAndTheSearchIndexDoesNotListIt()
    {
        var course = CartridgeReader.Read(_cartridge.Path);
        var quizHref = course.AllItems.First(i => i.Kind == ItemKind.Quiz).SiteHref;

        Build(course, excludeQuizzes: true);

        foreach (var file in EveryFile().Where(f => f.EndsWith(".html", StringComparison.Ordinal)
                                                 || f.EndsWith(".js", StringComparison.Ordinal)))
            Assert.DoesNotContain(quizHref, File.ReadAllText(file), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TheBuildSaysHowManyWereLeftOut()
    {
        var result = Build(CartridgeReader.Read(_cartridge.Path), excludeQuizzes: true);

        Assert.Contains(result.Warnings, w => w.Contains("left out of the website", StringComparison.Ordinal));
    }

    /// <summary>
    /// The course object belongs to the window, which reuses it for the next build. Hiding the
    /// quizzes must not outlive the build that asked for it.
    /// </summary>
    [Fact]
    public void TheCourseIsHandedBackUnchangedSoTheNextBuildCanIncludeThem()
    {
        var course = CartridgeReader.Read(_cartridge.Path);
        var before = course.PublishedCount;

        Build(course, excludeQuizzes: true);
        Assert.Equal(before, course.PublishedCount);
        Assert.All(course.AllItems.Where(i => i.Kind == ItemKind.Quiz), q => Assert.True(q.Include));

        // and building again without the rule brings the quiz back
        var again = Build(course, excludeQuizzes: false);
        Assert.Equal(0, again.QuizzesLeftOut);
        Assert.Contains(EveryFile(), f => Path.GetFileName(f).Contains("quiz", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DescribeMentionsTheQuizRule()
    {
        Assert.Equal("no quizzes", new PublishRules { ExcludeQuizzes = true }.Describe());
        Assert.True(new PublishRules { ExcludeQuizzes = true }.AnyAtAll);
        Assert.False(new PublishRules { ExcludeQuizzes = true }.Any);   // it is not a file rule
    }
}
