using System;
using System.Collections.Generic;
using System.Globalization;
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

public class UpdateAssignmentUseCaseTests
{
    private readonly DbContextOptions<EduTwinDbContext> _options;
    private readonly Mock<ITenantContext> _tenantMock;
    private readonly Mock<TimeProvider> _timeProviderMock;

    public UpdateAssignmentUseCaseTests()
    {
        _options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _tenantMock = new Mock<ITenantContext>();

        _timeProviderMock = new Mock<TimeProvider>();
        _timeProviderMock.Setup(x => x.GetUtcNow())
            .Returns(new DateTimeOffset(2026, 7, 30, 12, 0, 0, TimeSpan.Zero));
    }

    private EduTwinDbContext CreateContext(Guid centerId)
    {
        var tenantAccessorMock = new Mock<ITenantIdAccessor>();
        tenantAccessorMock.Setup(x => x.CenterId).Returns(centerId);
        return new EduTwinDbContext(_options, tenantAccessorMock.Object);
    }

    private UpdateAssignmentUseCase CreateSut(EduTwinDbContext ctx) =>
        new(ctx, _tenantMock.Object, _timeProviderMock.Object);

    private void SetupTenant(Guid centerId, Guid userId, string role)
    {
        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(userId);
        _tenantMock.SetupGet(x => x.Role).Returns(role);
    }

    private async Task<(Class ClassEntity, Assignment Assignment, Question Question)>
        SeedDraftAssignmentAsync(EduTwinDbContext ctx, Guid centerId, Guid teacherId)
    {
        var now = DateTime.UtcNow;
        var subjectId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var topicNodeId = (ulong)(now.Ticks % 80000 + 2000);
        var questionId = (ulong)(now.Ticks % 40000 + 7000);

        ctx.Centers.Add(new Center
        {
            CenterId = centerId, CenterCode = "C1", CenterName = "C", Status = CenterStatus.Active,
            Timezone = "UTC", IsDeleted = false, CreatedAt = now, UpdatedAt = now
        });
        ctx.Users.Add(new User
        {
            UserId = teacherId, CenterId = centerId, Username = "t1", PasswordHash = "h",
            RoleName = UserRole.Teacher, DisplayName = "T1", Status = UserStatus.Active,
            IsDeleted = false, CreatedAt = now, UpdatedAt = now
        });
        ctx.Teachers.Add(new Teacher
        {
            TeacherId = teacherId, CenterId = centerId, IsDeleted = false, CreatedAt = now, UpdatedAt = now
        });
        ctx.Subjects.Add(new Subject
        {
            SubjectId = subjectId, CenterId = centerId, SubjectCode = "MATH", SubjectName = "Toán",
            IsActive = true, IsDeleted = false, CreatedAt = now, UpdatedAt = now
        });
        ctx.KnowledgeNodes.Add(new EduTwin.DAL.KnowledgeGraph.KnowledgeNode
        {
            NodeId = topicNodeId, CenterId = centerId, SubjectId = subjectId,
            NodeType = EduTwin.Contracts.KnowledgeGraph.NodeType.Topic, NodeCode = "M.T1",
            NodeName = "Topic", IsActive = true, IsDeleted = false, ExamImportance = 50,
            EstimatedLearningMinutes = 60, CreatedAt = now, UpdatedAt = now
        });

        var classEntity = new Class
        {
            ClassId = classId, CenterId = centerId, TeacherId = teacherId, SubjectId = subjectId,
            ClassName = "Class A", AcademicYear = "2026-2027", Status = ClassStatus.Active,
            IsDeleted = false, CreatedAt = now, UpdatedAt = now
        };

        var question = new Question
        {
            QuestionId = questionId, CenterId = centerId, SubjectId = subjectId,
            PrimaryTopicNodeId = topicNodeId, CreatedByTeacherId = teacherId,
            QuestionType = QuestionType.ShortAnswer, Difficulty = 1,
            QuestionText = "Q1", CorrectAnswer = "A", Solution = "S",
            GradingCriteria = new EduTwin.Contracts.CurriculumAndQuestions.GradingCriteria { SchemaVersion = "1.0" },
            MaxScore = 1, EstimatedTimeSeconds = 60, ReasoningRequired = false,
            LanguageCode = "vi", Status = QuestionStatus.Active,
            IsDeleted = false, CreatedAt = now, UpdatedAt = now
        };

        var assignment = new Assignment
        {
            AssignmentId = assignmentId, CenterId = centerId, ClassId = classId,
            CreatedByTeacherId = teacherId, Title = "Draft Assignment",
            Status = AssignmentStatus.Draft, IsDeleted = false,
            RowVersion = 1, CreatedAt = now, UpdatedAt = now
        };

        ctx.Classes.Add(classEntity);
        ctx.Questions.Add(question);
        ctx.Assignments.Add(assignment);
        await ctx.SaveChangesAsync();

        return (classEntity, assignment, question);
    }

