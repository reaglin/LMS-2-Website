# Hand-testing LMS 2 Website

The automated tests cover the conversion and the Git push. What they cannot cover is the window,
the browser, and GitHub itself — those are here.

## Build a hand-test copy

```powershell
dotnet publish src/Lms2Website.App/Lms2Website.App.csproj -c Release -r win-x64 `
    --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o manual-test
```

`manual-test\` is gitignored. The machine has the .NET 10 runtime, so the framework-dependent build
is enough.

A demo cartridge lives at `samples\demo-course.imscc` (two sections, two pages, an image, a deck, a
quiz, an assignment, a discussion and a link). Re-write it with:

```powershell
.\manual-test\LMS2Website.exe --write-sample samples
```

---

## 1. Convert the demo course

1. Start `LMS2Website.exe`.
2. Drag `samples\demo-course.imscc` onto the window.
   - **Expect:** "Brightspace export · 2 sections · 2 pages · 1 link · 1 quiz · 1 assignment · 1 discussion".
   - **Expect:** the tree shows `Week 1: Getting Started` and `Week 2 / Measurement`, with
     `Say hello` under an **Extras** heading.
3. Press **Build website**, then **Open in browser**.

Check on the site:

- [ ] The home page lists both sections with their counts.
- [ ] The overview page's link to the lecture notes goes to the lecture page inside the site.
- [ ] The link to `class.example.edu/d2l/…` is marked "(link inside the LMS)".
- [ ] The lecture page shows the diagram and offers `lecture-1.pptx` as a download.
- [ ] The quiz page marks **sample** as the correct answer, and shows "Answer: 4" for the short one.
- [ ] The assignment page says 25 points and that work is handed in through the LMS.
- [ ] Search for "hello" finds the discussion; search works with the page opened from disk.
- [ ] The section menu marks the page you are on; previous/next walks the course in order.
- [ ] Narrow the window to phone width: the menu collapses behind the **Sections** button.
- [ ] Switch Windows to dark mode and reload: the site follows.

## 2. Convert a real course export

Use a `.imscc` from Brightspace (`C:\Users\ronal\Downloads\D2LCCExport_*.imscc`).

- [ ] Reading a 135 MB export takes a second or so and reports no warnings.
- [ ] Building writes ~149 pages and takes a couple of seconds.
- [ ] Open **Notes from the conversion** — the LMS links and the quiz images Brightspace left out
      are listed there, with the item they belong to.
- [ ] Spot-check three pages against the course in Brightspace: the text, the images and the
      lecture deck all arrive.
- [ ] A quiz with 40+ questions renders in order, each with its type and points.

## 3. Rebuilding

- [ ] Build twice into the same folder: the second build replaces it and does not double anything.
- [ ] Put a file in the folder by hand, build again: the app warns before emptying a folder it did
      not write.
- [ ] Build into a folder that is already a git repository: `.git` survives.

## 4. Publish with Git only (no token)

Needs Git for Windows and a repository you have already made on github.com.

1. Remove any saved token (**GitHub token…** → Remove).
2. Fill in the account and repository, press **Publish**.
   - [ ] The log shows init, add, commit, push.
   - [ ] The next-steps line tells you to switch Pages on, with the settings URL.
   - [ ] No token or password appears anywhere in the log.
3. Switch Pages on in the repository settings, wait a minute, open the address.
   - [ ] The site works, including the search box and the files.

## 5. Publish with a token — **set up 2026-09-15, not yet run**

The preparation below was done and checked on 2026-09-15. Nothing needs building or verifying
first — start at **Make the token**.

### Already prepared and passing

- `manual-test\LMS2Website.exe` rebuilt from commit `9538f1f`; solution builds clean, 61/61 tests green.
- The demo cartridge runs through that exact binary: 8 pages, 2 files, 0.2 s, `.nojekyll` written.
- **Git 2.53.0** is on PATH, so the token route will push **with Git** — see *What this will not cover*.
- No token is saved and no project settings exist yet, so step 2 starts from clean.
- `reaglin/lms2website-handtest` does not exist — the name in step 3 is free.

### Where the app keeps things on this machine

`Environment.SpecialFolder.MyDocuments` resolves to the **college OneDrive**, not `C:\Users\ronal\Documents`:

| What | Where |
|---|---|
| Token (DPAPI, current user) | `C:\Users\ronal\OneDrive - Daytona State College\Documents\LMS 2 Website\github-token.dat` |
| Per-course settings | `…\LMS 2 Website\projects\<slug>.json` |
| Default output folder | `…\LMS 2 Website\sites\<slug>` |

The token blob is encrypted for this Windows user and is useless on any other machine or account,
but it does sync into the Daytona State tenant. So does the built site — that is open question 1.

### Make the token

`github.com/settings/personal-access-tokens/new` → **Fine-grained**; a 7-day expiry is plenty.

- **Repository access → All repositories.** The repository in step 3 does not exist yet, so a token
  limited to selected repositories cannot create it.
- Repository permissions:

  | Permission | Access | Why it is needed |
  |---|---|---|
  | Administration | Read and write | `POST /user/repos` — creating the repository |
  | Contents | Read and write | the push |
  | Pages | Read and write | switching Pages on |

If GitHub still refuses to create the repository, a **classic** token with the `repo` scope is the
documented fallback.

### Run it

Use `samples\demo-course.imscc`. It is 64 KB built, so a failure costs seconds, and it publishes no
course material before open question 2 is settled.

1. Start `manual-test\LMS2Website.exe`, drop the demo cartridge on it, press **Build website**.
2. **GitHub token…** → paste → **Check and save**.
   - [ ] It reports `reaglin`.
   - [ ] The token is then shown masked (`gith…abcd`), never in full.
3. Repository `lms2website-handtest`, branch `main`, **not** private. Press **Publish**.
   - [ ] The log says "Creating reaglin/lms2website-handtest…".
   - [ ] It pushes with Git 2.53.0, then reports Pages serving `main`.
   - [ ] **No token text anywhere in the log** — every URL is redacted.
   - [ ] `https://reaglin.github.io/lms2website-handtest/` appears in the window.
