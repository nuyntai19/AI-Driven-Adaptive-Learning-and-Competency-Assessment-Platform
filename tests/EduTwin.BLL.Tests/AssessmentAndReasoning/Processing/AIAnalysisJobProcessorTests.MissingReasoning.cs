using EduTwin.BLL.AssessmentAndReasoning;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.DAL.CurriculumAndQuestions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Processing;

public sealed partial class AIAnalysisJobProcessorTests
{
    [Fact]
    public async Task CorrectChoiceWithoutReasoningRetainsAnswerPointsButCannotCreateTrustedReasoningEvidence()
    {
        var store = new InMemoryDatabaseRoot();
        var name = Guid.NewGuid().ToString();
        var centerId = Guid.NewGuid();
        await SeedAsync(store, name, centerId, retryCount: 0, finalAnswer: "100");
        var tenant = new TenantContext();
        using var scope = tenant.BeginScope(centerId);
        await using var context = CreateContext(store, name, tenant);
        (await context.Attempts.SingleAsync()).ReasoningText = "khum";
        (await context.Questions.SingleAsync()).QuestionType = QuestionType.MultipleChoice;
        context.QuestionOptions.Add(new QuestionOption { CenterId = centerId, QuestionId = 10,
            OptionId = 100, OptionLabel = "A", OptionText = "42", IsCorrect = true, CreatedAt = UtcNow, UpdatedAt = UtcNow });
        await context.SaveChangesAsync();
        var service = new RecordingAIService((_, _) => Task.FromResult(ValidResponse("vi") with {
            MethodDetected = null, ReasoningQuality = 0, ErrorType = ErrorType.None,
            ReasoningVerdict = "Uncertain", ReasoningIssues = [], MissingSteps = [], RootCauseNodeIds = [],
            AnswerAssessment = "Correct", SuggestedScore = 1, UsesAlternativeMethod = false,
            Feedback = ReasoningEvidencePolicy.MissingFeedback }));

        var result = await CreateSut(context, tenant, UtcNow, service, durable: true).ExecuteAsync(1, "worker-current", default);
        Assert.Equal(EduTwin.BLL.AssessmentAndReasoning.Processing.AIAnalysisJobProcessingOutcome.Completed, result.Outcome);
        var saved = await ReloadAsync(store, name, centerId);
        Assert.Equal(1m, saved.Attempt.AwardedScore);
        Assert.True(saved.Attempt.IsCorrect);
        Assert.Null(Assert.Single(saved.Analyses).ReasoningQuality);
        var evidence = Assert.Single(saved.Evidence);
        Assert.Equal(0, evidence.ReasoningWeight);
        Assert.True(evidence.RequiresTeacherReview);
    }
}