    [Fact]
    public async Task ExecuteAsync_ValidDraftUpdate_ReturnsUpdatedDto()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var ctx = CreateContext(centerId);
        var (_, assignment, question) = await SeedDraftAssignmentAsync(ctx, centerId, teacherId);

        var sut = CreateSut(ctx);

        var result = await sut.ExecuteAsync(assignment.AssignmentId, new UpdateAssignmentRequest
        {
            Title = "Updated Title",
            QuestionIds = new List<string> { question.QuestionId.ToString(CultureInfo.InvariantCulture) },
            RowVersion = "1"
        });

        Assert.True(result.IsSuccess, $"Expected success but got: {result.ErrorCode}");
        Assert.Equal("Updated Title", result.Data!.Title);
        Assert.Equal("2", result.Data.RowVersion); // incremented by DbContext
    }

    [Fact]
    public async Task ExecuteAsync_PublishedAssignment_ReturnsInvalidStateTransition()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var ctx = CreateContext(centerId);
        var (_, assignment, _) = await SeedDraftAssignmentAsync(ctx, centerId, teacherId);

        // Manually set assignment to Published
        var tracked = await ctx.Assignments.FindAsync(assignment.AssignmentId);
        tracked!.Status = AssignmentStatus.Published;
        tracked.PublishedAt = DateTime.UtcNow;
        await ctx.SaveChangesAsync();

        var sut = CreateSut(ctx);

        var result = await sut.ExecuteAsync(assignment.AssignmentId, new UpdateAssignmentRequest
        {
            Title = "Should fail",
            RowVersion = tracked.RowVersion.ToString(CultureInfo.InvariantCulture)
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidStateTransition, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_StaleRowVersion_ReturnsConcurrencyConflict()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var ctx = CreateContext(centerId);
        var (_, assignment, _) = await SeedDraftAssignmentAsync(ctx, centerId, teacherId);

        var sut = CreateSut(ctx);

        // Send stale rowVersion (2, but DB has 1)
        var result = await sut.ExecuteAsync(assignment.AssignmentId, new UpdateAssignmentRequest
        {
            Title = "Conflict",
            RowVersion = "2"  // stale
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_MissingRowVersion_ReturnsValidationFailed()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var ctx = CreateContext(centerId);
        var (_, assignment, _) = await SeedDraftAssignmentAsync(ctx, centerId, teacherId);

        var sut = CreateSut(ctx);

        var result = await sut.ExecuteAsync(assignment.AssignmentId, new UpdateAssignmentRequest
        {
            Title = "Test",
            RowVersion = ""  // missing
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_QuestionNotActiveInUpdate_ReturnsValidationFailed()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        SetupTenant(centerId, teacherId, nameof(UserRole.Teacher));

        var ctx = CreateContext(centerId);
        var (_, assignment, question) = await SeedDraftAssignmentAsync(ctx, centerId, teacherId);

        // Archive question
        var q = await ctx.Questions.FindAsync(question.QuestionId);
        q!.Status = QuestionStatus.Archived;
        await ctx.SaveChangesAsync();

        var sut = CreateSut(ctx);

        var result = await sut.ExecuteAsync(assignment.AssignmentId, new UpdateAssignmentRequest
        {
            QuestionIds = new List<string> { question.QuestionId.ToString(CultureInfo.InvariantCulture) },
            RowVersion = "1"
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_TeacherAccessingOtherTeacherAssignment_ReturnsNotFound()
    {
        var centerId = Guid.NewGuid();
        var teacher1Id = Guid.NewGuid();
        var teacher2Id = Guid.NewGuid();
        // teacher2 is the tenant but assignment belongs to teacher1's class
        SetupTenant(centerId, teacher2Id, nameof(UserRole.Teacher));

        var ctx = CreateContext(centerId);
        var (_, assignment, _) = await SeedDraftAssignmentAsync(ctx, centerId, teacher1Id);

        // Seed teacher2 user so tenant is valid
        var now = DateTime.UtcNow;
        ctx.Users.Add(new User
        {
            UserId = teacher2Id, CenterId = centerId, Username = "t2", PasswordHash = "h",
            RoleName = UserRole.Teacher, DisplayName = "T2", Status = UserStatus.Active,
            IsDeleted = false, CreatedAt = now, UpdatedAt = now
        });
        ctx.Teachers.Add(new Teacher
        {
            TeacherId = teacher2Id, CenterId = centerId, IsDeleted = false, CreatedAt = now, UpdatedAt = now
        });
        await ctx.SaveChangesAsync();

        var sut = CreateSut(ctx);

        var result = await sut.ExecuteAsync(assignment.AssignmentId, new UpdateAssignmentRequest
        {
            Title = "Intrusion attempt",
            RowVersion = "1"
        });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }
}
