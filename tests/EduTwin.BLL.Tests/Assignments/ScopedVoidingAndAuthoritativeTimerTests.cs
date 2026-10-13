using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using EduTwin.BLL.AssessmentAndReasoning;
using EduTwin.BLL.AssessmentAndReasoning.PreliminaryGrading;
using EduTwin.BLL.AssessmentAndReasoning.ReviewQueue;
using EduTwin.BLL.Assignments;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Assignments;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;

namespace EduTwin.BLL.Tests.Assignments;

public sealed class ScopedVoidingAndAuthoritativeTimerTests
{
    private sealed class MutableTimeProvider : TimeProvider
    {
        public DateTime UtcNow { get; set; } = DateTime.UtcNow;

        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(UtcNow, TimeSpan.Zero);
    }

    private static (EduTwinDbContext Context, Guid CenterId, Guid TeacherId, Guid StudentId) CreateTestContext()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var tenantAccessor = new Mock<ITenantIdAccessor>();
        tenantAccessor.SetupGet(a => a.CenterId).Returns(centerId);

        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new EduTwinDbContext(options, tenantAccessor.Object);
        return (context, centerId, teacherId, studentId);
    }

    [Fact]
    public async Task ScopedVoiding_SameQuestionInTwoAssignments_OnlyAffectsTargetAssignment()
    {
        // Arrange
        var (context, centerId, teacherId, studentId) = CreateTestContext();
        await using var db = context;

        var assignmentAId = Guid.NewGuid();
        var assignmentBId = Guid.NewGuid();
        const ulong sharedQuestionId = 5001UL;
        const ulong q2Id = 5002UL;
        const ulong q3Id = 5003UL;

        // Shared question in bank
        var sharedQuestion = new Question
        {
            CenterId = centerId,
            QuestionId = sharedQuestionId,
            SubjectId = Guid.NewGuid(),
            QuestionText = "Câu hỏi dùng chung 5001",
            CorrectAnswer = "A",
            Solution = "Lời giải chi tiết",
            LanguageCode = "vi",
            MaxScore = 10,
            Status = QuestionStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var question2 = new Question
        {
            CenterId = centerId,
            QuestionId = q2Id,
            SubjectId = Guid.NewGuid(),
            QuestionText = "Câu hỏi 5002",
            CorrectAnswer = "B",
            Solution = "Lời giải chi tiết 5002",
            LanguageCode = "vi",
            MaxScore = 10,
            Status = QuestionStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var question3 = new Question
        {
            CenterId = centerId,
            QuestionId = q3Id,
            SubjectId = Guid.NewGuid(),
            QuestionText = "Câu hỏi 5003",
            CorrectAnswer = "C",
            Solution = "Lời giải chi tiết 5003",
            LanguageCode = "vi",
            MaxScore = 10,
            Status = QuestionStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var classId = Guid.NewGuid();
        db.Teachers.Add(new Teacher
        {
            CenterId = centerId,
            TeacherId = teacherId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        db.Classes.Add(new Class
        {
            CenterId = centerId,
            ClassId = classId,
            TeacherId = teacherId,
            ClassName = "Class 10A",
            AcademicYear = "2026-2027",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        db.Questions.AddRange(sharedQuestion, question2, question3);

        // Assignment A (sharedQuestion + question2)
        var assignmentA = new Assignment
        {
            CenterId = centerId,
            AssignmentId = assignmentAId,
            ClassId = classId,
            CreatedByTeacherId = teacherId,
            Title = "Bài tập A",
            Status = AssignmentStatus.Published,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var aqA1 = new AssignmentQuestion { CenterId = centerId, AssignmentId = assignmentAId, QuestionId = sharedQuestionId, OrderIndex = 1, Points = 10, CreatedAt = DateTime.UtcNow };
        var aqA2 = new AssignmentQuestion { CenterId = centerId, AssignmentId = assignmentAId, QuestionId = q2Id, OrderIndex = 2, Points = 10, CreatedAt = DateTime.UtcNow };

        // Assignment B (sharedQuestion + question3)
        var assignmentB = new Assignment
        {
            CenterId = centerId,
            AssignmentId = assignmentBId,
            ClassId = classId,
            CreatedByTeacherId = teacherId,
            Title = "Bài tập B",
            Status = AssignmentStatus.Published,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var aqB1 = new AssignmentQuestion { CenterId = centerId, AssignmentId = assignmentBId, QuestionId = sharedQuestionId, OrderIndex = 1, Points = 10, CreatedAt = DateTime.UtcNow };
        var aqB3 = new AssignmentQuestion { CenterId = centerId, AssignmentId = assignmentBId, QuestionId = q3Id, OrderIndex = 2, Points = 10, CreatedAt = DateTime.UtcNow };

        db.Assignments.AddRange(assignmentA, assignmentB);
        db.AssignmentQuestions.AddRange(aqA1, aqA2, aqB1, aqB3);

        db.AssignmentTargets.Add(new AssignmentTarget { CenterId = centerId, AssignmentId = assignmentAId, StudentId = studentId, CreatedAt = DateTime.UtcNow });
        db.AssignmentTargets.Add(new AssignmentTarget { CenterId = centerId, AssignmentId = assignmentBId, StudentId = studentId, CreatedAt = DateTime.UtcNow });

        db.StudentAssignmentProgresses.Add(new StudentAssignmentProgress { ProgressId = 101UL, CenterId = centerId, AssignmentId = assignmentAId, StudentId = studentId, Status = ProgressStatus.InProgress, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        db.StudentAssignmentProgresses.Add(new StudentAssignmentProgress { ProgressId = 102UL, CenterId = centerId, AssignmentId = assignmentBId, StudentId = studentId, Status = ProgressStatus.InProgress, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });

        await db.SaveChangesAsync();

        // Teacher tenant context
        var teacherTenant = new Mock<ITenantContext>();
        teacherTenant.SetupGet(t => t.IsResolved).Returns(true);
        teacherTenant.SetupGet(t => t.CenterId).Returns(centerId);
        teacherTenant.SetupGet(t => t.UserId).Returns(teacherId);
        teacherTenant.SetupGet(t => t.Role).Returns(nameof(UserRole.Teacher));

        var voidUseCase = new VoidAssignmentQuestionUseCase(db, teacherTenant.Object, TimeProvider.System);

        // Act: Void question 5001 ONLY in Assignment A, do NOT archive in bank
        var voidResult = await voidUseCase.ExecuteAsync(
            assignmentAId,
            sharedQuestionId,
            new VoidAssignmentQuestionRequest
            {
                VoidReason = "Đề bài câu 5001 bị lỗi dữ liệu trong bài A",
                ArchiveQuestionInBank = false
            },
            CancellationToken.None);

        Assert.True(voidResult.IsSuccess, $"Failed with: {voidResult.Status} - {voidResult.ErrorMessage}");

        // Assert 1: Assignment A's assignment_question is voided
        var updatedAqA = await db.AssignmentQuestions.SingleAsync(aq => aq.AssignmentId == assignmentAId && aq.QuestionId == sharedQuestionId);
        Assert.True(updatedAqA.IsVoided);
        Assert.Equal("Đề bài câu 5001 bị lỗi dữ liệu trong bài A", updatedAqA.VoidReason);

        // Assert 2: Assignment B's assignment_question remains NOT voided
        var updatedAqB = await db.AssignmentQuestions.SingleAsync(aq => aq.AssignmentId == assignmentBId && aq.QuestionId == sharedQuestionId);
        Assert.False(updatedAqB.IsVoided);
        Assert.Null(updatedAqB.VoidReason);

        // Assert 3: Question bank status remains Active
        var bankQuestion = await db.Questions.SingleAsync(q => q.QuestionId == sharedQuestionId);
        Assert.Equal(QuestionStatus.Active, bankQuestion.Status);

        // Assert 4: Calculator for Assignment A gives 5.0 points (10/2) for voided question to unattempted student
        var calculator = new AssignmentResultCalculator(db);
        var summaryA = await calculator.CalculateForSingleAssignmentAsync(centerId, studentId, assignmentAId, CancellationToken.None);
        Assert.Equal(1, summaryA.VoidedQuestionCount);
        Assert.Equal(5.0m, summaryA.InternalAwardedScore); // 10 / 2 questions = 5.0m
        Assert.Equal(0, summaryA.AnsweredQuestionCount);
        Assert.Equal(1, summaryA.EvaluatedQuestionCount);

        // Assert 5: Calculator for Assignment B has 0 voided questions and 0 awarded score
        var summaryB = await calculator.CalculateForSingleAssignmentAsync(centerId, studentId, assignmentBId, CancellationToken.None);
        Assert.Equal(0, summaryB.VoidedQuestionCount);
        Assert.Null(summaryB.InternalAwardedScore);
        Assert.Equal(0, summaryB.AnsweredQuestionCount);
        Assert.Equal(0, summaryB.EvaluatedQuestionCount);
    }

    [Fact]
    public async Task ScopedVoiding_UnattemptedStudent_ReceivesVoidedScore_WithoutFakeAttempt_AndReachesProvisional()
    {
        // Arrange
        var (context, centerId, teacherId, studentId) = CreateTestContext();
        await using var db = context;

        var assignmentId = Guid.NewGuid();
        const ulong q1VoidedId = 6001UL;
        const ulong q2NormalId = 6002UL;

        var q1 = new Question { CenterId = centerId, QuestionId = q1VoidedId, QuestionText = "Câu 1 lỗi", CorrectAnswer = "A", Solution = "Lời giải 1", LanguageCode = "vi", Status = QuestionStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var q2 = new Question { CenterId = centerId, QuestionId = q2NormalId, QuestionText = "Câu 2 bình thường", CorrectAnswer = "B", Solution = "Lời giải 2", LanguageCode = "vi", Status = QuestionStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.Questions.AddRange(q1, q2);

        var assignment = new Assignment
        {
            CenterId = centerId,
            AssignmentId = assignmentId,
            CreatedByTeacherId = teacherId,
            Title = "Bài kiểm tra",
            Status = AssignmentStatus.Published,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Assignments.Add(assignment);

        // q1 is voided in assignment
        var aq1 = new AssignmentQuestion { CenterId = centerId, AssignmentId = assignmentId, QuestionId = q1VoidedId, OrderIndex = 1, IsVoided = true, VoidReason = "Đề bài sai", CreatedAt = DateTime.UtcNow };
        var aq2 = new AssignmentQuestion { CenterId = centerId, AssignmentId = assignmentId, QuestionId = q2NormalId, OrderIndex = 2, IsVoided = false, CreatedAt = DateTime.UtcNow };
        db.AssignmentQuestions.AddRange(aq1, aq2);

        db.AssignmentTargets.Add(new AssignmentTarget { CenterId = centerId, AssignmentId = assignmentId, StudentId = studentId, CreatedAt = DateTime.UtcNow });
        db.StudentAssignmentProgresses.Add(new StudentAssignmentProgress { ProgressId = 201UL, CenterId = centerId, AssignmentId = assignmentId, StudentId = studentId, Status = ProgressStatus.InProgress, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });

        // Student answered ONLY question 2 correctly (Attempt for q2 only, NO attempt for q1)
        var attemptQ2 = new Attempt
        {
            CenterId = centerId,
            AttemptId = 888UL,
            AssignmentId = assignmentId,
            QuestionId = q2NormalId,
            StudentId = studentId,
            FinalAnswer = "B",
            IsCorrect = true,
            AwardedScore = 10m,
            Status = AttemptStatus.Completed,
            ReasoningLanguage = "vi",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        db.Attempts.Add(attemptQ2);

        // ReasoningAnalysis completed for q2
        db.ReasoningAnalyses.Add(new ReasoningAnalysis
        {
            CenterId = centerId,
            AnalysisId = 999UL,
            AttemptId = 888UL,
            NeedsTeacherReview = false,
            Feedback = "Chính xác",
            SchemaVersion = "1.0",
            MissingSteps = JsonDocument.Parse("[]"),
            RootCauseNodeIds = JsonDocument.Parse("[]"),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();

        // Act 1: Calculate summary
        var calculator = new AssignmentResultCalculator(db);
        var summary = await calculator.CalculateForSingleAssignmentAsync(centerId, studentId, assignmentId, CancellationToken.None);

        // Assert 1: Full score 10.0 (5.0 from q2 + 5.0 from voided q1)
        Assert.Equal(10.0m, summary.InternalAwardedScore);
        Assert.Equal(1, summary.VoidedQuestionCount);
        Assert.Equal(1, summary.AnsweredQuestionCount); // ONLY 1 question actually answered
        Assert.Equal(2, summary.EvaluatedQuestionCount); // Both questions are evaluated (q2 + voided q1)
        Assert.Equal(0, summary.PendingQuestionCount);
        Assert.Equal("Provisional", summary.ResultStatus); // Not stuck in Processing!

        // Act 2: Fetch student assignment detail via GetStudentAssignmentUseCase
        var studentTenant = new Mock<ITenantContext>();
        studentTenant.SetupGet(t => t.IsResolved).Returns(true);
        studentTenant.SetupGet(t => t.CenterId).Returns(centerId);
        studentTenant.SetupGet(t => t.UserId).Returns(studentId);
        studentTenant.SetupGet(t => t.Role).Returns(nameof(UserRole.Student));

        var getUseCase = new GetStudentAssignmentUseCase(db, studentTenant.Object, TimeProvider.System, calculator);
        var detailResult = await getUseCase.ExecuteAsync(assignmentId, CancellationToken.None);

        Assert.True(detailResult.IsSuccess);
        Assert.NotNull(detailResult.Data);

        var q1Dto = detailResult.Data.Data.Questions.Single(q => q.QuestionId == q1VoidedId.ToString());
        Assert.True(q1Dto.IsVoided);
        Assert.Equal("Đề bài sai", q1Dto.VoidReason);
        Assert.Equal(5.0m, q1Dto.VoidedScore);
        Assert.Null(q1Dto.LatestAttempt); // NO fake attempt created
        Assert.Null(q1Dto.AttemptStatus); // Voiding is represented at question level (IsVoided), NOT as an attempt status
    }

    [Fact]
    public async Task AuthoritativeTimer_StartStudentAssignment_EnforcesPreconditionsAndIdempotency()
    {
        // Arrange
        var (context, centerId, teacherId, studentId) = CreateTestContext();
        await using var db = context;

        var mutableTime = new MutableTimeProvider
        {
            UtcNow = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc)
        };

        var assignmentId = Guid.NewGuid();
        var assignment = new Assignment
        {
            CenterId = centerId,
            AssignmentId = assignmentId,
            CreatedByTeacherId = teacherId,
            Title = "Timed Quiz",
            Status = AssignmentStatus.Published,
            TimeLimitMinutes = 30,
            DueAt = mutableTime.UtcNow.AddHours(2),
            CreatedAt = mutableTime.UtcNow,
            UpdatedAt = mutableTime.UtcNow
        };
        db.Assignments.Add(assignment);

        db.AssignmentTargets.Add(new AssignmentTarget
        {
            CenterId = centerId,
            AssignmentId = assignmentId,
            StudentId = studentId,
            CreatedAt = mutableTime.UtcNow
        });

        var progress = new StudentAssignmentProgress
        {
            ProgressId = 401UL,
            CenterId = centerId,
            AssignmentId = assignmentId,
            StudentId = studentId,
            Status = ProgressStatus.NotStarted,
            StartedAt = null,
            CreatedAt = mutableTime.UtcNow,
            UpdatedAt = mutableTime.UtcNow
        };
        db.StudentAssignmentProgresses.Add(progress);
        await db.SaveChangesAsync();

        var studentTenant = new Mock<ITenantContext>();
        studentTenant.SetupGet(t => t.IsResolved).Returns(true);
        studentTenant.SetupGet(t => t.CenterId).Returns(centerId);
        studentTenant.SetupGet(t => t.UserId).Returns(studentId);
        studentTenant.SetupGet(t => t.Role).Returns(nameof(UserRole.Student));

        var getUseCase = new GetStudentAssignmentUseCase(db, studentTenant.Object, mutableTime, new AssignmentResultCalculator(db));
        var startUseCase = new StartStudentAssignmentUseCase(db, studentTenant.Object, getUseCase, mutableTime);

        // Act 1: Initial start sets StartedAt to current time
        var startResult = await startUseCase.ExecuteAsync(assignmentId, CancellationToken.None);
        Assert.True(startResult.IsSuccess);

        var updatedProgress = await db.StudentAssignmentProgresses.SingleAsync(p => p.AssignmentId == assignmentId && p.StudentId == studentId);
        Assert.NotNull(updatedProgress.StartedAt);
        Assert.Equal(mutableTime.UtcNow, updatedProgress.StartedAt.Value);
        Assert.Equal(ProgressStatus.InProgress, updatedProgress.Status);

        var originalStartedAt = updatedProgress.StartedAt.Value;

        // Act 2: Advance clock by 10 minutes and call start again (idempotent: StartedAt must NOT change)
        mutableTime.UtcNow = mutableTime.UtcNow.AddMinutes(10);
        var secondStartResult = await startUseCase.ExecuteAsync(assignmentId, CancellationToken.None);
        Assert.True(secondStartResult.IsSuccess);

        var refreshedProgress = await db.StudentAssignmentProgresses.SingleAsync(p => p.AssignmentId == assignmentId && p.StudentId == studentId);
        Assert.Equal(originalStartedAt, refreshedProgress.StartedAt!.Value); // Clock NOT reset!

        // Act 3: Advance clock past 30-minute limit (e.g., 35 minutes after start) -> Start returns AssignmentNotAvailable
        mutableTime.UtcNow = originalStartedAt.AddMinutes(35);
        var expiredStartResult = await startUseCase.ExecuteAsync(assignmentId, CancellationToken.None);
        Assert.False(expiredStartResult.IsSuccess);
        Assert.Equal(ErrorCodes.AssignmentNotAvailable, expiredStartResult.ErrorCode);
    }

    [Fact]
    public async Task AuthoritativeTimer_AttemptSubmissionValidator_EnforcesZeroGracePeriod()
    {
        // Arrange
        var (context, centerId, teacherId, studentId) = CreateTestContext();
        await using var db = context;

        var mutableTime = new MutableTimeProvider
        {
            UtcNow = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc)
        };

        var assignmentId = Guid.NewGuid();
        const ulong questionId = 7001UL;

        // Seed student user and student record
        db.Users.Add(new User
        {
            CenterId = centerId,
            UserId = studentId,
            RoleName = UserRole.Student,
            DisplayName = "Test Student",
            Username = "student1",
            PasswordHash = "test-hash",
            CreatedAt = mutableTime.UtcNow,
            UpdatedAt = mutableTime.UtcNow
        });

        db.Students.Add(new Student
        {
            CenterId = centerId,
            StudentId = studentId,
            FullName = "Test Student",
            CreatedAt = mutableTime.UtcNow,
            UpdatedAt = mutableTime.UtcNow
        });

        db.Questions.Add(new Question
        {
            CenterId = centerId,
            QuestionId = questionId,
            QuestionType = QuestionType.ShortAnswer,
            CorrectAnswer = "42",
            Solution = "42",
            QuestionText = "Ý nghĩa cuộc sống?",
            LanguageCode = "vi",
            MaxScore = 10,
            Status = QuestionStatus.Active,
            CreatedAt = mutableTime.UtcNow,
            UpdatedAt = mutableTime.UtcNow
        });

        db.Assignments.Add(new Assignment
        {
            CenterId = centerId,
            AssignmentId = assignmentId,
            CreatedByTeacherId = teacherId,
            Title = "20-minute Quiz",
            Status = AssignmentStatus.Published,
            TimeLimitMinutes = 20,
            CreatedAt = mutableTime.UtcNow,
            UpdatedAt = mutableTime.UtcNow
        });

        db.AssignmentQuestions.Add(new AssignmentQuestion
        {
            CenterId = centerId,
            AssignmentId = assignmentId,
            QuestionId = questionId,
            OrderIndex = 1,
            Points = 10,
            CreatedAt = mutableTime.UtcNow
        });

        db.AssignmentTargets.Add(new AssignmentTarget
        {
            CenterId = centerId,
            AssignmentId = assignmentId,
            StudentId = studentId,
            CreatedAt = mutableTime.UtcNow
        });

        // StartedAt is 8:00:00
        var startedAt = mutableTime.UtcNow;
        db.StudentAssignmentProgresses.Add(new StudentAssignmentProgress
        {
            ProgressId = 301UL,
            CenterId = centerId,
            AssignmentId = assignmentId,
            StudentId = studentId,
            Status = ProgressStatus.InProgress,
            StartedAt = startedAt,
            CreatedAt = mutableTime.UtcNow,
            UpdatedAt = mutableTime.UtcNow
        });

        await db.SaveChangesAsync();

        var studentTenant = new Mock<ITenantContext>();
        studentTenant.SetupGet(t => t.IsResolved).Returns(true);
        studentTenant.SetupGet(t => t.CenterId).Returns(centerId);
        studentTenant.SetupGet(t => t.UserId).Returns(studentId);
        studentTenant.SetupGet(t => t.Role).Returns(nameof(UserRole.Student));

        var graderFactory = new PreliminaryGraderFactory(new MultipleChoiceGrader(), new ShortAnswerGrader(), new EssayGrader());
        var validator = new AttemptSubmissionValidator(db, studentTenant.Object, graderFactory, mutableTime);

        // Test 1: Submitting at 8:19:00 (19 minutes in, within 20m) -> Succeeded
        mutableTime.UtcNow = startedAt.AddMinutes(19);
        var validResult = await validator.ValidateAsync(new SubmitAttemptRequest
        {
            QuestionId = questionId.ToString(),
            AssignmentId = assignmentId,
            FinalAnswer = "42",
            ClientSubmissionId = Guid.NewGuid(),
            TimeSpentSeconds = 60,
            Confidence = 90
        });
        Assert.True(validResult.IsSuccess);

        // Test 2: Submitting at 8:20:01 (20 minutes + 1 second) -> Rejected with ZERO grace period!
        mutableTime.UtcNow = startedAt.AddMinutes(20).AddSeconds(1);
        var expiredResult = await validator.ValidateAsync(new SubmitAttemptRequest
        {
            QuestionId = questionId.ToString(),
            AssignmentId = assignmentId,
            FinalAnswer = "42",
            ClientSubmissionId = Guid.NewGuid(),
            TimeSpentSeconds = 60,
            Confidence = 90
        });
        Assert.False(expiredResult.IsSuccess);
        Assert.Equal(ErrorCodes.AssignmentNotAvailable, expiredResult.ErrorCode);
    }

    [Fact]
    public async Task AuthoritativeTimer_StartStudentAssignment_ConcurrentRequests_BothSucceedAndShareStartedAt()
    {
        // Arrange
        var (context, centerId, teacherId, studentId) = CreateTestContext();
        await using var db = context;

        var assignmentId = Guid.NewGuid();
        var assignment = new Assignment
        {
            AssignmentId = assignmentId,
            CenterId = centerId,
            Title = "Timed Quiz",
            Status = AssignmentStatus.Published,
            TimeLimitMinutes = 30,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RowVersion = 1
        };
        db.Assignments.Add(assignment);

        var target = new AssignmentTarget
        {
            CenterId = centerId,
            AssignmentId = assignmentId,
            StudentId = studentId,
            CreatedAt = DateTime.UtcNow
        };
        db.AssignmentTargets.Add(target);

        var progress = new StudentAssignmentProgress
        {
            ProgressId = 999123,
            CenterId = centerId,
            AssignmentId = assignmentId,
            StudentId = studentId,
            Status = ProgressStatus.NotStarted,
            StartedAt = null,
            TotalQuestionCount = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RowVersion = 1
        };
        db.StudentAssignmentProgresses.Add(progress);
        await db.SaveChangesAsync();

        var studentTenant = new Mock<ITenantContext>();
        studentTenant.SetupGet(t => t.IsResolved).Returns(true);
        studentTenant.SetupGet(t => t.CenterId).Returns(centerId);
        studentTenant.SetupGet(t => t.UserId).Returns(studentId);
        studentTenant.SetupGet(t => t.Role).Returns(nameof(UserRole.Student));

        var calculator = new AssignmentResultCalculator(db);
        var getUseCase = new GetStudentAssignmentUseCase(db, studentTenant.Object, TimeProvider.System, calculator);
        var startUseCase = new StartStudentAssignmentUseCase(db, studentTenant.Object, getUseCase, TimeProvider.System);

        // Act: Execute two start calls concurrently
        var task1 = startUseCase.ExecuteAsync(assignmentId, CancellationToken.None);
        var task2 = startUseCase.ExecuteAsync(assignmentId, CancellationToken.None);

        var results = await Task.WhenAll(task1, task2);

        // Assert: Both succeed and both see identical StartedAt
        Assert.True(results[0].IsSuccess);
        Assert.True(results[1].IsSuccess);
        Assert.NotNull(results[0].Data);
        Assert.NotNull(results[1].Data);

        var startedAt1 = results[0].Data!.Data.StartedAt;
        var startedAt2 = results[1].Data!.Data.StartedAt;
        Assert.NotNull(startedAt1);
        Assert.NotNull(startedAt2);
        Assert.Equal(startedAt1, startedAt2);
    }

    [Fact]
    public async Task ApproveAssignmentResult_UnattemptedVoidedQuestion_DoesNotBlockTeacherApproval()
    {
        // Arrange
        var (context, centerId, teacherId, studentId) = CreateTestContext();
        await using var db = context;

        var classId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();

        var classObj = new Class
        {
            ClassId = classId,
            CenterId = centerId,
            ClassName = "12A1",
            TeacherId = teacherId,
            GradeLevel = 12,
            AcademicYear = "2026-2027",
            Status = ClassStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RowVersion = 1
        };
        db.Classes.Add(classObj);

        var assignment = new Assignment
        {
            AssignmentId = assignmentId,
            CenterId = centerId,
            ClassId = classId,
            Class = classObj,
            Title = "Math Exam",
            Status = AssignmentStatus.Published,
            CreatedByTeacherId = teacherId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RowVersion = 1
        };
        db.Assignments.Add(assignment);

        var q1 = new Question
        {
            QuestionId = 101,
            CenterId = centerId,
            SubjectId = Guid.NewGuid(),
            QuestionText = "Question 1 (Normal)",
            QuestionType = QuestionType.MultipleChoice,
            CorrectAnswer = "A",
            Solution = "Solution 1",
            MaxScore = 10,
            LanguageCode = "vi",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RowVersion = 1
        };
        var q2 = new Question
        {
            QuestionId = 102,
            CenterId = centerId,
            SubjectId = Guid.NewGuid(),
            QuestionText = "Question 2 (Voided)",
            QuestionType = QuestionType.MultipleChoice,
            CorrectAnswer = "B",
            Solution = "Solution 2",
            MaxScore = 10,
            LanguageCode = "vi",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RowVersion = 1
        };
        db.Questions.AddRange(q1, q2);

        var aq1 = new AssignmentQuestion
        {
            CenterId = centerId,
            AssignmentId = assignmentId,
            QuestionId = 101,
            IsVoided = false,
            Points = 10,
            CreatedAt = DateTime.UtcNow
        };
        var aq2 = new AssignmentQuestion
        {
            CenterId = centerId,
            AssignmentId = assignmentId,
            QuestionId = 102,
            IsVoided = true,
            VoidReason = "Đề bài có lỗi",
            VoidedAt = DateTime.UtcNow,
            VoidedByUserId = teacherId,
            Points = 10,
            CreatedAt = DateTime.UtcNow
        };
        db.AssignmentQuestions.AddRange(aq1, aq2);

        // Student answered Q1 only; Q2 was voided so student never attempted it
        var attempt1 = new Attempt
        {
            AttemptId = 301,
            CenterId = centerId,
            StudentId = studentId,
            AssignmentId = assignmentId,
            QuestionId = 101,
            FinalAnswer = "A",
            Status = AttemptStatus.Completed,
            IsCorrect = true,
            AwardedScore = 10m,
            ReasoningLanguage = "vi",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RowVersion = 1
        };
        db.Attempts.Add(attempt1);

        var progress = new StudentAssignmentProgress
        {
            ProgressId = 401,
            CenterId = centerId,
            AssignmentId = assignmentId,
            StudentId = studentId,
            Status = ProgressStatus.InProgress,
            CompletedQuestionCount = 1, // Only 1 actually answered
            TotalQuestionCount = 2,
            TeacherFinalReviewStatus = TeacherFinalReviewStatus.Pending,
            FinalReviewVersion = 0,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            RowVersion = 1
        };
        db.StudentAssignmentProgresses.Add(progress);
        await db.SaveChangesAsync();

        var teacherTenant = new Mock<ITenantContext>();
        teacherTenant.SetupGet(t => t.IsResolved).Returns(true);
        teacherTenant.SetupGet(t => t.CenterId).Returns(centerId);
        teacherTenant.SetupGet(t => t.UserId).Returns(teacherId);
        teacherTenant.SetupGet(t => t.Role).Returns(nameof(UserRole.Teacher));

        var approveUseCase = new ApproveAssignmentResultUseCase(db, teacherTenant.Object, TimeProvider.System);

        // Act
        var result = await approveUseCase.ExecuteAsync(assignmentId, new ApproveAssignmentResultRequest
        {
            StudentId = studentId,
            FinalReviewVersion = 0,
            Note = "Duyệt kết quả."
        }, CancellationToken.None);

        // Assert: Approval succeeds because unattempted voided question counts towards completion!
        Assert.True(result.IsSuccess, $"Approval failed: {result.ErrorCode} - {result.ErrorMessage}");
        Assert.NotNull(result.Data);
        Assert.Equal("Approved", result.Data.TeacherFinalReviewStatus);

        var updatedProgress = await db.StudentAssignmentProgresses.SingleAsync(p => p.ProgressId == 401);
        Assert.Equal(TeacherFinalReviewStatus.Approved, updatedProgress.TeacherFinalReviewStatus);
        Assert.Equal(1u, updatedProgress.CompletedQuestionCount); // Actually answered count preserved!
    }

    [Fact]
    public async Task SaveDraftAndSubmitStudentAssignment_UnifiedBatch_EnforcesStrictAuthoritativeTiming()
    {
        // Arrange
        var (context, centerId, teacherId, studentId) = CreateTestContext();
        await using var db = context;

        var assignmentId = Guid.NewGuid();
        var startedAt = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);
        var mutableTime = new MutableTimeProvider { UtcNow = startedAt.AddMinutes(5) };

        var assignment = new Assignment
        {
            AssignmentId = assignmentId,
            CenterId = centerId,
            Title = "Batch Quiz",
            Status = AssignmentStatus.Published,
            TimeLimitMinutes = 15,
            CreatedAt = startedAt,
            UpdatedAt = startedAt,
            RowVersion = 1
        };
        db.Assignments.Add(assignment);

        db.AssignmentTargets.Add(new AssignmentTarget
        {
            CenterId = centerId,
            AssignmentId = assignmentId,
            StudentId = studentId,
            CreatedAt = startedAt
        });

        var q1 = new Question
        {
            QuestionId = 501,
            CenterId = centerId,
            SubjectId = Guid.NewGuid(),
            QuestionText = "1 + 1 = ?",
            QuestionType = QuestionType.MultipleChoice,
            CorrectAnswer = "A",
            Solution = "1+1=2",
            MaxScore = 10,
            LanguageCode = "vi",
            CreatedAt = startedAt,
            UpdatedAt = startedAt,
            RowVersion = 1
        };
        db.Questions.Add(q1);

        var opt = new QuestionOption
        {
            OptionId = 601,
            CenterId = centerId,
            QuestionId = 501,
            OptionLabel = "A",
            OptionText = "2",
            IsCorrect = true,
            OrderIndex = 1,
            CreatedAt = startedAt,
            UpdatedAt = startedAt
        };
        db.QuestionOptions.Add(opt);

        db.AssignmentQuestions.Add(new AssignmentQuestion
        {
            CenterId = centerId,
            AssignmentId = assignmentId,
            QuestionId = 501,
            Points = 10,
            CreatedAt = startedAt
        });

        var progress = new StudentAssignmentProgress
        {
            ProgressId = 801,
            CenterId = centerId,
            AssignmentId = assignmentId,
            StudentId = studentId,
            Status = ProgressStatus.InProgress,
            StartedAt = startedAt,
            TotalQuestionCount = 1,
            CreatedAt = startedAt,
            UpdatedAt = startedAt,
            RowVersion = 1
        };
        db.StudentAssignmentProgresses.Add(progress);
        await db.SaveChangesAsync();

        var studentTenant = new Mock<ITenantContext>();
        studentTenant.SetupGet(t => t.IsResolved).Returns(true);
        studentTenant.SetupGet(t => t.CenterId).Returns(centerId);
        studentTenant.SetupGet(t => t.UserId).Returns(studentId);
        studentTenant.SetupGet(t => t.Role).Returns(nameof(UserRole.Student));

        var draftUseCase = new SaveAssignmentDraftUseCase(db, studentTenant.Object, mutableTime);
        var submitUseCase = new SubmitStudentAssignmentUseCase(db, studentTenant.Object, mutableTime);

        // 1. Save draft at 8:05:00 (within 15m limit)
        var draftResult = await draftUseCase.ExecuteAsync(assignmentId, new SaveAssignmentDraftRequest
        {
            Answers = new List<AssignmentDraftAnswerItemDto>
            {
                new() { QuestionId = 501, FinalAnswer = "A", Confidence = 95, TimeSpentSeconds = 45 }
            }
        });
        Assert.True(draftResult.IsSuccess);

        // Verify draft answers saved on server
        var reloadedProgress = await db.StudentAssignmentProgresses.SingleAsync(p => p.ProgressId == 801);
        Assert.NotNull(reloadedProgress.DraftAnswersJson);
        Assert.NotNull(reloadedProgress.DraftSavedAt);

        // 2. Submit unified batch at 8:15:02 (timer expired on client):
        // Auto-submit triggers without payload -> Uses pre-saved draft saved before deadline!
        mutableTime.UtcNow = startedAt.AddMinutes(15).AddSeconds(2);
        var submitResult = await submitUseCase.ExecuteAsync(assignmentId, new SubmitAssignmentRequest());

        Assert.True(submitResult.IsSuccess);
        Assert.NotNull(submitResult.Data);
        Assert.True(submitResult.Data.IsCompleted);

        var finalProgress = await db.StudentAssignmentProgresses.SingleAsync(p => p.ProgressId == 801);
        Assert.Equal(ProgressStatus.Completed, finalProgress.Status);
        Assert.Equal(1u, finalProgress.CompletedQuestionCount);

        var createdAttempt = await db.Attempts.SingleAsync(a => a.AssignmentId == assignmentId && a.QuestionId == 501);
        Assert.Equal("A", createdAttempt.FinalAnswer);
        Assert.True(createdAttempt.IsCorrect);
        Assert.Equal(10m, createdAttempt.AwardedScore);
    }
}
