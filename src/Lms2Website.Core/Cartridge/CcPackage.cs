using System.IO.Compression;
using System.Text;
using Lms2Website.Core.Model;

namespace Lms2Website.Core.Cartridge;

/// <summary>
/// Read-only view of a Common Cartridge zip: indexes the entries so manifest hrefs resolve even
/// when an LMS bends the rules, and detects which LMS produced the package.
///
/// Resolution order for an href: exact · leading "./" or "/" stripped · case-insensitive ·
/// URL-decoded · without a D2L ";Display Name.html" suffix. D2L quirks (an entry name that
/// literally contains ";DisplayName.html"; a content folder spelled with a Cyrillic "с") resolve
/// on the exact match because the manifest uses the same bytes — do not "fix" them.
///
/// Ported from PreseMaker's <c>Services/CommonCartridge/CcPackage.cs</c>; the two copies are not
/// linked, so a fix here is worth considering there (and the other way round).
/// </summary>
public sealed class CcPackage : IDisposable
{
    private readonly ZipArchive _zip;
    private readonly Dictionary<string, ZipArchiveEntry> _exact = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ZipArchiveEntry> _ci    = new(StringComparer.OrdinalIgnoreCase);

    public string Path { get; }
    public long PackageBytes { get; }
    public IReadOnlyCollection<string> EntryNames => _exact.Keys;
    public int EntryCount => _exact.Count;

    /// <summary>Zip-relative path of the manifest (normally "imsmanifest.xml"; some exports nest it).</summary>
    public string ManifestHref { get; }
    /// <summary>Folder prefix every href is relative to ("" normally; "course/" when the manifest is nested).</summary>
    public string RootPrefix { get; }

    public CcPackage(string path)
    {
        Path = path;
        PackageBytes = new FileInfo(path).Length;
        _zip = ZipFile.OpenRead(path);
        foreach (var e in _zip.Entries)
        {
            if (string.IsNullOrEmpty(e.Name) && e.FullName.EndsWith('/')) continue;   // directory entry
            var name = Normalize(e.FullName);
            _exact[name] = e;
            _ci.TryAdd(name, e);
        }

        var manifest = _exact.Keys.FirstOrDefault(k => k == "imsmanifest.xml")
                    ?? _ci.Keys.FirstOrDefault(k => k.Equals("imsmanifest.xml", StringComparison.OrdinalIgnoreCase))
                    ?? _ci.Keys.Where(k => k.EndsWith("/imsmanifest.xml", StringComparison.OrdinalIgnoreCase))
                               .OrderBy(k => k.Count(c => c == '/')).FirstOrDefault();
        if (manifest == null)
        {
            _zip.Dispose();
            throw new InvalidDataException(
                "This file is not an LMS cartridge: it has no imsmanifest.xml inside it.");
        }
        ManifestHref = manifest;
        RootPrefix   = manifest.Contains('/') ? manifest[..(manifest.LastIndexOf('/') + 1)] : string.Empty;
    }

    public static string Normalize(string href)
    {
        var h = (href ?? string.Empty).Replace('\\', '/');
        while (h.StartsWith("./", StringComparison.Ordinal)) h = h[2..];
        return h.TrimStart('/');
    }

    /// <summary>Resolves a manifest href (relative to the manifest's folder) to an entry.</summary>
    public bool TryResolve(string? href, out ZipArchiveEntry entry)
    {
        entry = null!;
        if (string.IsNullOrWhiteSpace(href)) return false;
        foreach (var candidate in Candidates(href))
        {
            if (_exact.TryGetValue(candidate, out entry!)) return true;
            if (_ci.TryGetValue(candidate, out entry!)) return true;
        }
        return false;
    }

    public bool Exists(string? href) => TryResolve(href, out _);

    /// <summary>Size in bytes of the entry an href resolves to, or 0.</summary>
    public long SizeOf(string? href) => TryResolve(href, out var e) ? e.Length : 0;

    /// <summary>The normalized entry name an href resolves to (the canonical key for that file).</summary>
    public string? Canonical(string? href) => TryResolve(href, out var e) ? Normalize(e.FullName) : null;

