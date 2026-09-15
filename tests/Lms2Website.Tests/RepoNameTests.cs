using Lms2Website.Core;
using Lms2Website.Core.Publish;

namespace Lms2Website.Tests;

public class RepoNameTests
{
    [Theory]
    [InlineData("EGN3443", "L2W-EGN3443")]
    [InlineData("egn3443", "L2W-egn3443")]
    [InlineData("prob-and-stats-for-engineers", "L2W-prob-and-stats-for-engineers")]
    [InlineData("  EGN3443  ", "L2W-EGN3443")]
    public void APlainNameGetsThePrefix(string typed, string expected) =>
        Assert.Equal(expected, RepoName.Apply(typed));

    [Theory]
    [InlineData("L2W-EGN3443")]
    [InlineData("l2w-EGN3443")]
    [InlineData("L2w-EGN3443")]
    public void ANameThatAlreadyHasThePrefixDoesNotGetASecondOne(string typed)
    {
        var applied = RepoName.Apply(typed);

        Assert.Equal("L2W-EGN3443", applied);
        Assert.Equal(applied, RepoName.Apply(applied));   // applying twice changes nothing
    }

    [Fact]
    public void BlankStaysBlankSoTheUsualValidationStillSpeaksFirst()
    {
        Assert.Equal(string.Empty, RepoName.Apply(""));
        Assert.Equal(string.Empty, RepoName.Apply("   "));
        Assert.Equal(string.Empty, RepoName.Apply(null));
    }

    [Fact]
    public void HasPrefixIgnoresCapitalisationAndLeadingSpace()
    {
        Assert.True(RepoName.HasPrefix("L2W-x"));
        Assert.True(RepoName.HasPrefix("l2w-x"));
        Assert.True(RepoName.HasPrefix("  L2W-x"));
        Assert.False(RepoName.HasPrefix("EGN3443"));
        Assert.False(RepoName.HasPrefix("myL2W-x"));
        Assert.False(RepoName.HasPrefix(null));
    }

    /// <summary>
    /// The reason the prefix exists: reaglin/EGN3443 is a real repository, and publishing is a
    /// force-push. The proposed name must never be able to land on it.
    /// </summary>
    [Fact]
    public void TheNameCanNeverCollideWithAHandMadeRepository()
    {
        Assert.NotEqual("EGN3443", RepoName.Apply("EGN3443"));
        Assert.StartsWith(L2W.RepoPrefix, RepoName.Apply("EGN3443"), StringComparison.Ordinal);
    }
}
