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

## 5. Publish with a token — **not yet done**

Make a fine-grained token at github.com/settings/personal-access-tokens with **Administration**,
**Contents** and **Pages** (read and write).

1. **GitHub token…** → paste → **Check and save**.
   - [ ] It reports your GitHub login.
2. Type a repository name that does not exist yet, press **Publish**.
   - [ ] The app creates the repository, pushes, and switches Pages on.
   - [ ] The address appears in the window and opens (after the first build finishes — a minute or two).
3. Publish again after a rebuild.
   - [ ] The second publish replaces the branch and the site updates.
4. Try it wrong on purpose:
   - [ ] A token with no Pages permission → the site still uploads, and the app says to switch
         Pages on by hand.
   - [ ] A token that has been revoked → "GitHub did not accept the token".
   - [ ] A repository owned by someone else → the app says it can only create repositories under
         your own account.

## 6. The window itself

- [ ] Resize down to 720×560: nothing is cut off, the page scrolls.
- [ ] Cancel a build of the big course halfway: the app comes back, no crash.
- [ ] Choose a file that is not a cartridge: "This file is not an LMS cartridge…".
- [ ] Start the app with a cartridge as its only argument: it opens straight into step 1.
- [ ] `--convert <cartridge> <folder>` writes the site and a `convert-log.txt`, with no window.
