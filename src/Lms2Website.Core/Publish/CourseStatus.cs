using Lms2Website.Core.Site;

namespace Lms2Website.Core.Publish;

/// <summary>How far one course has got, in the order the three steps happen.</summary>
public enum StepState
{
    /// <summary>Not done.</summary>
    No,
    /// <summary>Done, but something has changed since.</summary>
    Stale,
    /// <summary>Done.</summary>
    Yes
}

/// <summary>
/// Where one course stands, read from the project file and the disk — never from the network. A
/// course has four things worth knowing, and they are asked in the order they happen:
///
/// <list type="number">
///   <item><b>Source</b> — is the export still where it was?</item>
///   <item><b>Converted</b> — has it been read and built at least once?</item>
///   <item><b>Site</b> — is the built website actually in the folder now?</item>
///   <item><b>Pushed</b> — is the folder a repository with a publish recorded against it?</item>
/// </list>
///
/// The fourth says only that the site was <b>pushed</b>. Whether it is reachable is a separate
/// question — GitHub Pages builds after a push, and the setting can be off — and it cannot be
/// answered from disk, so it is never implied. The window says outright that GitHub Pages has to
/// be checked for that, rather than quietly making requests on the user's behalf.
/// </summary>
public sealed class CourseStatus
{
    public required ProjectSettings Project { get; init; }

    public string Title => string.IsNullOrWhiteSpace(Project.CourseTitle) ? Project.Slug : Project.CourseTitle;

    // ── 1. the export ─────────────────────────────────────────────────────────

    public string SourcePath => Project.SourcePath;
    public string SourceName => string.IsNullOrWhiteSpace(SourcePath) ? string.Empty : Path.GetFileName(SourcePath);
    public bool SourceExists { get; private init; }
    public DateTime? SourceChangedUtc { get; private init; }

    public StepState Source => string.IsNullOrWhiteSpace(SourcePath) ? StepState.No
                             : SourceExists ? StepState.Yes
                             : StepState.Stale;   // remembered, but not there any more

    public string SourceText => Source switch
    {
        StepState.No => "no export recorded",
        StepState.Stale => $"{SourceName} — not where it was",
        _ => SourceName
    };

    // ── 2. converted ──────────────────────────────────────────────────────────

    public DateTime? BuiltUtc => Project.LastBuiltUtc;

    /// <summary>Built once, and the export has not been replaced since.</summary>
    public StepState Converted => BuiltUtc == null ? StepState.No
                                : SourceChangedUtc > BuiltUtc ? StepState.Stale
                                : StepState.Yes;

    public string ConvertedText => Converted switch
    {
        StepState.No => "never built",
        StepState.Stale => $"built {Local(BuiltUtc)} — the export is newer",
        _ => $"built {Local(BuiltUtc)}"
    };

    // ── 3. the website on disk ────────────────────────────────────────────────

    public string OutputFolder => Project.OutputFolder;
    public bool SiteExists { get; private init; }
    public int SitePages { get; private init; }
    public long SiteBytes { get; private init; }

    public StepState Site => SiteExists ? StepState.Yes : StepState.No;

    public string SiteText => SiteExists
        ? $"{SitePages} page(s), {Html.FileSize(SiteBytes)}"
        : BuiltUtc == null ? "not built yet" : "the folder is gone";

    // ── 4. pushed ─────────────────────────────────────────────────────────────

    /// <summary>The site folder is a git repository, so something has been committed here.</summary>
    public bool HasRepository { get; private init; }
    public DateTime? PublishedUtc => Project.LastPublishedUtc;
    public string PagesUrl => Project.PagesUrl;
    public string Repository => Project.Repository;

    public StepState Pushed => PublishedUtc == null ? StepState.No
                             : BuiltUtc > PublishedUtc ? StepState.Stale
                             : StepState.Yes;

    public string PushedText => Pushed switch
    {
        StepState.No => HasRepository ? "committed, never pushed from here" : "not pushed",
        StepState.Stale => $"pushed {Local(PublishedUtc)} — rebuilt since",
        _ => $"pushed {Local(PublishedUtc)}"
    };

    /// <summary>The one thing that would answer "is it live?", for the user to click.</summary>
    public bool CanOpenSite => PagesUrl.Length > 0;

    // ── reading it ────────────────────────────────────────────────────────────

    public static CourseStatus For(ProjectSettings project)
    {
        var sourceExists = !string.IsNullOrWhiteSpace(project.SourcePath) && File.Exists(project.SourcePath);
        DateTime? sourceChanged = null;
        if (sourceExists)
        {
            try { sourceChanged = File.GetLastWriteTimeUtc(project.SourcePath); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        var folder = project.OutputFolder;
        var siteExists = !string.IsNullOrWhiteSpace(folder)
                         && File.Exists(Path.Combine(folder, "index.html"));

        int pages = 0;
        long bytes = 0;
        if (siteExists)
        {
            try
            {
                foreach (var file in Publisher.PublishableFiles(folder))
                {
                    bytes += new FileInfo(file).Length;
                    if (file.EndsWith(".html", StringComparison.OrdinalIgnoreCase)) pages++;
                }
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        return new CourseStatus
        {
            Project          = project,
            SourceExists     = sourceExists,
            SourceChangedUtc = sourceChanged,
            SiteExists       = siteExists,
            SitePages        = pages,
            SiteBytes        = bytes,
            HasRepository    = siteExists && Directory.Exists(Path.Combine(folder, ".git"))
        };
    }

    /// <summary>Every course the app knows about, the one built most recently first.</summary>
    public static IReadOnlyList<CourseStatus> All() =>
        SettingsStore.Recent().Select(For).ToList();

    private static string Local(DateTime? utc) =>
        utc == null ? "never" : utc.Value.ToLocalTime().ToString("d MMM yyyy");
}
