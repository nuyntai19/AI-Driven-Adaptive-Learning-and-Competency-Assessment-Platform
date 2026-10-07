using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using MySql.Data.MySqlClient;
using Xunit;
using EduTwin.BLL.AssessmentAndReasoning;
using EduTwin.BLL.AssessmentAndReasoning.Attachments;
using EduTwin.BLL.AssessmentAndReasoning.PreliminaryGrading;
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
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;

namespace EduTwin.BLL.Tests.Assignments;

[Collection("MySqlDatabase")]
public sealed class AssignmentSubmissionAndConcurrencyTests
{
    private const string AdminConnectionVariable = "EDUTWIN_TEST_MYSQL_ADMIN_CONNECTION_STRING";

    private sealed class MutableTimeProvider : TimeProvider
    {
        public DateTime UtcNow { get; set; } = DateTime.UtcNow;

        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(UtcNow, TimeSpan.Zero);
    }

    private static (EduTwinDbContext Context, Guid CenterId, Guid StudentId) CreateTestContext()
    {
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var tenantAccessor = new Mock<ITenantIdAccessor>();
        tenantAccessor.SetupGet(a => a.CenterId).Returns(centerId);

        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var context = new EduTwinDbContext(options, tenantAccessor.Object);
        return (context, centerId, studentId);
    }

    private static Mock<ITenantContext> CreateStudentTenant(Guid centerId, Guid studentId)
    {
        var studentTenant = new Mock<ITenantContext>();
        studentTenant.SetupGet(t => t.IsResolved).Returns(true);
        studentTenant.SetupGet(t => t.CenterId).Returns(centerId);
        studentTenant.SetupGet(t => t.UserId).Returns(studentId);
        studentTenant.SetupGet(t => t.Role).Returns(nameof(UserRole.Student));
        return studentTenant;
    }

    [Fact]
    public async Task SubmitStudentAssignment_McqOptionId_AndNumericRational_AndReasoningRequired_EvaluatedCorrectly()
    {
        var (db, centerId, studentId) = CreateTestContext();
        await using var _ = db;

        var now = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);
        var mutableTime = new MutableTimeProvider { UtcNow = now };
        var assignmentId = Guid.NewGuid();

        // 1. Assignment
        db.Assignments.Add(new Assignment
        {
            AssignmentId = assignmentId,
            CenterId = centerId,
            Title = "Grading & Validation Test Assignment",
            Status = AssignmentStatus.Published,
            TimeLimitMinutes = 30,
            CreatedAt = now,
            UpdatedAt = now,
            RowVersion = 1
        });

        db.AssignmentTargets.Add(new AssignmentTarget
        {
            CenterId = centerId,
            AssignmentId = assignmentId,
            StudentId = studentId,
            CreatedAt = now
        });

