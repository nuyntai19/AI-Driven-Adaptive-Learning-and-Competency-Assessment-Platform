using EduTwin.BLL.AssessmentAndReasoning.Feedback;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Feedback;

public sealed class AnalysisFeedbackPresentationTests
{
    [Theory]
    [InlineData(true, "Đáp án được bộ chấm tự động công nhận đúng")]
    [InlineData(false, "Đáp án chưa khớp kết quả bộ chấm tự động")]
    [InlineData(null, "Kết quả đang chờ giáo viên xác nhận")]
    public void LegacyNoticeUsesEffectiveCorrectnessInsteadOfStaleText(bool? correctness, string expected)
    {
        var text = AnalysisFeedbackPresentation.Resolve("LegacySystem", "Stale wrong-answer message", correctness);
        Assert.StartsWith(expected, text);
        Assert.DoesNotContain("Stale", text);
    }

    [Theory]
    [InlineData("Gemini")]
    [InlineData("RuleBased")]
    [InlineData(null)]
    public void GenuineFeedbackIsNotRewrittenToAgreeWithAnswerGrading(string? origin)
    {
        Assert.Equal("Original analysis", AnalysisFeedbackPresentation.Resolve(origin, "Original analysis", true));
    }
}
