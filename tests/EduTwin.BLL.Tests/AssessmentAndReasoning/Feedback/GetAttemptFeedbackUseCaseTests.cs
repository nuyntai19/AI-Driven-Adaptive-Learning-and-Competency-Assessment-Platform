using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using EduTwin.BLL.AssessmentAndReasoning.Feedback;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Feedback;

public sealed class GetAttemptFeedbackUseCaseTests : IDisposable
{
    private readonly EduTwinDbContext _dbContext;
    private readonly TenantContext _tenantContext;
    private readonly Mock<IStudentOwnershipGuard> _guardMock;
    private readonly Guid _centerId = Guid.NewGuid();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Guid _teacherId = Guid.NewGuid();
    private readonly Guid _subjectId = Guid.NewGuid();

    public GetAttemptFeedbackUseCaseTests()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _tenantContext = new TenantContext();
        _tenantContext.Initialize(_centerId, _teacherId, nameof(UserRole.Teacher), 1);

        _dbContext = new EduTwinDbContext(options, _tenantContext);
        _guardMock = new Mock<IStudentOwnershipGuard>();
        _guardMock
            .Setup(g => g.CheckStudentAccessAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(OwnershipDecision.Allowed);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task ExecuteAsync_WhenTeacherOverrideExists_KeepsRawAIFeedbackPure_AndPopulatesTeacherEvaluationDto()
    {
        // 1. Seed Question
        var question = new Question
        {
            CenterId = _centerId,
            QuestionId = 1001,
            SubjectId = _subjectId,
            QuestionText = "Find x in 2x = 4",
            QuestionType = QuestionType.ShortAnswer,
            CorrectAnswer = "2",
            Solution = "2",
            LanguageCode = "vi",
            MaxScore = 10m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Questions.Add(question);

        // 2. Seed Attempt
        var attempt = new Attempt
        {
            CenterId = _centerId,
            AttemptId = 5001,
            StudentId = _studentId,
            QuestionId = 1001,
            FinalAnswer = "2",
            AwardedScore = 5m,
            IsCorrect = false,
            ReasoningLanguage = "vi",
            Status = AttemptStatus.Completed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Attempts.Add(attempt);

        // 3. Seed ReasoningAnalysis with both AI raw and Teacher override values
        var analysis = new ReasoningAnalysis
        {
            CenterId = _centerId,
            AnalysisId = 9001,
            AttemptId = 5001,
            SchemaVersion = "1.0",
            MethodDetected = "Direct Division",
            ReasoningQuality = 60m, // Raw AI quality
            ErrorType = ErrorType.Knowledge, // Raw AI error type
            AnalysisConfidence = 85m,
            Feedback = "AI says check your arithmetic",
            IsFallback = false,
            NeedsTeacherReview = false,
            MissingSteps = JsonDocument.Parse("[]"),
            RootCauseNodeIds = JsonDocument.Parse("[]"),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,

            // Teacher Override fields
            OverrideVersion = 1,
            OverrideIsCorrect = true,
            OverrideAwardedScore = 10m,
            OverrideReasoningQuality = 95m, // Teacher override quality
            OverrideErrorType = ErrorType.Reasoning, // Teacher override error type
            OverrideFeedback = "Excellent reasoning and clear steps shown",
            OverrideReason = "Legitimate alternate solution",
            OverriddenByUserId = _teacherId,
            OverriddenAt = DateTime.UtcNow
        };
        _dbContext.ReasoningAnalyses.Add(analysis);
        await _dbContext.SaveChangesAsync();

        var sut = new GetAttemptFeedbackUseCase(_dbContext, _tenantContext, _guardMock.Object);

        // Act
        var result = await sut.ExecuteAsync(5001, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);

        // Assert Raw AI block is NOT contaminated by teacher override
        var aiBlock = result.Data.Analysis;
        Assert.NotNull(aiBlock);
        Assert.True(aiBlock.IsRawAI);
        Assert.Equal(60, aiBlock.ReasoningQuality); // Must remain raw AI 60, not overridden 95
        Assert.Equal("Knowledge", aiBlock.ErrorType); // Must remain raw AI Knowledge, not overridden Reasoning
        Assert.Equal("AI says check your arithmetic", aiBlock.Feedback);
        Assert.True(aiBlock.HasTeacherOverride);

        // Assert Teacher Final Evaluation block holds the authoritative teacher override data
        var teacherBlock = result.Data.TeacherFinalEvaluation;
        Assert.NotNull(teacherBlock);
        Assert.True(teacherBlock.HasTeacherOverride);
        Assert.Equal(true, teacherBlock.TeacherIsCorrect);
        Assert.Equal(10m, teacherBlock.TeacherScore);
        Assert.Equal(95, teacherBlock.TeacherReasoningQuality);
        Assert.Equal("Reasoning", teacherBlock.TeacherErrorType);
        Assert.Equal("Excellent reasoning and clear steps shown", teacherBlock.TeacherFeedback);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMultipleChoiceHasMatchedOption_ResolvesLabelAndText_AndSetsAnswerDisplayLatexToNull()
    {
        // 1. Seed Question
        var question = new Question
        {
            CenterId = _centerId,
            QuestionId = 1002,
            SubjectId = _subjectId,
            QuestionText = "Cho x + 1 = 3. Giá trị của x là?",
            QuestionType = QuestionType.MultipleChoice,
            CorrectAnswer = "A",
            Solution = "x = 2",
            LanguageCode = "vi",
            MaxScore = 10m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Questions.Add(question);

        var option = new QuestionOption
        {
            CenterId = _centerId,
            QuestionId = 1002,
            OptionId = 2001UL,
            OptionLabel = "A",
            OptionText = "x = 2",
            IsCorrect = true,
            OrderIndex = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.QuestionOptions.Add(option);

        // 2. Seed Attempt with option ID in FinalAnswer and legacy UUID in AnswerDisplayLatex
        var legacyUuid = Guid.NewGuid();
        var attempt = new Attempt
        {
            CenterId = _centerId,
            AttemptId = 5002,
            StudentId = _studentId,
            QuestionId = 1002,
            FinalAnswer = "2001",
            AnswerDisplayLatex = legacyUuid.ToString(),
            AwardedScore = 10m,
            IsCorrect = true,
            ReasoningLanguage = "vi",
            Status = AttemptStatus.Completed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Attempts.Add(attempt);
        await _dbContext.SaveChangesAsync();

        var sut = new GetAttemptFeedbackUseCase(_dbContext, _tenantContext, _guardMock.Object);

        // Act
        var result = await sut.ExecuteAsync(5002, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.NotNull(result.Data.StudentSubmission);
        Assert.Equal("A. x = 2", result.Data.StudentSubmission.FinalAnswer);
        Assert.Null(result.Data.StudentSubmission.AnswerDisplayLatex);
    }

    [Fact]
    public async Task ExecuteAsync_WhenMultipleChoiceHasLegacyUnmatchedUuid_ResolvesToPhuongAnDaChon_AndSetsAnswerDisplayLatexToNull()
    {
        // 1. Seed Question without matching options
        var question = new Question
        {
            CenterId = _centerId,
            QuestionId = 1003,
            SubjectId = _subjectId,
            QuestionText = "Câu hỏi trắc nghiệm lịch sử",
            QuestionType = QuestionType.MultipleChoice,
            CorrectAnswer = "A",
            Solution = "Lời giải",
            LanguageCode = "vi",
            MaxScore = 10m,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Questions.Add(question);

        var unknownGuid = Guid.NewGuid();
        var attempt = new Attempt
        {
            CenterId = _centerId,
            AttemptId = 5003,
            StudentId = _studentId,
            QuestionId = 1003,
            FinalAnswer = unknownGuid.ToString(),
            AnswerDisplayLatex = unknownGuid.ToString(),
            AwardedScore = 0m,
            IsCorrect = false,
            ReasoningLanguage = "vi",
            Status = AttemptStatus.Completed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Attempts.Add(attempt);
        await _dbContext.SaveChangesAsync();

        var sut = new GetAttemptFeedbackUseCase(_dbContext, _tenantContext, _guardMock.Object);

        // Act
        var result = await sut.ExecuteAsync(5003, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.NotNull(result.Data.StudentSubmission);
        Assert.Equal("Phương án đã chọn", result.Data.StudentSubmission.FinalAnswer);
        Assert.Null(result.Data.StudentSubmission.AnswerDisplayLatex);
    }
}
