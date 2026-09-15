# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

**LMS 2 Website** — a Windows 10/11 desktop app that turns an **IMS Common Cartridge export of a
course** (`.imscc`, from Brightspace, Canvas, Moodle or Blackboard) into a **static website**, and
publishes it to **GitHub Pages**. Three steps in one window: choose the export, build the site,
publish. WPF on .NET 10, free on the Microsoft Store (planned).

PreseMaker can already do this as part of a much larger AI authoring tool. This app is the straight
line through it: no AI, no course model, no project file.

**Status: phases 0–4 built, 2026-09-14** (the day it was started). The conversion is verified
against a real 135 MB Brightspace export of EGN3443 — 19 sections, 88 pages, 15 quizzes, 13
discussions, 13 assignments — which becomes 149 pages and 25 files in about a second and a half.
103 tests green. The window has been driven end-to-end (open a cartridge, build the site) through UI
Automation. **The GitHub token route has never run against GitHub** — the Git route is tested
against a local bare repository; the API half needs Ron's token and one real publish
(`docs/MANUAL-TESTING.md`, test 5). Phase 5 is the Store.

### Read these first

| File | What it is |
|---|---|
| `docs/DEVELOPMENT-PLAN.md` | **The work breakdown** — phases, tasks, what is done, and the open questions for Ron. Start here. |
| `docs/PLAN.md` | The strategy — who it is for, the settled decisions and why, the site layout, the architecture. |
| `docs/CARTRIDGE-NOTES.md` | What is actually inside an LMS export: the D2L quirks, the QTI rules. Every line cost time to find. |
| `docs/MANUAL-TESTING.md` | The hand-test script, including the GitHub tests that have not been run. |

## Solution structure

```
LMS2Website.sln
├── src/Lms2Website.Core/     net10.0. No UI, no Windows API — the conversion is testable alone.
│   Cartridge/   CcPackage (the zip + the LMS quirks in it), CcManifest, CcManifestReader,
│                CcResourceReaders (web links, LTI, assignments, discussions), QtiReader (quizzes,
│                read-only, with the answer key), CartridgeReader (the whole course)
│   Model/       CourseSite → SiteModule → SiteItem (+ QuizContent, SiteAsset, ItemKind)
│   Site/        SiteBuilder (writes the folder), PublishRules (what is built but not published),
│                PageTemplate (the shell every page shares),
│                ContentRewriter (LMS HTML → site HTML), SiteAssets (the CSS and JS as strings),
│                Slug, Html
│   Publish/     Publisher (picks the route), GitHubApi (REST), GitCli (the command line),
│                TokenStore (DPAPI), RepoName (the L2W- rule), ProjectSettings + SettingsStore
│   L2W.cs       the marks that say a repository and a folder were made by this app
│   Samples/     SampleCartridge — the demo .imscc, and what the tests run against
├── src/Lms2Website.App/      net10.0-windows, WPF. Assembly name LMS2Website.
│   MainWindow   three steps down one page, a busy strip at the bottom
│   ViewModels/  MainViewModel (all the state), PreviewNode, RelayCommand, Converters
│   Views/       TokenWindow, HelpWindow (publishing explained for a GitHub novice)
├── tests/Lms2Website.Tests/  xUnit: the reader, QTI, slugs, the site builder, publishing
├── samples/     demo-course.imscc (written by `--write-sample`)
├── docs/        the four files above
├── tools/       capture-window.ps1, drive-app.ps1 — look at the running window without a human
└── manual-test/ the hand-test build (gitignored)
```

## Build, test, run

```powershell
dotnet build LMS2Website.sln
dotnet test LMS2Website.sln
dotnet run --project src/Lms2Website.App

# no window:
.\manual-test\LMS2Website.exe --convert <course.imscc> <output folder>   # writes the site + convert-log.txt
.\manual-test\LMS2Website.exe --write-sample samples                     # rewrites the demo cartridge
```

Hand-test build (framework-dependent; this machine has the .NET 10 runtime):

```powershell
dotnet publish src/Lms2Website.App/Lms2Website.App.csproj -c Release -r win-x64 `
    --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o manual-test
