using System.Text.Json;
using System.Text.Json.Serialization;
using Lms2Website.Core.Site;

namespace Lms2Website.Core.Publish;

/// <summary>
/// What the app remembers about one course between runs: where its cartridge and site are, and
/// where it publishes. Saved as JSON under <c>Documents\L2W\projects\</c>, keyed by the
/// course slug, so re-converting a newer export of the same course keeps the same repository.
/// No token is ever stored here — that lives in <see cref="TokenStore"/>.
/// </summary>
public sealed class ProjectSettings
{
    public string CourseTitle { get; set; } = string.Empty;
    /// <summary>The .imscc this site was last built from.</summary>
    public string SourcePath  { get; set; } = string.Empty;
    public string OutputFolder { get; set; } = string.Empty;

    public string Owner      { get; set; } = string.Empty;
    public string Repository { get; set; } = string.Empty;
    public string Branch     { get; set; } = "main";
    public bool   Private    { get; set; }

    /// <summary>Files bigger than this many MB are built but not published. 0 = publish them all.</summary>
    public int ExcludeOverMb { get; set; }
    /// <summary>Types kept out of the publish, as the user typed them (".pptx, .zip").</summary>
    public string ExcludeTypes { get; set; } = string.Empty;
    /// <summary>Leave the quizzes out of the website entirely — their answers are in them.</summary>
    public bool ExcludeQuizzes { get; set; }

    /// <summary>The two settings above as the rules the builder and the publisher both use.</summary>
    public PublishRules Rules() => new()
    {
        MaxBytes   = ExcludeOverMb > 0 ? ExcludeOverMb * 1024L * 1024L : 0,
        Extensions = PublishRules.ParseExtensions(ExcludeTypes),
        ExcludeQuizzes = ExcludeQuizzes
    };

    public DateTime? LastBuiltUtc     { get; set; }
    public DateTime? LastPublishedUtc { get; set; }
    public string PagesUrl { get; set; } = string.Empty;

    [JsonIgnore] public string Slug { get; set; } = string.Empty;

    /// <summary>
    /// The default site folder for a course: <c>Documents\L2W\sites\L2W-{slug}</c>. The folder is
    /// named exactly like the repository it will be pushed to, so a folder on disk and a repository
    /// on GitHub are recognisably the same thing — and so a site folder is never mistakable for
    /// something the user made themselves.
    /// </summary>
    public static string DefaultOutputFolder(string slug) =>
        Path.Combine(SettingsStore.Folder, "sites", RepoName.Apply(slug));
}

public static class SettingsStore
{
    /// <summary>
    /// <c>Documents\L2W</c>. In Documents because that is where people look for their own work and
    /// are meant to be able to open the folder directly; the token is the one thing that lives
    /// elsewhere (see <see cref="TokenStore"/>).
    /// </summary>
    public static string Folder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "L2W");

    /// <summary>What the folder was called before it was shortened to L2W.</summary>
    private static string LegacyFolder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "LMS 2 Website");

    private static string ProjectsFolder => Path.Combine(Folder, "projects");

    /// <summary>
    /// Brings forward what an older build remembered about each course. Only the small JSON files
    /// move: a site folder can be a hundred megabytes and each project records its own path, so
    /// existing sites are left exactly where they are and keep working.
    /// </summary>
    private static void MigrateProjects()
    {
        var legacyProjects = Path.Combine(LegacyFolder, "projects");
        if (Directory.Exists(ProjectsFolder) || !Directory.Exists(legacyProjects)) return;
        try
        {
            Directory.CreateDirectory(ProjectsFolder);
            foreach (var file in Directory.GetFiles(legacyProjects, "*.json"))
                File.Copy(file, Path.Combine(ProjectsFolder, Path.GetFileName(file)), overwrite: false);
        }
        catch (IOException) { /* the app still works; the courses list simply starts empty */ }
        catch (UnauthorizedAccessException) { }
    }

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = true };

    public static string PathFor(string slug) => Path.Combine(ProjectsFolder, slug + ".json");

    public static ProjectSettings Load(string courseTitle)
    {
        MigrateProjects();
        var slug = Slug.From(courseTitle, "course");
        var path = PathFor(slug);
        if (File.Exists(path))
        {
            try
            {
                var loaded = JsonSerializer.Deserialize<ProjectSettings>(File.ReadAllText(path));
                if (loaded != null) { loaded.Slug = slug; return loaded; }
            }
            catch (JsonException) { /* a corrupt file should not stop the app — start fresh */ }
            catch (IOException) { }
        }

        return new ProjectSettings
        {
            Slug         = slug,
            CourseTitle  = courseTitle,
            Repository   = RepoName.Apply(slug),
            OutputFolder = ProjectSettings.DefaultOutputFolder(slug)
        };
    }

    public static void Save(ProjectSettings settings)
    {
        if (string.IsNullOrWhiteSpace(settings.Slug))
            settings.Slug = Slug.From(settings.CourseTitle, "course");
        Directory.CreateDirectory(ProjectsFolder);
        File.WriteAllText(PathFor(settings.Slug), JsonSerializer.Serialize(settings, Json));
    }

    /// <summary>Every course the app has converted, most recently built first.</summary>
    public static IReadOnlyList<ProjectSettings> Recent()
    {
        MigrateProjects();
        if (!Directory.Exists(ProjectsFolder)) return [];
        var list = new List<ProjectSettings>();
        foreach (var file in Directory.GetFiles(ProjectsFolder, "*.json"))
        {
            try
            {
                var settings = JsonSerializer.Deserialize<ProjectSettings>(File.ReadAllText(file));
                if (settings == null) continue;
                settings.Slug = Path.GetFileNameWithoutExtension(file);
                list.Add(settings);
            }
            catch (JsonException) { }
            catch (IOException) { }
        }
        return list.OrderByDescending(s => s.LastBuiltUtc ?? DateTime.MinValue).ToList();
    }
}
