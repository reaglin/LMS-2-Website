using Lms2Website.Core.Site;

namespace Lms2Website.Tests;

public class PublishRulesTests
{
    private const long Mb = 1024L * 1024L;

    [Fact]
    public void NothingIsExcludedByDefault()
    {
        Assert.False(PublishRules.None.Any);
        Assert.Null(PublishRules.None.ExcludeReason("lecture-1.pptx", 500 * Mb));
    }

    [Fact]
    public void TheSizeRuleCatchesOnlyWhatIsOverIt()
    {
        var rules = new PublishRules { MaxBytes = 25 * Mb };

        Assert.Null(rules.ExcludeReason("small.pdf", 25 * Mb));           // exactly on the line stays
        Assert.NotNull(rules.ExcludeReason("big.pdf", 25 * Mb + 1));
        Assert.Contains("25 MB", rules.ExcludeReason("big.pdf", 40 * Mb)!, StringComparison.Ordinal);
    }

    [Fact]
    public void TheTypeRuleIgnoresCapitalisation()
    {
        var rules = new PublishRules { Extensions = [".pptx"] };

        Assert.NotNull(rules.ExcludeReason("Lecture.PPTX", 1));
        Assert.Null(rules.ExcludeReason("notes.pdf", 1));
    }

    [Fact]
    public void TheSizeRuleIsReportedAheadOfTheTypeRule()
    {
        var rules = new PublishRules { MaxBytes = 10 * Mb, Extensions = [".pptx"] };

        // A big .pptx breaks both; the size is the one a reader is least likely to guess.
        Assert.Contains("10 MB", rules.ExcludeReason("deck.pptx", 50 * Mb)!, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("pptx", ".pptx")]
    [InlineData(".pptx", ".pptx")]
    [InlineData("*.pptx", ".pptx")]
    [InlineData("  PPTX  ", ".pptx")]
    public void ExtensionsAreReadHoweverTheyAreTyped(string typed, string expected) =>
        Assert.Equal([expected], PublishRules.ParseExtensions(typed));

    [Fact]
    public void ExtensionsSplitOnCommasAndSpacesAndDoNotRepeat()
    {
        Assert.Equal([".pptx", ".zip", ".mp4"], PublishRules.ParseExtensions("pptx, .zip  mp4"));
        Assert.Equal([".pptx"], PublishRules.ParseExtensions(".pptx, pptx, PPTX"));
    }

    [Fact]
    public void NonsenseIsDroppedRatherThanGuessedAt()
    {
        Assert.Empty(PublishRules.ParseExtensions(null));
        Assert.Empty(PublishRules.ParseExtensions("   "));
        Assert.Empty(PublishRules.ParseExtensions("."));
        Assert.Empty(PublishRules.ParseExtensions("a/b"));      // a path, not an extension
        Assert.Empty(PublishRules.ParseExtensions("tar.gz"));   // two dots is not one extension
    }

    [Fact]
    public void DescribeReadsAsASentenceForTheGitIgnoreHeader()
    {
        Assert.Equal("everything is published", PublishRules.None.Describe());
        Assert.Equal("nothing over 25 MB", new PublishRules { MaxBytes = 25 * Mb }.Describe());
        Assert.Equal("no .pptx, .zip", new PublishRules { Extensions = [".pptx", ".zip"] }.Describe());
        Assert.Equal("nothing over 25 MB; no .pptx",
            new PublishRules { MaxBytes = 25 * Mb, Extensions = [".pptx"] }.Describe());
    }
}
