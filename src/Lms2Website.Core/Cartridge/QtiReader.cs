using System.Globalization;
using System.Xml.Linq;
using Lms2Website.Core.Model;
using Lms2Website.Core.Site;

namespace Lms2Website.Core.Cartridge;

/// <summary>
/// Reads a QTI 1.2 assessment or question bank (the Common Cartridge profile, plus the Canvas,
/// D2L and Moodle dialects seen in the wild) into a read-only <see cref="QuizContent"/>: the
/// stem, the choices, which choice is correct, and the feedback — all with the cartridge's own
/// HTML kept, because the website publishes it rather than re-typing it.
///
/// Question type: <c>cc_profile</c> → Canvas <c>question_type</c> → structure. Correct answers:
/// any <c>respcondition</c> whose <c>setvar</c> is greater than zero marks its <c>varequal</c>
/// values correct (a condition wrapped in <c>&lt;not&gt;</c> never does). A question whose
/// cartridge marks no answer is published with "the cartridge does not record the answer" rather
/// than a guess.
/// </summary>
public static class QtiReader
{
    public static QuizContent Read(string xml, string fallbackTitle)
    {
        var content = new QuizContent { Title = fallbackTitle };
        XDocument doc;
        try { doc = XDocument.Parse(xml, LoadOptions.None); }
        catch (System.Xml.XmlException ex)
        {
            content.Warnings.Add("The quiz XML could not be read: " + ex.Message);
            return content;
        }

        var root = doc.Root;
        if (root == null) return content;

        var assessment = root.DescendantsAndSelf().FirstOrDefault(e => e.Name.LocalName == "assessment");
        var bank       = root.DescendantsAndSelf().FirstOrDefault(e => e.Name.LocalName == "objectbank");
        var title = assessment?.Attribute("title")?.Value;
        content.Title = string.IsNullOrWhiteSpace(title) ? fallbackTitle : title.Trim();
        content.IsQuestionBank = assessment == null && bank != null;

        // Some exporters put the assessment instructions in a <rubric> or <presentation_material>.
        var intro = assessment?.Elements().FirstOrDefault(e =>
            e.Name.LocalName is "rubric" or "presentation_material");
        if (intro != null) content.DescriptionHtml = MaterialHtml(intro);

        int n = 0;
        foreach (var item in root.Descendants().Where(e => e.Name.LocalName == "item"))
        {
            n++;
            try
            {
                var q = ReadItem(item, n);
                if (q != null) content.Questions.Add(q);
            }
            catch (InvalidOperationException ex)
            {
                content.Warnings.Add($"Question {n} was left out — {ex.Message}");
            }
        }

        int unanswered = content.Questions.Count(q => q.HasNoMarkedAnswer);
        if (unanswered > 0)
            content.Warnings.Add($"{unanswered} of {content.Questions.Count} questions do not record a correct answer in the cartridge.");

        return content;
    }

    // ── one question ──────────────────────────────────────────────────────────

    private static QuizQuestion? ReadItem(XElement item, int n)
    {
        var meta = item.Descendants().Where(e => e.Name.LocalName == "qtimetadatafield")
                       .Select(f => (Key: Child(f, "fieldlabel")?.Value.Trim() ?? "",
                                     Val: Child(f, "fieldentry")?.Value.Trim() ?? ""))
                       .Where(kv => kv.Key.Length > 0)
                       .GroupBy(kv => kv.Key, StringComparer.OrdinalIgnoreCase)
                       .ToDictionary(g => g.Key, g => g.First().Val, StringComparer.OrdinalIgnoreCase);

        meta.TryGetValue("cc_profile", out var profile);
        meta.TryGetValue("question_type", out var canvasType);

        var presentation = Child(item, "presentation");
        if (presentation == null) return null;

        var lids = presentation.Descendants().Where(e => e.Name.LocalName == "response_lid").ToList();
        var strs = presentation.Descendants().Where(e => e.Name.LocalName == "response_str").ToList();

        var q = new QuizQuestion
        {
            Title      = (item.Attribute("title")?.Value ?? string.Empty).Trim(),
            PromptHtml = StemHtml(presentation),
            Points     = Points(meta)
        };
        if (q.Title.Length == 0) q.Title = $"Question {n}";

        var conditions = Child(item, "resprocessing")?.Elements()
                            .Where(e => e.Name.LocalName == "respcondition")
                            .Select(ReadCondition).ToList()
                         ?? new List<Condition>();

        var feedback = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var f in item.Elements().Where(e => e.Name.LocalName == "itemfeedback"))
        {
            var id = f.Attribute("ident")?.Value ?? string.Empty;
            var text = MaterialHtml(f);
            if (!feedback.TryGetValue(id, out var existing) || (string.IsNullOrWhiteSpace(existing) && text.Length > 0))
                feedback[id] = text;
        }

