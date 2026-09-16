using Lms2Website.Core.Publish;

namespace Lms2Website.Tests;

/// <summary>
/// The courses list answers four questions about each course, all from disk. These check the
/// answers are right — and, as much, that it never claims to know something it cannot: whether
/// GitHub is actually serving the site is not read from a project file.
/// </summary>
public class CourseStatusTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "lms2web-status-" + Guid.NewGuid().ToString("N")[..8]);

    private string Site(bool withIndex = true, bool withGit = false)
    {
        var folder = Path.Combine(_root, "site");
        Directory.CreateDirectory(folder);
        if (withIndex) File.WriteAllText(Path.Combine(folder, "index.html"), "<html></html>");
        if (withGit) Directory.CreateDirectory(Path.Combine(folder, ".git"));
        return folder;
    }

    private string Export()
    {
        Directory.CreateDirectory(_root);
        var path = Path.Combine(_root, "course.imscc");
        File.WriteAllText(path, "not really a zip");
        return path;
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch (DirectoryNotFoundException) { }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void AFreshCourseHasDoneNothing()
    {
        var status = CourseStatus.For(new ProjectSettings { CourseTitle = "EGN3443" });

        Assert.Equal(StepState.No, status.Source);
        Assert.Equal(StepState.No, status.Converted);
        Assert.Equal(StepState.No, status.Site);
        Assert.Equal(StepState.No, status.Pushed);
        Assert.Equal("EGN3443", status.Title);
        Assert.False(status.CanOpenSite);
    }

    [Fact]
    public void AnExportThatHasMovedIsFlaggedRatherThanCalledMissing()
    {
        var status = CourseStatus.For(new ProjectSettings { SourcePath = Path.Combine(_root, "gone.imscc") });

        Assert.Equal(StepState.Stale, status.Source);
        Assert.Contains("not where it was", status.SourceText, StringComparison.Ordinal);
    }

    [Fact]
    public void ABuiltSiteIsCountedFromTheFolderNotFromTheProjectFile()
    {
        var status = CourseStatus.For(new ProjectSettings
        {
            SourcePath = Export(),
            OutputFolder = Site(),
            LastBuiltUtc = DateTime.UtcNow
        });

        Assert.Equal(StepState.Yes, status.Source);
        Assert.Equal(StepState.Yes, status.Converted);
        Assert.Equal(StepState.Yes, status.Site);
        Assert.Equal(1, status.SitePages);
    }

    [Fact]
    public void ARecordedBuildWithNoFolderSaysTheFolderIsGone()
    {
        var status = CourseStatus.For(new ProjectSettings
        {
            OutputFolder = Path.Combine(_root, "nothing-here"),
            LastBuiltUtc = DateTime.UtcNow
        });

        Assert.Equal(StepState.No, status.Site);
        Assert.Equal("the folder is gone", status.SiteText);
    }

    [Fact]
    public void AnExportNewerThanTheBuildMakesTheConversionStale()
    {
        var export = Export();
        File.SetLastWriteTimeUtc(export, DateTime.UtcNow);

        var status = CourseStatus.For(new ProjectSettings
        {
            SourcePath = export,
            OutputFolder = Site(),
            LastBuiltUtc = DateTime.UtcNow.AddDays(-1)
        });

        Assert.Equal(StepState.Stale, status.Converted);
        Assert.Contains("the export is newer", status.ConvertedText, StringComparison.Ordinal);
    }

    [Fact]
    public void ABuildAfterThePublishMakesThePushStale()
    {
        var status = CourseStatus.For(new ProjectSettings
        {
            OutputFolder = Site(),
            LastBuiltUtc     = DateTime.UtcNow,
            LastPublishedUtc = DateTime.UtcNow.AddDays(-1),
            PagesUrl = "https://reaglin.github.io/L2W-egn3443/"
        });

        Assert.Equal(StepState.Stale, status.Pushed);
        Assert.Contains("rebuilt since", status.PushedText, StringComparison.Ordinal);
        Assert.True(status.CanOpenSite);
    }

    [Fact]
    public void ARepositoryWithNoRecordedPublishSaysExactlyThat()
    {
        var status = CourseStatus.For(new ProjectSettings { OutputFolder = Site(withGit: true) });

        Assert.Equal(StepState.No, status.Pushed);
        Assert.Contains("committed, never pushed", status.PushedText, StringComparison.Ordinal);
    }

    /// <summary>
    /// "Pushed" says the site was sent, and nothing more. Whether GitHub is serving it is a
    /// separate question the user checks in GitHub Pages, so no wording here may imply it.
    /// </summary>
    [Fact]
    public void ItSaysPushedAndNeverImpliesTheSiteIsReachable()
    {
        var status = CourseStatus.For(new ProjectSettings
        {
            OutputFolder = Site(),
            LastPublishedUtc = DateTime.UtcNow,
            PagesUrl = "https://reaglin.github.io/L2W-egn3443/"
        });

        Assert.StartsWith("pushed", status.PushedText, StringComparison.Ordinal);
        foreach (var claim in new[] { "live", "online", "reachable", "serving", "published", "available" })
            Assert.DoesNotContain(claim, status.PushedText, StringComparison.OrdinalIgnoreCase);
    }
}
