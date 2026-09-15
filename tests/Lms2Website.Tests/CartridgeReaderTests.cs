using Lms2Website.Core.Cartridge;
using Lms2Website.Core.Model;

namespace Lms2Website.Tests;

public class CartridgeReaderTests
{
    [Fact]
    public void ReadsSectionsItemsAndKinds()
    {
        using var cartridge = TestCartridge.Create();
        var course = CartridgeReader.Read(cartridge.Path);

        Assert.Equal("Demo Course", course.Title);
        Assert.Equal(CartridgeProducer.Brightspace, course.Producer);   // the Cyrillic сontent/ folder
        Assert.Equal(2, course.Modules.Count);
        Assert.Equal("Week 1: Getting Started", course.Modules[0].Title);
        Assert.Equal("01-week-1-getting-started", course.Modules[0].Slug);

        var kinds = course.AllItems.GroupBy(i => i.Kind).ToDictionary(g => g.Key, g => g.Count());
        Assert.Equal(2, kinds[ItemKind.Page]);         // overview + lecture
        Assert.Equal(1, kinds[ItemKind.Quiz]);
        Assert.Equal(1, kinds[ItemKind.Assignment]);
        Assert.Equal(1, kinds[ItemKind.Discussion]);
        Assert.Equal(1, kinds[ItemKind.Link]);
        Assert.False(kinds.ContainsKey(ItemKind.File));   // the deck is the lecture's attachment, not its own item
    }

    [Fact]
    public void ResolvesTheD2LSemicolonHref()
    {
        using var cartridge = TestCartridge.Create();
        var course = CartridgeReader.Read(cartridge.Path);

        var overview = course.AllItems.Single(i => i.Title == "Overview");
        Assert.Equal(ItemKind.Page, overview.Kind);
        Assert.Contains("Welcome", overview.BodyHtml, StringComparison.Ordinal);
    }

    [Fact]
    public void NestedFolderBecomesASectionHeading()
    {
        using var cartridge = TestCartridge.Create();
        var course = CartridgeReader.Read(cartridge.Path);

        var hello = course.AllItems.Single(i => i.Title == "Say hello");
        Assert.Equal("Extras", hello.Section);
        Assert.Equal("02-week-2-measurement", course.Modules[1].Slug);
    }

    [Fact]
    public void PageKeepsItsDependencyAsAnAttachment()
    {
        using var cartridge = TestCartridge.Create();
        var course = CartridgeReader.Read(cartridge.Path);

        var lecture = course.AllItems.Single(i => i.Title == "Lecture notes");
        var deck = Assert.Single(lecture.Attachments);
        Assert.EndsWith("Lecture 1.pptx", deck.SourceHref, StringComparison.Ordinal);
    }

    [Fact]
    public void AssignmentAndDiscussionKeepTheirHtmlAndPoints()
    {
        using var cartridge = TestCartridge.Create();
        var course = CartridgeReader.Read(cartridge.Path);

        var assignment = course.AllItems.Single(i => i.Kind == ItemKind.Assignment);
        Assert.Equal(25m, assignment.Points);
        Assert.Contains("<strong>report</strong>", assignment.BodyHtml, StringComparison.Ordinal);
        Assert.Equal(["file"], assignment.SubmissionFormats);

        var discussion = course.AllItems.Single(i => i.Kind == ItemKind.Discussion);
        Assert.Contains("Introduce yourself", discussion.BodyHtml, StringComparison.Ordinal);
    }

    [Fact]
    public void LinkPointsAtItsAddressRatherThanAPage()
    {
        using var cartridge = TestCartridge.Create();
        var course = CartridgeReader.Read(cartridge.Path);

        var link = course.AllItems.Single(i => i.Kind == ItemKind.Link);
        Assert.Equal("https://example.edu/course", link.Url);
        Assert.Equal(link.Url, link.SiteHref);
        Assert.Empty(link.Slug);
    }

    [Fact]
    public void NotACartridgeSaysSo()
    {
        var path = Path.Combine(Path.GetTempPath(), "not-a-cartridge-" + Guid.NewGuid().ToString("N")[..8] + ".imscc");
        using (var zip = System.IO.Compression.ZipFile.Open(path, System.IO.Compression.ZipArchiveMode.Create))
            zip.CreateEntry("readme.txt");
        try
        {
            var ex = Assert.Throws<InvalidDataException>(() => CartridgeReader.Read(path));
            Assert.Contains("imsmanifest.xml", ex.Message, StringComparison.Ordinal);
        }
        finally { File.Delete(path); }
    }
}
