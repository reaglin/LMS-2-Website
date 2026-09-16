using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Windows;
using Lms2Website.Core;
using Lms2Website.Core.Cartridge;
using Lms2Website.Core.Model;
using Lms2Website.Core.Publish;
using Lms2Website.Core.Site;

namespace Lms2Website.App.ViewModels;

/// <summary>
/// The whole app in one object: read a cartridge, build the site, publish it. The window is three
/// steps down a page, and each step's state lives here.
/// </summary>
public sealed class MainViewModel : INotifyPropertyChanged
{
    private CourseSite? _course;
    private ProjectSettings _project = new();
    private CancellationTokenSource? _cancellation;

    public MainViewModel()
    {
        ChooseSourceCommand  = new RelayCommand(ChooseSource, () => !IsBusy);
        ChooseOutputCommand  = new RelayCommand(ChooseOutput, () => !IsBusy && _course != null);
        BuildCommand         = new RelayCommand(() => _ = BuildAsync(), () => !IsBusy && _course != null);
        OpenFolderCommand    = new RelayCommand(() => Open(OutputFolder), () => SiteBuilt);
        OpenSiteCommand      = new RelayCommand(() => Open(Path.Combine(OutputFolder, "index.html")), () => SiteBuilt);
        PublishCommand       = new RelayCommand(() => _ = PublishAsync(), CanPublish);
        OpenPagesCommand     = new RelayCommand(() => Open(PagesUrl), () => PagesUrl.Length > 0);
        CancelCommand        = new RelayCommand(() => _cancellation?.Cancel(), () => IsBusy);
        RefreshTokenStatus();
        RefreshCourses();
    }

    // ── the courses already converted ─────────────────────────────────────────

    private List<CourseStatus> _courses = new();
    /// <summary>Every course the app knows about, with how far each one has got.</summary>
    public List<CourseStatus> Courses
    {
        get => _courses;
        private set { Set(ref _courses, value); Raise(nameof(HasCourses)); }
    }

    public bool HasCourses => _courses.Count > 0;

    /// <summary>Re-reads the list from disk. Cheap, and never touches the network.</summary>
    public void RefreshCourses() => Courses = CourseStatus.All().ToList();

    /// <summary>Opens a course from the list by reading its export again.</summary>
    public void OpenCourse(CourseStatus status)
    {
        if (status.SourceExists) { _ = ReadAsync(status.SourcePath); return; }

        MessageBox.Show(
            $"The export for {status.Title} is not where it was:\n\n{status.SourcePath}\n\n" +
            "Choose the file again to carry on with this course.",
            "That export has moved", MessageBoxButton.OK, MessageBoxImage.Information);
        ChooseSource();
    }

    public void OpenCourseFolder(CourseStatus status) => Open(status.OutputFolder);
    public void OpenCourseSite(CourseStatus status) => Open(status.PagesUrl);

    // ── step 1: the export ────────────────────────────────────────────────────

    public RelayCommand ChooseSourceCommand { get; }

    private string _sourcePath = string.Empty;
    public string SourcePath { get => _sourcePath; private set => Set(ref _sourcePath, value); }

    private List<PreviewNode> _preview = new();
    public List<PreviewNode> Preview { get => _preview; private set => Set(ref _preview, value); }

    private string _courseTitle = string.Empty;
    public string CourseTitle
    {
        get => _courseTitle;
        set
        {
            if (!Set(ref _courseTitle, value) || _course == null) return;
            _course.Title = value;   // the title drives the site's heading and every page title
        }
    }

    private string _courseSummary = string.Empty;
    public string CourseSummary { get => _courseSummary; private set => Set(ref _courseSummary, value); }

    private string _readWarnings = string.Empty;
    public string ReadWarnings { get => _readWarnings; private set => Set(ref _readWarnings, value); }

    public bool HasCourse => _course != null;

