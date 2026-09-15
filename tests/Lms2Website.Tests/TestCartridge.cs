using Lms2Website.Core.Samples;

namespace Lms2Website.Tests;

/// <summary>
/// The demo cartridge from Core, written to a temp folder for one test and deleted after.
/// Core owns the content so the app can write the same file as a sample for hand-testing.
/// </summary>
public sealed class TestCartridge : IDisposable
{
    public string Path { get; }
    private readonly string _folder;

    private TestCartridge(string path, string folder) { Path = path; _folder = folder; }

    public static TestCartridge Create()
    {
        var folder = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "lms2web-test-" + Guid.NewGuid().ToString("N")[..8]);
        return new TestCartridge(SampleCartridge.Write(folder, "TestCourse.imscc"), folder);
    }

    public void Dispose()
    {
        try { Directory.Delete(_folder, recursive: true); } catch (IOException) { /* a handle lingers; temp can keep it */ }
    }
}
