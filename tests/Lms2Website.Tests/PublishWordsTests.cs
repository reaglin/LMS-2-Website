using Lms2Website.Core.Publish;

namespace Lms2Website.Tests;

/// <summary>
/// The private-repository wording is the one place in this app where the obvious phrasing is
/// actively harmful: someone could tick "private", believe the course is restricted, and publish
/// an answer key to the open internet. GitHub's documentation is explicit — "GitHub Pages sites
/// are publicly available on the internet, even if the repository for the site is private" — so
/// these tests exist to stop the text drifting back towards the comfortable lie.
/// </summary>
public class PublishWordsTests
{
    private const string Text = PublishWords.PrivateDoesNotMeanHidden;

    [Fact]
    public void ItSaysPlainlyThatTheSiteIsStillPublic()
    {
        Assert.Contains("does not make the website private", Text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("public on the internet", Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ItNeverPromisesThatOnlyPermittedPeopleCanSeeIt()
    {
        foreach (var claim in new[]
                 {
                     "only people with access",
                     "only users with access",
                     "visible only to",
                     "keeps the site private",
                     "makes the site private"
                 })
            Assert.DoesNotContain(claim, Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ItNamesWhatWouldActuallyBeNeededAndWhatHappensOnAFreeAccount()
    {
        Assert.Contains("Enterprise Cloud", Text, StringComparison.Ordinal);
        Assert.Contains("free account", Text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ItEndsWithTheAdviceThatMatters() =>
        Assert.Contains("do not publish it here", Text, StringComparison.OrdinalIgnoreCase);
}
