using EduTwin.BLL.AssessmentAndReasoning.AI;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.AI;

public sealed partial class GeminiAIServiceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OperationalDeferralIsPropagatedWithoutBecomingAnInvalidStudentResponse(bool infrastructure)
    {
        Exception expected = infrastructure ? new AIAnalysisInfrastructureException()
            : new AIAnalysisDeferredException(TimeSpan.FromSeconds(60));
        var client = new FakeGeminiGenerateContentClient { Handler = (_, _, _, _) => throw expected };
        var actual = await Record.ExceptionAsync(() => CreateService(client).AnalyzeReasoningAsync(CreateRequest(), CancellationToken.None));
        Assert.Same(expected, actual);
    }
}
