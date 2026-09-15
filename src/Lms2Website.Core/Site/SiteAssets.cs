namespace Lms2Website.Core.Site;

/// <summary>
/// The stylesheet and script the generated site carries. Everything is written into the site
/// itself — no CDN, no web fonts, no build step — so the folder works opened from disk, on
/// GitHub Pages under a project path, and behind a campus proxy.
/// </summary>
public static class SiteAssets
{
    public const string StylesheetFileName = "assets/site.css";
    public const string ScriptFileName     = "assets/site.js";
    public const string SearchIndexFileName = "assets/search-index.js";

    public const string Stylesheet = """
/* LMS 2 Website — generated site styles. Edit freely; a re-publish overwrites this file. */

:root {
  color-scheme: light dark;
  --bg:        #ffffff;
  --bg-soft:   #f5f6f8;
  --bg-sunken: #eceef2;
  --text:      #1b1d21;
  --text-soft: #5b616b;
  --line:      #dcdfe5;
  --accent:    #1f5fa9;
  --accent-soft: #e8f0fa;
  --correct:   #1c7c45;
  --correct-soft: #e6f4ec;
  --warn:      #8a5a00;
  --warn-soft: #fbf0d9;
  --radius: 10px;
  --sidebar: 17rem;
  --measure: 46rem;
}

@media (prefers-color-scheme: dark) {
  :root {
    --bg:        #14161a;
    --bg-soft:   #1b1e24;
    --bg-sunken: #23272e;
    --text:      #e7e9ed;
    --text-soft: #a3a9b4;
    --line:      #2f343c;
    --accent:    #7db3f0;
    --accent-soft: #1d2a3a;
    --correct:   #6fcf97;
    --correct-soft: #16281f;
    --warn:      #e2b463;
    --warn-soft: #2c2415;
  }
}

* { box-sizing: border-box; }

body {
  margin: 0;
  background: var(--bg);
  color: var(--text);
  font: 16px/1.65 -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, "Helvetica Neue", Arial, sans-serif;
  -webkit-text-size-adjust: 100%;
}

a { color: var(--accent); text-decoration-thickness: 1px; text-underline-offset: 2px; }
a:hover { text-decoration-thickness: 2px; }

.skip {
  position: absolute; left: -9999px; top: 0; z-index: 10;
  background: var(--bg); padding: .6rem 1rem; border: 2px solid var(--accent);
}
.skip:focus { left: .5rem; top: .5rem; }

/* ── top bar ─────────────────────────────────────────────────────────── */

.topbar {
  position: sticky; top: 0; z-index: 5;
  display: flex; align-items: center; gap: .75rem;
  padding: .6rem 1rem;
  background: var(--bg-soft);
  border-bottom: 1px solid var(--line);
}
.brand {
  font-weight: 650; font-size: 1.02rem; color: inherit; text-decoration: none;
  overflow: hidden; text-overflow: ellipsis; white-space: nowrap;
}
.brand:hover { color: var(--accent); }
.nav-toggle {
  display: none;
  background: transparent; border: 1px solid var(--line); border-radius: 8px;
  color: inherit; font: inherit; padding: .3rem .6rem; cursor: pointer;
}
.search { margin-left: auto; position: relative; }
.search input {
  width: 15rem; max-width: 40vw;
  padding: .4rem .7rem;
  border: 1px solid var(--line); border-radius: 999px;
  background: var(--bg); color: inherit; font: inherit;
}
.search input:focus-visible { outline: 2px solid var(--accent); outline-offset: 1px; }
.results {
  position: absolute; right: 0; top: calc(100% + .4rem); width: min(28rem, 85vw);
  max-height: 60vh; overflow-y: auto;
  background: var(--bg); border: 1px solid var(--line); border-radius: var(--radius);
  box-shadow: 0 10px 30px rgb(0 0 0 / .18);
  padding: .35rem;
}
.results a { display: block; padding: .45rem .6rem; border-radius: 7px; text-decoration: none; color: inherit; }
.results a:hover, .results a:focus { background: var(--accent-soft); }
.results .r-title { font-weight: 600; }
.results .r-where { font-size: .82rem; color: var(--text-soft); }
.results .r-none { padding: .5rem .6rem; color: var(--text-soft); }

/* ── layout ──────────────────────────────────────────────────────────── */

.layout { display: flex; align-items: flex-start; gap: 2rem; max-width: 76rem; margin: 0 auto; padding: 0 1rem; }

.sidebar {
  position: sticky; top: 3.4rem;
  flex: 0 0 var(--sidebar); width: var(--sidebar);
  max-height: calc(100vh - 4rem); overflow-y: auto;
  padding: 1.25rem .25rem 2rem 0;
  font-size: .94rem;
}
.sidebar ol { list-style: none; margin: 0; padding: 0; }
.sidebar > ol > li { margin-bottom: .15rem; }
.sidebar a { display: block; padding: .3rem .55rem; border-radius: 7px; color: inherit; text-decoration: none; }
.sidebar a:hover { background: var(--bg-soft); }
.sidebar a[aria-current] { background: var(--accent-soft); color: var(--accent); font-weight: 600; }
.sidebar .section-title { font-weight: 600; }
.sidebar .items { margin: .1rem 0 .5rem .55rem; padding-left: .55rem; border-left: 1px solid var(--line); }
.sidebar .items a { font-size: .9rem; color: var(--text-soft); }
.sidebar .items a:hover { color: var(--text); }

main { flex: 1 1 auto; min-width: 0; padding: 1.5rem 0 4rem; max-width: var(--measure); }

@media (max-width: 800px) {
  .layout { display: block; padding: 0 .9rem; }
  .nav-toggle { display: inline-block; }
  .sidebar {
    position: static; width: auto; max-height: none; padding: .75rem 0;
    border-bottom: 1px solid var(--line);
  }
  .sidebar[hidden] { display: none; }
  main { padding-top: 1.1rem; max-width: none; }
  .search input { width: 9rem; }
}

/* ── content ─────────────────────────────────────────────────────────── */

.crumbs { font-size: .88rem; color: var(--text-soft); margin-bottom: .4rem; }
.crumbs a { color: inherit; }

h1 { font-size: 1.85rem; line-height: 1.25; margin: .2rem 0 .6rem; }
h2 { font-size: 1.3rem; margin-top: 2rem; }
h3 { font-size: 1.08rem; margin-top: 1.5rem; }

.lede { color: var(--text-soft); margin-top: 0; }

.content :is(img, video, iframe) { max-width: 100%; height: auto; }
.content table { border-collapse: collapse; width: 100%; display: block; overflow-x: auto; }
.content :is(td, th) { border: 1px solid var(--line); padding: .4rem .6rem; text-align: left; }
.content pre { background: var(--bg-sunken); padding: .8rem; border-radius: var(--radius); overflow-x: auto; }
.content blockquote { margin-inline: 0; padding-left: 1rem; border-left: 3px solid var(--line); color: var(--text-soft); }
/* LMS pages arrive with hard-coded colours that can vanish on a dark background. */
@media (prefers-color-scheme: dark) {
  .content [style*="color"] { color: var(--text) !important; }
  .content [style*="background"] { background: transparent !important; }
}

.badge {
  display: inline-block; font-size: .75rem; font-weight: 600; letter-spacing: .02em;
  text-transform: uppercase; padding: .12rem .45rem; border-radius: 5px;
  background: var(--bg-sunken); color: var(--text-soft);
}
.badge.quiz { background: var(--accent-soft); color: var(--accent); }
.badge.assignment, .badge.discussion { background: var(--warn-soft); color: var(--warn); }

.note {
  background: var(--bg-soft); border: 1px solid var(--line); border-left: 3px solid var(--accent);
  border-radius: var(--radius); padding: .7rem .9rem; margin: 1.2rem 0; font-size: .94rem;
}
.note.warn { border-left-color: var(--warn); }

.missing-image {
  display: inline-block; padding: .2rem .5rem; border: 1px dashed var(--line); border-radius: 6px;
  color: var(--text-soft); font-size: .85rem; font-style: italic;
}

a.lms-link::after {
  content: " (link inside the LMS)";
  font-size: .8rem; color: var(--text-soft);
}

/* ── item lists ──────────────────────────────────────────────────────── */

.items-list { list-style: none; margin: 1rem 0 0; padding: 0; }
.items-list li { border-bottom: 1px solid var(--line); }
.items-list li:first-child { border-top: 1px solid var(--line); }
.items-list a { display: flex; gap: .75rem; align-items: baseline; padding: .7rem .4rem; text-decoration: none; color: inherit; }
.items-list a:hover { background: var(--bg-soft); }
.items-list .t { font-weight: 550; }
.items-list .m { margin-left: auto; font-size: .82rem; color: var(--text-soft); white-space: nowrap; }
.section-head { margin-top: 2rem; font-size: 1rem; color: var(--text-soft); text-transform: uppercase; letter-spacing: .04em; }

.cards { display: grid; gap: .9rem; grid-template-columns: repeat(auto-fill, minmax(15rem, 1fr)); margin-top: 1.4rem; padding: 0; list-style: none; }
.card {
  border: 1px solid var(--line); border-radius: var(--radius); padding: .9rem 1rem;
  background: var(--bg-soft);
}
.card a { text-decoration: none; color: inherit; font-weight: 600; }
.card a:hover { color: var(--accent); }
.card p { margin: .35rem 0 0; font-size: .88rem; color: var(--text-soft); }

/* ── attachments ─────────────────────────────────────────────────────── */

.files { list-style: none; padding: 0; margin: 1rem 0; }
.files li { margin: .25rem 0; }
.files a { font-weight: 550; }
.files .size { color: var(--text-soft); font-size: .85rem; }
/* A file that was built but kept out of the publish: named, never linked. */
.files .unpublished { color: var(--warn); font-size: .85rem; }

.embed { width: 100%; height: 42rem; max-height: 80vh; border: 1px solid var(--line); border-radius: var(--radius); }

/* ── quiz ────────────────────────────────────────────────────────────── */

.question { border: 1px solid var(--line); border-radius: var(--radius); padding: 1rem 1.1rem; margin: 1.1rem 0; }
.question .q-head { display: flex; gap: .6rem; align-items: baseline; font-size: .85rem; color: var(--text-soft); margin-bottom: .5rem; }
.question ol.choices { list-style: upper-alpha; margin: .6rem 0 0; padding-left: 1.6rem; }
.question ol.choices li { margin: .25rem 0; padding: .1rem .3rem; border-radius: 6px; }
.question ol.choices li.correct { background: var(--correct-soft); }
.question .mark { color: var(--correct); font-weight: 700; margin-left: .35rem; }
.question .answer { margin-top: .7rem; font-size: .95rem; }
.question .answer strong { color: var(--correct); }
.question .feedback { margin-top: .5rem; font-size: .92rem; color: var(--text-soft); }
.question p:first-child { margin-top: 0; }

/* ── footers ─────────────────────────────────────────────────────────── */

.pager { display: flex; justify-content: space-between; gap: 1rem; margin-top: 3rem; padding-top: 1rem; border-top: 1px solid var(--line); font-size: .93rem; }
.pager span { color: var(--text-soft); }

.site-foot {
  max-width: 76rem; margin: 0 auto; padding: 1.2rem 1rem 2.5rem;
  border-top: 1px solid var(--line); color: var(--text-soft); font-size: .85rem;
}
""";

