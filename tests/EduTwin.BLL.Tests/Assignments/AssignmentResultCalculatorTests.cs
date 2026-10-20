using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.BLL.Assignments;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Assignments;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.Assignments;

public class AssignmentResultCalculatorTests
{
    [Fact]
    public async Task KnowledgeScope_UsesQuestionTags_SeparatesPendingAndVoided_AndHonorsTeacherCorrections()
    {
        var (context, center) = CreateInMemoryContext(); await using var db = context;
        var assignment = Guid.NewGuid(); var student = Guid.NewGuid(); var subject = Guid.NewGuid(); var now = DateTime.UtcNow;
        db.Assignments.Add(new Assignment { CenterId = center, AssignmentId = assignment, Title = "sdadas", Status = AssignmentStatus.Published, CreatedAt = now, UpdatedAt = now });
        db.KnowledgeNodes.Add(new EduTwin.DAL.KnowledgeGraph.KnowledgeNode { CenterId = center, SubjectId = subject, NodeId = 100,
            NodeName = "Thì", NodeCode = "TENSES", NodeType = EduTwin.Contracts.KnowledgeGraph.NodeType.Topic, CreatedAt = now, UpdatedAt = now });
        foreach (var id in new[] { 101ul, 102ul }) db.KnowledgeNodes.Add(new EduTwin.DAL.KnowledgeGraph.KnowledgeNode {
            CenterId = center, SubjectId = subject, NodeId = id, ParentNodeId = 100, NodeName = id == 101 ? "Hiện tại đơn" : "Quá khứ hoàn thành",
            NodeCode = $"SKILL-{id}", NodeType = EduTwin.Contracts.KnowledgeGraph.NodeType.Skill, CreatedAt = now, UpdatedAt = now });
        for (ulong i = 1; i <= 4; i++)
        {
            db.Questions.Add(new Question { CenterId = center, SubjectId = subject, QuestionId = i, PrimaryTopicNodeId = i == 2 ? 102ul : i == 3 ? 999ul : 101ul,
                QuestionText = "Question", CorrectAnswer = "Answer", Solution = "Explanation", LanguageCode = "en", MaxScore = 10, EstimatedTimeSeconds = 120,
                CreatedAt = now, UpdatedAt = now });
            db.AssignmentQuestions.Add(new AssignmentQuestion { CenterId = center, AssignmentId = assignment, QuestionId = i,
                OrderIndex = (uint)i, Points = 10, IsVoided = i == 4, CreatedAt = now });
        }
        db.Attempts.Add(new Attempt { CenterId = center, StudentId = student, AssignmentId = assignment, QuestionId = 1, AttemptId = 1,
            FinalAnswer = "Answer", ReasoningLanguage = "vi", IsCorrect = false, AwardedScore = 0, CreatedAt = now, UpdatedAt = now });
        db.ReasoningAnalyses.Add(new ReasoningAnalysis { CenterId = center, AttemptId = 1, AnalysisId = 1, OverrideIsCorrect = true,
            OverrideAwardedScore = 10, OverrideVersion = 1, SchemaVersion = "ai-analysis-v1", Feedback = "Đã sửa điểm",
            MissingSteps = JsonDocument.Parse("[]"), RootCauseNodeIds = JsonDocument.Parse("[]"), CreatedAt = now, UpdatedAt = now });
        await db.SaveChangesAsync();
        var result = await new AssignmentResultCalculator(db).CalculateForSingleAssignmentAsync(center, student, assignment, default);
        Assert.Equal(2, result.KnowledgeAreas.Count);
        var present = Assert.Single(result.KnowledgeAreas, a => a.SkillId == "101");
        Assert.Equal("Thì", present.TopicName); Assert.Equal("Hiện tại đơn", present.SkillName);
        Assert.Equal(1, present.QuestionCount); Assert.Equal(1, present.CorrectQuestionCount);
        Assert.Equal(0, Assert.Single(result.KnowledgeAreas, a => a.SkillId == "102").EvaluatedQuestionCount);
        Assert.Equal(1, result.UnclassifiedQuestionCount); Assert.Equal(1, result.VoidedQuestionCount);
        Assert.Equal(6, result.EstimatedMinutes);
        var another = await new AssignmentResultCalculator(db).CalculateForSingleAssignmentAsync(center, Guid.NewGuid(), assignment, default);
        Assert.All(another.KnowledgeAreas, a => Assert.Equal(0, a.EvaluatedQuestionCount));
    }

