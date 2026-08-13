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

public class CreateAssignmentUseCaseTests
{
    private readonly DbContextOptions<EduTwinDbContext> _options;
    private readonly Mock<ITenantContext> _tenantMock;
    private readonly Mock<TimeProvider> _timeProviderMock;

    public CreateAssignmentUseCaseTests()
    {
        _options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _tenantMock = new Mock<ITenantContext>();

        _timeProviderMock = new Mock<TimeProvider>();
        var utcNow = new DateTimeOffset(2026, 7, 30, 10, 0, 0, TimeSpan.Zero);
        _timeProviderMock.Setup(x => x.GetUtcNow()).Returns(utcNow);
    }

    private EduTwinDbContext CreateContext(Guid centerId)
    {
        var tenantAccessorMock = new Mock<ITenantIdAccessor>();
        tenantAccessorMock.Setup(x => x.CenterId).Returns(centerId);
        return new EduTwinDbContext(_options, tenantAccessorMock.Object);
    }

    private CreateAssignmentUseCase CreateSut(EduTwinDbContext dbContext) =>
        new(dbContext, _tenantMock.Object, _timeProviderMock.Object);

    private static async Task<Student> AddStudentToClassAsync(
        EduTwinDbContext context,
        Guid centerId,
        Guid classId)
    {
        var studentId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        context.Users.Add(new User
        {
            UserId = studentId,
            CenterId = centerId,
            Username = "student-" + studentId.ToString()[..6],
            PasswordHash = "hash",
            RoleName = UserRole.Student,
            DisplayName = "Selected Student",
            Status = UserStatus.Active,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        });
        var student = new Student
        {
            StudentId = studentId,
            CenterId = centerId,
            FullName = "Selected Student",
            GradeLevel = 12,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };
        context.Students.Add(student);
        context.ClassStudents.Add(new ClassStudent
        {
            CenterId = centerId,
            ClassId = classId,
            StudentId = studentId,
            Status = ClassStudentStatus.Active,
            JoinedAt = now
        });
        await context.SaveChangesAsync();
        return student;
    }

    private void SetupTenant(Guid centerId, Guid userId, string role)
    {
        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(userId);
        _tenantMock.SetupGet(x => x.Role).Returns(role);
    }

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

        var topicNodeId = (ulong)(now.Ticks % 100000 + 1000);
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

        var questionId = (ulong)(now.Ticks % 50000 + 5000);
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
            GradingCriteria = new EduTwin.Contracts.CurriculumAndQuestions.GradingCriteria { SchemaVersion = "1.0" },
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

    [Fact]
    public async Task ExecuteAsync_TeacherOwner_ValidWholeClass_ReturnsSuccess()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var ctx = CreateContext(centerId);
        var (_, _, _, _, classEntity, question) = await SeedBaseAsync(ctx, centerId, teacherId);

        var sut = CreateSut(ctx);

        var result = await sut.ExecuteAsync(new CreateAssignmentRequest
        {
            ClassId = classEntity.ClassId,
            Title = "Bài kiểm tra tuần 1",
            TargetMode = "WholeClass",
            QuestionIds = new List<string> { question.QuestionId.ToString(CultureInfo.InvariantCulture) }
        });