        q.TypeLabel = Classify(profile, canvasType, lids, strs);

        switch (q.TypeLabel)
        {
            case "Short answer":
            case "Numeric answer":
            {
                foreach (var c in conditions.Where(c => c.Score > 0 && !c.Negated))
                    foreach (var v in c.Values.Where(v => v.Length > 0))
                        if (!q.Answers.Contains(v, StringComparer.OrdinalIgnoreCase))
                            q.Answers.Add(v);
                break;
            }
            case "Essay":
                // Nothing is machine-scored; any feedback block carries the model answer.
                break;
            case "Matching":
            {
                foreach (var lid in lids)
                {
                    var left    = LidPromptHtml(lid);
                    var labels  = Labels(lid);
                    var correct = CorrectIdents(lid, conditions);
                    var right   = labels.Where(l => correct.Contains(l.Ident)).Select(l => l.Html).ToList();
                    if (left.Length == 0 && right.Count == 0) continue;
                    q.Answers.Add(right.Count > 0
                        ? $"{left} → {string.Join(", ", right)}"
                        : $"{left} → (no match recorded)");
                }
                break;
            }
            default:
            {
                var lid = lids.FirstOrDefault();
                if (lid == null) break;
                var correct = CorrectIdents(lid, conditions);
                foreach (var l in Labels(lid))
                {
                    q.Choices.Add(new QuizChoice
                    {
                        TextHtml  = l.Html,
                        IsCorrect = correct.Contains(l.Ident),
                        Feedback  = ChoiceFeedback(l.Ident, conditions, feedback)
                    });
                }
                break;
            }
        }

