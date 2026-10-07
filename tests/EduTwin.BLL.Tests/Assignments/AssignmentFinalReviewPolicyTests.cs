using EduTwin.BLL.Assignments;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.DAL.Assignments;
using EduTwin.DAL.AssessmentAndReasoning;
using Xunit;

namespace EduTwin.BLL.Tests.Assignments;

public sealed class AssignmentFinalReviewPolicyTests
{
    private static AssignmentQuestion Question(ulong id, bool isVoided = false) => new() { QuestionId = id, IsVoided = isVoided };
    private static Attempt Work(ulong id, ulong questionId, AttemptStatus status, int minutes = 0) =>
        new() { AttemptId = id, QuestionId = questionId, Status = status, CreatedAt = new DateTime(2026, 10, 6).AddMinutes(minutes) };

    [Fact]
    public void EmptyAssignmentAndMissingSubmission_AreNotReady()
    {
        Assert.False(AssignmentFinalReviewPolicy.Evaluate([], []).CanApprove);
        var result = AssignmentFinalReviewPolicy.Evaluate([Question(1), Question(2)], [Work(1, 1, AttemptStatus.Completed)]);
        Assert.False(result.CanApprove);
        Assert.Equal(1, result.MissingQuestionCount);
        Assert.Contains("chưa nộp", result.BlockReason!);
    }

    [Theory]
    [InlineData(AttemptStatus.PendingAnalysis, 1, 0, 0)]
    [InlineData(AttemptStatus.Processing, 1, 0, 0)]
    [InlineData(AttemptStatus.NeedsTeacherReview, 0, 1, 0)]
    [InlineData(AttemptStatus.AnalysisFailed, 0, 0, 1)]
    [InlineData((AttemptStatus)99, 0, 0, 1)]
    public void EveryUnresolvedStatus_BlocksApproval(AttemptStatus status, int processing, int review, int failed)
    {
        var result = AssignmentFinalReviewPolicy.Evaluate([Question(1)], [Work(1, 1, status)]);
        Assert.False(result.CanApprove);
        Assert.Equal(processing, result.ProcessingQuestionCount);
        Assert.Equal(review, result.PendingReviewQuestionCount);
        Assert.Equal(failed, result.FailedQuestionCount);
        Assert.NotNull(result.BlockReason);
    }

    [Fact]
    public void LatestSubmissionWins_AndUnrelatedQuestionsDoNotCount()
    {
        var result = AssignmentFinalReviewPolicy.Evaluate([Question(1)],
            [Work(1, 1, AttemptStatus.NeedsTeacherReview), Work(2, 1, AttemptStatus.Completed, 1), Work(3, 999, AttemptStatus.Processing)]);
        Assert.True(result.CanApprove);
        Assert.Null(result.BlockReason);
    }

    [Fact]
    public void LatestTieUsesAttemptId_Deterministically()
    {
        var result = AssignmentFinalReviewPolicy.Evaluate([Question(1)],
            [Work(1, 1, AttemptStatus.Completed), Work(2, 1, AttemptStatus.NeedsTeacherReview)]);
        Assert.False(result.CanApprove);
        Assert.Equal(1, result.PendingReviewQuestionCount);
    }

    [Fact]
    public void CompletedAiGradeCannotBypassRequiredRubric_OnlyLatestAttemptWithRubricCanFinalize()
    {
        var questions = new[] { Question(1) };
        var attempts = new[] { Work(1, 1, AttemptStatus.Completed), Work(2, 1, AttemptStatus.Completed, 1) };
        Assert.False(AssignmentFinalReviewPolicy.Evaluate(questions, attempts, new HashSet<ulong> { 1 }, new HashSet<ulong> { 1 }).CanApprove);
        var pending = AssignmentFinalReviewPolicy.Evaluate(questions, attempts, new HashSet<ulong> { 1 }, new HashSet<ulong>());
        Assert.Equal(1, pending.PendingReviewQuestionCount);
        Assert.True(AssignmentFinalReviewPolicy.Evaluate(questions, attempts, new HashSet<ulong> { 1 }, new HashSet<ulong> { 2 }).CanApprove);
        Assert.True(AssignmentFinalReviewPolicy.Evaluate([Question(1, true)], [], new HashSet<ulong> { 1 }, new HashSet<ulong>()).CanApprove);
    }

    [Fact]
    public void VoidedQuestionsRequireNeitherSubmissionNorAnalysis()
    {
        var result = AssignmentFinalReviewPolicy.Evaluate([Question(1), Question(2, true)], [Work(1, 1, AttemptStatus.Completed)]);
        Assert.True(result.CanApprove);
        Assert.True(AssignmentFinalReviewPolicy.Evaluate([Question(1, true)], []).CanApprove);
    }
}
