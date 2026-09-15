# LMS-2-Website — development plan

Numbered tasks per phase, each with what "done" means. Phases 0–4 were built on **2026-09-14**;
phase 5 (the Store) and phase 6 (polish) are open.

---

## Phase 0 — the repository ✅ 2026-09-14

| # | Task | Done when |
|---|---|---|
| 0.1 | Solution, `Directory.Build.props`, `.gitignore` | ✅ `dotnet build` clean with `TreatWarningsAsErrors` |
| 0.2 | `Lms2Website.Core` (net10.0) and `Lms2Website.App` (net10.0-windows, WPF) | ✅ |
| 0.3 | xUnit test project | ✅ |
| 0.4 | `docs/PLAN.md`, this file | ✅ |

---

## Phase 1 — read the cartridge ✅ 2026-09-14

| # | Task | Done when |
|---|---|---|
| 1.1 | `CcPackage`: open the zip, resolve manifest hrefs through the LMS's quirks | ✅ Resolves Brightspace's `page.html;/Display Name.html` and its Cyrillic `сontent/` folder |
| 1.2 | `CcManifestReader`: CC 1.0–1.3 and plain IMS CP, namespace-agnostic | ✅ |
| 1.3 | `CcResourceReaders`: web links, LTI links, assignments, discussions | ✅ Bodies kept as HTML |
| 1.4 | `QtiReader`: QTI 1.2 into read-only questions with the answer key | ✅ Multiple choice, true/false, multi-select, short answer, essay, matching |
| 1.5 | `CartridgeReader`: the organization tree → sections, items, slugs | ✅ Nested folders become headings inside a section |
| 1.6 | Tests against a synthetic cartridge carrying the real quirks | ✅ `SampleCartridge` + 13 tests |

**Verified on a real export:** `D2LCCExport_511555_EGN3443…imscc` (135 MB, 167 entries) reads in
0.2 s as 19 sections · 88 pages · 15 quizzes · 13 discussions · 13 assignments, no warnings.

---

## Phase 2 — build the website ✅ 2026-09-14

| # | Task | Done when |
|---|---|---|
| 2.1 | `SiteAssets`: the stylesheet and script, self-contained, light and dark | ✅ No CDN, no web fonts |
| 2.2 | `PageTemplate`: header, section menu, breadcrumb, previous/next | ✅ |
| 2.3 | `ContentRewriter`: strip the LMS's scripts and stylesheets, repoint every reference | ✅ Page-to-page links survive; LMS links are marked; a missing image says so |
| 2.4 | `SiteBuilder`: item pages, section pages, home page, files, search index | ✅ |
| 2.5 | Quiz, assignment, discussion and file pages | ✅ Answers marked; "handed in through the LMS" said plainly |
| 2.6 | Client-side search with no server | ✅ `assets/search-index.js`, works from `file://` |
| 2.7 | Rebuild in place keeps `.git` | ✅ Tested |

**Verified on the same export:** 149 pages, 25 files, 134 MB, 1.4 s.

---

## Phase 3 — the window ✅ 2026-09-14

| # | Task | Done when |
|---|---|---|
| 3.1 | Three steps down one window, with a busy strip and Cancel | ✅ |
| 3.2 | Step 1: choose or drop a cartridge; editable course title; preview tree | ✅ |
| 3.3 | Step 2: output folder, Build, Open in browser, Open folder | ✅ Driven end-to-end through UI Automation |
| 3.4 | Step 3: account, repository, branch, private, Publish | ✅ Built; see phase 4 for what is untested |
| 3.5 | Token window: paste, check against GitHub, save, remove | ✅ |
| 3.6 | Remember each course's folder and repository between runs | ✅ `Documents\LMS 2 Website\projects\<slug>.json` |
| 3.7 | `--convert` and `--write-sample` command lines | ✅ |

---

## Phase 4 — publish ✅ code complete, part hand-test pending

| # | Task | Done when |
|---|---|---|
| 4.1 | `TokenStore`: DPAPI, current user | ✅ |
| 4.2 | `GitCli`: init → commit → force-push, token on the URL for one push only | ✅ Tested against a local bare repository |
| 4.3 | `GitHubApi`: whoami, find/create repository, upload a folder, switch Pages on | ⚠ **Not yet run against GitHub** — needs Ron's token |
| 4.4 | `Publisher`: token route, Git route, and the preflight size checks | ✅ Logic tested; the GitHub half rides on 4.3 |
| 4.5 | Errors a person can act on (bad token, missing permission, rate limit, 100 MB file) | ✅ Written; the messages themselves are untested against real failures |
| 4.6 | Every published site is marked as one: `L2W-` repository name, `lms-2-website` topic, "Built with LMS 2 Website" description, `l2w-site.json`, generator meta | ✅ 2026-09-15. `RepoName.Apply` is the one rule; the name is shown under the box before Publish. The prefix also makes it impossible to force-push a course site onto a hand-made repository |

**Left to do:** one real publish with Ron's token. The runbook was **prepared 2026-09-15** and is
ready to run as-is — `docs/MANUAL-TESTING.md`, test 5 — with the binary rebuilt from `9538f1f`, the
exact fine-grained token permissions (Administration + Contents + Pages, all read/write, scoped to
all repositories), and a free repository name.  Until it has run, the token route is code, not a
verified feature.

Git is installed on this machine, so the token route pushes with Git and
`GitHubApi.UploadFolderAsync` is **not** exercised by test 5; test 5 carries a recipe for covering
it separately.

---

## Phase 5 — the Microsoft Store ⬜