        // 2. Q1: MultipleChoice with OptionId 7001
        const ulong q1Id = 1001;
        const ulong opt1Id = 7001;
        db.Questions.Add(new Question
        {
            QuestionId = q1Id,
            CenterId = centerId,
            SubjectId = Guid.NewGuid(),
            QuestionType = QuestionType.MultipleChoice,
            QuestionText = "Câu trắc nghiệm 1001",
            CorrectAnswer = "A",
            MaxScore = 10m,
            LanguageCode = "vi",
            Solution = "Lời giải chi tiết câu trắc nghiệm",
            CreatedAt = now,
            UpdatedAt = now
        });
        db.QuestionOptions.Add(new QuestionOption
        {
            OptionId = opt1Id,
            QuestionId = q1Id,
            CenterId = centerId,
            OptionLabel = "A",
            OptionText = "Đáp án đúng A",
            IsCorrect = true,
            OrderIndex = 1,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.AssignmentQuestions.Add(new AssignmentQuestion
        {
            CenterId = centerId,
            AssignmentId = assignmentId,
            QuestionId = q1Id,
            Points = 10m,
            CreatedAt = now
        });

        // 3. Q2: ShortAnswer with NumericRational evaluation
        const ulong q2Id = 1002;
        db.Questions.Add(new Question
        {
            QuestionId = q2Id,
            CenterId = centerId,
            SubjectId = Guid.NewGuid(),
            QuestionType = QuestionType.ShortAnswer,
            AnswerEvaluationMode = QuestionAnswerEvaluationMode.NumericRational,
            QuestionText = "Rút gọn phân số 4/8",
            CorrectAnswer = "1/2",
            MaxScore = 10m,
            LanguageCode = "vi",
            Solution = "4/8 = 1/2",
            CreatedAt = now,
            UpdatedAt = now
        });
        db.AssignmentQuestions.Add(new AssignmentQuestion
        {
            CenterId = centerId,
            AssignmentId = assignmentId,
            QuestionId = q2Id,
            Points = 10m,
            CreatedAt = now
        });

        // 4. Q3: ReasoningRequired question
        const ulong q3Id = 1003;
        db.Questions.Add(new Question
        {
            QuestionId = q3Id,
            CenterId = centerId,
            SubjectId = Guid.NewGuid(),
            QuestionType = QuestionType.ShortAnswer,
            AnswerEvaluationMode = QuestionAnswerEvaluationMode.TextExact,
            QuestionText = "Câu hỏi yêu cầu lập luận",
            CorrectAnswer = "100",
            MaxScore = 10m,
            ReasoningRequired = true,
            LanguageCode = "vi",
            Solution = "Lời giải chi tiết yêu cầu lập luận",
            CreatedAt = now,
            UpdatedAt = now
        });
        db.AssignmentQuestions.Add(new AssignmentQuestion
        {
            CenterId = centerId,
            AssignmentId = assignmentId,
            QuestionId = q3Id,
            Points = 10m,
            CreatedAt = now
        });

        // 5. Progress started
        db.StudentAssignmentProgresses.Add(new StudentAssignmentProgress
        {
            ProgressId = 901,
            CenterId = centerId,
            AssignmentId = assignmentId,
            StudentId = studentId,
            Status = ProgressStatus.InProgress,
            StartedAt = now,
            TotalQuestionCount = 3,
            CreatedAt = now,
            UpdatedAt = now,
            RowVersion = 1
        });
        await db.SaveChangesAsync();

        var studentTenant = CreateStudentTenant(centerId, studentId);
        var submitUseCase = new SubmitStudentAssignmentUseCase(db, studentTenant.Object, mutableTime);

        // Scenario A: Q3 requires reasoning, student answers without reasoning -> rejected with QuestionReasoningRequired
        var invalidReq = new SubmitAssignmentRequest
        {
            Answers = new List<AssignmentDraftAnswerItemDto>
            {
                new() { QuestionId = (long)q1Id, FinalAnswer = "7001" },
                new() { QuestionId = (long)q2Id, FinalAnswer = "2/4" },
                new() { QuestionId = (long)q3Id, FinalAnswer = "100", ReasoningText = "" } // Missing reasoning!
            }
        };
        var rejectedRes = await submitUseCase.ExecuteAsync(assignmentId, invalidReq);
        Assert.False(rejectedRes.IsSuccess);
        Assert.Equal(ErrorCodes.QuestionReasoningRequired, rejectedRes.ErrorCode);

        // Scenario B: Provide reasoning -> submission succeeds and preliminary grading evaluates all types accurately
        var validReq = new SubmitAssignmentRequest
        {
            Answers = new List<AssignmentDraftAnswerItemDto>
            {
                new() { QuestionId = (long)q1Id, FinalAnswer = "7001" }, // Selected by OptionId!
                new() { QuestionId = (long)q2Id, FinalAnswer = "2/4" },  // Numeric equivalent to 1/2!
                new() { QuestionId = (long)q3Id, FinalAnswer = "100", ReasoningText = "Giải thích chi tiết các bước tính" }
            }
        };
        var successRes = await submitUseCase.ExecuteAsync(assignmentId, validReq);
        Assert.True(successRes.IsSuccess);
        Assert.Equal(3, successRes.Data!.SubmittedAttemptsCount);
        Assert.True(successRes.Data.IsCompleted);

        // Verify Q1 MCQ: OptionId 7001 scored 10 points and IsCorrect = true!
        var attempt1 = await db.Attempts.SingleAsync(a => a.AssignmentId == assignmentId && a.QuestionId == q1Id);
        Assert.True(attempt1.IsCorrect);
        Assert.Equal(10m, attempt1.AwardedScore);
        Assert.Equal(PreliminaryGradingReasonCodes.ExactMatch, attempt1.PreliminaryGradingReasonCode);

        // Verify Q2 NumericRational: 2/4 recognized as equivalent to 1/2, scored 10 points!
        var attempt2 = await db.Attempts.SingleAsync(a => a.AssignmentId == assignmentId && a.QuestionId == q2Id);
        Assert.True(attempt2.IsCorrect);
        Assert.Equal(10m, attempt2.AwardedScore);
        Assert.Equal(PreliminaryGradingReasonCodes.NumericEquivalent, attempt2.PreliminaryGradingReasonCode);

        // Verify Q3: Reasoning saved and scored 10 points
        var attempt3 = await db.Attempts.SingleAsync(a => a.AssignmentId == assignmentId && a.QuestionId == q3Id);
        Assert.True(attempt3.IsCorrect);
        Assert.Equal(10m, attempt3.AwardedScore);
        Assert.Equal("Giải thích chi tiết các bước tính", attempt3.ReasoningText);
    }

    [Fact]
    public async Task SubmitStudentAssignment_AttachmentToken_PromotedAndPersisted()
    {
        var (db, centerId, studentId) = CreateTestContext();
        await using var _ = db;

        var now = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);
        var mutableTime = new MutableTimeProvider { UtcNow = now };
        var assignmentId = Guid.NewGuid();
        const ulong qId = 2001;

        db.Assignments.Add(new Assignment
        {
            AssignmentId = assignmentId,
            CenterId = centerId,
            Title = "Attachment Test Assignment",
            Status = AssignmentStatus.Published,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.AssignmentTargets.Add(new AssignmentTarget
        {
            CenterId = centerId,
            AssignmentId = assignmentId,
            StudentId = studentId,
            CreatedAt = now
        });
        db.Questions.Add(new Question
        {
            QuestionId = qId,
            CenterId = centerId,
            SubjectId = Guid.NewGuid(),
            QuestionType = QuestionType.ShortAnswer,
            QuestionText = "Question 2001",
            CorrectAnswer = "42",
            MaxScore = 10,
            LanguageCode = "vi",
            Solution = "Lời giải câu 2001",
            CreatedAt = now,
            UpdatedAt = now
        });
        db.AssignmentQuestions.Add(new AssignmentQuestion
        {
            CenterId = centerId,
            AssignmentId = assignmentId,
            QuestionId = qId,
            Points = 10,
            CreatedAt = now
        });
        db.StudentAssignmentProgresses.Add(new StudentAssignmentProgress
        {
            ProgressId = 902,
            CenterId = centerId,
            AssignmentId = assignmentId,
            StudentId = studentId,
            Status = ProgressStatus.InProgress,
            StartedAt = now,
            TotalQuestionCount = 1,
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();

        const string token = "valid-upload-token-abc";
        var payload = new AttachmentUploadTokenPayload(
            centerId,
            studentId,
            Guid.NewGuid().ToString("N"),
            new string('a', 64),
            "scratchpad.png",
            1024,
            now.AddHours(1)
        );

        var mockTokens = new Mock<IAttemptAttachmentTokenService>();
        mockTokens.Setup(t => t.TryRead(token, out payload)).Returns(true);

        var mockStorage = new Mock<IAttemptAttachmentStorage>();
        mockStorage
            .Setup(s => s.PromoteToPermanentAsync(payload, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PromotedAttemptAttachment("permanent-storage-key-123", true));

        var studentTenant = CreateStudentTenant(centerId, studentId);
        var submitUseCase = new SubmitStudentAssignmentUseCase(
            db,
            studentTenant.Object,
            mutableTime,
            graderFactory: null,
            attachmentTokens: mockTokens.Object,
            attachmentStorage: mockStorage.Object);

        var req = new SubmitAssignmentRequest
        {
            Answers = new List<AssignmentDraftAnswerItemDto>
            {
                new()
                {
                    QuestionId = (long)qId,
                    FinalAnswer = "42",
                    DrawingUploadToken = token
                }
            }
        };

        var result = await submitUseCase.ExecuteAsync(assignmentId, req);
        Assert.True(result.IsSuccess);

        // Verify AttemptAttachment was created in the database
        var attachment = await db.AttemptAttachments.SingleOrDefaultAsync(a => a.CenterId == centerId);
        Assert.NotNull(attachment);
        Assert.Equal("permanent-storage-key-123", attachment.StorageKey);
        Assert.Equal(payload.UploadNonce, attachment.UploadNonce);
        Assert.Equal("image/png", attachment.ContentType);
    }

    [Fact]
    public async Task SubmitStudentAssignment_NotStarted_OrPastDue_RejectsNewAnswers()
    {
        var (db, centerId, studentId) = CreateTestContext();
        await using var _ = db;

        var now = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);
        var mutableTime = new MutableTimeProvider { UtcNow = now };
        var assignmentId = Guid.NewGuid();
        const ulong qId = 3001;

        db.Assignments.Add(new Assignment
        {
            AssignmentId = assignmentId,
            CenterId = centerId,
            Title = "Authoritative Timer Test",
            Status = AssignmentStatus.Published,
            TimeLimitMinutes = 30,
            DueAt = now.AddMinutes(60),
            CreatedAt = now,
            UpdatedAt = now
        });
        db.AssignmentTargets.Add(new AssignmentTarget
        {
            CenterId = centerId,
            AssignmentId = assignmentId,
            StudentId = studentId,
            CreatedAt = now
        });
        db.Questions.Add(new Question
        {
            QuestionId = qId,
            CenterId = centerId,
            SubjectId = Guid.NewGuid(),
            QuestionType = QuestionType.ShortAnswer,
            QuestionText = "Question 3001",
            CorrectAnswer = "Ans",
            MaxScore = 10,
            LanguageCode = "vi",
            Solution = "Lời giải câu 3001",
            CreatedAt = now,
            UpdatedAt = now
        });
        db.AssignmentQuestions.Add(new AssignmentQuestion
        {
            CenterId = centerId,
            AssignmentId = assignmentId,
            QuestionId = qId,
            Points = 10,
            CreatedAt = now
        });

        // Progress has StartedAt == null (Student has NOT started yet!)
        var progress = new StudentAssignmentProgress
        {
            ProgressId = 903,
            CenterId = centerId,
            AssignmentId = assignmentId,
            StudentId = studentId,
            Status = ProgressStatus.NotStarted,
            StartedAt = null,
            TotalQuestionCount = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.StudentAssignmentProgresses.Add(progress);
        await db.SaveChangesAsync();

        var studentTenant = CreateStudentTenant(centerId, studentId);
        var draftUseCase = new SaveAssignmentDraftUseCase(db, studentTenant.Object, mutableTime);
        var submitUseCase = new SubmitStudentAssignmentUseCase(db, studentTenant.Object, mutableTime);

        // 1. Save draft when StartedAt == null must be REJECTED and must NOT self-record StartedAt
        var draftResult = await draftUseCase.ExecuteAsync(assignmentId, new SaveAssignmentDraftRequest
        {
            Answers = new List<AssignmentDraftAnswerItemDto>
            {
                new() { QuestionId = (long)qId, FinalAnswer = "Draft" }
            }
        });
        Assert.False(draftResult.IsSuccess);
        Assert.Equal(ErrorCodes.AssignmentNotAvailable, draftResult.ErrorCode);

        var reloaded = await db.StudentAssignmentProgresses.AsNoTracking().SingleAsync(p => p.ProgressId == 903);
        Assert.Null(reloaded.StartedAt); // Must remain null!

        // 2. Submit new answers when StartedAt == null must be REJECTED
        var submitResult = await submitUseCase.ExecuteAsync(assignmentId, new SubmitAssignmentRequest
        {
            Answers = new List<AssignmentDraftAnswerItemDto>
            {
                new() { QuestionId = (long)qId, FinalAnswer = "Ans" }
            }
        });
        Assert.False(submitResult.IsSuccess);
        Assert.Equal(ErrorCodes.AssignmentNotAvailable, submitResult.ErrorCode);

        // 3. Now start the assignment properly at 8:00
        progress.StartedAt = now;
        progress.Status = ProgressStatus.InProgress;
        await db.SaveChangesAsync();

        // 4. Save draft at 8:10 (within 30m limit)
        mutableTime.UtcNow = now.AddMinutes(10);
        var draftSaveOk = await draftUseCase.ExecuteAsync(assignmentId, new SaveAssignmentDraftRequest
        {
            Answers = new List<AssignmentDraftAnswerItemDto>
            {
                new() { QuestionId = (long)qId, FinalAnswer = "Ans" }
            }
        });
        Assert.True(draftSaveOk.IsSuccess);

        // 5. Try submitting new answers after 30m limit (at 8:35):
        // New client answers past deadline must be rejected from direct submission, but pre-saved draft is finalized!
        mutableTime.UtcNow = now.AddMinutes(35);
        var expiredClientReq = new SubmitAssignmentRequest
        {
            Answers = new List<AssignmentDraftAnswerItemDto>
            {
                new() { QuestionId = (long)qId, FinalAnswer = "NewAnswerAfterDeadline" }
            }
        };
        var finalizeRes = await submitUseCase.ExecuteAsync(assignmentId, expiredClientReq);
        Assert.True(finalizeRes.IsSuccess);

        // The finalized answer must be "Ans" from the pre-saved draft, NOT "NewAnswerAfterDeadline"!
        var attempt = await db.Attempts.SingleAsync(a => a.AssignmentId == assignmentId && a.QuestionId == qId);
        Assert.Equal("Ans", attempt.FinalAnswer);
    }

    [Fact]
    public async Task SubmitStudentAssignment_ExtraAndDuplicateQuestionIds_SanitizedAndCapCompletedCount()
    {
        var (db, centerId, studentId) = CreateTestContext();
        await using var _ = db;

        var now = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);
        var mutableTime = new MutableTimeProvider { UtcNow = now };
        var assignmentId = Guid.NewGuid();
        const ulong qId = 4001;

        db.Assignments.Add(new Assignment
        {
            AssignmentId = assignmentId,
            CenterId = centerId,
            Title = "Payload Sanitization Test",
            Status = AssignmentStatus.Published,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.AssignmentTargets.Add(new AssignmentTarget
        {
            CenterId = centerId,
            AssignmentId = assignmentId,
            StudentId = studentId,
            CreatedAt = now
        });
        db.Questions.Add(new Question
        {
            QuestionId = qId,
            CenterId = centerId,
            SubjectId = Guid.NewGuid(),
            QuestionType = QuestionType.ShortAnswer,
            QuestionText = "Question 4001",
            CorrectAnswer = "Right",
            MaxScore = 10,
            LanguageCode = "vi",
            Solution = "Lời giải câu 4001",
            CreatedAt = now,
            UpdatedAt = now
        });
        db.AssignmentQuestions.Add(new AssignmentQuestion
        {
            CenterId = centerId,
            AssignmentId = assignmentId,
            QuestionId = qId,
            Points = 10,
            CreatedAt = now
        });
        db.StudentAssignmentProgresses.Add(new StudentAssignmentProgress
        {
            ProgressId = 904,
            CenterId = centerId,
            AssignmentId = assignmentId,
            StudentId = studentId,
            Status = ProgressStatus.InProgress,
            StartedAt = now,
            TotalQuestionCount = 1,
            CompletedQuestionCount = 0,
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();

        var studentTenant = CreateStudentTenant(centerId, studentId);
        var submitUseCase = new SubmitStudentAssignmentUseCase(db, studentTenant.Object, mutableTime);

        // Payload contains:
        // 1) Q4001 first entry
        // 2) Q4001 duplicate entry (must not cause ToDictionary collision)
        // 3) Q9999 external question ID not in assignment (must be filtered out and not counted)
        var req = new SubmitAssignmentRequest
        {
            Answers = new List<AssignmentDraftAnswerItemDto>
            {
                new() { QuestionId = (long)qId, FinalAnswer = "First" },
                new() { QuestionId = (long)qId, FinalAnswer = "Right" }, // Duplicate!
                new() { QuestionId = 9999, FinalAnswer = "ExternalQuestion" } // External!
            }
        };

        var result = await submitUseCase.ExecuteAsync(assignmentId, req);
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Data!.SubmittedAttemptsCount);

        // Verify completed question count is exactly 1 (not 2 or 3!)
        var progress = await db.StudentAssignmentProgresses.SingleAsync(p => p.ProgressId == 904);
        Assert.Equal(1u, progress.CompletedQuestionCount);
        Assert.Equal(ProgressStatus.Completed, progress.Status);

        var attempt = await db.Attempts.SingleAsync(a => a.AssignmentId == assignmentId && a.QuestionId == qId);
        Assert.Equal("Right", attempt.FinalAnswer);
        Assert.True(attempt.IsCorrect);
    }

    [Fact]
    public async Task SubmitStudentAssignment_QuestionMaxScoreUsedForGrading_WhenAssignmentQuestionPointsIsOne()
    {
        var (db, centerId, studentId) = CreateTestContext();
        await using var _ = db;

        var now = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);
        var mutableTime = new MutableTimeProvider { UtcNow = now };
        var assignmentId = Guid.NewGuid();
        const ulong qId = 7001;

        db.Assignments.Add(new Assignment
        {
            AssignmentId = assignmentId,
            CenterId = centerId,
            Title = "Grading Scale Test",
            Status = AssignmentStatus.Published,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.AssignmentTargets.Add(new AssignmentTarget
        {
            CenterId = centerId,
            AssignmentId = assignmentId,
            StudentId = studentId,
            CreatedAt = now
        });
        // Question has MaxScore = 10m
        db.Questions.Add(new Question
        {
            QuestionId = qId,
            CenterId = centerId,
            SubjectId = Guid.NewGuid(),
            QuestionType = QuestionType.ShortAnswer,
            QuestionText = "Question with MaxScore 10",
            CorrectAnswer = "42",
            MaxScore = 10m,
            LanguageCode = "vi",
            Solution = "42",
            CreatedAt = now,
            UpdatedAt = now
        });
        // AssignmentQuestion created by default has Points = 1m!
        db.AssignmentQuestions.Add(new AssignmentQuestion
        {
            CenterId = centerId,
            AssignmentId = assignmentId,
            QuestionId = qId,
            Points = 1m, // Default points = 1
            CreatedAt = now
        });
        db.StudentAssignmentProgresses.Add(new StudentAssignmentProgress
        {
            ProgressId = 910,
            CenterId = centerId,
            AssignmentId = assignmentId,
            StudentId = studentId,
            Status = ProgressStatus.InProgress,
            StartedAt = now,
            TotalQuestionCount = 1,
            CreatedAt = now,
            UpdatedAt = now
        });
        await db.SaveChangesAsync();

        var studentTenant = CreateStudentTenant(centerId, studentId);
        var submitUseCase = new SubmitStudentAssignmentUseCase(db, studentTenant.Object, mutableTime);

        var req = new SubmitAssignmentRequest
        {
            Answers = new List<AssignmentDraftAnswerItemDto>
            {
                new() { QuestionId = (long)qId, FinalAnswer = "42" }
            }
        };

        var submitResult = await submitUseCase.ExecuteAsync(assignmentId, req);
        Assert.True(submitResult.IsSuccess);

        // Verify Attempt was awarded Question.MaxScore (10m), NOT AssignmentQuestion.Points (1m)!
        var attempt = await db.Attempts.SingleAsync(a => a.AssignmentId == assignmentId && a.QuestionId == qId);
        Assert.True(attempt.IsCorrect);
        Assert.Equal(10m, attempt.AwardedScore);

        // Verify AssignmentResultCalculator calculates full score 10/10 (100%), not 1/10 (10%)
        var calculator = new AssignmentResultCalculator(db);
        var summary = await calculator.CalculateForSingleAssignmentAsync(centerId, studentId, assignmentId, CancellationToken.None);
        Assert.Equal(10m, summary.InternalAwardedScore);
        Assert.Equal(10m, summary.InternalMaxScore);
        Assert.Equal(1, summary.CorrectQuestionCount);
    }

    [Fact]
    public async Task SubmitStudentAssignment_AutoFinalizeExpired_MissingReasoning_FinalizesDraftWithoutErrorOrFakeText()
    {
        var (db, centerId, studentId) = CreateTestContext();
        await using var _ = db;

        var now = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);
        var mutableTime = new MutableTimeProvider { UtcNow = now };
        var assignmentId = Guid.NewGuid();
        const ulong qId = 7002;

        db.Assignments.Add(new Assignment
        {
            AssignmentId = assignmentId,
            CenterId = centerId,
            Title = "Expired Draft Finalize Test",
            Status = AssignmentStatus.Published,
            TimeLimitMinutes = 30,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.AssignmentTargets.Add(new AssignmentTarget
        {
            CenterId = centerId,
            AssignmentId = assignmentId,
            StudentId = studentId,
            CreatedAt = now
        });
        db.Questions.Add(new Question
        {
            QuestionId = qId,
            CenterId = centerId,
            SubjectId = Guid.NewGuid(),
            QuestionType = QuestionType.ShortAnswer,
            QuestionText = "Question requiring reasoning",
            CorrectAnswer = "42",
            MaxScore = 10m,
            ReasoningRequired = true, // Reasoning is required!
            LanguageCode = "vi",
            Solution = "42",
            CreatedAt = now,
            UpdatedAt = now
        });
        db.AssignmentQuestions.Add(new AssignmentQuestion
        {
            CenterId = centerId,
            AssignmentId = assignmentId,
            QuestionId = qId,
            Points = 10m,
            CreatedAt = now
        });

        // Student started and saved draft answer with NO reasoning text before deadline
        var draftAnswers = new List<AssignmentDraftAnswerItemDto>
        {
            new() { QuestionId = (long)qId, FinalAnswer = "42", ReasoningText = null }
        };
        var draftJson = DraftAnswersHelper.SerializeDraft(draftAnswers, 1);

        db.StudentAssignmentProgresses.Add(new StudentAssignmentProgress
        {
            ProgressId = 911,
            CenterId = centerId,
            AssignmentId = assignmentId,
            StudentId = studentId,
            Status = ProgressStatus.InProgress,
            StartedAt = now,
            DraftSavedAt = now,
            TotalQuestionCount = 1,
            DraftAnswersJson = draftJson,
            CreatedAt = now,
            UpdatedAt = now,
            RowVersion = 1
        });
        await db.SaveChangesAsync();

        var studentTenant = CreateStudentTenant(centerId, studentId);
        var submitUseCase = new SubmitStudentAssignmentUseCase(db, studentTenant.Object, mutableTime);

        // Time expires: now 35 minutes later (> 30 min limit)
        mutableTime.UtcNow = now.AddMinutes(35);

        // Client triggers submit (or auto-submit runs) past deadline
        var req = new SubmitAssignmentRequest
        {
            Answers = new List<AssignmentDraftAnswerItemDto>
            {
                new() { QuestionId = (long)qId, FinalAnswer = "NewAttemptAnswer", ReasoningText = null }
            }
        };

        var result = await submitUseCase.ExecuteAsync(assignmentId, req);

        // Must succeed (not blocked by QUESTION_REASONING_REQUIRED)
        Assert.True(result.IsSuccess);
        Assert.True(result.Data!.IsCompleted);
        Assert.Equal(1, result.Data.SubmittedAttemptsCount);

        // The finalized answer must come from the pre-saved draft ("42"), NOT "NewAttemptAnswer"
        var attempt = await db.Attempts.SingleAsync(a => a.AssignmentId == assignmentId && a.QuestionId == qId);
        Assert.Equal("42", attempt.FinalAnswer);

        // ReasoningText must be null or empty, NOT synthetic "[Hết giờ...]" fake reasoning
        Assert.True(string.IsNullOrEmpty(attempt.ReasoningText));

        // Progress must be completed
        var progress = await db.StudentAssignmentProgresses.SingleAsync(p => p.ProgressId == 911);
        Assert.Equal(ProgressStatus.Completed, progress.Status);
    }

    [Fact]
    public async Task SaveAssignmentDraft_StaleVersion_IgnoredAndDoesNotOverwriteNewerDraft()
    {
        var (db, centerId, studentId) = CreateTestContext();
        await using var _ = db;

        var now = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);
        var mutableTime = new MutableTimeProvider { UtcNow = now };
        var assignmentId = Guid.NewGuid();
        const ulong qId = 7003;

        db.Assignments.Add(new Assignment
        {
            AssignmentId = assignmentId,
            CenterId = centerId,
            Title = "Draft Version OCC Test",
            Status = AssignmentStatus.Published,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.AssignmentTargets.Add(new AssignmentTarget
        {
            CenterId = centerId,
            AssignmentId = assignmentId,
            StudentId = studentId,
            CreatedAt = now
        });
        db.Questions.Add(new Question
        {
            QuestionId = qId,
            CenterId = centerId,
            SubjectId = Guid.NewGuid(),
            QuestionType = QuestionType.ShortAnswer,
            QuestionText = "Question 7003",
            CorrectAnswer = "42",
            MaxScore = 10,
            LanguageCode = "vi",
            Solution = "42",
            CreatedAt = now,
            UpdatedAt = now
        });
        db.AssignmentQuestions.Add(new AssignmentQuestion
        {
            CenterId = centerId,
            AssignmentId = assignmentId,
            QuestionId = qId,
            Points = 10,
            CreatedAt = now
        });
        db.StudentAssignmentProgresses.Add(new StudentAssignmentProgress
        {
            ProgressId = 912,
            CenterId = centerId,
            AssignmentId = assignmentId,
            StudentId = studentId,
            Status = ProgressStatus.InProgress,
            StartedAt = now,
            TotalQuestionCount = 1,
            CreatedAt = now,
            UpdatedAt = now,
            RowVersion = 1
        });
        await db.SaveChangesAsync();

        var studentTenant = CreateStudentTenant(centerId, studentId);
        var draftUseCase = new SaveAssignmentDraftUseCase(db, studentTenant.Object, mutableTime);

        // 1. Save newer draft v2 first
        var reqV2 = new SaveAssignmentDraftRequest
        {
            DraftVersion = 2,
            Answers = new List<AssignmentDraftAnswerItemDto>
            {
                new() { QuestionId = (long)qId, FinalAnswer = "NewDraftAnswerV2" }
            }
        };
        var resV2 = await draftUseCase.ExecuteAsync(assignmentId, reqV2);
        Assert.True(resV2.IsSuccess);

        // 2. An older/stale request v1 arrives late with different content
        var reqV1 = new SaveAssignmentDraftRequest
        {
            DraftVersion = 1,
            Answers = new List<AssignmentDraftAnswerItemDto>
            {
                new() { QuestionId = (long)qId, FinalAnswer = "OldStaleDraftAnswerV1" }
            }
        };
        var resV1 = await draftUseCase.ExecuteAsync(assignmentId, reqV1);
        Assert.False(resV1.IsSuccess);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, resV1.ErrorCode);
        Assert.Equal(2, resV1.DraftVersion);

        // 3. Verify DB still retains v2 content and version 2!
        var progress = await db.StudentAssignmentProgresses.AsNoTracking().SingleAsync(p => p.ProgressId == 912);
        var parsed = DraftAnswersHelper.ParseDraft(progress.DraftAnswersJson);
        Assert.Equal(2, parsed.Version);
        Assert.Equal("NewDraftAnswerV2", parsed.Answers.Single().FinalAnswer);

        // 4. Verify GetStudentAssignmentUseCase returns draftVersion = 2 and NewDraftAnswerV2
        var getUseCase = new GetStudentAssignmentUseCase(
            db,
            studentTenant.Object,
            mutableTime,
            new AssignmentResultCalculator(db));
        var detailRes = await getUseCase.ExecuteAsync(assignmentId, CancellationToken.None);
        Assert.True(detailRes.IsSuccess);
        Assert.Equal(2, detailRes.Data!.Data.DraftVersion);
        Assert.Equal("NewDraftAnswerV2", detailRes.Data.Data.DraftAnswers!.Single().FinalAnswer);
    }

    [Fact]
    public async Task SaveAssignmentDraft_SameVersion_IdempotentIfSamePayload_ConflictIfDifferentPayload_AndUnversionedRejected()
    {
        var (db, centerId, studentId) = CreateTestContext();
        await using var _ = db;

        var now = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);
        var mutableTime = new MutableTimeProvider { UtcNow = now };
        var assignmentId = Guid.NewGuid();
        const ulong qId = 7010;

        db.Assignments.Add(new Assignment
        {
            AssignmentId = assignmentId,
            CenterId = centerId,
            Title = "Same Version Conflict & Unversioned Rejection Test",
            Status = AssignmentStatus.Published,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.AssignmentTargets.Add(new AssignmentTarget
        {
            CenterId = centerId,
            AssignmentId = assignmentId,
            StudentId = studentId,
            CreatedAt = now
        });
        db.Questions.Add(new Question
        {
            QuestionId = qId,
            CenterId = centerId,
            SubjectId = Guid.NewGuid(),
            QuestionType = QuestionType.ShortAnswer,
            QuestionText = "Question 7010",
            CorrectAnswer = "Answer7010",
            MaxScore = 10,
            LanguageCode = "vi",
            Solution = "Sol",
            CreatedAt = now,
            UpdatedAt = now
        });
        db.AssignmentQuestions.Add(new AssignmentQuestion
        {
            CenterId = centerId,
            AssignmentId = assignmentId,
            QuestionId = qId,
            Points = 10,
            CreatedAt = now
        });
        db.StudentAssignmentProgresses.Add(new StudentAssignmentProgress
        {
            ProgressId = 918,
            CenterId = centerId,
            AssignmentId = assignmentId,
            StudentId = studentId,
            Status = ProgressStatus.InProgress,
            StartedAt = now,
            TotalQuestionCount = 1,
            CreatedAt = now,
            UpdatedAt = now,
            RowVersion = 1
        });
        await db.SaveChangesAsync();

        var studentTenant = CreateStudentTenant(centerId, studentId);
        var draftUseCase = new SaveAssignmentDraftUseCase(db, studentTenant.Object, mutableTime);

        // 1. Tab 1 saves version 2 with Payload A
        var reqV2A = new SaveAssignmentDraftRequest
        {
            DraftVersion = 2,
            Answers = new List<AssignmentDraftAnswerItemDto>
            {
                new() { QuestionId = (long)qId, FinalAnswer = "PayloadA" }
            }
        };
        var resV2A = await draftUseCase.ExecuteAsync(assignmentId, reqV2A);
        Assert.True(resV2A.IsSuccess);
        Assert.Equal(2, resV2A.DraftVersion);

        // 2. Tab 2 sends same version 2 but with different Payload B -> MUST return ConcurrencyConflict!
        var reqV2B = new SaveAssignmentDraftRequest
        {
            DraftVersion = 2,
            Answers = new List<AssignmentDraftAnswerItemDto>
            {
                new() { QuestionId = (long)qId, FinalAnswer = "PayloadB_Different" }
            }
        };
        var resV2B = await draftUseCase.ExecuteAsync(assignmentId, reqV2B);
        Assert.False(resV2B.IsSuccess);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, resV2B.ErrorCode);
        Assert.Equal(2, resV2B.DraftVersion);

        // 3. Tab 1 resends version 2 with identical Payload A (retry/idempotent) -> MUST succeed
        var reqV2ARetry = new SaveAssignmentDraftRequest
        {
            DraftVersion = 2,
            Answers = new List<AssignmentDraftAnswerItemDto>
            {
                new() { QuestionId = (long)qId, FinalAnswer = "PayloadA" }
            }
        };
        var resV2ARetry = await draftUseCase.ExecuteAsync(assignmentId, reqV2ARetry);
        Assert.True(resV2ARetry.IsSuccess);
        Assert.Equal(2, resV2ARetry.DraftVersion);

        // 4. Tab 3 sends unversioned request (DraftVersion = null) while DB is versioned -> MUST return ConcurrencyConflict
        var reqUnversioned = new SaveAssignmentDraftRequest
        {
            DraftVersion = null,
            Answers = new List<AssignmentDraftAnswerItemDto>
            {
                new() { QuestionId = (long)qId, FinalAnswer = "PayloadC_Unversioned" }
            }
        };
        var resUnversioned = await draftUseCase.ExecuteAsync(assignmentId, reqUnversioned);
        Assert.False(resUnversioned.IsSuccess);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, resUnversioned.ErrorCode);
        Assert.Equal(2, resUnversioned.DraftVersion);

        // 5. Verify database still safely retains Payload A and version 2!
        var progress = await db.StudentAssignmentProgresses.AsNoTracking().SingleAsync(p => p.ProgressId == 918);
        var parsed = DraftAnswersHelper.ParseDraft(progress.DraftAnswersJson);
        Assert.Equal(2, parsed.Version);
        Assert.Equal("PayloadA", parsed.Answers.Single().FinalAnswer);
    }

    [Fact]
    public async Task SubmitStudentAssignment_EmptyExamTimeout_AutoFinalizedWithoutDraft_AllQuestionsSkipped()
    {
        var (db, centerId, studentId) = CreateTestContext();
        await using var _ = db;

        var now = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);
        var mutableTime = new MutableTimeProvider { UtcNow = now };
        var assignmentId = Guid.NewGuid();
        const ulong q1Id = 7021;
        const ulong q2Id = 7022;

        db.Assignments.Add(new Assignment
        {
            AssignmentId = assignmentId,
            CenterId = centerId,
            Title = "Empty Exam Timeout Finalization Test",
            Status = AssignmentStatus.Published,
            TimeLimitMinutes = 20,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.AssignmentTargets.Add(new AssignmentTarget
        {
            CenterId = centerId,
            AssignmentId = assignmentId,
            StudentId = studentId,
            CreatedAt = now
        });
        db.Questions.Add(new Question
        {
            QuestionId = q1Id,
            CenterId = centerId,
            SubjectId = Guid.NewGuid(),
            QuestionType = QuestionType.ShortAnswer,
            QuestionText = "Question 7021",
            CorrectAnswer = "42",
            MaxScore = 10,
            LanguageCode = "vi",
            Solution = "42",
            CreatedAt = now,
            UpdatedAt = now
        });
        db.Questions.Add(new Question
        {
            QuestionId = q2Id,
            CenterId = centerId,
            SubjectId = Guid.NewGuid(),
            QuestionType = QuestionType.ShortAnswer,
            QuestionText = "Question 7022",
            CorrectAnswer = "100",
            MaxScore = 10,
            LanguageCode = "vi",
            Solution = "100",
            CreatedAt = now,
            UpdatedAt = now
        });
        db.AssignmentQuestions.Add(new AssignmentQuestion
        {
            CenterId = centerId,
            AssignmentId = assignmentId,
            QuestionId = q1Id,
            Points = 10,
            CreatedAt = now
        });
        db.AssignmentQuestions.Add(new AssignmentQuestion
        {
            CenterId = centerId,
            AssignmentId = assignmentId,
            QuestionId = q2Id,
            Points = 10,
            CreatedAt = now
        });
        // Student started the exam, but NEVER answered or saved any draft (DraftAnswersJson is null)
        db.StudentAssignmentProgresses.Add(new StudentAssignmentProgress
        {
            ProgressId = 919,
            CenterId = centerId,
            AssignmentId = assignmentId,
            StudentId = studentId,
            Status = ProgressStatus.InProgress,
            StartedAt = now,
            DraftAnswersJson = null,
            DraftSavedAt = null,
            TotalQuestionCount = 2,
            CompletedQuestionCount = 0,
            CreatedAt = now,
            UpdatedAt = now,
            RowVersion = 1
        });
        await db.SaveChangesAsync();

        var studentTenant = CreateStudentTenant(centerId, studentId);
        var submitUseCase = new SubmitStudentAssignmentUseCase(db, studentTenant.Object, mutableTime);

        // Time expires: 25 minutes later (> 20 min limit)
        mutableTime.UtcNow = now.AddMinutes(25);

        // Client triggers submit (with empty payload or null payload)
        var req = new SubmitAssignmentRequest
        {
            Answers = new List<AssignmentDraftAnswerItemDto>()
        };

        var result = await submitUseCase.ExecuteAsync(assignmentId, req);

        // Must succeed without ASSIGNMENT_NOT_AVAILABLE
        Assert.True(result.IsSuccess);
        Assert.True(result.Data!.IsCompleted);
        Assert.Equal(2, result.Data.SubmittedAttemptsCount);

        // Both questions must be recorded as SKIPPED attempts
        var attempts = await db.Attempts
            .Where(a => a.AssignmentId == assignmentId)
            .OrderBy(a => a.QuestionId)
            .ToListAsync();

        Assert.Equal(2, attempts.Count);
        Assert.All(attempts, a =>
        {
            Assert.Equal("SKIPPED", a.FinalAnswer);
            Assert.True(a.Skipped);
            Assert.Equal(0m, a.AwardedScore);
            Assert.False(a.IsCorrect);
        });

        // Progress must be Completed with 0 completed answers
        var progress = await db.StudentAssignmentProgresses.SingleAsync(p => p.ProgressId == 919);
        Assert.Equal(ProgressStatus.Completed, progress.Status);
        Assert.Equal(0u, progress.CompletedQuestionCount);
    }

    [Fact]
    public async Task SubmitStudentAssignment_AttachmentCleanupOnFailure_AndDuplicateTokenHandling()
    {
        var (db, centerId, studentId) = CreateTestContext();
        await using var _ = db;

        var now = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);
        var mutableTime = new MutableTimeProvider { UtcNow = now };
        var assignmentId = Guid.NewGuid();
        const ulong q1Id = 7004;
        const ulong q2Id = 7005;

        db.Assignments.Add(new Assignment
        {
            AssignmentId = assignmentId,
            CenterId = centerId,
            Title = "Attachment Cleanup & Token Deduplication Test",
            Status = AssignmentStatus.Published,
            CreatedAt = now,
            UpdatedAt = now
        });
        db.AssignmentTargets.Add(new AssignmentTarget
        {
            CenterId = centerId,
            AssignmentId = assignmentId,
            StudentId = studentId,
            CreatedAt = now
        });
        db.Questions.Add(new Question
        {
            QuestionId = q1Id,
            CenterId = centerId,
            SubjectId = Guid.NewGuid(),
            QuestionType = QuestionType.ShortAnswer,
            QuestionText = "Question 7004",
            CorrectAnswer = "42",
            MaxScore = 10,
            LanguageCode = "vi",
            Solution = "42",
            CreatedAt = now,
            UpdatedAt = now
        });
        db.Questions.Add(new Question
        {
            QuestionId = q2Id,
            CenterId = centerId,
            SubjectId = Guid.NewGuid(),
            QuestionType = QuestionType.ShortAnswer,
            QuestionText = "Question 7005",
            CorrectAnswer = "43",
            MaxScore = 10,
            LanguageCode = "vi",
            Solution = "43",
            CreatedAt = now,
            UpdatedAt = now
        });
        db.AssignmentQuestions.Add(new AssignmentQuestion
        {
            CenterId = centerId,
            AssignmentId = assignmentId,
            QuestionId = q1Id,
            Points = 10,
            CreatedAt = now
        });
        db.AssignmentQuestions.Add(new AssignmentQuestion
        {
            CenterId = centerId,
            AssignmentId = assignmentId,
            QuestionId = q2Id,
            Points = 10,
            CreatedAt = now
        });
        db.StudentAssignmentProgresses.Add(new StudentAssignmentProgress
        {
            ProgressId = 913,
            CenterId = centerId,
            AssignmentId = assignmentId,
            StudentId = studentId,
            Status = ProgressStatus.InProgress,
            StartedAt = now,
            TotalQuestionCount = 2,
            CreatedAt = now,
            UpdatedAt = now
        });

        // Existing attachment already in DB using nonce "used-nonce-1"
        db.AttemptAttachments.Add(new AttemptAttachment
        {
            AttachmentId = 1,
            CenterId = centerId,
            AttemptId = 9999,
            StorageKey = "permanent/used-key",
            FileName = "used.png",
            ContentType = "image/png",
            FileSizeBytes = 100,
            UploadNonce = "used-nonce-1",
            CreatedBy = studentId,
            CreatedAt = now
        });
        await db.SaveChangesAsync();

        const string tokenUsed = "token-with-already-used-nonce";
        var payloadUsed = new AttachmentUploadTokenPayload(
            centerId,
            studentId,
            "used-nonce-1", // Already exists in AttemptAttachments!
            new string('a', 64),
            "scratchpad1.png",
            1024,
            now.AddHours(1)
        );

        const string tokenFresh1 = "token-fresh-1";
        var payloadFresh1 = new AttachmentUploadTokenPayload(
            centerId,
            studentId,
            "fresh-nonce-1",
            new string('b', 64),
            "scratchpad1.png",
            1024,
            now.AddHours(1)
        );

        const string tokenFresh2 = "token-fresh-2";
        var payloadFresh2 = new AttachmentUploadTokenPayload(
            centerId,
            studentId,
            "fresh-nonce-2",
            new string('c', 64),
            "scratchpad2.png",
            1024,
            now.AddHours(1)
        );

        var mockTokens = new Mock<IAttemptAttachmentTokenService>();
        mockTokens.Setup(t => t.TryRead(tokenUsed, out payloadUsed)).Returns(true);
        mockTokens.Setup(t => t.TryRead(tokenFresh1, out payloadFresh1)).Returns(true);
        mockTokens.Setup(t => t.TryRead(tokenFresh2, out payloadFresh2)).Returns(true);

        var mockStorage = new Mock<IAttemptAttachmentStorage>();
        mockStorage
            .Setup(s => s.PromoteToPermanentAsync(payloadFresh1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PromotedAttemptAttachment("permanent-storage-fresh-1", true));
        mockStorage
            .Setup(s => s.PromoteToPermanentAsync(payloadFresh2, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Storage exploded during batch"));
        mockStorage
            .Setup(s => s.DeletePermanentAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var studentTenant = CreateStudentTenant(centerId, studentId);
        var submitUseCase = new SubmitStudentAssignmentUseCase(
            db,
            studentTenant.Object,
            mutableTime,
            graderFactory: null,
            attachmentTokens: mockTokens.Object,
            attachmentStorage: mockStorage.Object);

        // Submitting with tokenUsed must be rejected with UploadTokenAlreadyUsed
        var reqWithUsedToken = new SubmitAssignmentRequest
        {
            Answers = new List<AssignmentDraftAnswerItemDto>
            {
                new() { QuestionId = (long)q1Id, FinalAnswer = "42", DrawingUploadToken = tokenUsed }
            }
        };

        var failResult = await submitUseCase.ExecuteAsync(assignmentId, reqWithUsedToken);
        Assert.False(failResult.IsSuccess);
        Assert.Equal(ErrorCodes.UploadTokenAlreadyUsed, failResult.ErrorCode);

        // Submitting duplicate token in same batch must be rejected with UploadTokenAlreadyUsed
        var reqDuplicateInBatch = new SubmitAssignmentRequest
        {
            Answers = new List<AssignmentDraftAnswerItemDto>
            {
                new() { QuestionId = (long)q1Id, FinalAnswer = "42", DrawingUploadToken = tokenFresh1 },
                new() { QuestionId = (long)q2Id, FinalAnswer = "43", DrawingUploadToken = tokenFresh1 }
            }
        };
        var dupResult = await submitUseCase.ExecuteAsync(assignmentId, reqDuplicateInBatch);
        Assert.False(dupResult.IsSuccess);
        Assert.Equal(ErrorCodes.UploadTokenAlreadyUsed, dupResult.ErrorCode);

        // Part B: Newly promoted attachment cleaned up if subsequent batch operation fails
        // Here Q1 fresh token gets promoted to permanent-storage-fresh-1, but Q2 promotion throws an error
        var reqMixed = new SubmitAssignmentRequest
        {
            Answers = new List<AssignmentDraftAnswerItemDto>
            {
                new() { QuestionId = (long)q1Id, FinalAnswer = "42", DrawingUploadToken = tokenFresh1 },
                new() { QuestionId = (long)q2Id, FinalAnswer = "43", DrawingUploadToken = tokenFresh2 }
            }
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => submitUseCase.ExecuteAsync(assignmentId, reqMixed));

        // Verify DeletePermanentAsync was called for "permanent-storage-fresh-1" because WasNewlyPromoted == true!
        mockStorage.Verify(
            s => s.DeletePermanentAsync("permanent-storage-fresh-1", It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task SubmitStudentAssignment_OCCContention_TwoIndependentDbContexts_ResolvedIdempotently()
    {
        // Two independent DbContext instances sharing the same in-memory store name
        var dbName = Guid.NewGuid().ToString();
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        var tenantAccessor = new Mock<ITenantIdAccessor>();
        tenantAccessor.SetupGet(a => a.CenterId).Returns(centerId);

        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var now = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);
        var mutableTime = new MutableTimeProvider { UtcNow = now };
        var assignmentId = Guid.NewGuid();
        const ulong qId = 5001;

        await using (var seedDb = new EduTwinDbContext(options, tenantAccessor.Object))
        {
            seedDb.Assignments.Add(new Assignment
            {
                AssignmentId = assignmentId,
                CenterId = centerId,
                Title = "OCC Contention Test",
                Status = AssignmentStatus.Published,
                CreatedAt = now,
                UpdatedAt = now
            });
            seedDb.AssignmentTargets.Add(new AssignmentTarget
            {
                CenterId = centerId,
                AssignmentId = assignmentId,
                StudentId = studentId,
                CreatedAt = now
            });
            seedDb.Questions.Add(new Question
            {
                QuestionId = qId,
                CenterId = centerId,
                SubjectId = Guid.NewGuid(),
                QuestionType = QuestionType.ShortAnswer,
                QuestionText = "Question 5001",
                CorrectAnswer = "42",
                MaxScore = 10,
                LanguageCode = "vi",
                Solution = "Lời giải câu 5001",
                CreatedAt = now,
                UpdatedAt = now
            });
            seedDb.AssignmentQuestions.Add(new AssignmentQuestion
            {
                CenterId = centerId,
                AssignmentId = assignmentId,
                QuestionId = qId,
                Points = 10,
                CreatedAt = now
            });
            seedDb.StudentAssignmentProgresses.Add(new StudentAssignmentProgress
            {
                ProgressId = 905,
                CenterId = centerId,
                AssignmentId = assignmentId,
                StudentId = studentId,
                Status = ProgressStatus.InProgress,
                StartedAt = now,
                TotalQuestionCount = 1,
                CreatedAt = now,
                UpdatedAt = now,
                RowVersion = 1
            });
            await seedDb.SaveChangesAsync();
        }

        // Two independent DbContext instances
        await using var db1 = new EduTwinDbContext(options, tenantAccessor.Object);
        await using var db2 = new EduTwinDbContext(options, tenantAccessor.Object);

        var studentTenant = CreateStudentTenant(centerId, studentId);
        var submit1 = new SubmitStudentAssignmentUseCase(db1, studentTenant.Object, mutableTime);
        var submit2 = new SubmitStudentAssignmentUseCase(db2, studentTenant.Object, mutableTime);

        var req = new SubmitAssignmentRequest
        {
            Answers = new List<AssignmentDraftAnswerItemDto>
            {
                new() { QuestionId = (long)qId, FinalAnswer = "42" }
            }
        };

        // First request commits
        var res1 = await submit1.ExecuteAsync(assignmentId, req);
        Assert.True(res1.IsSuccess);
        Assert.Equal(1, res1.Data!.SubmittedAttemptsCount);

        // Second request arrives concurrently: resolves idempotently without crashing
        var res2 = await submit2.ExecuteAsync(assignmentId, req);
        Assert.True(res2.IsSuccess);
        Assert.True(res2.Data!.IsCompleted);
        Assert.Equal(0, res2.Data.SubmittedAttemptsCount);
    }

    [MySqlIntegrationFact]
    public async Task SubmitStudentAssignment_LiveMySql_JobIdPopulated_AndConstraintsPreserved()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        const ulong qId = 6001;

        var tenant = new TenantContext();
        tenant.Initialize(centerId, studentId, nameof(UserRole.Student), 1);

        var now = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);
        var mutableTime = new MutableTimeProvider { UtcNow = now };

        await using (var setupContext = CreateContext(database.ConnectionString, tenant))
        {
            await setupContext.Database.OpenConnectionAsync();
            try
            {
                await setupContext.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 0;");

                setupContext.Centers.Add(new EduTwin.DAL.Organization.Center
                {
                    CenterId = centerId,
                    CenterCode = $"C_{Guid.NewGuid():N}"[..10],
                    CenterName = "MySQL Test Center",
                    Status = CenterStatus.Active,
                    Timezone = "Asia/Ho_Chi_Minh",
                    CreatedAt = now,
                    UpdatedAt = now,
                    RowVersion = 1
                });
                setupContext.Subjects.Add(new EduTwin.DAL.Organization.Subject
                {
                    CenterId = centerId,
                    SubjectId = subjectId,
                    SubjectCode = "MATH",
                    SubjectName = "Mathematics",
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now,
                    RowVersion = 1
                });
                setupContext.Users.Add(new EduTwin.DAL.IdentityAndTenancy.User
                {
                    UserId = teacherId,
                    CenterId = centerId,
                    Username = $"teacher_{Guid.NewGuid():N}"[..20],
                    DisplayName = "MySQL Test Teacher",
                    PasswordHash = "hash",
                    RoleName = UserRole.Teacher,
                    Status = UserStatus.Active,
                    CreatedAt = now,
                    UpdatedAt = now,
                    RowVersion = 1
                });
                setupContext.Teachers.Add(new EduTwin.DAL.Organization.Teacher
                {
                    TeacherId = teacherId,
                    CenterId = centerId,
                    CreatedAt = now,
                    UpdatedAt = now,
                    RowVersion = 1
                });
                setupContext.Users.Add(new EduTwin.DAL.IdentityAndTenancy.User
                {
                    UserId = studentId,
                    CenterId = centerId,
                    Username = $"student_{Guid.NewGuid():N}"[..20],
                    DisplayName = "MySQL Test Student",
                    PasswordHash = "hash",
                    RoleName = UserRole.Student,
                    Status = UserStatus.Active,
                    CreatedAt = now,
                    UpdatedAt = now,
                    RowVersion = 1
                });
                setupContext.Students.Add(new EduTwin.DAL.Organization.Student
                {
                    StudentId = studentId,
                    CenterId = centerId,
                    FullName = "MySQL Test Student",
                    GradeLevel = 10,
                    CreatedAt = now,
                    UpdatedAt = now,
                    RowVersion = 1
                });
                setupContext.Classes.Add(new EduTwin.DAL.Organization.Class
                {
                    CenterId = centerId,
                    ClassId = classId,
                    TeacherId = teacherId,
                    SubjectId = subjectId,
                    ClassName = "Class 10-MySQL",
                    AcademicYear = "2026-2027",
                    Status = ClassStatus.Active,
                    GradeLevel = 10,
                    CreatedAt = now,
                    UpdatedAt = now,
                    RowVersion = 1
                });
                setupContext.ClassStudents.Add(new EduTwin.DAL.Organization.ClassStudent
                {
                    CenterId = centerId,
                    ClassId = classId,
                    StudentId = studentId,
                    Status = ClassStudentStatus.Active,
                    JoinedAt = now
                });
                setupContext.KnowledgeNodes.Add(new EduTwin.DAL.KnowledgeGraph.KnowledgeNode
                {
                    CenterId = centerId,
                    NodeId = 101,
                    SubjectId = subjectId,
                    NodeType = EduTwin.Contracts.KnowledgeGraph.NodeType.Topic,
                    NodeCode = "TOPIC-101",
                    NodeName = "Math Topic",
                    OrderIndex = 1,
                    ExamImportance = 1.0m,
                    EstimatedLearningMinutes = 45,
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now,
                    RowVersion = 1
                });
                setupContext.Assignments.Add(new Assignment
                {
                    AssignmentId = assignmentId,
                    CenterId = centerId,
                    ClassId = classId,
                    CreatedByTeacherId = teacherId,
                    Title = "Live MySQL Submission Test",
                    Status = AssignmentStatus.Published,
                    TimeLimitMinutes = 30,
                    CreatedAt = now,
                    UpdatedAt = now,
                    RowVersion = 1
                });
                setupContext.AssignmentTargets.Add(new AssignmentTarget
                {
                    CenterId = centerId,
                    AssignmentId = assignmentId,
                    StudentId = studentId,
                    CreatedAt = now
                });
                setupContext.Questions.Add(new Question
                {
                    QuestionId = qId,
                    CenterId = centerId,
                    SubjectId = subjectId,
                    PrimaryTopicNodeId = 101,
                    CreatedByTeacherId = teacherId,
                    QuestionType = QuestionType.ShortAnswer,
                    QuestionText = "Question 6001",
                    CorrectAnswer = "42",
                    Difficulty = 3,
                    Status = QuestionStatus.Active,
                    EstimatedTimeSeconds = 60,
                    MaxScore = 10,
                    LanguageCode = "vi",
                    Solution = "Lời giải câu 6001",
                    CreatedAt = now,
                    UpdatedAt = now
                });
                setupContext.AssignmentQuestions.Add(new AssignmentQuestion
                {
                    CenterId = centerId,
                    AssignmentId = assignmentId,
                    QuestionId = qId,
                    Points = 10,
                    CreatedAt = now
                });
                setupContext.StudentAssignmentProgresses.Add(new StudentAssignmentProgress
                {
                    CenterId = centerId,
                    AssignmentId = assignmentId,
                    StudentId = studentId,
                    Status = ProgressStatus.InProgress,
                    StartedAt = now,
                    TotalQuestionCount = 1,
                    CompletedQuestionCount = 0,
                    CreatedAt = now,
                    UpdatedAt = now,
                    RowVersion = 1
                });
                await setupContext.SaveChangesAsync();
            }
            finally
            {
                await setupContext.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 1;");
            }
        }

        // Run submission on live MySQL
        await using var db = CreateContext(database.ConnectionString, tenant);
        var submitUseCase = new SubmitStudentAssignmentUseCase(db, tenant, mutableTime);

        var req = new SubmitAssignmentRequest
        {
            Answers = new List<AssignmentDraftAnswerItemDto>
            {
                new() { QuestionId = (long)qId, FinalAnswer = "42" },
                new() { QuestionId = 9999, FinalAnswer = "ExtraQuestion" } // External question must be ignored
            }
        };

        var result = await submitUseCase.ExecuteAsync(assignmentId, req);
        Assert.True(result.IsSuccess);
        Assert.Equal(1, result.Data!.SubmittedAttemptsCount);
        Assert.True(result.Data.IsCompleted);

        // Verification of Issue 6: Job ID must be positive and NOT "0"!
        Assert.NotNull(result.Data.LastAnalysisJobId);
        Assert.NotEqual("0", result.Data.LastAnalysisJobId);
        Assert.True(ulong.Parse(result.Data.LastAnalysisJobId) > 0);

        // Verification of Issue 5: MySQL CompletedQuestionCount constraint preserved
        await using var verifyDb = CreateContext(database.ConnectionString, tenant);
        var progress = await verifyDb.StudentAssignmentProgresses.SingleAsync(p => p.CenterId == centerId && p.AssignmentId == assignmentId);
        Assert.Equal(1u, progress.CompletedQuestionCount);
        Assert.Equal(ProgressStatus.Completed, progress.Status);
    }

    [MySqlIntegrationFact]
    public async Task SubmitStudentAssignment_LiveMySql_ConcurrentSubmissionsWithBarrier_ResolvedIdempotently()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        const ulong qId = 8001;

        var tenantA = new TenantContext();
        var tenantB = new TenantContext();
        tenantA.Initialize(centerId, studentId, nameof(UserRole.Student), 1);
        tenantB.Initialize(centerId, studentId, nameof(UserRole.Student), 1);

        var now = new DateTime(2026, 10, 5, 8, 0, 0, DateTimeKind.Utc);
        var mutableTime = new MutableTimeProvider { UtcNow = now };

        // Seed data
        await using (var setupContext = CreateContext(database.ConnectionString, tenantA))
        {
            await setupContext.Database.OpenConnectionAsync();
            try
            {
                await setupContext.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 0;");

                setupContext.Centers.Add(new EduTwin.DAL.Organization.Center
                {
                    CenterId = centerId,
                    CenterCode = $"C_{Guid.NewGuid():N}"[..10],
                    CenterName = "MySQL Concurrency Center",
                    Status = CenterStatus.Active,
                    Timezone = "Asia/Ho_Chi_Minh",
                    CreatedAt = now,
                    UpdatedAt = now,
                    RowVersion = 1
                });
                setupContext.Subjects.Add(new EduTwin.DAL.Organization.Subject
                {
                    CenterId = centerId,
                    SubjectId = subjectId,
                    SubjectCode = "MATH",
                    SubjectName = "Mathematics",
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now,
                    RowVersion = 1
                });
                setupContext.Users.Add(new EduTwin.DAL.IdentityAndTenancy.User
                {
                    UserId = teacherId,
                    CenterId = centerId,
                    Username = $"teacher_{Guid.NewGuid():N}"[..20],
                    DisplayName = "MySQL Concurrency Teacher",
                    PasswordHash = "hash",
                    RoleName = UserRole.Teacher,
                    Status = UserStatus.Active,
                    CreatedAt = now,
                    UpdatedAt = now,
                    RowVersion = 1
                });
                setupContext.Teachers.Add(new EduTwin.DAL.Organization.Teacher
                {
                    TeacherId = teacherId,
                    CenterId = centerId,
                    CreatedAt = now,
                    UpdatedAt = now,
                    RowVersion = 1
                });
                setupContext.Users.Add(new EduTwin.DAL.IdentityAndTenancy.User
                {
                    UserId = studentId,
                    CenterId = centerId,
                    Username = $"student_{Guid.NewGuid():N}"[..20],
                    DisplayName = "MySQL Concurrency Student",
                    PasswordHash = "hash",
                    RoleName = UserRole.Student,
                    Status = UserStatus.Active,
                    CreatedAt = now,
                    UpdatedAt = now,
                    RowVersion = 1
                });
                setupContext.Students.Add(new EduTwin.DAL.Organization.Student
                {
                    StudentId = studentId,
                    CenterId = centerId,
                    FullName = "MySQL Concurrency Student",
                    GradeLevel = 10,
                    CreatedAt = now,
                    UpdatedAt = now,
                    RowVersion = 1
                });
                setupContext.Classes.Add(new EduTwin.DAL.Organization.Class
                {
                    CenterId = centerId,
                    ClassId = classId,
                    TeacherId = teacherId,
                    SubjectId = subjectId,
                    ClassName = "Class 10-Concurrency",
                    AcademicYear = "2026-2027",
                    Status = ClassStatus.Active,
                    GradeLevel = 10,
                    CreatedAt = now,
                    UpdatedAt = now,
                    RowVersion = 1
                });
                setupContext.ClassStudents.Add(new EduTwin.DAL.Organization.ClassStudent
                {
                    CenterId = centerId,
                    ClassId = classId,
                    StudentId = studentId,
                    Status = ClassStudentStatus.Active,
                    JoinedAt = now
                });
                setupContext.KnowledgeNodes.Add(new EduTwin.DAL.KnowledgeGraph.KnowledgeNode
                {
                    CenterId = centerId,
                    NodeId = 201,
                    SubjectId = subjectId,
                    NodeType = EduTwin.Contracts.KnowledgeGraph.NodeType.Topic,
                    NodeCode = "TOPIC-201",
                    NodeName = "Math Topic",
                    OrderIndex = 1,
                    ExamImportance = 1.0m,
                    EstimatedLearningMinutes = 45,
                    IsActive = true,
                    CreatedAt = now,
                    UpdatedAt = now,
                    RowVersion = 1
                });
                setupContext.Assignments.Add(new Assignment
                {
                    AssignmentId = assignmentId,
                    CenterId = centerId,
                    ClassId = classId,
                    CreatedByTeacherId = teacherId,
                    Title = "Live MySQL Barrier Concurrency Test",
                    Status = AssignmentStatus.Published,
                    TimeLimitMinutes = 30,
                    CreatedAt = now,
                    UpdatedAt = now,
                    RowVersion = 1
                });
                setupContext.AssignmentTargets.Add(new AssignmentTarget
                {
                    CenterId = centerId,
                    AssignmentId = assignmentId,
                    StudentId = studentId,
                    CreatedAt = now
                });
                setupContext.Questions.Add(new Question
                {
                    QuestionId = qId,
                    CenterId = centerId,
                    SubjectId = subjectId,
                    PrimaryTopicNodeId = 201,
                    CreatedByTeacherId = teacherId,
                    QuestionType = QuestionType.ShortAnswer,
                    QuestionText = "Question 8001",
                    CorrectAnswer = "42",
                    Difficulty = 3,
                    Status = QuestionStatus.Active,
                    EstimatedTimeSeconds = 60,
                    MaxScore = 10,
                    LanguageCode = "vi",
                    Solution = "Lời giải câu 8001",
                    CreatedAt = now,
                    UpdatedAt = now
                });
                setupContext.AssignmentQuestions.Add(new AssignmentQuestion
                {
                    CenterId = centerId,
                    AssignmentId = assignmentId,
                    QuestionId = qId,
                    Points = 10,
                    CreatedAt = now
                });
                setupContext.StudentAssignmentProgresses.Add(new StudentAssignmentProgress
                {
                    CenterId = centerId,
                    AssignmentId = assignmentId,
                    StudentId = studentId,
                    Status = ProgressStatus.InProgress,
                    StartedAt = now,
                    TotalQuestionCount = 1,
                    CompletedQuestionCount = 0,
                    CreatedAt = now,
                    UpdatedAt = now,
                    RowVersion = 1
                });
                await setupContext.SaveChangesAsync();
            }
            finally
            {
                await setupContext.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 1;");
            }
        }

        // Two independent contexts hooked with a CompetingSaveBarrier(2)
        var barrier = new CompetingSaveBarrier(2);
        await using var dbA = CreateContext(database.ConnectionString, tenantA, barrier);
        await using var dbB = CreateContext(database.ConnectionString, tenantB, barrier);

        var submitUseCaseA = new SubmitStudentAssignmentUseCase(dbA, tenantA, mutableTime);
        var submitUseCaseB = new SubmitStudentAssignmentUseCase(dbB, tenantB, mutableTime);

        var req = new SubmitAssignmentRequest
        {
            Answers = new List<AssignmentDraftAnswerItemDto>
            {
                new() { QuestionId = (long)qId, FinalAnswer = "42" }
            }
        };

        // Truly concurrent submissions overlapping at SavingChangesAsync
        var results = await Task.WhenAll(
            submitUseCaseA.ExecuteAsync(assignmentId, req),
            submitUseCaseB.ExecuteAsync(assignmentId, req));

        // Both return success
        Assert.All(results, r => Assert.True(r.IsSuccess));
        Assert.All(results, r => Assert.True(r.Data!.IsCompleted));

        // Exactly one submission recorded the attempt (Count == 1), and the concurrent one was idempotent (Count == 0)
        Assert.Single(results, r => r.Data!.SubmittedAttemptsCount == 1);
        Assert.Single(results, r => r.Data!.SubmittedAttemptsCount == 0);

        // Verification on live MySQL: exactly 1 attempt persisted and progress completed
        await using var verifyDb2 = CreateContext(database.ConnectionString, tenantA);
        var attempts = await verifyDb2.Attempts
            .Where(a => a.CenterId == centerId && a.AssignmentId == assignmentId && a.QuestionId == qId)
            .ToListAsync();
        Assert.Single(attempts);

        var progress2 = await verifyDb2.StudentAssignmentProgresses
            .SingleAsync(p => p.CenterId == centerId && p.AssignmentId == assignmentId);
        Assert.Equal(1u, progress2.CompletedQuestionCount);
        Assert.Equal(ProgressStatus.Completed, progress2.Status);
    }

    private static EduTwinDbContext CreateContext(
        string connectionString,
        TenantContext tenant,
        params IInterceptor[] interceptors)
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseMySQL(connectionString);
        if (interceptors.Length > 0)
        {
            options.AddInterceptors(interceptors);
        }
        return new EduTwinDbContext(options.Options, tenant);
    }

    private sealed class CompetingSaveBarrier : SaveChangesInterceptor
    {
        private readonly TaskCompletionSource _allArrived =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _remaining;

        public CompetingSaveBarrier(int participants)
        {
            _remaining = participants;
        }

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            if (Interlocked.Decrement(ref _remaining) == 0)
            {
                _allArrived.TrySetResult();
            }

            await _allArrived.Task.WaitAsync(cancellationToken);
            return result;
        }
    }

    private sealed class MySqlIntegrationFactAttribute : FactAttribute
    {
        public MySqlIntegrationFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(AdminConnectionVariable)))
            {
                Skip = $"Set {AdminConnectionVariable} to run MySQL integration tests.";
            }
        }
    }

    private sealed class MySqlTestDatabase : IAsyncDisposable
    {
        private readonly string _adminConnectionString;
        private readonly string _databaseName;

        private MySqlTestDatabase(string adminConnectionString, string databaseName, string connectionString)
        {
            _adminConnectionString = adminConnectionString;
            _databaseName = databaseName;
            ConnectionString = connectionString;
        }

        public string ConnectionString { get; }

        public static async Task<MySqlTestDatabase> CreateAsync()
        {
            var configuredConnection = Environment.GetEnvironmentVariable(AdminConnectionVariable)
                ?? throw new InvalidOperationException($"{AdminConnectionVariable} is required.");
            var adminBuilder = new MySqlConnectionStringBuilder(configuredConnection)
            {
                Database = string.Empty,
                Pooling = false
            };
            var databaseName = $"edutwin_asg_{Guid.NewGuid():N}";
            await using (var connection = new MySqlConnection(adminBuilder.ConnectionString))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = $"CREATE DATABASE `{databaseName}` CHARACTER SET utf8mb4;";
                await command.ExecuteNonQueryAsync();
            }

            var databaseBuilder = new MySqlConnectionStringBuilder(adminBuilder.ConnectionString)
            {
                Database = databaseName,
                Pooling = false
            };
            var database = new MySqlTestDatabase(
                adminBuilder.ConnectionString,
                databaseName,
                databaseBuilder.ConnectionString);
            try
            {
                var tenant = new TenantContext();
                await using var context = CreateContext(database.ConnectionString, tenant);
                await context.Database.MigrateAsync();
                return database;
            }
            catch
            {
                await database.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var connection = new MySqlConnection(_adminConnectionString);
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = $"DROP DATABASE IF EXISTS `{_databaseName}`;";
                await command.ExecuteNonQueryAsync();
            }
            catch
            {
                // Best-effort cleanup
            }
        }
    }
}