    [Fact]
    public async Task AIProposal_IsAnalyzedButNotFinalScore_AndAwaitingApprovalIsNotIncorrect()
    {
        var (context, center) = CreateInMemoryContext(); await using var db = context;
        var assignment = Guid.NewGuid(); var student = Guid.NewGuid(); var now = DateTime.UtcNow;
        db.Assignments.Add(new Assignment { AssignmentId = assignment, CenterId = center, Title = "Synthetic English", Status = AssignmentStatus.Published, CreatedAt = now, UpdatedAt = now });
        db.Questions.Add(new Question { CenterId = center, QuestionId = 1, QuestionType = QuestionType.Essay, MaxScore = 2,
            QuestionText = "Rewrite the sentence", CorrectAnswer = "I have been learning English since 2020.",
            Solution = "Dùng thì hiện tại hoàn thành tiếp diễn.", LanguageCode = "en", CreatedAt = now, UpdatedAt = now });
        db.AssignmentQuestions.Add(new AssignmentQuestion { CenterId = center, AssignmentId = assignment, QuestionId = 1, OrderIndex = 1, Points = 2, CreatedAt = now });
        db.Attempts.Add(new Attempt { CenterId = center, AttemptId = 1, AssignmentId = assignment, StudentId = student, QuestionId = 1,
            FinalAnswer = "I have been learning English since 2020.", ReasoningLanguage = "vi", Status = AttemptStatus.NeedsTeacherReview, CreatedAt = now, UpdatedAt = now });
        db.ReasoningAnalyses.Add(new ReasoningAnalysis { CenterId = center, AnalysisId = 1, AttemptId = 1, SuggestedScore = 2,
            AnswerAssessment = "Correct", ReasoningVerdict = "Valid", NeedsTeacherReview = true, Feedback = "Đáp án đúng.",
            SchemaVersion = "ai-analysis-v1", MissingSteps = JsonDocument.Parse("[]"), RootCauseNodeIds = JsonDocument.Parse("[]"), CreatedAt = now, UpdatedAt = now });
        db.StudentAssignmentProgresses.Add(new StudentAssignmentProgress { CenterId = center, AssignmentId = assignment, StudentId = student,
            IsOverallAiCommentStale = true, CreatedAt = now, UpdatedAt = now });
        await db.SaveChangesAsync();
        var result = await new AssignmentResultCalculator(db).CalculateForSingleAssignmentAsync(center, student, assignment, default);
        Assert.Equal(1, result.AiAnalyzedQuestionCount); Assert.Equal(0, result.EvaluatedQuestionCount);
        Assert.Equal(0, result.IncorrectQuestionCount); Assert.Null(result.InternalAwardedScore);
        var comment = await new OverallAssignmentCommentWorkflow(db, TimeProvider.System).GenerateAndCacheOverallCommentAsync(center, assignment, student, default);
        Assert.Contains("1 câu đang chờ giáo viên duyệt điểm", comment);
        Assert.Contains("chưa được kết luận là sai", comment);
        Assert.DoesNotContain("Gợi ý cải thiện", comment);
    }

    private (EduTwinDbContext context, Guid centerId) CreateInMemoryContext()
    {
        var centerId = Guid.NewGuid();
        var accessor = new Mock<ITenantIdAccessor>();
        accessor.SetupGet(x => x.CenterId).Returns(centerId);

        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new EduTwinDbContext(options, accessor.Object);
        return (context, centerId);
    }

