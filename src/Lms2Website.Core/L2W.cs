using System.Reflection;

namespace Lms2Website.Core;

/// <summary>
/// The marks that say a folder, and the repository it is pushed to, were made by this app.
///
/// They exist because a published course site is disposable and a hand-made repository is not:
/// the site folder is rebuilt from the cartridge every time and published with a force-push, so
/// anyone — the app, or a person reading github.com — needs to be able to tell the two apart at a
/// glance. Four marks, from the outside in:
///
/// <list type="bullet">
///   <item>the repository name always starts with <see cref="RepoPrefix"/>,</item>
///   <item>the repository carries the <see cref="Topic"/> topic and says so in its description,</item>
///   <item>the site root holds <see cref="MarkerFileName"/>,</item>
///   <item>every page carries <c>&lt;meta name="generator"&gt;</c>.</item>
/// </list>
/// </summary>
public static class L2W
{
    public const string Product = "LMS 2 Website";

    /// <summary>Every repository the app publishes is named <c>L2W-&lt;course&gt;</c>.</summary>
    public const string RepoPrefix = "L2W-";

    /// <summary>The GitHub topic put on every repository the app publishes.</summary>
    public const string Topic = "lms-2-website";

    /// <summary>The marker file written to the root of every site the app builds.</summary>
    public const string MarkerFileName = "l2w-site.json";

    /// <summary>The version of this build, without any "+commit" suffix.</summary>
    public static string Version
    {
        get
        {
            var informational = typeof(L2W).Assembly
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(informational))
                return informational.Split('+')[0];
            return typeof(L2W).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
        }
    }

    /// <summary>"LMS 2 Website 0.1.0" — what goes in the generator meta tag and the marker file.</summary>
    public static string Generator => $"{Product} {Version}";
}
