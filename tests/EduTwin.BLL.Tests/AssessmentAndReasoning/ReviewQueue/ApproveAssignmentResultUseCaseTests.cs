using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using EduTwin.BLL.AssessmentAndReasoning.ReviewQueue;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.Assignments;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.ReviewQueue;

public sealed class ApproveAssignmentResultUseCaseTests : IDisposable
{
    private readonly EduTwinDbContext _dbContext;
    private readonly TenantContext _tenantContext;
    private readonly Guid _centerId = Guid.NewGuid();
    private readonly Guid _teacherId = Guid.NewGuid();

    public ApproveAssignmentResultUseCaseTests()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _tenantContext = new TenantContext();
        _tenantContext.Initialize(_centerId, _teacherId, nameof(UserRole.Teacher), 1);

        _dbContext = new EduTwinDbContext(options, _tenantContext);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task ExecuteAsync_WhenStudentIdIsEmpty_ReturnsValidationFailed_StudentIdRequired()
    {
        var sut = new ApproveAssignmentResultUseCase(_dbContext, _tenantContext, TimeProvider.System);

        var request = new ApproveAssignmentResultRequest
        {
            StudentId = Guid.Empty,
            FinalReviewVersion = 1,
            Note = "Valid note"
        };

        var result = await sut.ExecuteAsync(Guid.NewGuid(), request, CancellationToken.None);

        Assert.Equal(TeacherApproveStatus.ValidationFailed, result.Status);
        Assert.Equal("STUDENT_ID_REQUIRED", result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_WhenNoteExceeds1000Chars_ReturnsValidationFailed_NoteTooLong()
    {
        var sut = new ApproveAssignmentResultUseCase(_dbContext, _tenantContext, TimeProvider.System);

        var request = new ApproveAssignmentResultRequest
        {
            StudentId = Guid.NewGuid(),
            FinalReviewVersion = 1,
            Note = new string('X', 1001)
        };

        var result = await sut.ExecuteAsync(Guid.NewGuid(), request, CancellationToken.None);

        Assert.Equal(TeacherApproveStatus.ValidationFailed, result.Status);
        Assert.Equal("NOTE_TOO_LONG", result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_WhenUserIsNotTeacher_ReturnsForbidden()
    {
        var studentTenant = new TenantContext();
        studentTenant.Initialize(_centerId, Guid.NewGuid(), nameof(UserRole.Student), 1);

        var sut = new ApproveAssignmentResultUseCase(_dbContext, studentTenant, TimeProvider.System);

        var request = new ApproveAssignmentResultRequest
        {
            StudentId = Guid.NewGuid(),
            FinalReviewVersion = 1,
            Note = "Valid note"
        };

        var result = await sut.ExecuteAsync(Guid.NewGuid(), request, CancellationToken.None);

        Assert.Equal(TeacherApproveStatus.Forbidden, result.Status);
        Assert.Equal("FORBIDDEN", result.ErrorCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_WhenValidRequest_ApprovesAssignment_UpdatesFinalReviewAndReturnsSuccess(bool useRubric)
    {
        // Arrange
        var assignmentId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var questionId = 2001ul;

        var classEntity = new EduTwin.DAL.Organization.Class
        {
            CenterId = _centerId,
            ClassId = classId,
            TeacherId = _teacherId,
            SubjectId = Guid.NewGuid(),
            ClassName = "12A1",
            AcademicYear = "2025-2026",
            Status = EduTwin.Contracts.Organization.ClassStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Classes.Add(classEntity);

        var assignment = new EduTwin.DAL.Assignments.Assignment
        {
            CenterId = _centerId,
            AssignmentId = assignmentId,
            ClassId = classId,
            CreatedByTeacherId = _teacherId,
            Title = "Kiểm tra 15 phút",
            Status = AssignmentStatus.Published,
            Class = classEntity,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Assignments.Add(assignment);

        var progress = new StudentAssignmentProgress
        {
            CenterId = _centerId,
            AssignmentId = assignmentId,
            StudentId = studentId,
            Status = ProgressStatus.Completed,
            TeacherFinalReviewStatus = TeacherFinalReviewStatus.Pending,
            FinalReviewVersion = 1,
            Assignment = assignment,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.StudentAssignmentProgresses.Add(progress);

        var assignmentQuestion = new AssignmentQuestion
        {
            CenterId = _centerId,
            AssignmentId = assignmentId,
            QuestionId = questionId,
            OrderIndex = 1,
            Points = 10m,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.AssignmentQuestions.Add(assignmentQuestion);

        var attempt = new EduTwin.DAL.AssessmentAndReasoning.Attempt
        {
            CenterId = _centerId,
            AttemptId = 8801,
            StudentId = studentId,
            AssignmentId = assignmentId,
            QuestionId = questionId,
            FinalAnswer = "42",
            AwardedScore = 10m,
            IsCorrect = true,
            ReasoningLanguage = "vi",
            Status = AttemptStatus.Completed,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        _dbContext.Attempts.Add(attempt);
        if (useRubric) _dbContext.Questions.Add(new()
        {
            CenterId = _centerId, QuestionId = questionId, MaxScore = 10, QuestionText = "Question", CorrectAnswer = "42", Solution = "Reference", LanguageCode = "vi", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            GradingCriteria = new() { Criteria = [new() { CriterionId = "result", Title = "Kết quả", Description = "Kết quả đúng", MaxScore = 10 }] }
        });
        await _dbContext.SaveChangesAsync();

        var sut = new ApproveAssignmentResultUseCase(_dbContext, _tenantContext, TimeProvider.System);

        var request = new ApproveAssignmentResultRequest
        {
            StudentId = studentId,
            FinalReviewVersion = 1,
            Note = "Bài làm xuất sắc và giải thích chi tiết."
        };

        // Act
        if (useRubric)
        {
            Assert.Equal("ASSIGNMENT_HAS_PENDING_REVIEWS", (await sut.ExecuteAsync(assignmentId, request, CancellationToken.None)).ErrorCode);
            var analysis = new EduTwin.DAL.AssessmentAndReasoning.ReasoningAnalysis
            {
                CenterId = _centerId, AnalysisId = 8802, AttemptId = attempt.AttemptId, SchemaVersion = "1.0", Feedback = "Teacher grade",
                MissingSteps = System.Text.Json.JsonDocument.Parse("[]"), RootCauseNodeIds = System.Text.Json.JsonDocument.Parse("[]"), OverrideVersion = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
            };
            _dbContext.ReasoningAnalyses.Add(analysis);
            _dbContext.TeacherReviewHistories.Add(new()
            {
                CenterId = _centerId, AnalysisId = analysis.AnalysisId, AttemptId = attempt.AttemptId, TeacherId = _teacherId,
                Decision = TeacherReviewDecision.Adjusted, OverrideVersion = 1, CreatedAt = DateTime.UtcNow, RubricResultJson = RubricGrade.Serialize(new()
                { MaxScore = 10, AwardedScore = 10, Criteria = [new() { CriterionId = "result", Title = "Kết quả", MaxScore = 10, AwardedScore = 10 }] })
            });
            await _dbContext.SaveChangesAsync();
        }
        var result = await sut.ExecuteAsync(assignmentId, request, CancellationToken.None);

        // Assert
        Assert.Equal(TeacherApproveStatus.Success, result.Status);
        Assert.NotNull(result.Data);
        Assert.Equal(assignmentId.ToString("D"), result.Data.AssignmentId);
        Assert.Equal(studentId.ToString("D"), result.Data.StudentId);
        Assert.Equal(TeacherFinalReviewStatus.Approved.ToString(), result.Data.TeacherFinalReviewStatus);
        Assert.Equal(_teacherId.ToString("D"), result.Data.ReviewedByUserId);
        Assert.Equal("Bài làm xuất sắc và giải thích chi tiết.", result.Data.Note);
        Assert.Equal(2u, result.Data.FinalReviewVersion); // Incremented from 1 to 2

        var reloadedProgress = await _dbContext.StudentAssignmentProgresses.SingleAsync(p => p.ProgressId == progress.ProgressId);
        Assert.NotNull(reloadedProgress);
        Assert.Equal(TeacherFinalReviewStatus.Approved, reloadedProgress.TeacherFinalReviewStatus);
        Assert.Equal(_teacherId, reloadedProgress.FinalReviewedByUserId);
        Assert.Equal(2u, reloadedProgress.FinalReviewVersion);
        Assert.Equal("Bài làm xuất sắc và giải thích chi tiết.", reloadedProgress.FinalTeacherNote);
    }
}