    private IEnumerable<string> Candidates(string href)
    {
        var n = Normalize(href);
        // Strip a query / fragment (hrefs inside HTML) but NOT a ";name" suffix — D2L entry names contain it.
        int q = n.IndexOfAny(['?', '#']);
        var noSuffix = q >= 0 ? n[..q] : n;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var baseName in new[] { n, noSuffix })
        {
            foreach (var v in new[] { RootPrefix + baseName, baseName })
            {
                if (seen.Add(v)) yield return v;
                string decoded;
                try { decoded = Uri.UnescapeDataString(v); } catch (UriFormatException) { decoded = v; }
                if (decoded != v && seen.Add(decoded)) yield return decoded;
                // D2L: the manifest says "path/file.html;Display Name.html" while the zip entry is
                // "path/file.html;/Display Name.html" (the semicolon part is a folder). Try both,
                // and the bare path without the suffix.
                int semi = v.LastIndexOf(';');
                if (semi > 0)
                {
                    var slashed = v[..(semi + 1)] + "/" + v[(semi + 1)..];
                    if (seen.Add(slashed)) yield return slashed;
                    var bare = v[..semi];
                    if (seen.Add(bare)) yield return bare;
                }
            }
        }
    }

    /// <summary>Resolves a page-relative reference (e.g. "images/x.png" inside "content/i1/page.html").</summary>
    public bool TryResolveRelative(string pageHref, string reference, out string resolvedHref)
    {
        resolvedHref = string.Empty;
        if (string.IsNullOrWhiteSpace(reference)) return false;
        var dir = pageHref.Contains('/') ? pageHref[..(pageHref.LastIndexOf('/') + 1)] : string.Empty;
        if (TryResolve(Combine(dir, reference), out var entry))
        {
            resolvedHref = Normalize(entry.FullName);
            return true;
        }
        return false;
    }

    /// <summary>Normalized entry names in the same zip folder as the given href's entry.</summary>
    public IEnumerable<string> SiblingEntries(string href)
    {
        if (!TryResolve(href, out var e)) yield break;
        var full = Normalize(e.FullName);
        var dir  = full.Contains('/') ? full[..(full.LastIndexOf('/') + 1)] : string.Empty;
        foreach (var k in _exact.Keys)
        {
            if (dir.Length == 0 ? !k.Contains('/')
                                : k.StartsWith(dir, StringComparison.OrdinalIgnoreCase))
                yield return k;
        }
    }

    /// <summary>Pure path combine with ".." handling, forward slashes.</summary>
    public static string Combine(string dir, string rel)
    {
        var stack = new List<string>();
        foreach (var p in (dir + rel).Replace('\\', '/').Split('/'))
        {
            if (p.Length == 0 || p == ".") continue;
            if (p == "..") { if (stack.Count > 0) stack.RemoveAt(stack.Count - 1); continue; }
            stack.Add(p);
        }
        return string.Join("/", stack);
    }

    public string ReadText(string href)
    {
        if (!TryResolve(href, out var e)) throw new FileNotFoundException("Entry not in package", href);
        return ReadText(e);
    }

    public static string ReadText(ZipArchiveEntry e)
    {
        using var s = e.Open();
        using var r = new StreamReader(s, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        return r.ReadToEnd();
    }

    public void ExtractTo(string href, string destinationPath)
    {
        if (!TryResolve(href, out var e)) throw new FileNotFoundException("Entry not in package", href);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destinationPath)!);
        using var src = e.Open();
        using var dst = File.Create(destinationPath);
        src.CopyTo(dst);
    }

    /// <summary>
    /// Which LMS produced the package — from tell-tale files and folder conventions.
    /// Affects notes and quirk handling only, never whether a package converts.
    /// </summary>
    public CartridgeProducer DetectProducer()
    {
        bool Any(Func<string, bool> pred) => _exact.Keys.Any(pred);

        if (Any(k => k.StartsWith(RootPrefix + "course_settings/", StringComparison.OrdinalIgnoreCase)) ||
            Any(k => k.EndsWith("canvas_export.txt", StringComparison.OrdinalIgnoreCase)))
            return CartridgeProducer.Canvas;

        // D2L: a "сontent/" folder spelled with a Cyrillic "с", or D2LCCExport in the file name.
        if (Any(k => k.StartsWith(RootPrefix + "сontent/", StringComparison.Ordinal)) ||
            System.IO.Path.GetFileName(Path).StartsWith("D2LCCExport", StringComparison.OrdinalIgnoreCase))
            return CartridgeProducer.Brightspace;

        if (Any(k => k.Contains("res0", StringComparison.Ordinal)) &&
            Any(k => k.EndsWith(".dat", StringComparison.OrdinalIgnoreCase)))
            return CartridgeProducer.Blackboard;

        if (Any(k => k.Contains("moodle", StringComparison.OrdinalIgnoreCase)) ||
            Any(k => k.StartsWith(RootPrefix + "course_files/", StringComparison.OrdinalIgnoreCase)))
            return CartridgeProducer.Moodle;

        if (Any(k => k.StartsWith(RootPrefix + "content/i0", StringComparison.Ordinal)) ||
            Any(k => k.StartsWith(RootPrefix + "weblinks/i0", StringComparison.Ordinal)))
            return CartridgeProducer.PreseMaker;

        return CartridgeProducer.Unknown;
    }

    public void Dispose() => _zip.Dispose();
}