| # | Task | Done when |
|---|---|---|
| 5.1 | App icon and tiles from artwork Ron provides | A 2048×2048 source image exists in `resources/` |
| 5.2 | Port `packaging/` from Statistle (`pack.ps1`, `make-msixupload.ps1`, `run-wack.ps1`, `make-store-assets.ps1`, Inno Setup installer) | A signed dev MSIX installs on this machine |
| 5.3 | `AppxManifest.xml` with the identity Partner Center reserves | PFN matches the reservation |
| 5.4 | Privacy page on PunchMonkeyServer (`/privacy/lms2website`) | Live, names the app and Dean Eaglin |
| 5.5 | WACK, listing copy, screenshots, submit | WACK passes; `resources/STORE-SUBMISSION.md` complete |

Statistle's `resources/STORE-SUBMISSION.md` is the template for the whole phase.

---

## Phase 6 — the things worth doing next ⬜

| # | Task | Why |
|---|---|---|
| 6.1 | Tick boxes in the preview to leave sections or items out | The model already carries `Include`; only the view model work is missing |
| 6.2 | A course home page written by the user (a paragraph above the section cards) | Right now the home page shows only the cartridge's own description |
| 6.3 | Remember several courses and offer them on start-up | `SettingsStore.Recent()` already returns them |
| 6.4 | ~~Leave files out of the **publish**~~ ✅ **2026-09-15** | Answer to question 2. `PublishRules` (size, type, and quizzes) reaches the builder, so the app writes the `.gitignore` *and* marks every excluded file on its own page — no published page links to a file the site does not carry. Quizzes go further: not written at all, because the answer key is in them, so nothing reaches the folder to be published by accident. The API upload path applies the same rules, and the size checks no longer count files that are never sent |
| 6.5 | Canvas and Moodle exports hand-checked | Only Brightspace has been run through a real export |
| 6.6 | An "unpublish" that empties the branch | Deleting a course site currently means doing it on github.com |
| 6.7 | A setting for where sites are written: Documents (the default), a local drive, or a folder of the user's choosing | Answer to question 1 — Documents stays the default because that is where people look, but a machine whose Documents is a synced work OneDrive should be able to opt out. Not started: Ron wants to explore it first |
| 6.8 | ~~Move `github-token.dat` to `%LOCALAPPDATA%`~~ ✅ **2026-09-15** | Sites stay in Documents (question 1); the token does not, because nobody browses to a DPAPI blob. A token saved by an older build is moved out of Documents once, on first read, and **Remove** deletes both places |
| 6.9 | ~~**Publishing, explained for someone who does not know GitHub**~~ ✅ **2026-09-15** | Answer to question 3. A "New to GitHub?" button in the header — reachable before a site is built, which is when it is wanted — opens a help window that assumes nothing: what a repository and Pages are, the address you will get, why the repository must be public, what a token is, and the five clicks to switch Pages on. Step 3 carries a short version inline, and the Git-only next-steps are now numbered |
| 6.10 | ~~Say plainly what "Private repository" costs~~ ✅ **2026-09-15** | Answer to question 3 — but **not** with the wording first asked for. "Only people with access can see it" is false: GitHub's own documentation says "GitHub Pages sites are publicly available on the internet, even if the repository for the site is private". Ticking the box now raises a warning saying exactly that, shared word for word with the publish log via `PublishWords`, and pinned by tests so it cannot drift back |

---

## Open questions for Ron — all 3 answered (asked 2026-09-14, answered 2026-09-15)

1. ~~**Should the default output folder move off OneDrive?**~~ **Answered 2026-09-15: no — Documents
   stays.** It is where people will look for their data, and going to the folder directly is part of
   how the app is meant to be used. A local drive becomes an *option*, not the default (task 6.7),
   and Ron wants to explore the shape of that first.

   Context that stands: `MyDocuments` here resolves to
   `C:\Users\ronal\OneDrive - Daytona State College\Documents`, so course sites sync into the
   college tenant. Two things soften it — the deck opt-out in 6.4 would cut the bulk at its source
   (question 2), and the token could move to `%LOCALAPPDATA%` on its own (task 6.8) without
   touching where sites go, since nobody browses to the token.
2. ~~**Should the decks be an opt-out?**~~ **Answered 2026-09-15: yes, as a "side" option — a
   `.gitignore` the app writes into the site folder,** with two kinds of rule: everything over a
   given size, and everything of a given type. Task 6.4. Two things to settle before it is built:

   - **A `.gitignore` trims the push, not the build.** The folder on disk keeps every file, so the
     local site is whole; the published site is missing them, and the download links on its pages
     point at files that are not there. That runs against "say what was lost" (`CLAUDE.md`, idea 2),
     which is the reason this app never leaves a broken reference. Either the builder learns the
     same rules and marks those items on the page, or the published site carries dead links by
     design. **Open — see the question put to Ron.**
   - **Only one of the two publish routes honours it.** `GitCli` runs `git add -A`, so a
     `.gitignore` at the site root just works. `GitHubApi.UploadFolderAsync` walks the folder
     itself and would upload the excluded files anyway, so it needs the same rules applied in code.
3. ~~**Which GitHub account** publishes the course sites?~~ **Answered 2026-09-15: whichever the user
   says.** There is no fixed account or organisation — the person publishing names the place, and
   they must already have access to it. Two things follow:

   - **The repository has to be public.** A private one cannot serve Pages without a paid plan, and
     a course site nobody can open is not a published course site. Task 6.10.
   - **The audience does not know GitHub.** The app has to convey which settings are needed and how
     to set them, in plain language, rather than assuming an account, a repository and Pages are
     familiar words. Task 6.9.
