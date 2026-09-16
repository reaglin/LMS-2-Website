using Lms2Website.Core.Publish;

namespace Lms2Website.Tests;

public class OutputFolderTests
{
    [Fact]
    public void TheAppFolderIsDocumentsL2W()
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        Assert.Equal(Path.Combine(documents, "L2W"), SettingsStore.Folder);
    }

    /// <summary>
    /// The folder on disk is named exactly like the repository it will be pushed to, so the two
    /// are recognisably the same thing — and a site folder can never be mistaken for one the user
    /// made themselves.
    /// </summary>
    [Fact]
    public void ASiteFolderIsNamedLikeItsRepository()
    {
        var folder = ProjectSettings.DefaultOutputFolder("egn3443");

        Assert.EndsWith(Path.Combine("L2W", "sites", "L2W-egn3443"), folder, StringComparison.Ordinal);
        Assert.Equal(RepoName.Apply("egn3443"), Path.GetFileName(folder));
    }

    [Fact]
    public void ThePrefixIsNotDoubledInTheFolderName() =>
        Assert.Equal("L2W-egn3443", Path.GetFileName(ProjectSettings.DefaultOutputFolder("L2W-egn3443")));
}