    private void ChooseSource()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Choose the LMS export",
            Filter = "LMS export (*.imscc;*.zip)|*.imscc;*.zip|All files (*.*)|*.*",
            CheckFileExists = true
        };
        if (dialog.ShowDialog() == true) _ = ReadAsync(dialog.FileName);
    }

    public async Task ReadAsync(string path)
    {
        IsBusy = true;
        BusyMessage = "Reading " + Path.GetFileName(path);
        Progress = 0;
        try
        {
            var course = await Task.Run(() => CartridgeReader.Read(path));
            _course = course;

            SourcePath    = path;
            CourseTitle   = course.Title;
            CourseSummary = $"{course.Producer} export · {course.Summary()} · {Html.FileSize(course.SourceBytes)}";
            Preview       = PreviewNode.Build(course);
            ReadWarnings  = course.Warnings.Count == 0
                ? string.Empty
                : $"{course.Warnings.Count} note(s) while reading:\n• " + string.Join("\n• ", course.Warnings.Take(20));

            _project = SettingsStore.Load(course.Title);
            _project.CourseTitle = course.Title;
            _project.SourcePath  = path;
            if (string.IsNullOrWhiteSpace(_project.OutputFolder))
                _project.OutputFolder = ProjectSettings.DefaultOutputFolder(_project.Slug);

            OutputFolder = _project.OutputFolder;
            Owner        = _project.Owner;
            Repository   = string.IsNullOrWhiteSpace(_project.Repository) ? _project.Slug : _project.Repository;
            Branch       = string.IsNullOrWhiteSpace(_project.Branch) ? "main" : _project.Branch;
            IsPrivate    = _project.Private;
            PagesUrl     = _project.PagesUrl;
            ExcludeOverMb = _project.ExcludeOverMb > 0 ? _project.ExcludeOverMb.ToString() : string.Empty;
            ExcludeTypes  = _project.ExcludeTypes;
            ExcludeQuizzes = _project.ExcludeQuizzes;
            ExcludeFiles  = _project.ExcludeOverMb > 0 || _project.ExcludeTypes.Trim().Length > 0
                            || _project.ExcludeQuizzes;

            SiteBuilt   = false;
            BuildResult = string.Empty;
            BuildWarnings = string.Empty;
            Raise(nameof(HasCourse));
        }
        catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
        {
            _course = null;
            Raise(nameof(HasCourse));
            MessageBox.Show(ex.Message, "That file could not be read", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ── step 2: the website ───────────────────────────────────────────────────

    public RelayCommand ChooseOutputCommand { get; }
    public RelayCommand BuildCommand { get; }
    public RelayCommand OpenFolderCommand { get; }
    public RelayCommand OpenSiteCommand { get; }

    private string _outputFolder = string.Empty;
    public string OutputFolder { get => _outputFolder; set => Set(ref _outputFolder, value); }

    private bool _siteBuilt;
    public bool SiteBuilt
    {
        get => _siteBuilt;
        private set
        {
            if (Set(ref _siteBuilt, value))
                System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
    }

    private string _buildResult = string.Empty;
    public string BuildResult { get => _buildResult; private set => Set(ref _buildResult, value); }

    private string _buildWarnings = string.Empty;
    public string BuildWarnings { get => _buildWarnings; private set => Set(ref _buildWarnings, value); }

    private void ChooseOutput()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Choose where to build the website",
            InitialDirectory = Directory.Exists(OutputFolder) ? OutputFolder : SettingsStore.Folder
        };
        if (dialog.ShowDialog() == true) OutputFolder = dialog.FolderName;
    }

    public async Task BuildAsync()
    {
        if (_course == null) return;
        if (!ConfirmOverwrite()) return;

        IsBusy = true;
        BusyMessage = "Building the website";
        Progress = 0;
        _cancellation = new CancellationTokenSource();
        try
        {
            var course = _course;
            var folder = OutputFolder;
            var progress = new Progress<(int Percent, string Message)>(p =>
            {
                Progress = p.Percent;
                BusyMessage = p.Message;
            });

            var rules = CurrentRules();
            var result = await Task.Run(() => SiteBuilder.Build(
                course, new SiteBuildOptions { OutputFolder = folder, Rules = rules },
                progress, _cancellation.Token));

            SiteBuilt = true;
            BuildResult = $"{result.PagesWritten} pages and {result.FilesCopied} files " +
                          $"({Html.FileSize(Publisher.SiteSize(folder, rules))}{(result.NotPublished.Count > 0 ? " to publish" : string.Empty)})" +
                          $" in {result.Elapsed.TotalSeconds:0.0} seconds.";
            BuildWarnings = result.Warnings.Count == 0
                ? string.Empty
                : $"{result.Warnings.Count} thing(s) worth knowing:\n• " + string.Join("\n• ", result.Warnings.Take(40));

            _project.OutputFolder  = folder;
            _project.ExcludeOverMb = ExcludeFiles ? ParseMb(ExcludeOverMb) : 0;
            _project.ExcludeTypes  = ExcludeFiles ? ExcludeTypes.Trim() : string.Empty;
            _project.ExcludeQuizzes = ExcludeFiles && ExcludeQuizzes;
            _project.LastBuiltUtc  = DateTime.UtcNow;
            _project.CourseTitle   = course.Title;
            SettingsStore.Save(_project);
            RefreshCourses();
        }
        catch (OperationCanceledException)
        {
            BuildResult = "Cancelled.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            MessageBox.Show(ex.Message, "The website could not be built", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _cancellation?.Dispose();
            _cancellation = null;
            IsBusy = false;
        }
    }

    /// <summary>A build empties the folder, so anything unexpected in it is worth a question.</summary>
    private bool ConfirmOverwrite()
    {
        if (string.IsNullOrWhiteSpace(OutputFolder))
        {
            MessageBox.Show("Choose a folder for the website first.", "Where should the website go?",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }
        if (!Directory.Exists(OutputFolder)) return true;

        var entries = Directory.GetFileSystemEntries(OutputFolder)
            .Where(e => !Path.GetFileName(e).Equals(".git", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (entries.Count == 0) return true;

        bool looksLikeOurSite = File.Exists(Path.Combine(OutputFolder, "index.html")) &&
                                Directory.Exists(Path.Combine(OutputFolder, "assets"));
        if (looksLikeOurSite) return true;   // rebuilding a site we made is the normal case

        return MessageBox.Show(
            $"{OutputFolder}\n\nholds {entries.Count} item(s) that were not put there by this app. " +
            "Building the website empties the folder first (a .git folder is kept).\n\nCarry on?",
            "That folder is not empty", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes;
    }

    // ── step 3: GitHub ────────────────────────────────────────────────────────

    public RelayCommand PublishCommand { get; }
    public RelayCommand OpenPagesCommand { get; }

    private bool _excludeFiles;
    /// <summary>Whether any file is kept out of the publish at all.</summary>
    public bool ExcludeFiles
    {
        get => _excludeFiles;
        set { if (Set(ref _excludeFiles, value)) Raise(nameof(ExcludeSummary)); }
    }

    private string _excludeOverMb = string.Empty;
    /// <summary>Megabytes, as typed. Blank or unreadable means "no size rule".</summary>
    public string ExcludeOverMb
    {
        get => _excludeOverMb;
        set { if (Set(ref _excludeOverMb, value)) Raise(nameof(ExcludeSummary)); }
    }

    private bool _excludeQuizzes;
    /// <summary>Leave the quizzes out of the website entirely — the answer key is in them.</summary>
    public bool ExcludeQuizzes
    {
        get => _excludeQuizzes;
        set { if (Set(ref _excludeQuizzes, value)) Raise(nameof(ExcludeSummary)); }
    }

    private string _excludeTypes = string.Empty;
    /// <summary>Types, as typed: "pptx, .zip mp4" all mean the same thing.</summary>
    public string ExcludeTypes
    {
        get => _excludeTypes;
        set { if (Set(ref _excludeTypes, value)) Raise(nameof(ExcludeSummary)); }
    }

    /// <summary>What the rules add up to, in the words the site itself will use.</summary>
    public string ExcludeSummary
    {
        get
        {
            if (!ExcludeFiles) return string.Empty;
            var rules = CurrentRules();
            if (!rules.AnyAtAll)
                return "Nothing is excluded yet — set a size, a list of types, tick quizzes, or any of them.";

            var text = $"Kept out of the publish: {rules.Describe()}. ";
            if (rules.Any)
                text += "Excluded files stay in the folder on this computer and are named on their own page. ";
            if (rules.ExcludeQuizzes)
                text += "Quizzes are not written at all, so no answer key reaches the folder. ";
            return text + "Build again to apply a change.";
        }
    }

    /// <summary>The rules as they stand in the window right now.</summary>
    private PublishRules CurrentRules() => ExcludeFiles
        ? new PublishRules
        {
            MaxBytes       = ParseMb(ExcludeOverMb) * 1024L * 1024L,
            Extensions     = PublishRules.ParseExtensions(ExcludeTypes),
            ExcludeQuizzes = ExcludeQuizzes
        }
        : PublishRules.None;

    /// <summary>Whole megabytes, or 0 for anything that is not a positive number.</summary>
    private static int ParseMb(string text) =>
        int.TryParse(text.Trim(), out var mb) && mb > 0 ? mb : 0;

    private string _owner = string.Empty;
    public string Owner
    {
        get => _owner;
        set { if (Set(ref _owner, value)) Raise(nameof(PublishTarget)); }
    }

    private string _repository = string.Empty;
    public string Repository
    {
        get => _repository;
        set { if (Set(ref _repository, value)) Raise(nameof(PublishTarget)); }
    }

    /// <summary>
    /// The repository this will actually land in, shown under the box so the L2W- prefix is
    /// never a surprise at the moment of pressing Publish.
    /// </summary>
    public string PublishTarget
    {
        get
        {
            var repository = RepoName.Apply(Repository);
            if (repository.Length == 0) return string.Empty;
            var owner = Owner.Trim();
            var full = owner.Length == 0 ? repository : $"{owner}/{repository}";
            return RepoName.HasPrefix(Repository)
                ? $"Publishes to {full}."
                : $"Publishes to {full} — course sites are always named {L2W.RepoPrefix}… so they cannot land on a repository you made by hand.";
        }
    }

    private string _branch = "main";
    public string Branch { get => _branch; set => Set(ref _branch, value); }

    private bool _isPrivate;
    public bool IsPrivate
    {
        get => _isPrivate;
        set { if (Set(ref _isPrivate, value)) Raise(nameof(PrivateWarning)); }
    }

    /// <summary>
    /// What ticking "private" actually does, which is not what the words suggest. A private
    /// repository hides the <i>files</i>; the website built from them is still public — GitHub's
    /// own wording is "GitHub Pages sites are publicly available on the internet, even if the
    /// repository for the site is private". Someone publishing course material needs to know that
    /// before they rely on it, not after.
    /// </summary>
    public string PrivateWarning => IsPrivate ? PublishWords.PrivateDoesNotMeanHidden : string.Empty;

    private string _publishLog = string.Empty;
    public string PublishLog { get => _publishLog; private set => Set(ref _publishLog, value); }

    private string _pagesUrl = string.Empty;
    public string PagesUrl { get => _pagesUrl; private set => Set(ref _pagesUrl, value); }

    private string _nextSteps = string.Empty;
    public string NextSteps { get => _nextSteps; private set => Set(ref _nextSteps, value); }

    private string _tokenStatus = string.Empty;
    public string TokenStatus { get => _tokenStatus; private set => Set(ref _tokenStatus, value); }

    public void RefreshTokenStatus()
    {
        var token = TokenStore.Load();
        TokenStatus = token == null
            ? "No GitHub token saved — the app will use Git and you will switch Pages on yourself."
            : $"Signed in with a saved token ({TokenStore.Mask(token)}). The app can create the repository and switch Pages on.";
    }

    private bool CanPublish() =>
        !IsBusy && SiteBuilt && Repository.Trim().Length > 0;

    public async Task PublishAsync()
    {
        var token = TokenStore.Load();
        var warnings = Publisher.Preflight(OutputFolder, CurrentRules());
        if (warnings.Count > 0)
        {
            var blocking = warnings.Where(w => w.Blocking).ToList();
            var text = string.Join("\n• ", warnings.Select(w => w.Message));
            if (blocking.Count > 0)
            {
                MessageBox.Show("• " + text, "GitHub will not accept this site",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            if (MessageBox.Show("• " + text + "\n\nPublish anyway?", "Before publishing",
                    MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        }

        IsBusy = true;
        BusyMessage = "Publishing to GitHub";
        Progress = 0;
        PublishLog = string.Empty;
        NextSteps = string.Empty;
        _cancellation = new CancellationTokenSource();

        var lines = new List<string>();
        void Log(string line)
        {
            lines.Add(line);
            Application.Current?.Dispatcher.Invoke(() => PublishLog = string.Join('\n', lines));
        }

        try
        {
            // Settle the name before anything is sent, and show the user what it became.
            Repository = RepoName.Apply(Repository);

            var request = new PublishRequest
            {
                SiteFolder    = OutputFolder,
                Owner         = Owner.Trim(),
                Repository    = Repository,
                Branch        = string.IsNullOrWhiteSpace(Branch) ? "main" : Branch.Trim(),
                Rules         = CurrentRules(),
                CommitMessage = $"Publish {CourseTitle} — {DateTime.Now:d MMMM yyyy HH:mm}",
                Description   = $"Course website for {CourseTitle}, converted from an LMS export.",
                Private       = IsPrivate
            };

            var progress = new Progress<(int Percent, string Message)>(p =>
            {
                Progress = p.Percent;
                BusyMessage = p.Message;
            });

            var result = await Publisher.PublishAsync(request, token, Log, progress, _cancellation.Token);

            PagesUrl  = result.PagesUrl;
            NextSteps = string.Join('\n', result.NextSteps);
            Log($"Done — {result.Method}.");

            _project.Owner            = Owner.Trim();
            _project.Repository       = request.Repository;
            _project.Branch           = request.Branch;
            _project.Private          = IsPrivate;
            _project.PagesUrl         = result.PagesUrl;
            _project.LastPublishedUtc = DateTime.UtcNow;
            SettingsStore.Save(_project);
            RefreshCourses();
        }
        catch (OperationCanceledException)
        {
            Log("Cancelled.");
        }
        catch (Exception ex) when (ex is GitHubException or InvalidOperationException or IOException or HttpRequestException)
        {
            Log("Stopped: " + ex.Message);
            MessageBox.Show(ex.Message, "The site was not published", MessageBoxButton.OK, MessageBoxImage.Warning);
        }
        finally
        {
            _cancellation?.Dispose();
            _cancellation = null;
            IsBusy = false;
        }
    }

    // ── busy state ────────────────────────────────────────────────────────────

    public RelayCommand CancelCommand { get; }

    private bool _isBusy;
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (!Set(ref _isBusy, value)) return;
            Raise(nameof(IsIdle));
            // WPF only re-asks CanExecute on user input, so a button whose state changed while
            // work was running would stay greyed until the mouse moved. Ask it to re-check now.
            System.Windows.Input.CommandManager.InvalidateRequerySuggested();
        }
    }

    public bool IsIdle => !_isBusy;

    private string _busyMessage = string.Empty;
    public string BusyMessage { get => _busyMessage; private set => Set(ref _busyMessage, value); }

    private int _progress;
    public int Progress { get => _progress; private set => Set(ref _progress, value); }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static void Open(string target)
    {
        if (string.IsNullOrWhiteSpace(target)) return;
        try
        {
            Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
        }
        catch (System.ComponentModel.Win32Exception ex)
        {
            MessageBox.Show($"{target}\n\n{ex.Message}", "That could not be opened",
                MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        Raise(name);
        return true;
    }

    private void Raise(string? name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
