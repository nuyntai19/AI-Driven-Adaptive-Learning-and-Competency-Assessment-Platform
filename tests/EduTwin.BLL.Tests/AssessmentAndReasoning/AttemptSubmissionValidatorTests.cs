using EduTwin.BLL.AssessmentAndReasoning;
using EduTwin.BLL.AssessmentAndReasoning.PreliminaryGrading;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Assignments;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning;

public sealed class AttemptSubmissionValidatorTests : IDisposable
{
    private static readonly DateTime FixedUtcNow =
        new(2026, 8, 12, 8, 0, 0, DateTimeKind.Utc);

    private readonly Guid _centerId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private readonly Guid _studentId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private readonly Guid _teacherId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private readonly Mock<ITenantContext> _tenantContext = new();
    private readonly EduTwinDbContext _dbContext;

    public AttemptSubmissionValidatorTests()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var tenantAccessor = new Mock<ITenantIdAccessor>();
        tenantAccessor.SetupGet(accessor => accessor.CenterId).Returns(_centerId);

        _dbContext = new EduTwinDbContext(options, tenantAccessor.Object);
        SetTenant(nameof(UserRole.Student));
    }

    [Fact]
    public async Task ValidateAsync_FreePracticeMultipleChoice_ReturnsGradedSubmission()
    {
        await SeedActiveStudentAsync();
        await SeedQuestionAsync(
            questionType: QuestionType.MultipleChoice,
            correctAnswer: "B",
            options:
            [
                new QuestionOption { OptionId = 1001, OptionLabel = "A", IsCorrect = false, OrderIndex = 1 },
                new QuestionOption { OptionId = 1002, OptionLabel = "B", IsCorrect = true, OrderIndex = 2 }
            ]);
        _dbContext.ChangeTracker.Clear();

        var request = CreateRequest(finalAnswer: "1002");
        var result = await CreateSut().ValidateAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Null(result.ErrorCode);
        Assert.NotNull(result.Submission);
        Assert.Equal(_centerId, result.Submission.CenterId);
        Assert.Equal(_studentId, result.Submission.StudentId);
        Assert.Equal(100UL, result.Submission.QuestionId);
        Assert.Equal("vi", result.Submission.ReasoningLanguage);
        Assert.True(result.Submission.IsCorrect);
        Assert.Equal(2m, result.Submission.AwardedScore);
        Assert.False(result.Submission.IsIdempotentReplay);
    }

    [Fact]
    public async Task ValidateAsync_Essay_ReturnsManualPreliminaryGrade()
    {
        await SeedActiveStudentAsync();
        await SeedQuestionAsync(questionType: QuestionType.Essay, reasoningRequired: true);

        var result = await CreateSut().ValidateAsync(CreateRequest());

        Assert.True(result.IsSuccess);
        Assert.Null(result.Submission!.IsCorrect);
        Assert.Equal(0m, result.Submission.AwardedScore);
    }

    [Fact]
    public async Task ValidateAsync_PublishedAssignmentTarget_ReturnsSuccessWithoutRequiringCurrentMembership()
    {
        await SeedActiveStudentAsync();
        await SeedQuestionAsync();
        var assignmentId = await SeedAssignmentAsync(
            AssignmentStatus.Published,
            includeTarget: true,
            includeQuestion: true);

        var result = await CreateSut().ValidateAsync(CreateRequest(assignmentId: assignmentId));

        Assert.True(result.IsSuccess);
        Assert.Equal(assignmentId, result.Submission!.AssignmentId);
        Assert.Empty(_dbContext.ClassStudents);
    }

    [Fact]
    public async Task ValidateAsync_SameIdempotencyPayload_ReturnsExistingAttempt()
    {
        await SeedActiveStudentAsync();
        var request = CreateRequest();
        await SeedExistingAttemptAsync(request, attemptId: 7001);
        _dbContext.ChangeTracker.Clear();

        var result = await CreateSut().ValidateAsync(request);

        Assert.True(result.IsSuccess);
        Assert.True(result.Submission!.IsIdempotentReplay);
        Assert.Equal(7001UL, result.Submission.ExistingAttemptId);
        Assert.Equal(AttemptStatus.PendingAnalysis, await _dbContext.Attempts
            .Where(attempt => attempt.AttemptId == 7001)
            .Select(attempt => attempt.Status)
            .SingleAsync());
    }

    [Fact]
    public async Task ValidateAsync_SameIdempotencyKeyWithChangedPayload_ReturnsDuplicateSubmission()
    {
        await SeedActiveStudentAsync();
        var original = CreateRequest();
        await SeedExistingAttemptAsync(original, attemptId: 7002);

        var changed = CreateRequest(clientSubmissionId: original.ClientSubmissionId);
        changed.FinalAnswer = "changed";

        var result = await CreateSut().ValidateAsync(changed);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.DuplicateSubmission, result.ErrorCode);
    }

    [Fact]
    public async Task ValidateAsync_NullAndEmptyReasoningAreDifferentIdempotencyPayloads()
    {
        await SeedActiveStudentAsync();
        var original = CreateRequest();
        original.ReasoningText = null;
        await SeedExistingAttemptAsync(original, attemptId: 7003);

        var changed = CreateRequest(clientSubmissionId: original.ClientSubmissionId);
        changed.ReasoningText = string.Empty;

        var result = await CreateSut().ValidateAsync(changed);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.DuplicateSubmission, result.ErrorCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("+1")]
    [InlineData("-1")]
    [InlineData("1.0")]
    [InlineData(" 1")]
    [InlineData("1 ")]
    [InlineData("١")]
    [InlineData("18446744073709551616")]
    public async Task ValidateAsync_InvalidQuestionId_ReturnsValidationFailed(string? questionId)
    {
        var request = CreateRequest();
        request.QuestionId = questionId!;

        var result = await CreateSut().ValidateAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(100.01)]
    public async Task ValidateAsync_ConfidenceOutsideRange_ReturnsValidationFailed(double confidence)
    {
        var request = CreateRequest();
        request.Confidence = (decimal)confidence;

        var result = await CreateSut().ValidateAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task ValidateAsync_EmptyClientSubmissionId_ReturnsValidationFailed()
    {
        var result = await CreateSut().ValidateAsync(
            CreateRequest(clientSubmissionId: Guid.Empty));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task ValidateAsync_NullRequest_ReturnsValidationFailed()
    {
        var result = await CreateSut().ValidateAsync(null!);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task ValidateAsync_NullFinalAnswer_ReturnsValidationFailed()
    {
        var request = CreateRequest();
        request.FinalAnswer = null!;

        var result = await CreateSut().ValidateAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task ValidateAsync_EmptyAssignmentId_ReturnsValidationFailed()
    {
        var result = await CreateSut().ValidateAsync(
            CreateRequest(assignmentId: Guid.Empty));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task ValidateAsync_UnansweredAndNotSkipped_ReturnsValidationFailed()
    {
        var result = await CreateSut().ValidateAsync(
            CreateRequest(finalAnswer: "   ", skipped: false));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task ValidateAsync_UnansweredButSkipped_IsAccepted()
    {
        await SeedActiveStudentAsync();
        await SeedQuestionAsync(reasoningRequired: false);

        var result = await CreateSut().ValidateAsync(
            CreateRequest(finalAnswer: string.Empty, skipped: true, reasoningText: null));

        Assert.True(result.IsSuccess);
        Assert.False(result.Submission!.IsCorrect);
        Assert.Equal(0m, result.Submission.AwardedScore);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ValidateAsync_ReasoningRequiredButMissing_ReturnsQuestionReasoningRequired(
        string? reasoningText)
    {
        await SeedActiveStudentAsync();
        await SeedQuestionAsync(reasoningRequired: true);

        var result = await CreateSut().ValidateAsync(
            CreateRequest(reasoningText: reasoningText));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.QuestionReasoningRequired, result.ErrorCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("student")]
    [InlineData("Teacher")]
    [InlineData("CenterManager")]
    public async Task ValidateAsync_NonStudentRole_ReturnsResourceNotFound(string? role)
    {
        SetTenant(role);

        var result = await CreateSut().ValidateAsync(CreateRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ValidateAsync_UnresolvedTenant_ReturnsResourceNotFound()
    {
        _tenantContext.SetupGet(context => context.IsResolved).Returns(false);

        var result = await CreateSut().ValidateAsync(CreateRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData(UserStatus.Locked)]
    [InlineData(UserStatus.Disabled)]
    public async Task ValidateAsync_InactiveStudentUser_ReturnsResourceNotFound(UserStatus status)
    {
        await SeedActiveStudentAsync(status);

        var result = await CreateSut().ValidateAsync(CreateRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ValidateAsync_CrossTenantStudent_ReturnsResourceNotFound()
    {
        var otherCenterId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        await SeedActiveStudentAsync(centerId: otherCenterId);
        Assert.True(await _dbContext.Students
            .IgnoreQueryFilters()
            .AnyAsync(student =>
                student.CenterId == otherCenterId &&
                student.StudentId == _studentId));

        var result = await CreateSut().ValidateAsync(CreateRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData(QuestionStatus.Draft)]
    [InlineData(QuestionStatus.Archived)]
    public async Task ValidateAsync_QuestionNotActive_ReturnsResourceNotFound(QuestionStatus status)
    {
        await SeedActiveStudentAsync();
        await SeedQuestionAsync(status: status);

        var result = await CreateSut().ValidateAsync(CreateRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ValidateAsync_CrossTenantQuestion_ReturnsResourceNotFound()
    {
        await SeedActiveStudentAsync();
        var otherCenterId = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        await SeedQuestionAsync(centerId: otherCenterId);
        Assert.True(await _dbContext.Questions
            .IgnoreQueryFilters()
            .AnyAsync(question =>
                question.CenterId == otherCenterId &&
                question.QuestionId == 100));

        var result = await CreateSut().ValidateAsync(CreateRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ValidateAsync_MissingAssignment_ReturnsResourceNotFound()
    {
        await SeedActiveStudentAsync();
        await SeedQuestionAsync();

        var result = await CreateSut().ValidateAsync(
            CreateRequest(assignmentId: Guid.NewGuid()));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ValidateAsync_CrossTenantAssignment_ReturnsResourceNotFound()
    {
        await SeedActiveStudentAsync();
        await SeedQuestionAsync();
        var assignmentId = Guid.Parse("88888888-8888-8888-8888-888888888888");
        var otherCenterId = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        _dbContext.Assignments.Add(new Assignment
        {
            AssignmentId = assignmentId,
            CenterId = otherCenterId,
            ClassId = Guid.Parse("99999999-9999-9999-9999-999999999999"),
            CreatedByTeacherId = _teacherId,
            Title = "Bài tập Center khác",
            Status = AssignmentStatus.Published,
            CreatedAt = FixedUtcNow,
            UpdatedAt = FixedUtcNow
        });
        await _dbContext.SaveChangesAsync();
        Assert.True(await _dbContext.Assignments
            .IgnoreQueryFilters()
            .AnyAsync(assignment =>
                assignment.CenterId == otherCenterId &&
                assignment.AssignmentId == assignmentId));

        var result = await CreateSut().ValidateAsync(
            CreateRequest(assignmentId: assignmentId));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData(AssignmentStatus.Draft)]
    [InlineData(AssignmentStatus.Closed)]
    [InlineData(AssignmentStatus.Archived)]
    public async Task ValidateAsync_AssignmentNotPublished_ReturnsAssignmentNotAvailable(
        AssignmentStatus status)
    {
        await SeedActiveStudentAsync();
        await SeedQuestionAsync();
        var assignmentId = await SeedAssignmentAsync(status);

        var result = await CreateSut().ValidateAsync(
            CreateRequest(assignmentId: assignmentId));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.AssignmentNotAvailable, result.ErrorCode);
    }

    [Fact]
    public async Task ValidateAsync_StudentNotTargeted_ReturnsAssignmentNotAvailable()
    {
        await SeedActiveStudentAsync();
        await SeedQuestionAsync();
        var assignmentId = await SeedAssignmentAsync(
            AssignmentStatus.Published,
            includeTarget: false,
            includeQuestion: true);

        var result = await CreateSut().ValidateAsync(
            CreateRequest(assignmentId: assignmentId));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.AssignmentNotAvailable, result.ErrorCode);
    }

    [Fact]
    public async Task ValidateAsync_QuestionNotInAssignment_ReturnsAssignmentNotAvailable()
    {
        await SeedActiveStudentAsync();
        await SeedQuestionAsync();
        var assignmentId = await SeedAssignmentAsync(
            AssignmentStatus.Published,
            includeTarget: true,
            includeQuestion: false);

        var result = await CreateSut().ValidateAsync(
            CreateRequest(assignmentId: assignmentId));

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.AssignmentNotAvailable, result.ErrorCode);
    }

    [Fact]
    public async Task ValidateAsync_Success_DoesNotPersistOrTrackEntities()
    {
        await SeedActiveStudentAsync();
        await SeedQuestionAsync();
        _dbContext.ChangeTracker.Clear();

        var result = await CreateSut().ValidateAsync(CreateRequest());

        Assert.True(result.IsSuccess);
        Assert.Empty(_dbContext.ChangeTracker.Entries());
        Assert.Equal(0, await _dbContext.Attempts.CountAsync());
        Assert.Equal(0, await _dbContext.AIAnalysisJobs.CountAsync());
    }

    [Fact]
    public async Task ValidateAsync_CancelledToken_PropagatesCancellation()
    {
        await SeedActiveStudentAsync();
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            CreateSut().ValidateAsync(CreateRequest(), cancellation.Token));
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private AttemptSubmissionValidator CreateSut() => new(
        _dbContext,
        _tenantContext.Object,
        new PreliminaryGraderFactory(
            new MultipleChoiceGrader(),
            new ShortAnswerGrader(),
            new EssayGrader()));

    private void SetTenant(string? role)
    {
        _tenantContext.SetupGet(context => context.IsResolved).Returns(true);
        _tenantContext.SetupGet(context => context.CenterId).Returns(_centerId);
        _tenantContext.SetupGet(context => context.UserId).Returns(_studentId);
        _tenantContext.SetupGet(context => context.Role).Returns(role);
    }

    private SubmitAttemptRequest CreateRequest(
        Guid? clientSubmissionId = null,
        Guid? assignmentId = null,
        string finalAnswer = "B",
        bool skipped = false,
        string? reasoningText = "Em giải theo định nghĩa.") => new()
        {
            ClientSubmissionId = clientSubmissionId ??
            Guid.Parse("44444444-4444-4444-4444-444444444444"),
            QuestionId = "100",
            AssignmentId = assignmentId,
            FinalAnswer = finalAnswer,
            ReasoningText = reasoningText,
            TimeSpentSeconds = 165,
            Confidence = 80m,
            AnswerChanges = 1,
            Skipped = skipped
        };

    private async Task SeedActiveStudentAsync(
        UserStatus status = UserStatus.Active,
        Guid? centerId = null)
    {
        var effectiveCenterId = centerId ?? _centerId;

        _dbContext.Users.Add(new User
        {
            UserId = _studentId,
            CenterId = effectiveCenterId,
            Username = "student.001",
            PasswordHash = "test-hash",
            RoleName = UserRole.Student,
            DisplayName = "Học sinh kiểm thử",
            Status = status,
            AuthVersion = 1,
            CreatedAt = FixedUtcNow,
            UpdatedAt = FixedUtcNow
        });
        _dbContext.Students.Add(new Student
        {
            StudentId = _studentId,
            CenterId = effectiveCenterId,
            FullName = "Học sinh kiểm thử",
            GradeLevel = 12,
            CreatedAt = FixedUtcNow,
            UpdatedAt = FixedUtcNow
        });

        await _dbContext.SaveChangesAsync();
    }

    private async Task SeedQuestionAsync(
        QuestionStatus status = QuestionStatus.Active,
        QuestionType questionType = QuestionType.ShortAnswer,
        bool reasoningRequired = false,
        string correctAnswer = "B",
        Guid? centerId = null,
        IReadOnlyCollection<QuestionOption>? options = null)
    {
        var effectiveCenterId = centerId ?? _centerId;
        var question = new Question
        {
            QuestionId = 100,
            CenterId = effectiveCenterId,
            SubjectId = Guid.Parse("55555555-5555-5555-5555-555555555555"),
            PrimaryTopicNodeId = 10,
            CreatedByTeacherId = _teacherId,
            QuestionType = questionType,
            Difficulty = 3,
            QuestionText = "Câu hỏi kiểm thử",
            CorrectAnswer = correctAnswer,
            Solution = "Lời giải kiểm thử",
            GradingCriteria = new GradingCriteria(),
            MaxScore = 2m,
            EstimatedTimeSeconds = 180,
            ReasoningRequired = reasoningRequired,
            LanguageCode = "vi",
            Status = status,
            CreatedAt = FixedUtcNow,
            UpdatedAt = FixedUtcNow
        };
        _dbContext.Questions.Add(question);

        if (options is not null)
        {
            foreach (var option in options)
            {
                option.CenterId = effectiveCenterId;
                option.QuestionId = question.QuestionId;
                option.OptionText = $"Lựa chọn {option.OptionLabel}";
                option.CreatedAt = FixedUtcNow;
                option.UpdatedAt = FixedUtcNow;
                _dbContext.QuestionOptions.Add(option);
            }
        }

        await _dbContext.SaveChangesAsync();
    }

    private async Task<Guid> SeedAssignmentAsync(
        AssignmentStatus status,
        bool includeTarget = true,
        bool includeQuestion = true)
    {
        var assignmentId = Guid.Parse("66666666-6666-6666-6666-666666666666");
        _dbContext.Assignments.Add(new Assignment
        {
            AssignmentId = assignmentId,
            CenterId = _centerId,
            ClassId = Guid.Parse("77777777-7777-7777-7777-777777777777"),
            CreatedByTeacherId = _teacherId,
            Title = "Bài tập kiểm thử",
            Status = status,
            CreatedAt = FixedUtcNow,
            UpdatedAt = FixedUtcNow
        });

        if (includeTarget)
        {
            _dbContext.AssignmentTargets.Add(new AssignmentTarget
            {
                CenterId = _centerId,
                AssignmentId = assignmentId,
                StudentId = _studentId,
                TargetSource = TargetSource.SelectedStudents,
                CreatedAt = FixedUtcNow,
                CreatedBy = _teacherId
            });
        }

        if (includeQuestion)
        {
            _dbContext.AssignmentQuestions.Add(new AssignmentQuestion
            {
                CenterId = _centerId,
                AssignmentId = assignmentId,
                QuestionId = 100,
                OrderIndex = 1,
                Points = 2m,
                CreatedAt = FixedUtcNow
            });
        }

        await _dbContext.SaveChangesAsync();
        return assignmentId;
    }

    private async Task SeedExistingAttemptAsync(
        SubmitAttemptRequest request,
        ulong attemptId)
    {
        _dbContext.Attempts.Add(new Attempt
        {
            AttemptId = attemptId,
            CenterId = _centerId,
            StudentId = _studentId,
            QuestionId = ulong.Parse(request.QuestionId),
            AssignmentId = request.AssignmentId,
            FinalAnswer = request.FinalAnswer,
            ReasoningText = request.ReasoningText,
            IsCorrect = true,
            AwardedScore = 2m,
            TimeSpentSeconds = request.TimeSpentSeconds,
            Confidence = request.Confidence,
            AnswerChanges = request.AnswerChanges,
            Skipped = request.Skipped,
            ReasoningLanguage = "vi",
            Status = AttemptStatus.PendingAnalysis,
            ClientSubmissionId = request.ClientSubmissionId,
            CreatedAt = FixedUtcNow,
            CreatedBy = _studentId,
            UpdatedAt = FixedUtcNow
        });
        await _dbContext.SaveChangesAsync();
    }
}