```

## The ideas that carry the design

1. **The preview cannot lie.** `CartridgeReader` parses every payload — page HTML, quiz, assignment,
   discussion, link — before the window shows a count. What step 1 lists is what step 2 writes.
2. **Say what was lost.** A reference that is not in the cartridge, an image the LMS did not export,
   a link back into the LMS, a question with no recorded answer: each is named on the page and
   counted in the notes. Never a broken image icon, never a guessed answer.
3. **The site needs nothing.** No CDN, no web fonts, no build step, no server. The same folder opens
   from disk and serves from a GitHub Pages project path. Search is a script that sets one array.
4. **A publish is a clean overwrite of one branch.** The site folder is rebuilt from the cartridge
   every time, so `git push --force` is the honest operation. A `.git` folder inside the site
   survives a rebuild.
5. **Token first, Git second.** With a token the app creates the repository, pushes and switches
   Pages on; the transfer still goes through Git when Git is installed, because a course with
   lecture decks is 100 MB+. Without a token it is a plain push and the user switches Pages on.
6. **A generated site says so, in four places.** The repository is named `L2W-<course>`, carries
   the `lms-2-website` topic and says "Built with LMS 2 Website" in its description; the site root
   holds `l2w-site.json`; every page carries `<meta name="generator">`. The prefix is the
   load-bearing one — publishing is a force-push and the name is proposed from the course title, so
   without it a course called EGN3443 would aim straight at the real `reaglin/EGN3443`.
   `RepoName.Apply` is the single rule: applied to the proposed name and again at publish, shown
   under the box beforehand, and it never doubles the prefix.
7. **What is not published says so where it would have been.** `PublishRules` (over a size, of a
   type, or the quizzes) reaches the *builder*, not just the push — so the app writes the `.gitignore` and marks
   each excluded file on its page in the same pass. The folder on disk stays whole; only the push
   is trimmed. Git honours the file, and `GitHubApi.UploadFolderAsync` applies the rules itself,
   because it walks the folder rather than reading `.gitignore`. Quizzes are the exception that
   proves the rule: an answer key is not written at all rather than written and held back, so there
   is nothing in the folder to publish by mistake — and since every list in the site already filters
   on `Include`, clearing it makes a quiz vanish with nothing left pointing at it.
8. **Nobody is told something comforting and wrong.** "Private repository" reads as "only my
   students can see it", and that is false — GitHub Pages serves a site publicly even when its
   repository is private, and restricting viewers needs Enterprise Cloud. `PublishWords` holds that
   sentence once, the window and the publish log both use it, and `PublishWordsTests` fails if it
   ever drifts back towards the comfortable version. The audience is assumed not to know GitHub.
9. **The token never leaks.** DPAPI for the current user in `%LOCALAPPDATA%\LMS 2 Website` — sites live in Documents where people look, a secret does not; spliced onto
   the push URL for one push and taken back out of `.git/config` afterwards; `GitCli.Redact` runs
   over every line that reaches the log.

## Conventions

- `Directory.Build.props`: one version property (`Lms2WebsiteVersion`), nullable, implicit usings,
  **TreatWarningsAsErrors**. Catch specific exceptions, never bare `catch (Exception)`.
- Packages must be MIT/BSD/Apache: AngleSharp (MIT) parses the LMS's HTML;
  `System.Security.Cryptography.ProtectedData` for the token.
- `Core` never references a UI assembly, and never `System.Windows.*`.
- WPF: a `TextBox`, `ProgressBar` or anything else bound to a get-only view-model property needs
  `Mode=OneWay` — the default is TwoWay and WPF throws at load. Two startup crashes came from that.
  After async work, call `CommandManager.InvalidateRequerySuggested()` or buttons stay greyed until
  the mouse moves.
- Checking the window: launch it and capture with **PrintWindow** (`PW_RENDERFULLCONTENT`), never a
  screen grab; drive it with **UI Automation**, never synthetic input. `Process.MainWindowHandle`
  comes back 0 for this app, so find the window by enumerating the process's top-level windows.
  `tools\capture-window.ps1` (launch and photograph) and `tools\drive-app.ps1` (open a cartridge,
  set the output folder, press Build, photograph) do both. Rendered pages of the generated site can
  be checked with headless Chrome (`--headless=new --disable-lcd-text --screenshot`).
- Hand-test at the end of every phase (`docs/MANUAL-TESTING.md`) — Ron wants something to look at,
  not just green tests.

## Sibling repos worth knowing

- `..\PreseMaker` — where `CcPackage`, `CcManifestReader` and `CcResourceReaders` were copied from
  (`Services/CommonCartridge/`), and where the GitHub push began
  (`Services/WebsiteGitHubPublishService.cs`). **The copies are not linked.** This repo has since
  fixed a D2L href case PreseMaker still misses — see `docs/CARTRIDGE-NOTES.md`.
- `..\Statistle`, `..\SMADA10` — the WPF/.NET 10 Store apps whose `packaging/` scripts phase 5 will
  port (`pack.ps1`, `make-msixupload.ps1`, `run-wack.ps1`, `make-store-assets.ps1`).