        Assert.True(result.IsSuccess, $"Expected success but got error: {result.ErrorCode}");
        Assert.NotNull(result.Data);
        Assert.Equal("Draft", result.Data!.Status);
        Assert.Equal("1", result.Data.RowVersion);
        Assert.Single(result.Data.Questions);
    }

    [Fact]
    public async Task ExecuteAsync_TeacherOwner_ClassNotBelongToTeacher_ReturnsNotFound()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var otherTeacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var ctx = CreateContext(centerId);
        // Seed class belonging to otherTeacher
        await SeedBaseAsync(ctx, centerId, otherTeacherId);

        var now = DateTime.UtcNow;
        var otherTeacherUser = new User
        {
            UserId = teacherId,
            CenterId = centerId,
            Username = "teacher-own",
            PasswordHash = "hash",
            RoleName = UserRole.Teacher,
            DisplayName = "Teacher Own",
            Status = UserStatus.Active,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };
        var otherTeacher = new Teacher
        {
            TeacherId = teacherId,
            CenterId = centerId,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };
        ctx.Users.Add(otherTeacherUser);
        ctx.Teachers.Add(otherTeacher);
        await ctx.SaveChangesAsync();

        var otherClass = ctx.Classes.AsNoTracking().First();

        var sut = CreateSut(ctx);

        var result = await sut.ExecuteAsync(new CreateAssignmentRequest
        {
            ClassId = otherClass.ClassId,   // class belongs to otherTeacherId
            Title = "Test",
            TargetMode = "WholeClass",
            QuestionIds = new List<string>()
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_QuestionNotActive_ReturnsValidationFailed()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var ctx = CreateContext(centerId);
        var (_, _, _, _, classEntity, question) = await SeedBaseAsync(ctx, centerId, teacherId);

        // Archive the question so it is not Active
        var q = await ctx.Questions.FindAsync(question.QuestionId);
        q!.Status = QuestionStatus.Draft;
        await ctx.SaveChangesAsync();

        var sut = CreateSut(ctx);

        var result = await sut.ExecuteAsync(new CreateAssignmentRequest
        {
            ClassId = classEntity.ClassId,
            Title = "Test",
            TargetMode = "WholeClass",
            QuestionIds = new List<string> { question.QuestionId.ToString(CultureInfo.InvariantCulture) }
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_QuestionWrongSubject_ReturnsValidationFailed()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var ctx = CreateContext(centerId);
        var (_, _, _, _, classEntity, _) = await SeedBaseAsync(ctx, centerId, teacherId);

        // Create a question belonging to a DIFFERENT subject
        var otherSubjectId = Guid.NewGuid();
        var now = DateTime.UtcNow;
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
        var otherTopicId = (ulong)(now.Ticks % 30000 + 999);
        var otherNode = new EduTwin.DAL.KnowledgeGraph.KnowledgeNode
        {
            NodeId = otherTopicId,
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
        var wrongQId = (ulong)(now.Ticks % 20000 + 9999);
        var wrongQuestion = new Question
        {
            QuestionId = wrongQId,
            CenterId = centerId,
            SubjectId = otherSubjectId,  // different subject!
            PrimaryTopicNodeId = otherTopicId,
            CreatedByTeacherId = teacherId,
            QuestionType = QuestionType.ShortAnswer,
            Difficulty = 1,
            QuestionText = "English question",
            CorrectAnswer = "Answer",
            Solution = "Solution",
            GradingCriteria = new EduTwin.Contracts.CurriculumAndQuestions.GradingCriteria { SchemaVersion = "1.0" },
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
        ctx.KnowledgeNodes.Add(otherNode);
        ctx.Questions.Add(wrongQuestion);
        await ctx.SaveChangesAsync();

        var sut = CreateSut(ctx);

        var result = await sut.ExecuteAsync(new CreateAssignmentRequest
        {
            ClassId = classEntity.ClassId,
            Title = "Test",
            TargetMode = "WholeClass",
            QuestionIds = new List<string> { wrongQId.ToString(CultureInfo.InvariantCulture) }
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_DuplicateQuestionIds_ReturnsValidationFailed()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var ctx = CreateContext(centerId);
        var (_, _, _, _, classEntity, question) = await SeedBaseAsync(ctx, centerId, teacherId);

        var sut = CreateSut(ctx);
        var qIdStr = question.QuestionId.ToString(CultureInfo.InvariantCulture);

        var result = await sut.ExecuteAsync(new CreateAssignmentRequest
        {
            ClassId = classEntity.ClassId,
            Title = "Test",
            TargetMode = "WholeClass",
            QuestionIds = new List<string> { qIdStr, qIdStr }   // duplicate!
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidTargetMode_ReturnsValidationFailed()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var ctx = CreateContext(centerId);
        var (_, _, _, _, classEntity, _) = await SeedBaseAsync(ctx, centerId, teacherId);

        var sut = CreateSut(ctx);

        var result = await sut.ExecuteAsync(new CreateAssignmentRequest
        {
            ClassId = classEntity.ClassId,
            Title = "Test",
            TargetMode = "InvalidMode",
            QuestionIds = new List<string>()
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_SelectedStudentsMode_ActiveMember_CreatesDraftTarget()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        await using var ctx = CreateContext(centerId);
        var (_, _, _, _, classEntity, question) = await SeedBaseAsync(ctx, centerId, teacherId);
        var student = await AddStudentToClassAsync(ctx, centerId, classEntity.ClassId);

        var result = await CreateSut(ctx).ExecuteAsync(new CreateAssignmentRequest
        {
            ClassId = classEntity.ClassId,
            Title = "Giao riêng cho học sinh",
            TargetMode = "SelectedStudents",
            QuestionIds = new List<string>
            {
                question.QuestionId.ToString(CultureInfo.InvariantCulture)
            },
            StudentIds = new List<string> { student.StudentId.ToString() }
        });

        Assert.True(result.IsSuccess, $"Expected success but got: {result.ErrorCode}");
        Assert.NotNull(result.Data);
        Assert.Equal("Draft", result.Data!.Status);
        Assert.Equal(1, result.Data.TargetStudentCount);
        var target = Assert.Single(result.Data.Targets);
        Assert.Equal(student.StudentId.ToString("D").ToLowerInvariant(), target.StudentId);
        Assert.Equal("SelectedStudents", target.TargetSource);

        var persistedTarget = await ctx.AssignmentTargets.SingleAsync();
        Assert.Equal(student.StudentId, persistedTarget.StudentId);
        Assert.Equal(TargetSource.SelectedStudents, persistedTarget.TargetSource);
    }

    [Fact]
    public async Task ExecuteAsync_SelectedStudentsMode_StudentNotInClass_ReturnsValidationFailed()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var ctx = CreateContext(centerId);
        var (_, _, _, _, classEntity, question) = await SeedBaseAsync(ctx, centerId, teacherId);

        var sut = CreateSut(ctx);
        var randomStudentId = Guid.NewGuid();  // not a member of the class

        var result = await sut.ExecuteAsync(new CreateAssignmentRequest
        {
            ClassId = classEntity.ClassId,
            Title = "Test",
            TargetMode = "SelectedStudents",
            QuestionIds = new List<string> { question.QuestionId.ToString(CultureInfo.InvariantCulture) },
            StudentIds = new List<string> { randomStudentId.ToString() }
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_UnresolvedTenant_ReturnsNotFound()
    {
        _tenantMock.SetupGet(x => x.IsResolved).Returns(false);

        var centerId = Guid.NewGuid();
        var ctx = CreateContext(centerId);
        var sut = CreateSut(ctx);

        var result = await sut.ExecuteAsync(new CreateAssignmentRequest
        {
            ClassId = Guid.NewGuid(),
            Title = "Test",
            TargetMode = "WholeClass",
            QuestionIds = new List<string>()
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyTitle_ReturnsValidationFailed()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var ctx = CreateContext(centerId);
        var (_, _, _, _, classEntity, _) = await SeedBaseAsync(ctx, centerId, teacherId);

        var sut = CreateSut(ctx);

        var result = await sut.ExecuteAsync(new CreateAssignmentRequest
        {
            ClassId = classEntity.ClassId,
            Title = "",
            TargetMode = "WholeClass",
            QuestionIds = new List<string>()
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_CenterManager_CanCreateForAnyClassInCenter()
    {
        var centerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, managerId, nameof(UserRole.CenterManager));

        var ctx = CreateContext(centerId);
        var (_, _, _, _, classEntity, question) = await SeedBaseAsync(ctx, centerId, teacherId);

        var sut = CreateSut(ctx);

        var result = await sut.ExecuteAsync(new CreateAssignmentRequest
        {
            ClassId = classEntity.ClassId,
            Title = "Manager Assignment",
            TargetMode = "WholeClass",
            QuestionIds = new List<string> { question.QuestionId.ToString(CultureInfo.InvariantCulture) }
        });

        Assert.True(result.IsSuccess, $"Expected success but got: {result.ErrorCode}");
        Assert.NotNull(result.Data);
        Assert.Equal("Draft", result.Data!.Status);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    public async Task ExecuteAsync_DueAtNotInFuture_ReturnsValidationFailed(int offsetMinutes)
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));
        var ctx = CreateContext(centerId);

        var result = await CreateSut(ctx).ExecuteAsync(new CreateAssignmentRequest
        {
            ClassId = Guid.NewGuid(),
            Title = "Deadline invalid",
            DueAt = new DateTime(2026, 7, 30, 10, 0, 0, DateTimeKind.Utc).AddMinutes(offsetMinutes),
            TargetMode = "WholeClass",
            QuestionIds = new List<string>()
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }
}
