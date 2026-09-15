# LMS 2 Website

Turn a course export out of your LMS into a website, and put it on GitHub Pages.

Export your course from Brightspace, Canvas, Moodle or Blackboard as a Common Cartridge
(`.imscc`), drop the file on this app, and press two buttons. You get a plain, fast website with
every page, file, quiz, assignment and discussion the export carried — and a public address for it.

```
1  Choose the LMS export        course.imscc
2  Build the website            Documents\LMS 2 Website\sites\my-course
3  Publish to GitHub Pages      https://you.github.io/my-course/
```

## What the website is

- One page per course item, in course order, with a section menu, breadcrumbs and previous/next.
- A search box that works with no server — even with the folder opened from a disk.
- Light and dark, and readable on a phone.
- Quizzes as read-only question pages with the answers the export records.
- No CDN, no fonts to fetch, no build step, no JavaScript framework. It is HTML you can read.

The app tells you what it could not carry: links that go back into the LMS, images the LMS left out
of the export, questions with no recorded answer.

## Publishing

With a GitHub personal access token the app creates the repository, pushes the site and switches
GitHub Pages on for you. Without one it does a plain `git push` to a repository you made yourself,
and tells you where to switch Pages on. The token is encrypted for your Windows account and never
travels with a course or a site.

## Building it

```powershell
dotnet build LMS2Website.sln
dotnet test LMS2Website.sln
dotnet run --project src/Lms2Website.App
```

There is a demo course in `samples/demo-course.imscc` to try it on.

WPF on .NET 10. See `docs/PLAN.md` for the design and `docs/DEVELOPMENT-PLAN.md` for what is built
and what is next.

---

© Ron Eaglin