4. Wait a minute or two — the first Pages build 404s until it finishes — then open that address.
   - [ ] The site works from the Pages URL: section menu, search, and `files/lecture-1.pptx`.
5. Check the token did not stay behind in the site folder's repo:

   ```powershell
   git -C "$([Environment]::GetFolderPath('MyDocuments'))\LMS 2 Website\sites\demo-course" config --get remote.origin.url
   ```

   - [ ] The URL has no token in it.
6. Rebuild the site and publish again.
   - [ ] The second publish replaces the branch and the site updates.
7. Try it wrong on purpose, with a second token missing the permission in question:
   - [ ] No Pages permission → the site still uploads, and the app says to switch Pages on by hand.
   - [ ] A revoked token → "GitHub did not accept the token".
   - [ ] Owner set to an account that is not yours → "the app can only create repositories under
         your own account".

### ⚠ One thing not to do

**Do not type `egn3443` as the repository name.** `reaglin/EGN3443` already exists — it is the course
repo Statistle follows — and publishing is `git push --force` onto the branch, so it would overwrite
it. The real export's own default is harmless: its course title is *Prob and Stats for
Engineers_521F_FA26_ON*, so the app proposes `prob-and-stats-for-engineers-521f-fa26-on`.

### What this will not cover

Git is installed, so the token route pushes with Git and **`GitHubApi.UploadFolderAsync` never
runs** — the file-by-file API upload stays untested. That is the path for a machine without Git, so
it matters for a Store release. Exercising it means taking Git off PATH for one run:

```powershell
$saved = $env:PATH
$env:PATH = ($env:PATH -split ';' | Where-Object { $_ -notmatch 'Git' }) -join ';'
.\manual-test\LMS2Website.exe          # build and publish the demo course again
$env:PATH = $saved
```

### Afterwards

Delete the throwaway repository (`github.com/reaglin/lms2website-handtest/settings`, bottom of the
page), or keep it if the published demo is worth showing. Then tick task 4.3 in
`docs/DEVELOPMENT-PLAN.md` and update the status line in `CLAUDE.md`.

## 6. The window itself

- [ ] Resize down to 720×560: nothing is cut off, the page scrolls.
- [ ] Cancel a build of the big course halfway: the app comes back, no crash.
- [ ] Choose a file that is not a cartridge: "This file is not an LMS cartridge…".
- [ ] Start the app with a cartridge as its only argument: it opens straight into step 1.
- [ ] `--convert <cartridge> <folder>` writes the site and a `convert-log.txt`, with no window.
