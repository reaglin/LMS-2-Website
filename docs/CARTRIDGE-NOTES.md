# What is actually inside an LMS export

Notes taken while making real course exports work. Written down because every one of these cost
time to find, and because PreseMaker's copy of the cartridge reader may want the same fixes.

## Brightspace (D2L)

Observed in `D2LCCExport_511555_EGN3443_521F_FA26_202682420.imscc` — CC 1.3.0, 167 entries, 135 MB:
110 `webcontent`, 23 `imsdt_xmlv1p3` (discussions), 16 QTI assessments, 14 `assignment_xmlv1p0`.

- **The content folder is spelled with a Cyrillic "с" (U+0441), not a Latin "c".** `сontent/…`
  It is consistent between the manifest and the zip, so an exact match works — but never "correct"
  it, and never compare against the literal `content/`. It is also the surest way to recognise a
  D2L export.
- **A display name is appended to an entry after a semicolon**, and the two sides disagree about a
  slash: the manifest says
  `сontent/i…/236a696d….html;Overview.html`
  while the zip entry is
  `сontent/i…/236a696d….html;/Overview.html`
  (so the semicolon part is a folder). One href in that course, and it is the course overview.
  `CcPackage` tries the plain form, the `;/` form and the bare path.
  **PreseMaker's copy does not try the `;/` form and would miss this page.**
- **The student-facing text of an assignment is in `<instructor_text>`**, not `<text>`.
- **Quiz images are not exported.** A question's HTML references `untitled1/f1q6g1.jpg`; nothing of
  the sort is in the zip. The site says "[image not included in the export]" where the picture was,
  and the notes count them per quiz.
- **Links between course items are D2L quicklinks**
  (`…/d2l/common/dialogs/quickLink/quickLink.d2l?ou={orgUnitId}&type=content&rcode=…`) — dead to
  anyone outside the course. They are marked "(link inside the LMS)" on the page and counted.
- **Pages link the Brightspace stylesheet** (`templates.lcs.brightspace.com/…/styles.min.css`) and a
  site-absolute `/d2l/le/contentstyler/…` — both are dropped; the page keeps its inline styles.
- **The same quiz can hang off several org items** (a final exam listed under two modules). Both
  entries are published; they are separate pages with the same content.
- Item titles sometimes carry the file extension (`Overview.html`). The reader strips it.

## Canvas

Not yet checked against a real export. The reader handles what the format specifies:

- The course title and description live in `course_settings/course_settings.xml`, not in the LOM.
- `$IMS-CC-FILEBASE$/…` in page HTML means `web_resources/…`.
- Question types come from a `question_type` metadata field rather than `cc_profile`.

## Moodle and Blackboard

Recognised by `CcPackage.DetectProducer`, nothing else special done. Both write standard CC, so the
generic path should cover them; neither has been run against a real export (phase 6.5).

## QTI 1.2, as everyone writes it

- The correct answer is the `respcondition` whose `setvar` is greater than zero. In a D2L export the
  wrong answer's condition comes **first** and carries no `setvar` at all.
- `cc_weighting` is the points. `cc_profile` (`cc.multiple_choice.v0p1`, `cc.fib.v0p1`, …) is the
  most reliable type; Canvas's `question_type` is the fallback; structure is the last resort.
- A stem is `presentation/material/mattext` — but `response_label`s have `mattext` too, so the stem
  must exclude anything under a `response_*` element.
- `mattext texttype="text/html"` is entity-escaped HTML. `XElement.Value` un-escapes it, so the
  markup arrives intact and is published as it was written.
- Some exports repeat an `itemfeedback` ident (D2L emits `correct_fb` twice). Keep the first
  non-empty one.
- A question the export does not mark an answer for is published saying so. Guessing would be worse
  than useless on a page that claims to be the answer key.
