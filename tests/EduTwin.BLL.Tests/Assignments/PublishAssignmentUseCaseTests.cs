using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.BLL.Assignments;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.Assignments;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.Assignments;

/// <summary>
/// Unit tests cho PublishAssignmentUseCase (P10-T02).
/// Bao phủ tất cả Acceptance Criteria và Business Invariants.
/// Test phải deterministic, không gọi DB thật, không phụ thuộc thời gian hệ thống trực tiếp.
/// </summary>
public class PublishAssignmentUseCaseTests
{
    private readonly DbContextOptions<EduTwinDbContext> _options;
    private readonly Mock<ITenantContext> _tenantMock;
    private readonly Mock<TimeProvider> _timeProviderMock;
    private readonly DateTimeOffset _fixedUtcNow;

    public PublishAssignmentUseCaseTests()
    {
        _options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _tenantMock = new Mock<ITenantContext>();

        _fixedUtcNow = new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero);
        _timeProviderMock = new Mock<TimeProvider>();
        _timeProviderMock.Setup(x => x.GetUtcNow()).Returns(_fixedUtcNow);
    }

    private EduTwinDbContext CreateContext(Guid centerId)
    {
        var tenantAccessorMock = new Mock<ITenantIdAccessor>();
        tenantAccessorMock.Setup(x => x.CenterId).Returns(centerId);
        return new EduTwinDbContext(_options, tenantAccessorMock.Object);
    }

    private PublishAssignmentUseCase CreateSut(EduTwinDbContext dbContext) =>
        new(dbContext, _tenantMock.Object, _timeProviderMock.Object);

    private void SetupTenant(Guid centerId, Guid userId, string role)
    {
        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(userId);
        _tenantMock.SetupGet(x => x.Role).Returns(role);
    }

    /// <summary>
    /// Seed dữ liệu cơ bản: Center, Teacher, Subject, Class, Question(s).
    /// Returns (centerId, teacherId, classId, subjectId, question).
    /// </summary>
    private static async Task<(Center Center, User TeacherUser, Teacher Teacher, Subject Subject, Class Class, Question Question)>
        SeedBaseAsync(EduTwinDbContext ctx, Guid centerId, Guid teacherId)
    {
        var subjectId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var center = new Center
        {
            CenterId = centerId,
            CenterCode = "CTR-" + centerId.ToString()[..6],
            CenterName = "Test Center",
            Status = CenterStatus.Active,
            Timezone = "UTC",
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        var teacherUser = new User
        {
            UserId = teacherId,
            CenterId = centerId,
            Username = "teacher-" + teacherId.ToString()[..6],
            PasswordHash = "hash",
            RoleName = UserRole.Teacher,
            DisplayName = "Teacher Test",
            Status = UserStatus.Active,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        var teacher = new Teacher
        {
            TeacherId = teacherId,
            CenterId = centerId,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        var subject = new Subject
        {
            SubjectId = subjectId,
            CenterId = centerId,
            SubjectCode = "MATH",
            SubjectName = "Toán",
            IsActive = true,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        var topicNodeId = (ulong)(Math.Abs(centerId.GetHashCode()) % 100000 + 1000);
        var topicNode = new EduTwin.DAL.KnowledgeGraph.KnowledgeNode
        {
            NodeId = topicNodeId,
            CenterId = centerId,
            SubjectId = subjectId,
            NodeType = EduTwin.Contracts.KnowledgeGraph.NodeType.Topic,
            NodeCode = "MATH.T1",
            NodeName = "Topic 1",
            IsActive = true,
            IsDeleted = false,
            ExamImportance = 50,
            EstimatedLearningMinutes = 60,
            CreatedAt = now,
            UpdatedAt = now
        };

        var classEntity = new Class
        {
            ClassId = classId,
            CenterId = centerId,
            TeacherId = teacherId,
            SubjectId = subjectId,
            ClassName = "Toán 12A",
            AcademicYear = "2026-2027",
            Status = ClassStatus.Active,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        var questionId = (ulong)(Math.Abs(centerId.GetHashCode()) % 50000 + 5000);
        var question = new Question
        {
            QuestionId = questionId,
            CenterId = centerId,
            SubjectId = subjectId,
            PrimaryTopicNodeId = topicNodeId,
            CreatedByTeacherId = teacherId,
            QuestionType = QuestionType.ShortAnswer,
            Difficulty = 2,
            QuestionText = "Câu hỏi kiểm tra",
            CorrectAnswer = "Đáp án",
            Solution = "Giải thích",
            GradingCriteria = new GradingCriteria { SchemaVersion = "1.0" },
            MaxScore = 1,
            EstimatedTimeSeconds = 120,
            ReasoningRequired = false,
            LanguageCode = "vi",
            Status = QuestionStatus.Active,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        ctx.Centers.Add(center);
        ctx.Users.Add(teacherUser);
        ctx.Teachers.Add(teacher);
        ctx.Subjects.Add(subject);
        ctx.KnowledgeNodes.Add(topicNode);
        ctx.Classes.Add(classEntity);
        ctx.Questions.Add(question);
        await ctx.SaveChangesAsync();

        return (center, teacherUser, teacher, subject, classEntity, question);
    }

    private static async Task<(User User, Student Student)> AddStudentToClassAsync(
        EduTwinDbContext ctx, Guid centerId, Guid classId)
    {
        var studentId = Guid.NewGuid();
        var now = DateTime.UtcNow;

        var studentUser = new User
        {
            UserId = studentId,
            CenterId = centerId,
            Username = "student-" + studentId.ToString()[..6],
            PasswordHash = "hash",
            RoleName = UserRole.Student,
            DisplayName = "Student Test",
            Status = UserStatus.Active,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        var student = new Student
        {
            StudentId = studentId,
            CenterId = centerId,
            FullName = "Student Test",
            GradeLevel = 12,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        var membership = new ClassStudent
        {
            CenterId = centerId,
            ClassId = classId,
            StudentId = studentId,
            Status = ClassStudentStatus.Active,
            JoinedAt = now
        };

        ctx.Users.Add(studentUser);
        ctx.Students.Add(student);
        ctx.ClassStudents.Add(membership);
        await ctx.SaveChangesAsync();

        return (studentUser, student);
    }

    private static async Task<Assignment> SeedDraftAssignmentAsync(
        EduTwinDbContext ctx, Guid centerId, Guid teacherId, Guid classId, ulong questionId,
        List<Guid>? selectedStudentIds = null)
    {
        var now = DateTime.UtcNow;
        var assignmentId = Guid.NewGuid();

        var assignment = new Assignment
        {
            AssignmentId = assignmentId,
            CenterId = centerId,
            ClassId = classId,
            CreatedByTeacherId = teacherId,
            Title = "Bài kiểm tra",
            Status = AssignmentStatus.Draft,
            IsDeleted = false,
            RowVersion = 1,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = teacherId,
            UpdatedBy = teacherId
        };

        var aq = new AssignmentQuestion
        {
            CenterId = centerId,
            AssignmentId = assignmentId,
            QuestionId = questionId,
            OrderIndex = 1,
            Points = 1m,
            CreatedAt = now
        };

        ctx.Assignments.Add(assignment);
        ctx.AssignmentQuestions.Add(aq);

        // Draft targets for SelectedStudents
        if (selectedStudentIds != null && selectedStudentIds.Count > 0)
        {
            foreach (var sid in selectedStudentIds)
            {
                ctx.AssignmentTargets.Add(new AssignmentTarget
                {
                    CenterId = centerId,
                    AssignmentId = assignmentId,
                    StudentId = sid,
                    TargetSource = TargetSource.SelectedStudents,
                    CreatedAt = now,
                    CreatedBy = teacherId
                });
            }
        }

        await ctx.SaveChangesAsync();
        return assignment;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TC-01: Publish toàn Class (WholeClass) — Happy path
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteAsync_WholeClass_PublishesSuccessfully()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var ctx = CreateContext(centerId);
        var (_, _, _, _, classEntity, question) = await SeedBaseAsync(ctx, centerId, teacherId);

        // Add 3 students to class
        var (_, student1) = await AddStudentToClassAsync(ctx, centerId, classEntity.ClassId);
        var (_, student2) = await AddStudentToClassAsync(ctx, centerId, classEntity.ClassId);
        var (_, student3) = await AddStudentToClassAsync(ctx, centerId, classEntity.ClassId);

        var assignment = await SeedDraftAssignmentAsync(ctx, centerId, teacherId, classEntity.ClassId, question.QuestionId);
        var sut = CreateSut(ctx);

        var result = await sut.ExecuteAsync(assignment.AssignmentId, new PublishAssignmentRequest { RowVersion = "1" });

        Assert.True(result.IsSuccess, $"Expected success but got: {result.ErrorCode}");
        Assert.NotNull(result.Data);
        Assert.Equal("Published", result.Data!.Status);
        Assert.Equal("2", result.Data.RowVersion); // RowVersion incremented
        Assert.Equal(3, result.Data.TargetStudentCount);
        Assert.Equal("WholeClass", result.Data.Targets[0].TargetSource);
        Assert.Equal(1, result.Data.QuestionCount);

        // Verify DB state
        var dbAssignment = await ctx.Assignments.FindAsync(assignment.AssignmentId);
        Assert.Equal(AssignmentStatus.Published, dbAssignment!.Status);
        Assert.NotNull(dbAssignment.PublishedAt);

        var targets = await ctx.AssignmentTargets.Where(t => t.AssignmentId == assignment.AssignmentId).ToListAsync();
        Assert.Equal(3, targets.Count);

        var progresses = await ctx.StudentAssignmentProgresses.Where(p => p.AssignmentId == assignment.AssignmentId).ToListAsync();
        Assert.Equal(3, progresses.Count);
        Assert.All(progresses, p =>
        {
            Assert.Equal(ProgressStatus.NotStarted, p.Status);
            Assert.Equal(0u, p.CompletedQuestionCount);
            Assert.Equal(1u, p.TotalQuestionCount);
        });
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TC-02: Publish subset SelectedStudents — Happy path
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteAsync_SelectedStudents_PublishesCorrectSubset()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var ctx = CreateContext(centerId);
        var (_, _, _, _, classEntity, question) = await SeedBaseAsync(ctx, centerId, teacherId);

        var (_, student1) = await AddStudentToClassAsync(ctx, centerId, classEntity.ClassId);
        var (_, student2) = await AddStudentToClassAsync(ctx, centerId, classEntity.ClassId);
        var (_, student3) = await AddStudentToClassAsync(ctx, centerId, classEntity.ClassId); // NOT in target

        // Only student1 and student2 are selected
        var assignment = await SeedDraftAssignmentAsync(ctx, centerId, teacherId, classEntity.ClassId, question.QuestionId,
            selectedStudentIds: new List<Guid> { student1.StudentId, student2.StudentId });

        var sut = CreateSut(ctx);
        var result = await sut.ExecuteAsync(assignment.AssignmentId, new PublishAssignmentRequest { RowVersion = "1" });

        Assert.True(result.IsSuccess, $"Expected success but got: {result.ErrorCode}");
        Assert.Equal(2, result.Data!.TargetStudentCount);
        Assert.All(result.Data.Targets, t => Assert.Equal("SelectedStudents", t.TargetSource));

        var progresses = await ctx.StudentAssignmentProgresses.Where(p => p.AssignmentId == assignment.AssignmentId).ToListAsync();
        Assert.Equal(2, progresses.Count);
        Assert.DoesNotContain(progresses, p => p.StudentId == student3.StudentId);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TC-03: Từ chối publish hai lần (Published không phải Draft)
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteAsync_AlreadyPublished_ReturnsInvalidStateTransition()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var ctx = CreateContext(centerId);
        var (_, _, _, _, classEntity, question) = await SeedBaseAsync(ctx, centerId, teacherId);
        await AddStudentToClassAsync(ctx, centerId, classEntity.ClassId);

        var assignment = await SeedDraftAssignmentAsync(ctx, centerId, teacherId, classEntity.ClassId, question.QuestionId);
        var sut = CreateSut(ctx);

        // First publish
        var firstResult = await sut.ExecuteAsync(assignment.AssignmentId, new PublishAssignmentRequest { RowVersion = "1" });
        Assert.True(firstResult.IsSuccess);

        // Second publish (now status = Published)
        var secondResult = await sut.ExecuteAsync(assignment.AssignmentId, new PublishAssignmentRequest { RowVersion = "2" });
        Assert.False(secondResult.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidStateTransition, secondResult.ErrorCode);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TC-04: rowVersion mismatch → CONCURRENCY_CONFLICT
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteAsync_RowVersionMismatch_ReturnsConcurrencyConflict()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var ctx = CreateContext(centerId);
        var (_, _, _, _, classEntity, question) = await SeedBaseAsync(ctx, centerId, teacherId);
        var assignment = await SeedDraftAssignmentAsync(ctx, centerId, teacherId, classEntity.ClassId, question.QuestionId);
        var sut = CreateSut(ctx);

        var result = await sut.ExecuteAsync(assignment.AssignmentId, new PublishAssignmentRequest { RowVersion = "99" });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, result.ErrorCode);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TC-05: rowVersion format không hợp lệ → VALIDATION_FAILED
    // ─────────────────────────────────────────────────────────────────────────
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("1.0")]
    [InlineData(" 1")]
    [InlineData("1 ")]
    [InlineData("+1")]
    [InlineData("abc")]
    public async Task ExecuteAsync_InvalidRowVersionFormat_ReturnsValidationFailed(string? rowVersion)
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var ctx = CreateContext(centerId);
        var sut = CreateSut(ctx);

        var result = await sut.ExecuteAsync(Guid.NewGuid(), new PublishAssignmentRequest { RowVersion = rowVersion! });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TC-06: Assignment không tồn tại hoặc sai tenant → NOT_FOUND
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteAsync_AssignmentNotFound_ReturnsNotFound()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var ctx = CreateContext(centerId);
        var sut = CreateSut(ctx);

        var result = await sut.ExecuteAsync(Guid.NewGuid(), new PublishAssignmentRequest { RowVersion = "1" });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TC-07: Teacher không phải owner → NOT_FOUND (fail-closed)
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteAsync_TeacherNotOwner_ReturnsNotFound()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var otherTeacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var ctx = CreateContext(centerId);
        // Seed assignment belonging to otherTeacher
        var (_, _, _, _, classEntity, question) = await SeedBaseAsync(ctx, centerId, otherTeacherId);

        // Add current teacher as user (no Teacher profile needed for ownership check)
        var now = DateTime.UtcNow;
        ctx.Users.Add(new User
        {
            UserId = teacherId,
            CenterId = centerId,
            Username = "teacher-own",
            PasswordHash = "hash",
            RoleName = UserRole.Teacher,
            DisplayName = "Other Teacher",
            Status = UserStatus.Active,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        });
        ctx.Teachers.Add(new Teacher
        {
            TeacherId = teacherId,
            CenterId = centerId,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        });
        await ctx.SaveChangesAsync();

        var assignment = await SeedDraftAssignmentAsync(ctx, centerId, otherTeacherId, classEntity.ClassId, question.QuestionId);
        var sut = CreateSut(ctx);

        var result = await sut.ExecuteAsync(assignment.AssignmentId, new PublishAssignmentRequest { RowVersion = "1" });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TC-08: Empty question list → VALIDATION_FAILED (no questions to publish)
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteAsync_NoQuestions_ReturnsValidationFailed()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var ctx = CreateContext(centerId);
        var (_, _, _, _, classEntity, _) = await SeedBaseAsync(ctx, centerId, teacherId);
        await AddStudentToClassAsync(ctx, centerId, classEntity.ClassId);

        // Create assignment with NO questions
        var now = DateTime.UtcNow;
        var assignmentId = Guid.NewGuid();
        var assignment = new Assignment
        {
            AssignmentId = assignmentId,
            CenterId = centerId,
            ClassId = classEntity.ClassId,
            CreatedByTeacherId = teacherId,
            Title = "Empty questions assignment",
            Status = AssignmentStatus.Draft,
            IsDeleted = false,
            RowVersion = 1,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = teacherId,
            UpdatedBy = teacherId
        };
        ctx.Assignments.Add(assignment);
        await ctx.SaveChangesAsync();

        var sut = CreateSut(ctx);
        var result = await sut.ExecuteAsync(assignmentId, new PublishAssignmentRequest { RowVersion = "1" });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TC-09: Empty target snapshot (no class members) → VALIDATION_FAILED
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteAsync_NoActiveClassMembers_ReturnsValidationFailed()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var ctx = CreateContext(centerId);
        var (_, _, _, _, classEntity, question) = await SeedBaseAsync(ctx, centerId, teacherId);

        // No students added to class
        var assignment = await SeedDraftAssignmentAsync(ctx, centerId, teacherId, classEntity.ClassId, question.QuestionId);
        var sut = CreateSut(ctx);

        var result = await sut.ExecuteAsync(assignment.AssignmentId, new PublishAssignmentRequest { RowVersion = "1" });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TC-10: SelectedStudents — student ngoài class → VALIDATION_FAILED
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteAsync_SelectedStudents_StudentNotInClass_ReturnsValidationFailed()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var ctx = CreateContext(centerId);
        var (_, _, _, _, classEntity, question) = await SeedBaseAsync(ctx, centerId, teacherId);

        // Add a student to class
        var (_, student) = await AddStudentToClassAsync(ctx, centerId, classEntity.ClassId);

        // Seed targets with a random student NOT in class
        var outsiderStudentId = Guid.NewGuid();
        var assignment = await SeedDraftAssignmentAsync(ctx, centerId, teacherId, classEntity.ClassId, question.QuestionId,
            selectedStudentIds: new List<Guid> { student.StudentId, outsiderStudentId });

        var sut = CreateSut(ctx);
        var result = await sut.ExecuteAsync(assignment.AssignmentId, new PublishAssignmentRequest { RowVersion = "1" });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TC-11: Question không Active → VALIDATION_FAILED
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteAsync_QuestionNotActive_ReturnsValidationFailed()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var ctx = CreateContext(centerId);
        var (_, _, _, _, classEntity, question) = await SeedBaseAsync(ctx, centerId, teacherId);
        await AddStudentToClassAsync(ctx, centerId, classEntity.ClassId);

        // Archive the question
        var dbQuestion = await ctx.Questions.FindAsync(question.QuestionId);
        dbQuestion!.Status = QuestionStatus.Archived;
        await ctx.SaveChangesAsync();

        var assignment = await SeedDraftAssignmentAsync(ctx, centerId, teacherId, classEntity.ClassId, question.QuestionId);
        var sut = CreateSut(ctx);

        var result = await sut.ExecuteAsync(assignment.AssignmentId, new PublishAssignmentRequest { RowVersion = "1" });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TC-12: Question khác Subject với Class → VALIDATION_FAILED
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteAsync_QuestionWrongSubject_ReturnsValidationFailed()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var ctx = CreateContext(centerId);
        var (_, _, _, subject, classEntity, _) = await SeedBaseAsync(ctx, centerId, teacherId);
        await AddStudentToClassAsync(ctx, centerId, classEntity.ClassId);

        // Create a question with a DIFFERENT subject
        var now = DateTime.UtcNow;
        var otherSubjectId = Guid.NewGuid();
        var otherSubject = new Subject
        {
            SubjectId = otherSubjectId,
            CenterId = centerId,
            SubjectCode = "ENG",
            SubjectName = "Tiếng Anh",
            IsActive = true,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };
        var topicNodeId = (ulong)(Math.Abs(otherSubjectId.GetHashCode()) % 100000 + 200000);
        var topicNode = new EduTwin.DAL.KnowledgeGraph.KnowledgeNode
        {
            NodeId = topicNodeId,
            CenterId = centerId,
            SubjectId = otherSubjectId,
            NodeType = EduTwin.Contracts.KnowledgeGraph.NodeType.Topic,
            NodeCode = "ENG.T1",
            NodeName = "English Topic",
            IsActive = true,
            IsDeleted = false,
            ExamImportance = 30,
            EstimatedLearningMinutes = 45,
            CreatedAt = now,
            UpdatedAt = now
        };
        var wrongSubjectQuestion = new Question
        {
            QuestionId = (ulong)(Math.Abs(otherSubjectId.GetHashCode()) % 50000 + 300000),
            CenterId = centerId,
            SubjectId = otherSubjectId,
            PrimaryTopicNodeId = topicNodeId,
            CreatedByTeacherId = teacherId,
            QuestionType = QuestionType.ShortAnswer,
            Difficulty = 1,
            QuestionText = "English question",
            CorrectAnswer = "Answer",
            Solution = "Solution",
            GradingCriteria = new GradingCriteria { SchemaVersion = "1.0" },
            MaxScore = 1,
            EstimatedTimeSeconds = 60,
            ReasoningRequired = false,
            LanguageCode = "en",
            Status = QuestionStatus.Active,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };
        ctx.Subjects.Add(otherSubject);
        ctx.KnowledgeNodes.Add(topicNode);
        ctx.Questions.Add(wrongSubjectQuestion);
        await ctx.SaveChangesAsync();

        var assignment = await SeedDraftAssignmentAsync(ctx, centerId, teacherId, classEntity.ClassId, wrongSubjectQuestion.QuestionId);
        var sut = CreateSut(ctx);

        var result = await sut.ExecuteAsync(assignment.AssignmentId, new PublishAssignmentRequest { RowVersion = "1" });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TC-13: CenterManager có thể publish (không cần là Teacher owner)
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteAsync_CenterManagerPublishes_Success()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        // CenterManager is the actor (not the Teacher owner)
        SetupTenant(centerId, managerId, nameof(UserRole.CenterManager));

        var ctx = CreateContext(centerId);
        var (_, _, _, _, classEntity, question) = await SeedBaseAsync(ctx, centerId, teacherId);
        await AddStudentToClassAsync(ctx, centerId, classEntity.ClassId);

        var assignment = await SeedDraftAssignmentAsync(ctx, centerId, teacherId, classEntity.ClassId, question.QuestionId);
        var sut = CreateSut(ctx);

        var result = await sut.ExecuteAsync(assignment.AssignmentId, new PublishAssignmentRequest { RowVersion = "1" });

        Assert.True(result.IsSuccess, $"Expected success but got: {result.ErrorCode}");
        Assert.Equal("Published", result.Data!.Status);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TC-14: Tenant isolation — assignment thuộc Center khác → NOT_FOUND
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteAsync_CrossTenantAssignment_ReturnsNotFound()
    {
        var centerA = Guid.NewGuid();
        var centerB = Guid.NewGuid();
        var teacherA = Guid.NewGuid();
        var teacherB = Guid.NewGuid();

        // Actor is from Center A
        SetupTenant(centerA, teacherA, nameof(UserRole.Teacher));

        // Seed assignment in Center B
        var ctxB = CreateContext(centerB);
        var (_, _, _, _, classBEntity, questionB) = await SeedBaseAsync(ctxB, centerB, teacherB);
        await AddStudentToClassAsync(ctxB, centerB, classBEntity.ClassId);
        var assignmentB = await SeedDraftAssignmentAsync(ctxB, centerB, teacherB, classBEntity.ClassId, questionB.QuestionId);

        // Actor from Center A tries to publish Center B's assignment
        var ctxA = CreateContext(centerA);
        var sut = CreateSut(ctxA);

        var result = await sut.ExecuteAsync(assignmentB.AssignmentId, new PublishAssignmentRequest { RowVersion = "1" });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TC-15: Question order preserved from Draft (không thay đổi ordering khi publish)
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteAsync_QuestionOrderPreservedFromDraft()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var ctx = CreateContext(centerId);
        var (_, _, _, _, classEntity, q1) = await SeedBaseAsync(ctx, centerId, teacherId);
        await AddStudentToClassAsync(ctx, centerId, classEntity.ClassId);

        // Create a second question
        var now = DateTime.UtcNow;
        var q2Id = (ulong)(Math.Abs(centerId.GetHashCode()) % 50000 + 90000);
        ctx.Questions.Add(new Question
        {
            QuestionId = q2Id,
            CenterId = centerId,
            SubjectId = q1.SubjectId,
            PrimaryTopicNodeId = q1.PrimaryTopicNodeId,
            CreatedByTeacherId = teacherId,
            QuestionType = QuestionType.ShortAnswer,
            Difficulty = 3,
            QuestionText = "Câu 2",
            CorrectAnswer = "Đáp án 2",
            Solution = "Giải 2",
            GradingCriteria = new GradingCriteria { SchemaVersion = "1.0" },
            MaxScore = 1,
            EstimatedTimeSeconds = 180,
            ReasoningRequired = false,
            LanguageCode = "vi",
            Status = QuestionStatus.Active,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        });

        var assignmentId = Guid.NewGuid();
        var assignment = new Assignment
        {
            AssignmentId = assignmentId,
            CenterId = centerId,
            ClassId = classEntity.ClassId,
            CreatedByTeacherId = teacherId,
            Title = "Multi-question",
            Status = AssignmentStatus.Draft,
            IsDeleted = false,
            RowVersion = 1,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = teacherId,
            UpdatedBy = teacherId
        };
        // Intentional order: q1 at index 1, q2 at index 2
        ctx.Assignments.Add(assignment);
        ctx.AssignmentQuestions.AddRange(
            new AssignmentQuestion { CenterId = centerId, AssignmentId = assignmentId, QuestionId = q1.QuestionId, OrderIndex = 1, Points = 1m, CreatedAt = now },
            new AssignmentQuestion { CenterId = centerId, AssignmentId = assignmentId, QuestionId = q2Id, OrderIndex = 2, Points = 2m, CreatedAt = now }
        );
        await ctx.SaveChangesAsync();

        var sut = CreateSut(ctx);
        var result = await sut.ExecuteAsync(assignmentId, new PublishAssignmentRequest { RowVersion = "1" });

        Assert.True(result.IsSuccess, $"Expected success but got: {result.ErrorCode}");
        Assert.Equal(2, result.Data!.QuestionCount);

        // Order must be preserved
        Assert.Equal(q1.QuestionId.ToString(CultureInfo.InvariantCulture), result.Data.Questions[0].QuestionId);
        Assert.Equal(1u, result.Data.Questions[0].OrderIndex);
        Assert.Equal(q2Id.ToString(CultureInfo.InvariantCulture), result.Data.Questions[1].QuestionId);
        Assert.Equal(2u, result.Data.Questions[1].OrderIndex);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TC-16: Student bị Removed từ Class không được tính vào WholeClass target
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteAsync_WholeClass_ExcludesRemovedMembers()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var ctx = CreateContext(centerId);
        var (_, _, _, _, classEntity, question) = await SeedBaseAsync(ctx, centerId, teacherId);

        var (_, activeStudent) = await AddStudentToClassAsync(ctx, centerId, classEntity.ClassId);

        // Add a student then remove them
        var (_, removedStudent) = await AddStudentToClassAsync(ctx, centerId, classEntity.ClassId);
        var membership = await ctx.ClassStudents
            .FirstAsync(cs => cs.ClassId == classEntity.ClassId && cs.StudentId == removedStudent.StudentId);
        membership.Status = ClassStudentStatus.Removed;
        await ctx.SaveChangesAsync();

        var assignment = await SeedDraftAssignmentAsync(ctx, centerId, teacherId, classEntity.ClassId, question.QuestionId);
        var sut = CreateSut(ctx);

        var result = await sut.ExecuteAsync(assignment.AssignmentId, new PublishAssignmentRequest { RowVersion = "1" });

        Assert.True(result.IsSuccess, $"Expected success but got: {result.ErrorCode}");
        Assert.Equal(1, result.Data!.TargetStudentCount);
        Assert.DoesNotContain(result.Data.Targets, t => t.StudentId == removedStudent.StudentId.ToString());
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TC-17: Unresolved tenant context → NOT_FOUND
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteAsync_UnresolvedTenant_ReturnsNotFound()
    {
        _tenantMock.SetupGet(x => x.IsResolved).Returns(false);

        var ctx = CreateContext(Guid.NewGuid());
        var sut = CreateSut(ctx);

        var result = await sut.ExecuteAsync(Guid.NewGuid(), new PublishAssignmentRequest { RowVersion = "1" });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TC-18: Student role không được phép → NOT_FOUND (fail-closed)
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteAsync_StudentRole_ReturnsNotFound()
    {
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        SetupTenant(centerId, studentId, nameof(UserRole.Student));

        var ctx = CreateContext(centerId);
        var sut = CreateSut(ctx);

        var result = await sut.ExecuteAsync(Guid.NewGuid(), new PublishAssignmentRequest { RowVersion = "1" });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TC-19: Published status có published_at và RowVersion tăng đúng
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteAsync_Success_PublishedAtSetAndRowVersionIncremented()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var ctx = CreateContext(centerId);
        var (_, _, _, _, classEntity, question) = await SeedBaseAsync(ctx, centerId, teacherId);
        await AddStudentToClassAsync(ctx, centerId, classEntity.ClassId);

        var assignment = await SeedDraftAssignmentAsync(ctx, centerId, teacherId, classEntity.ClassId, question.QuestionId);
        var sut = CreateSut(ctx);

        var result = await sut.ExecuteAsync(assignment.AssignmentId, new PublishAssignmentRequest { RowVersion = "1" });

        Assert.True(result.IsSuccess);
        Assert.Equal("2", result.Data!.RowVersion);

        var dbAssignment = await ctx.Assignments.FindAsync(assignment.AssignmentId);
        Assert.NotNull(dbAssignment!.PublishedAt);
        Assert.Equal(_fixedUtcNow.UtcDateTime, dbAssignment.PublishedAt);
        Assert.Equal(2UL, dbAssignment.RowVersion);
    }

    // ─────────────────────────────────────────────────────────────────────────
    // TC-20: Progress TotalQuestionCount = snapshot số question khi publish
    // ─────────────────────────────────────────────────────────────────────────
    [Fact]
    public async Task ExecuteAsync_TotalQuestionCountMatchesSnapshot()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var ctx = CreateContext(centerId);
        var (_, _, _, _, classEntity, q1) = await SeedBaseAsync(ctx, centerId, teacherId);
        await AddStudentToClassAsync(ctx, centerId, classEntity.ClassId);

        // 2 questions
        var now = DateTime.UtcNow;
        var q2Id = (ulong)(Math.Abs(centerId.GetHashCode()) % 50000 + 700000);
        ctx.Questions.Add(new Question
        {
            QuestionId = q2Id,
            CenterId = centerId,
            SubjectId = q1.SubjectId,
            PrimaryTopicNodeId = q1.PrimaryTopicNodeId,
            CreatedByTeacherId = teacherId,
            QuestionType = QuestionType.MultipleChoice,
            Difficulty = 2,
            QuestionText = "MC Câu 2",
            CorrectAnswer = "A",
            Solution = "Sol",
            GradingCriteria = new GradingCriteria { SchemaVersion = "1.0" },
            MaxScore = 1,
            EstimatedTimeSeconds = 90,
            ReasoningRequired = false,
            LanguageCode = "vi",
            Status = QuestionStatus.Active,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        });

        var assignmentId = Guid.NewGuid();
        ctx.Assignments.Add(new Assignment
        {
            AssignmentId = assignmentId,
            CenterId = centerId,
            ClassId = classEntity.ClassId,
            CreatedByTeacherId = teacherId,
            Title = "2 Questions",
            Status = AssignmentStatus.Draft,
            IsDeleted = false,
            RowVersion = 1,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = teacherId,
            UpdatedBy = teacherId
        });
        ctx.AssignmentQuestions.AddRange(
            new AssignmentQuestion { CenterId = centerId, AssignmentId = assignmentId, QuestionId = q1.QuestionId, OrderIndex = 1, Points = 1m, CreatedAt = now },
            new AssignmentQuestion { CenterId = centerId, AssignmentId = assignmentId, QuestionId = q2Id, OrderIndex = 2, Points = 1m, CreatedAt = now }
        );
        await ctx.SaveChangesAsync();

        var sut = CreateSut(ctx);
        var result = await sut.ExecuteAsync(assignmentId, new PublishAssignmentRequest { RowVersion = "1" });

        Assert.True(result.IsSuccess);

        var progresses = await ctx.StudentAssignmentProgresses.Where(p => p.AssignmentId == assignmentId).ToListAsync();
        Assert.All(progresses, p => Assert.Equal(2u, p.TotalQuestionCount));
    }
}
