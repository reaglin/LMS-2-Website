using System.Xml.Linq;

namespace Lms2Website.Core.Cartridge;

/// <summary>
/// Parses <c>imsmanifest.xml</c> from any Common Cartridge (1.0–1.3) or plain IMS Content
/// Packaging 1.1 package. Namespace-agnostic (matches on local names), tolerant of missing
/// metadata, multiple organizations (takes the one with the most items) and unknown elements.
///
/// Ported from PreseMaker's <c>CcManifestReader</c>.
/// </summary>
public static class CcManifestReader
{
    public static CcManifest Read(string xml, List<string>? warnings = null)
    {
        XDocument doc;
        try { doc = XDocument.Parse(xml, LoadOptions.None); }
        catch (System.Xml.XmlException ex)
        {
            throw new InvalidDataException("imsmanifest.xml is not well-formed XML: " + ex.Message, ex);
        }
        return Read(doc, warnings);
    }

    public static CcManifest Read(XDocument doc, List<string>? warnings = null)
    {
        warnings ??= new List<string>();
        var root = doc.Root ?? throw new InvalidDataException("imsmanifest.xml is empty.");
        if (root.Name.LocalName != "manifest")
            throw new InvalidDataException($"imsmanifest.xml starts with <{root.Name.LocalName}>, expected <manifest>.");

        var m = new CcManifest { Identifier = Attr(root, "identifier") ?? Guid.NewGuid().ToString("N") };

        // ── metadata ──
        var metadata = Child(root, "metadata");
        if (metadata != null)
        {
            m.SchemaVersion = Child(metadata, "schemaversion")?.Value.Trim() ?? string.Empty;
            m.Schema        = Child(metadata, "schema")?.Value.Trim() ?? string.Empty;
            var general = metadata.Descendants().FirstOrDefault(e => e.Name.LocalName == "general");
            if (general != null)
            {
                m.Title       = LangString(Child(general, "title"));
                m.Description = LangString(Child(general, "description"));
            }
        }

        // ── organizations ──
        var orgs = Child(root, "organizations");
        var org  = orgs?.Elements().Where(e => e.Name.LocalName == "organization")
                        .OrderByDescending(o => o.Descendants().Count(e => e.Name.LocalName == "item"))
                        .FirstOrDefault();
        if (org != null)
        {
            m.OrganizationIdentifier = Attr(org, "identifier") ?? m.OrganizationIdentifier;
            var topItems = org.Elements().Where(e => e.Name.LocalName == "item").ToList();
            // CC: one untitled root item wraps the modules. IMS CP and some exports: modules directly.
            if (topItems.Count == 1 && string.IsNullOrWhiteSpace(TitleOf(topItems[0])) && Attr(topItems[0], "identifierref") == null)
                foreach (var i in topItems[0].Elements().Where(e => e.Name.LocalName == "item"))
                    m.Items.Add(ReadItem(i));
            else
                foreach (var i in topItems) m.Items.Add(ReadItem(i));
        }
        else
        {
            warnings.Add("The cartridge has no organization tree — every resource is listed in one section.");
        }

        // ── resources ──
        var resources = Child(root, "resources");
        if (resources != null)
        {
            foreach (var r in resources.Elements().Where(e => e.Name.LocalName == "resource"))
            {
                var res = new CcResource
                {
                    Identifier = Attr(r, "identifier") ?? string.Empty,
                    Type       = Attr(r, "type") ?? string.Empty,
                    Href       = Attr(r, "href") ?? string.Empty
                };
                foreach (var f in r.Elements().Where(e => e.Name.LocalName == "file"))
                {
                    var href = Attr(f, "href");
                    if (!string.IsNullOrWhiteSpace(href)) res.Files.Add(href);
                }
                foreach (var d in r.Elements().Where(e => e.Name.LocalName == "dependency"))
                {
                    var id = Attr(d, "identifierref");
                    if (!string.IsNullOrWhiteSpace(id)) res.Dependencies.Add(id);
                }
                if (string.IsNullOrEmpty(res.Href) && res.Files.Count > 0) res.Href = res.Files[0];
                if (res.Files.Count == 0 && !string.IsNullOrEmpty(res.Href)) res.Files.Add(res.Href);

                var rmeta = Child(r, "metadata");
                var rgeneral = rmeta?.Descendants().FirstOrDefault(e => e.Name.LocalName == "general");
                if (rgeneral != null) res.Title = LangString(Child(rgeneral, "title"));

                if (string.IsNullOrEmpty(res.Identifier))
                    warnings.Add($"A resource (type {res.Type}, file {res.Href}) has no identifier and was skipped.");
                else
                    m.Resources.Add(res);
            }
        }

        var resIds = new HashSet<string>(m.Resources.Select(r => r.Identifier), StringComparer.Ordinal);
        void Check(CcOrgItem item)
        {
            if (item.IdentifierRef != null && !resIds.Contains(item.IdentifierRef))
                warnings.Add($"\"{item.Title}\" points at a resource that is not in the cartridge — left out.");
            foreach (var c in item.Children) Check(c);
        }
        foreach (var i in m.Items) Check(i);

        return m;
    }

    private static CcOrgItem ReadItem(XElement e)
    {
        var item = new CcOrgItem
        {
            Identifier    = Attr(e, "identifier") ?? Guid.NewGuid().ToString("N"),
            IdentifierRef = Attr(e, "identifierref"),
            Title         = TitleOf(e)
        };
        if (string.IsNullOrWhiteSpace(item.IdentifierRef)) item.IdentifierRef = null;
        foreach (var c in e.Elements().Where(x => x.Name.LocalName == "item"))
            item.Children.Add(ReadItem(c));
        return item;
    }

    private static string TitleOf(XElement item) => (Child(item, "title")?.Value ?? string.Empty).Trim();

    private static XElement? Child(XElement parent, string localName) =>
        parent.Elements().FirstOrDefault(e => e.Name.LocalName == localName);

    private static string? Attr(XElement e, string name) => e.Attribute(name)?.Value;

    /// <summary>LOM title/description: &lt;string language=…&gt; or &lt;langstring&gt; — first non-empty.</summary>
    private static string LangString(XElement? e)
    {
        if (e == null) return string.Empty;
        var s = e.Descendants().FirstOrDefault(x =>
            (x.Name.LocalName == "string" || x.Name.LocalName == "langstring") && !string.IsNullOrWhiteSpace(x.Value));
        return (s?.Value ?? e.Value).Trim();
    }
}
