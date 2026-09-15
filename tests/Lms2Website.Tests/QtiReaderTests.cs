using Lms2Website.Core.Cartridge;
using Lms2Website.Core.Model;

namespace Lms2Website.Tests;

public class QtiReaderTests
{
    private static QuizContent ReadTestQuiz()
    {
        using var cartridge = TestCartridge.Create();
        var course = CartridgeReader.Read(cartridge.Path);
        return course.AllItems.Single(i => i.Kind == ItemKind.Quiz).Quiz!;
    }

    [Fact]
    public void ReadsTitleAndQuestions()
    {
        var quiz = ReadTestQuiz();
        Assert.Equal("Week 2 Quiz", quiz.Title);
        Assert.Equal(2, quiz.Questions.Count);
        Assert.False(quiz.IsQuestionBank);
    }

    [Fact]
    public void CorrectChoiceIsTheOneTheCartridgeScores()
    {
        var quiz = ReadTestQuiz();
        var q = quiz.Questions[0];

        Assert.Equal("Multiple choice", q.TypeLabel);
        Assert.Equal(2m, q.Points);
        Assert.Contains("<b>population</b>", q.PromptHtml, StringComparison.Ordinal);   // the stem keeps its HTML
        Assert.Equal(2, q.Choices.Count);
        Assert.False(q.Choices[0].IsCorrect);      // "population" scores nothing
        Assert.True(q.Choices[1].IsCorrect);       // "sample" carries setvar 100
        Assert.False(q.HasNoMarkedAnswer);
    }

    [Fact]
    public void ShortAnswerKeepsTheAcceptedAnswers()
    {
        var quiz = ReadTestQuiz();
        var q = quiz.Questions[1];

        Assert.Equal("Short answer", q.TypeLabel);
        Assert.Empty(q.Choices);
        Assert.Equal(["4"], q.Answers);
        Assert.Contains("mean of 2, 4 and 6", q.PromptHtml, StringComparison.Ordinal);
    }

    [Fact]
    public void TrueFalseIsRecognisedFromTheChoicesAlone()
    {
        const string xml = """
        <questestinterop><assessment ident="a" title="TF">
          <item ident="i1" title="One">
            <presentation>
              <material><mattext texttype="text/html">The mean is a measure of centre.</mattext></material>
              <response_lid ident="r1" rcardinality="Single"><render_choice>
                <response_label ident="c1"><material><mattext>True</mattext></material></response_label>
                <response_label ident="c2"><material><mattext>False</mattext></material></response_label>
              </render_choice></response_lid>
            </presentation>
            <resprocessing>
              <respcondition><conditionvar><varequal respident="r1">c1</varequal></conditionvar>
                <setvar action="Set" varname="SCORE">100</setvar></respcondition>
            </resprocessing>
          </item>
        </assessment></questestinterop>
        """;

        var quiz = QtiReader.Read(xml, "fallback");
        var q = Assert.Single(quiz.Questions);
        Assert.Equal("True/False", q.TypeLabel);
        Assert.True(q.Choices[0].IsCorrect);
    }

    [Fact]
    public void AQuestionWithNoScoredAnswerIsFlaggedNotGuessed()
    {
        const string xml = """
        <questestinterop><assessment ident="a" title="No key">
          <item ident="i1" title="One">
            <presentation>
              <material><mattext>Pick one.</mattext></material>
              <response_lid ident="r1"><render_choice>
                <response_label ident="c1"><material><mattext>Alpha</mattext></material></response_label>
                <response_label ident="c2"><material><mattext>Beta</mattext></material></response_label>
              </render_choice></response_lid>
            </presentation>
          </item>
        </assessment></questestinterop>
        """;

        var quiz = QtiReader.Read(xml, "fallback");
        var q = Assert.Single(quiz.Questions);
        Assert.DoesNotContain(q.Choices, c => c.IsCorrect);
        Assert.True(q.HasNoMarkedAnswer);
        Assert.Contains(quiz.Warnings, w => w.Contains("do not record a correct answer", StringComparison.Ordinal));
    }

    [Fact]
    public void BadXmlIsAWarningNotAnException()
    {
        var quiz = QtiReader.Read("<questestinterop><assessment>", "Broken quiz");
        Assert.Equal("Broken quiz", quiz.Title);
        Assert.Empty(quiz.Questions);
        Assert.NotEmpty(quiz.Warnings);
    }
}
