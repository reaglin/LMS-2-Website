using System.Security.Cryptography;
using System.Text;

namespace Lms2Website.Core.Publish;

/// <summary>
/// Keeps the GitHub personal access token on this machine, encrypted with DPAPI for the current
/// Windows user, in <c>Documents\LMS 2 Website\github-token.dat</c>. Nobody signed in as anyone
/// else can read it, and it never travels with a project or a site.
/// </summary>
public static class TokenStore
{
    public static string Folder =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "LMS 2 Website");

    public static string TokenPath => Path.Combine(Folder, "github-token.dat");

    public static bool HasToken => File.Exists(TokenPath);

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
        if (!File.Exists(TokenPath)) return null;
        try
        {
            var plain = Unprotect(File.ReadAllBytes(TokenPath));
            var token = Encoding.UTF8.GetString(plain).Trim();
            return token.Length == 0 ? null : token;
        }
        catch (CryptographicException) { return null; }   // written by another user or machine
        catch (IOException) { return null; }
    }

    public static void Clear()
    {
        if (File.Exists(TokenPath)) File.Delete(TokenPath);
    }

    /// <summary>"ghp_1234…cdef" — enough to recognise, not enough to use.</summary>
    public static string Mask(string token) =>
        token.Length <= 10 ? new string('•', token.Length) : $"{token[..4]}…{token[^4..]}";

    private static byte[] Protect(byte[] data) =>
        OperatingSystem.IsWindows() ? ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser) : data;

    private static byte[] Unprotect(byte[] data) =>
        OperatingSystem.IsWindows() ? ProtectedData.Unprotect(data, null, DataProtectionScope.CurrentUser) : data;
}
