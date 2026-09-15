using Lms2Website.Core.Cartridge;
using Lms2Website.Core.Publish;
using Lms2Website.Core.Site;

namespace Lms2Website.Tests;

/// <summary>
/// A file kept out of the publish must still be in the folder, must not be linked from any page,
/// and must be invisible to everything that decides what gets sent to GitHub.
/// </summary>
public class ExcludedFileTests : IDisposable
{
    private readonly TestCartridge _cartridge = TestCartridge.Create();
    private readonly string _output = Path.Combine(Path.GetTempPath(), "lms2web-excl-" + Guid.NewGuid().ToString("N")[..8]);

    private SiteBuildResult Build(PublishRules rules) =>
        SiteBuilder.Build(CartridgeReader.Read(_cartridge.Path),
                          new SiteBuildOptions { OutputFolder = _output, Rules = rules });

    private string Read(string relative) =>
        File.ReadAllText(Path.Combine(_output, relative.Replace('/', Path.DirectorySeparatorChar)));

    public void Dispose()
    {
        _cartridge.Dispose();
        try { Directory.Delete(_output, recursive: true); } catch (DirectoryNotFoundException) { }
        GC.SuppressFinalize(this);
    }

    [Fact]
    public void WithNoRulesNothingChangesAndNoGitIgnoreIsWritten()
    {
        var result = Build(PublishRules.None);

        Assert.Empty(result.NotPublished);
        Assert.False(File.Exists(Path.Combine(_output, ".gitignore")));
    }

    [Fact]
    public void AnExcludedFileIsStillWrittenToTheFolder()
    {
        var result = Build(new PublishRules { Extensions = [".pptx"] });

        var excluded = Assert.Single(result.NotPublished.Keys);
        Assert.EndsWith(".pptx", excluded, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(Path.Combine(_output, excluded.Replace('/', Path.DirectorySeparatorChar))),
            "the local site keeps every file — only the push is trimmed");
    }

    [Fact]
    public void TheGitIgnoreWritesATypeRuleAsAPatternAndSaysWhoWroteIt()
    {
        Build(new PublishRules { Extensions = [".pptx"] });

        var ignore = Read(".gitignore");
        Assert.Contains("*.pptx", ignore, StringComparison.Ordinal);
        Assert.Contains("LMS 2 Website", ignore, StringComparison.Ordinal);
        Assert.Contains("no .pptx", ignore, StringComparison.Ordinal);
    }

    /// <summary>git cannot match on size, so those files have to be listed one by one.</summary>
    [Fact]
    public void TheGitIgnoreListsSizeExcludedFilesIndividually()
    {
        var result = Build(new PublishRules { MaxBytes = 1 });   // everything is "too big"

        var ignore = Read(".gitignore");
        Assert.DoesNotContain("*.", ignore, StringComparison.Ordinal);
        foreach (var href in result.NotPublished.Keys)
            Assert.Contains("/" + href, ignore, StringComparison.Ordinal);
    }

    /// <summary>
    /// The point of the whole feature: the published site must not carry a link to a file it does
    /// not have. The page still names the file — it is the download that goes, not the mention.
    /// </summary>
    [Fact]
    public void NoPageLinksToAnExcludedFileAndThePageSaysWhy()
    {
        // What the page looks like when the deck *is* published, so the link is known to exist.
        var before = Build(PublishRules.None);
        var linked = Directory.GetFiles(_output, "*.html", SearchOption.AllDirectories)
                              .Count(f => File.ReadAllText(f).Contains("files/lecture-1.pptx", StringComparison.OrdinalIgnoreCase));
        Assert.True(linked > 0, "the deck should be linked when nothing is excluded");
        Assert.Empty(before.NotPublished);

        var result = Build(new PublishRules { Extensions = [".pptx"] });
        var excluded = result.NotPublished.Keys.Single();

        var pages = Directory.GetFiles(_output, "*.html", SearchOption.AllDirectories)
                             .Select(File.ReadAllText).ToList();

        Assert.DoesNotContain(pages, html => html.Contains(excluded, StringComparison.OrdinalIgnoreCase));
        Assert.Contains(pages, html => html.Contains("Not published", StringComparison.Ordinal));

        // and the file is still named, so a reader knows what they are missing
        Assert.Contains(pages, html => html.Contains("Lecture 1.pptx", StringComparison.Ordinal));
    }

    [Fact]
    public void TheBuildSaysOutLoudThatSomethingWillNotBePublished()
    {
        var result = Build(new PublishRules { Extensions = [".pptx"] });

        Assert.Contains(result.Warnings, w =>
            w.Contains("will not be published", StringComparison.Ordinal));
        Assert.True(result.NotPublishedBytes > 0);
    }

    [Fact]
    public void WhatGetsPublishedIgnoresTheExcludedFileAndTheGitFolder()
    {
        var rules = new PublishRules { Extensions = [".pptx"] };
        var result = Build(rules);
        var excluded = result.NotPublished.Keys.Single();

        Directory.CreateDirectory(Path.Combine(_output, ".git"));
        File.WriteAllText(Path.Combine(_output, ".git", "config"), "[core]");

        var published = Publisher.PublishableFiles(_output, rules).ToList();

        Assert.DoesNotContain(published, f => f.EndsWith(".pptx", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(published, f => f.Replace('\\', '/').Contains("/.git/", StringComparison.Ordinal));
        Assert.Contains(published, f => f.EndsWith("index.html", StringComparison.Ordinal));

        // and the size the publish reports is smaller than the folder on disk
        Assert.True(Publisher.SiteSize(_output, rules) < Publisher.SiteSize(_output),
            "the excluded file should not count towards what is sent");
        Assert.DoesNotContain(excluded, string.Join('|', published.Select(f => Path.GetRelativePath(_output, f).Replace('\\', '/'))),
            StringComparison.OrdinalIgnoreCase);
    }
}
