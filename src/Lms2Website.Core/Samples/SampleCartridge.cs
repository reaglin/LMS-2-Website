using System.IO.Compression;
using System.Text;

namespace Lms2Website.Core.Samples;

/// <summary>
/// Writes a small, complete cartridge — two sections, two pages with an image and a deck, a quiz,
/// an assignment, a discussion and a web link. It is what the tests run against and what
/// <c>LMS2Website.exe --write-sample</c> puts in a folder, so anyone can try the app without a
/// real course export.
///
/// It carries the Brightspace quirks on purpose: a content folder spelled with a Cyrillic "с",
/// and a manifest href of "page.html;Display Name.html" where the zip entry is
/// "page.html;/Display Name.html".
/// </summary>
public static class SampleCartridge
{
    /// <summary>Writes demo-course.imscc into <paramref name="folder"/> and returns its path.</summary>
    public static string Write(string folder, string fileName = "demo-course.imscc")
    {
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, fileName);
        if (File.Exists(path)) File.Delete(path);

        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        Add(zip, "imsmanifest.xml", Manifest);
        Add(zip, "сontent/i100/overview.html;/Overview.html", OverviewPage);
        Add(zip, "сontent/i200/lecture.html", LecturePage);
        Add(zip, "сontent/i200/diagram.png", "not really a png");
        Add(zip, "сontent/i200/Lecture 1.pptx", "not really a pptx");
        Add(zip, "quiz/i300/assessment.xml", QuizXml);
        Add(zip, "assignment/i400/assignment.xml", AssignmentXml);
        Add(zip, "discussion/i500/topic.xml", DiscussionXml);
        Add(zip, "weblinks/i600/link.xml", WebLinkXml);
        return path;
    }

    private static void Add(ZipArchive zip, string name, string content)
    {
        using var stream = zip.CreateEntry(name).Open();
        var bytes = Encoding.UTF8.GetBytes(content);
        stream.Write(bytes, 0, bytes.Length);
    }

    private const string Manifest = """
<?xml version="1.0" encoding="utf-8"?>
<manifest identifier="itest-manifest" xmlns="http://www.imsglobal.org/xsd/imsccv1p3/imscp_v1p1"
          xmlns:lomm="http://ltsc.ieee.org/xsd/imsccv1p3/LOM/manifest">
  <metadata>
    <schema>IMS Common Cartridge</schema>
    <schemaversion>1.3.0</schemaversion>
    <lomm:lom><lomm:general><lomm:title><lomm:string language="en-US">Demo Course</lomm:string></lomm:title></lomm:general></lomm:lom>
  </metadata>
  <organizations>
    <organization identifier="iorg" structure="rooted-hierarchy">
      <item identifier="iroot">
        <item identifier="imod1">
          <title>Week 1: Getting Started</title>
          <item identifier="ii1" identifierref="ires-overview"><title>Overview.html</title></item>
          <item identifier="ii2" identifierref="ires-link"><title>Course site</title></item>
        </item>
        <item identifier="imod2">
          <title>Week 2 / Measurement</title>
          <item identifier="ii3" identifierref="ires-lecture"><title>Lecture notes</title></item>
          <item identifier="ii4" identifierref="ires-quiz"><title>Week 2 Quiz</title></item>
          <item identifier="ii5" identifierref="ires-assignment"><title>Assignment 1</title></item>
          <item identifier="isub">
            <title>Extras</title>
            <item identifier="ii6" identifierref="ires-discussion"><title>Say hello</title></item>
          </item>
        </item>
      </item>
    </organization>
  </organizations>
  <resources>
    <resource identifier="ires-overview" type="webcontent">
      <file href="сontent/i100/overview.html;Overview.html" />
    </resource>
    <resource identifier="ires-lecture" type="webcontent">
      <file href="сontent/i200/lecture.html" />
      <dependency identifierref="ires-deck" />
    </resource>
    <resource identifier="ires-deck" type="webcontent">
      <file href="сontent/i200/Lecture 1.pptx" />
    </resource>
    <resource identifier="ires-quiz" type="imsqti_xmlv1p2/imscc_xmlv1p3/assessment">
      <file href="quiz/i300/assessment.xml" />
    </resource>
    <resource identifier="ires-assignment" type="assignment_xmlv1p0">
      <file href="assignment/i400/assignment.xml" />
    </resource>
    <resource identifier="ires-discussion" type="imsdt_xmlv1p3">
      <file href="discussion/i500/topic.xml" />
    </resource>
    <resource identifier="ires-link" type="imswl_xmlv1p3">
      <file href="weblinks/i600/link.xml" />
    </resource>
  </resources>
</manifest>
""";

    private const string OverviewPage = """
<!DOCTYPE html>
<html><head><link rel="stylesheet" href="https://templates.lcs.brightspace.com/lib/assets/css/styles.min.css"></head>
<body><h2>Welcome</h2><p>Read the <a href="../i200/lecture.html">lecture notes</a> first.</p>
<p>Course tools are in <a href="https://class.example.edu/d2l/le/content/1234/Home">the LMS</a>.</p>
<script>alert('tracking');</script></body></html>
""";

    private const string LecturePage = """
<!DOCTYPE html>
<html><body><h2>Measurement</h2><p><img src="diagram.png" alt="A diagram"></p>
<p><a href="missing-file.pdf">A handout that is not in the export</a></p></body></html>
""";

    private const string QuizXml = """
<?xml version="1.0" encoding="utf-8"?>
<questestinterop xmlns="http://www.imsglobal.org/xsd/ims_qtiasiv1p2">
  <assessment ident="ia1" title="Week 2 Quiz">
    <section ident="is1">
      <item ident="iq1" title="Population or sample">
        <itemmetadata><qtimetadata>
          <qtimetadatafield><fieldlabel>cc_profile</fieldlabel><fieldentry>cc.multiple_choice.v0p1</fieldentry></qtimetadatafield>
          <qtimetadatafield><fieldlabel>cc_weighting</fieldlabel><fieldentry>2</fieldentry></qtimetadatafield>
        </qtimetadata></itemmetadata>
        <presentation>
          <material><mattext texttype="text/html">&lt;p&gt;Every fourth shopper is measured. Is that a &lt;b&gt;population&lt;/b&gt; or a sample?&lt;/p&gt;</mattext></material>
          <response_lid ident="ir1" rcardinality="Single">
            <render_choice>
              <response_label ident="ic1"><material><mattext texttype="text/html">population</mattext></material></response_label>
              <response_label ident="ic2"><material><mattext texttype="text/html">sample</mattext></material></response_label>
            </render_choice>
          </response_lid>
        </presentation>
        <resprocessing>
          <outcomes><decvar minvalue="0" maxvalue="100" varname="SCORE" vartype="Decimal" /></outcomes>
          <respcondition><conditionvar><varequal respident="ir1">ic1</varequal></conditionvar></respcondition>
          <respcondition continue="No">
            <conditionvar><varequal respident="ir1">ic2</varequal></conditionvar>
            <setvar action="Set" varname="SCORE">100</setvar>
          </respcondition>
        </resprocessing>
      </item>
      <item ident="iq2" title="Short one">
        <itemmetadata><qtimetadata>
          <qtimetadatafield><fieldlabel>cc_profile</fieldlabel><fieldentry>cc.fib.v0p1</fieldentry></qtimetadatafield>
        </qtimetadata></itemmetadata>
        <presentation>
          <material><mattext texttype="text/plain">The mean of 2, 4 and 6 is what?</mattext></material>
          <response_str ident="ir2"><render_fib><response_label ident="il1" /></render_fib></response_str>
        </presentation>
        <resprocessing>
          <outcomes><decvar minvalue="0" maxvalue="100" varname="SCORE" vartype="Decimal" /></outcomes>
          <respcondition continue="No">
            <conditionvar><varequal respident="ir2">4</varequal></conditionvar>
            <setvar action="Set" varname="SCORE">100</setvar>
          </respcondition>
        </resprocessing>
      </item>
    </section>
  </assessment>
</questestinterop>
""";

    private const string AssignmentXml = """
<?xml version="1.0" encoding="utf-8"?>
<assignment xmlns="http://www.imsglobal.org/xsd/imscc_extensions/assignment" identifier="ia400">
  <title>Assignment 1</title>
  <instructor_text texttype="text/html">&lt;p&gt;Write a &lt;strong&gt;report&lt;/strong&gt;.&lt;/p&gt;</instructor_text>
  <gradable points_possible="25" />
  <submission_formats><format type="file" /></submission_formats>
</assignment>
""";

    private const string DiscussionXml = """
<?xml version="1.0" encoding="utf-8"?>
<topic xmlns="http://www.imsglobal.org/xsd/imsccv1p3/imsdt_v1p3">
  <title>Say hello</title>
  <text texttype="text/html">&lt;p&gt;Introduce yourself.&lt;/p&gt;</text>
</topic>
""";

    private const string WebLinkXml = """
<?xml version="1.0" encoding="utf-8"?>
<webLink xmlns="http://www.imsglobal.org/xsd/imsccv1p3/imswl_v1p3">
  <title>Course site</title>
  <url href="https://example.edu/course" target="_blank" />
</webLink>
""";
}
