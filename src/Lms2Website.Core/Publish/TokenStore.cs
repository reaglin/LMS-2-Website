using System.Security.Cryptography;
using System.Text;

namespace Lms2Website.Core.Publish;

/// <summary>
/// Keeps the GitHub personal access token on this machine, encrypted with DPAPI for the current
/// Windows user, in <c>%LOCALAPPDATA%\LMS 2 Website\github-token.dat</c>. Nobody signed in as
/// anyone else can read it, and it never travels with a project or a site.
///
/// It lives there rather than beside the sites in Documents because the two are different kinds of
/// thing. Sites are the user's own work and belong where they will go looking for them — which on
/// a managed machine is often a synced OneDrive. A token is a machine-local secret nobody
/// navigates to, so there is no reason for it to be copied into a company tenant. It is DPAPI
/// encrypted and useless anywhere else either way; this simply keeps it off the wire.
/// </summary>
public static class TokenStore
{
    public static string Folder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LMS 2 Website");

    public static string TokenPath => Path.Combine(Folder, "github-token.dat");

    /// <summary>Where the token used to live, before it moved out of Documents.</summary>
    private static string LegacyTokenPath =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                     "LMS 2 Website", "github-token.dat");

    public static bool HasToken => File.Exists(TokenPath) || File.Exists(LegacyTokenPath);

    /// <summary>
    /// Moves a token saved by an older build out of Documents, once. The file is encrypted for
    /// this Windows user on this machine, so moving it keeps it working; a copy left behind in a
    /// synced folder would not be readable elsewhere, but there is no reason to leave one.
    /// </summary>
    private static void MigrateFromDocuments()
    {
        if (File.Exists(TokenPath) || !File.Exists(LegacyTokenPath)) return;
        try
        {
            Directory.CreateDirectory(Folder);
            File.Move(LegacyTokenPath, TokenPath);
        }
        catch (IOException) { /* it stays where it is and is still read below */ }
        catch (UnauthorizedAccessException) { }
    }

    public static void Save(string token)
    {
        Directory.CreateDirectory(Folder);
        if (string.IsNullOrWhiteSpace(token)) { Clear(); return; }
        var bytes = Encoding.UTF8.GetBytes(token.Trim());
        File.WriteAllBytes(TokenPath, Protect(bytes));
    }

    /// <summary>The stored token, or null when there is none or it cannot be decrypted here.</summary>
    public static string? Load()
    {
        MigrateFromDocuments();

        var path = File.Exists(TokenPath) ? TokenPath
                 : File.Exists(LegacyTokenPath) ? LegacyTokenPath   // the move failed; still usable
                 : null;
        if (path == null) return null;
        try
        {
            var plain = Unprotect(File.ReadAllBytes(path));
            var token = Encoding.UTF8.GetString(plain).Trim();
            return token.Length == 0 ? null : token;
        }
        catch (CryptographicException) { return null; }   // written by another user or machine
        catch (IOException) { return null; }
    }

    /// <summary>Removes the token — from the old place as well, so "Remove" really removes it.</summary>
    public static void Clear()
    {
        if (File.Exists(TokenPath)) File.Delete(TokenPath);
        if (File.Exists(LegacyTokenPath)) File.Delete(LegacyTokenPath);
    }

    /// <summary>"ghp_1234…cdef" — enough to recognise, not enough to use.</summary>
    public static string Mask(string token) =>
        token.Length <= 10 ? new string('•', token.Length) : $"{token[..4]}…{token[^4..]}";

    private static byte[] Protect(byte[] data) =>
        OperatingSystem.IsWindows() ? ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser) : data;

    private static byte[] Unprotect(byte[] data) =>
        OperatingSystem.IsWindows() ? ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser) : data;
}
