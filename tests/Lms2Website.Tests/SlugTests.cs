using Lms2Website.Core.Site;

namespace Lms2Website.Tests;

public class SlugTests
{
    [Theory]
    [InlineData("Week 1_Welcome- Start Here", "week-1-welcome-start-here")]
    [InlineData("Week 2/Module 1 - Statistics", "week-2-module-1-statistics")]
    [InlineData("Overview.html", "overview")]
    [InlineData("  Café Notes  ", "cafe-notes")]
    [InlineData("¿Qué?", "que")]
    [InlineData("", "item")]
    [InlineData("!!!", "item")]
    [InlineData("CON", "con-page")]
    public void MakesUrlSafeSegments(string title, string expected) =>
        Assert.Equal(expected, Slug.From(title));

    [Fact]
    public void LongTitlesAreCutAtAWordBoundary()
    {
        var slug = Slug.From("Introduction to the theory of probability and its many applications in engineering practice");
        Assert.True(slug.Length <= 60);
        Assert.DoesNotContain("--", slug, StringComparison.Ordinal);
        Assert.False(slug.EndsWith('-'));
    }

    [Fact]
    public void DuplicatesGetANumber()
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Assert.Equal("overview", Slug.Unique("overview", used));
        Assert.Equal("overview-2", Slug.Unique("overview", used));
        Assert.Equal("overview-3", Slug.Unique("overview", used));
    }

    [Fact]
    public void FileNamesKeepTheirExtension()
    {
        Assert.Equal("lecture-1.pptx", Slug.FileName("Lecture 1.pptx"));
        Assert.Equal("report-template.docx", Slug.FileName("Report Template.DOCX"));
        Assert.Equal("file.png", Slug.FileName(" .png", "file"));
    }
}
