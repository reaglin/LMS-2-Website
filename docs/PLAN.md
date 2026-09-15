# LMS-2-Website — the plan

## What it is

A Windows desktop app that turns an **IMS Common Cartridge export of a course** (`.imscc`) into a
**static website**, and puts that website on **GitHub Pages**. Three steps, one window:

1. Choose the `.imscc`.
2. Build the website into a folder.
3. Publish it to GitHub.

Nothing else. No AI, no editing, no course model to learn, no project format. PreseMaker already
does all of this as part of a much larger authoring tool; this app is the straight line through it
for a teacher who just wants the course readable on the open web.

## Who it is for

Ron, and any instructor with a course in Brightspace, Canvas, Moodle or Blackboard who wants:

- a public, linkable copy of the course reading material;
- an archive of a course that is about to be deleted from the LMS;
- a course site for students who cannot reach the LMS (auditors, prospective students, colleagues).

## Settled decisions (2026-09-14)

| Decision | Choice | Why |
|---|---|---|
| Coverage | **Everything, read-only**: pages, files, links, quizzes, assignments, discussions | A course archive with the assessments missing is half an archive. Nothing is interactive, so there is nothing to grade and nothing to cheat at. |
| Quizzes | A question page with the answer marked | The cartridge records which response scores; showing it is honest and useful for review. A question with no recorded answer says so rather than guessing. |
| GitHub | **Token preferred, Git as the fallback** | With a token the app creates the repository, pushes, and switches Pages on — the user never leaves the app. Without one it does a plain `git push` and tells the user to switch Pages on. |
| Transfer | Git when it is installed, the API when it is not | A course with lecture decks is easily 100 MB+; Git handles that, the API does not. Under 40 MB with no Git installed, the API route works fine. |
| Site look | A fresh template written for this app | No CDN, no fonts to fetch, no build step; works from disk and under a GitHub Pages project path. PreseMaker's CSS is built for a different content model. |
| Distribution | Free on the Microsoft Store | Same route as SMADA10 and Statistle. |
| UI | WPF on .NET 10 | Matches SMADA10, Statistle and EasyPeasyRetirement; MSIX packaging is already solved in those repos. |

## What the website looks like

```
index.html                    course home: the sections, as cards
assets/site.css               one stylesheet, light and dark
assets/site.js                the section menu and the search box
assets/search-index.js        every page's title and text, as a script (works from file://)
NN-section/index.html         one section, its items in course order
NN-section/item.html          a page, quiz, assignment, discussion or file
files/…                       every file the cartridge carried, renamed URL-safe
.nojekyll                     GitHub Pages serves the folder verbatim
README.md                     what made this, from what, when
```

Every page carries the course name, the section menu, a breadcrumb, previous/next, and a search
box that works without a server. A web link has no page of its own — the menus point straight at
the address.

## What the app is honest about

The conversion tells the user what it could not carry, rather than quietly dropping it:

- **Links back into the LMS** are marked on the page and counted in the notes — they will ask a
  visitor to sign in.
- **Images a quiz uses that the LMS did not export** (Brightspace does this) appear as
  "[image not included in the export]", not a broken image icon.
- **A question with no recorded answer** says the export does not record one.
- **Assignments and discussions** say work is still handed in through the LMS.
- **Anything the app cannot publish** is listed in the preview as "not published".

## Architecture

```
Lms2Website.Core (net10.0)            no UI, no Windows API
  Cartridge/   CcPackage (the zip, and the LMS quirks in it), CcManifestReader,
               CcResourceReaders (links, assignments, discussions), QtiReader (quizzes),
               CartridgeReader (the whole course, ready to preview)
  Model/       CourseSite → SiteModule → SiteItem (+ QuizContent)
  Site/        SiteBuilder (writes the folder), PageTemplate (the page shell),
               ContentRewriter (LMS HTML → site HTML), SiteAssets (the CSS and JS), Slug, Html
  Publish/     GitHubApi (REST), GitCli (the command line), Publisher (decides which),
               TokenStore (DPAPI), ProjectSettings (what to remember per course)
  Samples/     SampleCartridge — the demo .imscc, also what the tests run against

Lms2Website.App (net10.0-windows, WPF)
  MainWindow   the three steps, top to bottom
  ViewModels/  MainViewModel (all of it), PreviewNode, RelayCommand, converters
  Views/       TokenWindow
```

`Core` knows nothing about WPF, so the conversion is testable and could be driven from a command
line or a service. The app already exposes `--convert` and `--write-sample` for exactly that.

## Ported from PreseMaker, deliberately not shared

`CcPackage`, `CcManifestReader` and `CcResourceReaders` started as copies of PreseMaker's
`Services/CommonCartridge/*`. The two are **not** linked: this app publishes HTML, PreseMaker
imports into a course model, and a shared library would make both harder to change. A fix in one
is worth considering in the other — see `docs/CARTRIDGE-NOTES.md` for the ones already found.