        q.Feedback = GeneralFeedback(feedback, conditions);
        return q;
    }

    private static string Classify(string? profile, string? canvasType, List<XElement> lids, List<XElement> strs)
    {
        string p = (profile ?? string.Empty).ToLowerInvariant();
        string c = (canvasType ?? string.Empty).ToLowerInvariant();

        if (p.Contains("true_false") || c.Contains("true_false")) return "True/False";
        if (p.Contains("multiple_response") || c.Contains("multiple_answers")) return "Multi-select";
        if (p.Contains("multiple_choice") || c.Contains("multiple_choice")) return "Multiple choice";
        if (p.Contains("essay") || c.Contains("essay")) return "Essay";
        if (p.Contains("pattern_match") || p.Contains("fib") || c.Contains("short_answer")) return "Short answer";
        if (c.Contains("numerical")) return "Numeric answer";
        if (p.Contains("matching") || c.Contains("matching")) return "Matching";

        if (lids.Count > 1) return "Matching";
        if (lids.Count == 1)
        {
            var card = lids[0].Attribute("rcardinality")?.Value ?? "Single";
            if (card.Equals("Multiple", StringComparison.OrdinalIgnoreCase)) return "Multi-select";
            var labels = Labels(lids[0]);
            if (labels.Count == 2 && labels.All(l => IsTrueFalseText(l.Html))) return "True/False";
            return "Multiple choice";
        }
        if (strs.Count > 0) return "Short answer";
        return "Question";
    }

    private static bool IsTrueFalseText(string html)
    {
        var t = Html.ToPlainText(html).Trim().ToLowerInvariant();
        return t is "true" or "false" or "t" or "f" or "yes" or "no";
    }

    private static decimal? Points(Dictionary<string, string> meta)
    {
        foreach (var key in new[] { "cc_weighting", "points_possible", "qmd_weighting" })
            if (meta.TryGetValue(key, out var v) &&
                decimal.TryParse(v, NumberStyles.Any, CultureInfo.InvariantCulture, out var d))
                return d;
        return null;
    }

    // ── response processing ───────────────────────────────────────────────────

    private sealed class Condition
    {
        public decimal Score { get; init; }
        public bool Negated { get; init; }
        /// <summary>varequal values, by respident ("" when the element carries none).</summary>
        public List<(string RespIdent, string Value)> Matches { get; } = new();
        public IEnumerable<string> Values => Matches.Select(e => e.Value);
        public List<string> FeedbackIds { get; } = new();
    }

    private static Condition ReadCondition(XElement rc)
    {
        decimal score = 0;
        foreach (var sv in rc.Elements().Where(e => e.Name.LocalName == "setvar"))
            if (decimal.TryParse(sv.Value.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) && v > score)
                score = v;

        var conditionVar = Child(rc, "conditionvar");
        bool negated = conditionVar?.Descendants().Any(e => e.Name.LocalName == "not") ?? false;

        var c = new Condition { Score = score, Negated = negated };
        if (conditionVar != null)
        {
            foreach (var ve in conditionVar.Descendants().Where(e => e.Name.LocalName == "varequal"))
                c.Matches.Add((ve.Attribute("respident")?.Value ?? string.Empty, ve.Value.Trim()));
        }
        foreach (var df in rc.Elements().Where(e => e.Name.LocalName == "displayfeedback"))
        {
            var id = df.Attribute("linkrefid")?.Value;
            if (!string.IsNullOrEmpty(id)) c.FeedbackIds.Add(id);
        }
        return c;
    }

    private static HashSet<string> CorrectIdents(XElement lid, List<Condition> conditions)
    {
        var ident = lid.Attribute("ident")?.Value ?? string.Empty;
        var set = new HashSet<string>(StringComparer.Ordinal);
        foreach (var c in conditions.Where(c => c.Score > 0 && !c.Negated))
            foreach (var (respIdent, value) in c.Matches)
                if (respIdent.Length == 0 || respIdent == ident)
                    set.Add(value);
        return set;
    }

    private static string ChoiceFeedback(string ident, List<Condition> conditions, Dictionary<string, string> feedback)
    {
        foreach (var c in conditions.Where(c => !c.Negated && c.FeedbackIds.Count > 0))
            if (c.Matches.Count == 1 && c.Matches[0].Value == ident)
                foreach (var id in c.FeedbackIds)
                    if (feedback.TryGetValue(id, out var text) && text.Length > 0)
                        return text;
        return string.Empty;
    }

    /// <summary>Feedback shown whatever the answer: an ident that says so, else a block no condition points at.</summary>
    private static string GeneralFeedback(Dictionary<string, string> feedback, List<Condition> conditions)
    {
        foreach (var kv in feedback)
            if (kv.Key.Contains("general", StringComparison.OrdinalIgnoreCase) && kv.Value.Length > 0)
                return kv.Value;

        var referenced = conditions.SelectMany(c => c.FeedbackIds).ToHashSet(StringComparer.Ordinal);
        var loose = feedback.Where(kv => !referenced.Contains(kv.Key) && kv.Value.Length > 0).ToList();
        return loose.Count == 1 ? loose[0].Value : string.Empty;
    }

    // ── material ──────────────────────────────────────────────────────────────

    private sealed record Label(string Ident, string Html);

    private static List<Label> Labels(XElement lid) =>
        lid.Descendants().Where(e => e.Name.LocalName == "response_label")
           .Select(l => new Label(l.Attribute("ident")?.Value ?? string.Empty, MaterialHtml(l)))
           .ToList();

    /// <summary>The stem: material that is not inside a response element.</summary>
    private static string StemHtml(XElement presentation)
    {
        var parts = presentation.Descendants().Where(e => e.Name.LocalName == "mattext")
            .Where(t => !t.Ancestors().Any(a => a.Name.LocalName is "response_lid" or "response_str" or "response_num"))
            .Select(MatTextHtml)
            .Where(s => s.Length > 0);
        return string.Join("\n", parts);
    }

    /// <summary>For matching: the row's own prompt (material directly under the response_lid).</summary>
    private static string LidPromptHtml(XElement lid)
    {
        var parts = lid.Elements().Where(e => e.Name.LocalName == "material")
                       .Select(MaterialHtml).Where(s => s.Length > 0);
        return string.Join(" ", parts);
    }

    /// <summary>All mattext under an element (a response_label, an itemfeedback, a rubric).</summary>
    private static string MaterialHtml(XElement e) =>
        string.Join("\n", e.Descendants().Where(x => x.Name.LocalName == "mattext")
                           .Select(MatTextHtml).Where(s => s.Length > 0));

    private static string MatTextHtml(XElement mattext)
    {
        var type = mattext.Attribute("texttype")?.Value ?? "text/html";
        var v = mattext.Value.Trim();
        if (v.Length == 0) return string.Empty;
        return type.Equals("text/plain", StringComparison.OrdinalIgnoreCase)
            ? System.Net.WebUtility.HtmlEncode(v)
            : v;
    }

    private static XElement? Child(XElement parent, string localName) =>
        parent.Elements().FirstOrDefault(e => e.Name.LocalName == localName);
}
