namespace Lms2Website.Core.Cartridge;

/// <summary>One &lt;resource&gt; of the manifest.</summary>
public sealed class CcResource
{
    public string Identifier { get; set; } = string.Empty;
    /// <summary>Raw type string — "webcontent", "imsqti_xmlv1p2/imscc_xmlv1p3/assessment", "imsdt_xmlv1p3"…</summary>
    public string Type { get; set; } = string.Empty;
    /// <summary>Primary file (the resource's own href, or the first &lt;file&gt;).</summary>
    public string Href { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public List<string> Files { get; } = new();
    public List<string> Dependencies { get; } = new();
}

/// <summary>One &lt;item&gt; of the organization tree: a folder (children, no ref) or a leaf (ref).</summary>
public sealed class CcOrgItem
{
    public string Identifier { get; set; } = string.Empty;
    public string? IdentifierRef { get; set; }
    public string Title { get; set; } = string.Empty;
    public List<CcOrgItem> Children { get; } = new();

    public bool IsLeaf => IdentifierRef != null;
}

/// <summary>The parsed imsmanifest.xml — organization tree plus resources.</summary>
public sealed class CcManifest
{
    public string Identifier { get; set; } = string.Empty;
    public string Schema { get; set; } = string.Empty;
    public string SchemaVersion { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string OrganizationIdentifier { get; set; } = string.Empty;
    /// <summary>Top-level items of the organization (the wrapping root item is unwrapped by the reader).</summary>
    public List<CcOrgItem> Items { get; } = new();
    public List<CcResource> Resources { get; } = new();
}