    public const string Script = """
// LMS 2 Website — generated site script: the section menu on small screens and the search box.
(function () {
  "use strict";

  var toggle = document.querySelector(".nav-toggle");
  var sidebar = document.getElementById("sidebar");
  if (toggle && sidebar) {
    var narrow = window.matchMedia("(max-width: 800px)");
    var apply = function () { sidebar.hidden = narrow.matches; toggle.setAttribute("aria-expanded", String(!narrow.matches)); };
    apply();
    narrow.addEventListener("change", apply);
    toggle.addEventListener("click", function () {
      var open = sidebar.hidden;
      sidebar.hidden = !open;
      toggle.setAttribute("aria-expanded", String(open));
    });
  }

  var input = document.getElementById("q");
  var panel = document.getElementById("results");
  var pages = window.SITE_SEARCH || [];
  var root = document.documentElement.getAttribute("data-root") || "";
  if (!input || !panel) return;

  function score(page, terms) {
    var title = page.t.toLowerCase(), body = (page.b || "").toLowerCase(), where = (page.s || "").toLowerCase();
    var total = 0;
    for (var i = 0; i < terms.length; i++) {
      var term = terms[i];
      if (!term) continue;
      var hit = 0;
      if (title.indexOf(term) >= 0) hit += title.indexOf(term) === 0 ? 12 : 8;
      if (where.indexOf(term) >= 0) hit += 3;
      if (body.indexOf(term) >= 0) hit += 2;
      if (!hit) return 0;          // every term must appear somewhere
      total += hit;
    }
    return total;
  }

  function render(matches, query) {
    panel.innerHTML = "";
    if (!query) { panel.hidden = true; return; }
    if (!matches.length) {
      var none = document.createElement("div");
      none.className = "r-none";
      none.textContent = 'Nothing matches "' + query + '".';
      panel.appendChild(none);
      panel.hidden = false;
      return;
    }
    matches.slice(0, 12).forEach(function (page) {
      var a = document.createElement("a");
      a.href = root + page.u;
      var t = document.createElement("div"); t.className = "r-title"; t.textContent = page.t;
      var w = document.createElement("div"); w.className = "r-where"; w.textContent = page.s ? page.s + " · " + page.k : page.k;
      a.appendChild(t); a.appendChild(w);
      panel.appendChild(a);
    });
    panel.hidden = false;
  }

  function search() {
    var query = input.value.trim();
    var terms = query.toLowerCase().split(/\s+/).filter(Boolean);
    if (!terms.length) { render([], ""); return; }
    var matches = [];
    for (var i = 0; i < pages.length; i++) {
      var s = score(pages[i], terms);
      if (s > 0) matches.push({ page: pages[i], s: s });
    }
    matches.sort(function (a, b) { return b.s - a.s; });
    render(matches.map(function (m) { return m.page; }), query);
  }

  input.addEventListener("input", search);
  input.addEventListener("focus", search);
  input.form.addEventListener("submit", function (e) {
    e.preventDefault();
    var first = panel.querySelector("a");
    if (first) window.location.href = first.href;
  });
  document.addEventListener("click", function (e) {
    if (!panel.contains(e.target) && e.target !== input) panel.hidden = true;
  });
  document.addEventListener("keydown", function (e) {
    if (e.key === "Escape") { panel.hidden = true; input.blur(); }
    if (e.key === "/" && document.activeElement !== input) { e.preventDefault(); input.focus(); }
  });
})();
""";
}