    [Fact]
    public async Task CalculateAsync_WithNoAttempts_ReturnsZeroScoreAndNotStarted()
    {
        var (context, centerId) = CreateInMemoryContext();
        var assignmentId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var assignment = new Assignment
        {
            AssignmentId = assignmentId,
            CenterId = centerId,
            Title = "Math Quiz",
            Status = AssignmentStatus.Published,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var q1 = new Question
        {
            QuestionId = 1001,
            CenterId = centerId,
            QuestionType = QuestionType.MultipleChoice,
            Difficulty = 2,
            QuestionText = "1 + 1 = ?",
            CorrectAnswer = "2",
            Solution = "1+1=2",
            LanguageCode = "vi",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var q2 = new Question
        {
            QuestionId = 1002,
            CenterId = centerId,
            QuestionType = QuestionType.ShortAnswer,
            Difficulty = 2,
            QuestionText = "2 + 2 = ?",
            CorrectAnswer = "4",
            Solution = "2+2=4",
            LanguageCode = "vi",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var aq1 = new AssignmentQuestion { CenterId = centerId, AssignmentId = assignmentId, QuestionId = 1001, OrderIndex = 1, Points = 10, Question = q1, CreatedAt = DateTime.UtcNow };
        var aq2 = new AssignmentQuestion { CenterId = centerId, AssignmentId = assignmentId, QuestionId = 1002, OrderIndex = 2, Points = 10, Question = q2, CreatedAt = DateTime.UtcNow };

        context.Assignments.Add(assignment);
        context.Questions.AddRange(q1, q2);
        context.AssignmentQuestions.AddRange(aq1, aq2);
        await context.SaveChangesAsync();

        var calculator = new AssignmentResultCalculator(context);
        var summary = await calculator.CalculateForSingleAssignmentAsync(centerId, studentId, assignmentId, CancellationToken.None);

        Assert.Null(summary.InternalAwardedScore);
        Assert.Equal(10, summary.InternalMaxScore);
        Assert.Equal(2, summary.TotalQuestionCount);
        Assert.Equal(0, summary.CorrectQuestionCount);
        Assert.Equal("Processing", summary.ResultStatus);
        Assert.Equal("Pending", summary.TeacherFinalReviewStatus);
        Assert.Null(summary.OverallAiComment);
    }

    [Fact]
    public async Task CalculateAsync_WithHumanReview_MarksTeacherReviewStatusAsReviewed()
    {
        var (context, centerId) = CreateInMemoryContext();
        var assignmentId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var assignment = new Assignment
        {
            AssignmentId = assignmentId,
            CenterId = centerId,
            Title = "Physics Exam",
            Status = AssignmentStatus.Published,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var q1 = new Question
        {
            QuestionId = 2001,
            CenterId = centerId,
            QuestionType = QuestionType.Essay,
            Difficulty = 3,
            QuestionText = "Nêu định luật II Newton",
            CorrectAnswer = "F = m * a",
            Solution = "F = m * a",
            LanguageCode = "vi",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var aq1 = new AssignmentQuestion { CenterId = centerId, AssignmentId = assignmentId, QuestionId = 2001, OrderIndex = 1, Points = 10, Question = q1, CreatedAt = DateTime.UtcNow };

        var attempt = new Attempt
        {
            AttemptId = 501,
            CenterId = centerId,
            AssignmentId = assignmentId,
            QuestionId = 2001,
            StudentId = studentId,
            FinalAnswer = "F = m * a",
            AwardedScore = 10m,
            IsCorrect = true,
            Status = AttemptStatus.Completed,
            ReasoningLanguage = "vi",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var emptyJson = JsonDocument.Parse("[]");
        var analysis = new ReasoningAnalysis
        {
            AnalysisId = 301,
            CenterId = centerId,
            AttemptId = 501,
            NeedsTeacherReview = false,
            ReviewedAt = DateTime.UtcNow,
            ReviewDecision = "Approved",
            TeacherReviewNote = "Chính xác",
            Feedback = "Tốt",
            SchemaVersion = "1.0",
            MissingSteps = emptyJson,
            RootCauseNodeIds = emptyJson,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        var progress = new StudentAssignmentProgress
        {
            ProgressId = 9001, CenterId = centerId, AssignmentId = assignmentId, StudentId = studentId,
            Status = ProgressStatus.Completed, CompletedQuestionCount = 1, TotalQuestionCount = 1,
            TeacherFinalReviewStatus = TeacherFinalReviewStatus.Approved,
            FinalReviewedAt = DateTime.UtcNow, FinalReviewedByUserId = Guid.NewGuid(), FinalReviewVersion = 1,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        };

        context.Assignments.Add(assignment);
        context.Questions.Add(q1);
        context.AssignmentQuestions.Add(aq1);
        context.Attempts.Add(attempt);
        context.ReasoningAnalyses.Add(analysis);
        context.StudentAssignmentProgresses.Add(progress);
        await context.SaveChangesAsync();

        var calculator = new AssignmentResultCalculator(context);
        var summary = await calculator.CalculateForSingleAssignmentAsync(centerId, studentId, assignmentId, CancellationToken.None);

        Assert.Equal(10m, summary.InternalAwardedScore);
        Assert.Equal(10m, summary.InternalMaxScore);
        Assert.Equal(1, summary.CorrectQuestionCount);
        Assert.Equal("Approved", summary.TeacherFinalReviewStatus);
        Assert.Equal("Final", summary.ResultStatus);
    }

    [Fact]
    public async Task CalculateAsync_LegacyDataWithNeedsTeacherReviewFalse_ReturnsNotRequired_NotReviewed()
    {
        var (context, centerId) = CreateInMemoryContext();
        var assignmentId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var assignment = new Assignment
        {
            AssignmentId = assignmentId,
            CenterId = centerId,
            Title = "Chemistry Quiz",
            Status = AssignmentStatus.Published,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var q1 = new Question
        {
            QuestionId = 3001,
            CenterId = centerId,
            QuestionType = QuestionType.ShortAnswer,
            Difficulty = 1,
            QuestionText = "Khối lượng mol của H2O?",
            CorrectAnswer = "18",
            Solution = "18 g/mol",
            LanguageCode = "vi",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var aq1 = new AssignmentQuestion { CenterId = centerId, AssignmentId = assignmentId, QuestionId = 3001, OrderIndex = 1, Points = 10, Question = q1, CreatedAt = DateTime.UtcNow };

        var attempt = new Attempt
        {
            AttemptId = 601,
            CenterId = centerId,
            AssignmentId = assignmentId,
            QuestionId = 3001,
            StudentId = studentId,
            FinalAnswer = "18",
            AwardedScore = 10m,
            IsCorrect = true,
            Status = AttemptStatus.Completed,
            ReasoningLanguage = "vi",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var emptyJson = JsonDocument.Parse("[]");
        // Legacy record: NeedsTeacherReview=false, but ReviewedAt=null, ReviewDecision=null
        var analysis = new ReasoningAnalysis
        {
            AnalysisId = 401,
            CenterId = centerId,
            AttemptId = 601,
            NeedsTeacherReview = false,
            ReviewedAt = null,
            ReviewDecision = null,
            Feedback = "Đúng",
            SchemaVersion = "1.0",
            MissingSteps = emptyJson,
            RootCauseNodeIds = emptyJson,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.Assignments.Add(assignment);
        context.Questions.Add(q1);
        context.AssignmentQuestions.Add(aq1);
        context.Attempts.Add(attempt);
        context.ReasoningAnalyses.Add(analysis);
        await context.SaveChangesAsync();

        var calculator = new AssignmentResultCalculator(context);
        var summary = await calculator.CalculateForSingleAssignmentAsync(centerId, studentId, assignmentId, CancellationToken.None);

        Assert.Equal("Pending", summary.TeacherFinalReviewStatus);
        Assert.Equal("Provisional", summary.ResultStatus);
        analysis.NeedsTeacherReview = true;
        await context.SaveChangesAsync();
        var awaitingReview = await calculator.CalculateForSingleAssignmentAsync(centerId, studentId, assignmentId, CancellationToken.None);
        Assert.Equal(1, awaitingReview.PendingQuestionCount);
        Assert.Equal("Provisional", awaitingReview.ResultStatus); // Graded, but not final until teacher confirmation.
    }
}
