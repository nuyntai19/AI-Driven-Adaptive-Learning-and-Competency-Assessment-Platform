using System;
using System.Collections.Generic;
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

public class AssignmentGradeAndTargetTests
{
    private readonly DbContextOptions<EduTwinDbContext> _options;
    private readonly Mock<ITenantContext> _tenantMock;
    private readonly Mock<TimeProvider> _timeProviderMock;
    private readonly DateTimeOffset _fixedUtcNow;
    private readonly Guid _centerId = Guid.NewGuid();
    private readonly Guid _teacherUserId = Guid.NewGuid();

    public AssignmentGradeAndTargetTests()
    {
        _options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _tenantMock = new Mock<ITenantContext>();
        _fixedUtcNow = new DateTimeOffset(2026, 8, 1, 10, 0, 0, TimeSpan.Zero);
        _timeProviderMock = new Mock<TimeProvider>();
        _timeProviderMock.Setup(x => x.GetUtcNow()).Returns(_fixedUtcNow);

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(_centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(_teacherUserId);
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.Teacher));
    }

    private EduTwinDbContext CreateContext()
    {
        var tenantAccessorMock = new Mock<ITenantIdAccessor>();
        tenantAccessorMock.Setup(x => x.CenterId).Returns(_centerId);
        return new EduTwinDbContext(_options, tenantAccessorMock.Object);
    }

    private async Task<(Subject Subject, Class Class, Student Student1, Student Student2, Question QuestionGrade10, Question QuestionGrade11)> SeedBaseAsync(
        EduTwinDbContext ctx,
        byte? classGrade = 10,
        ClassStatus classStatus = ClassStatus.Active)
    {
        var subjectId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var student1Id = Guid.NewGuid();
        var student2Id = Guid.NewGuid();
        var q10Id = 1001UL;
        var q11Id = 1002UL;
        var now = DateTime.UtcNow;

        var center = new Center
        {
            CenterId = _centerId,
            CenterName = "Test Center",
            CenterCode = "TC01",
            Timezone = "UTC",
            Status = CenterStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };
        ctx.Centers.Add(center);

        var teacherUser = new User
        {
            UserId = _teacherUserId,
            CenterId = _centerId,
            Username = "teacher_user",
            DisplayName = "Test Teacher",
            RoleName = UserRole.Teacher,
            Status = UserStatus.Active,
            PasswordHash = "hash",
            CreatedAt = now,
            UpdatedAt = now
        };
        ctx.Users.Add(teacherUser);

        var teacher = new Teacher
        {
            TeacherId = _teacherUserId,
            CenterId = _centerId,
            CreatedAt = now,
            UpdatedAt = now,
            User = teacherUser
        };
        ctx.Teachers.Add(teacher);

        var subject = new Subject
        {
            SubjectId = subjectId,
            CenterId = _centerId,
            SubjectName = "Toán Học",
            SubjectCode = "MATH",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };
        ctx.Subjects.Add(subject);

        var classEntity = new Class
        {
            ClassId = classId,
            CenterId = _centerId,
            ClassName = "10A1",
            AcademicYear = "2026-2027",
            SubjectId = subjectId,
            TeacherId = _teacherUserId,
            GradeLevel = classGrade,
            Status = classStatus,
            RowVersion = 1,
            IsDeleted = false,
            CreatedAt = now,
            UpdatedAt = now
        };
        ctx.Classes.Add(classEntity);

        var s1User = new User
        {
            UserId = student1Id,
            CenterId = _centerId,
            Username = "student_1",
            DisplayName = "Student 1",
            RoleName = UserRole.Student,
            Status = UserStatus.Active,
            PasswordHash = "hash",
            CreatedAt = now,
            UpdatedAt = now
        };
        var s2User = new User
        {
            UserId = student2Id,
            CenterId = _centerId,
            Username = "student_2",
            DisplayName = "Student 2",
            RoleName = UserRole.Student,
            Status = UserStatus.Active,
            PasswordHash = "hash",
            CreatedAt = now,
            UpdatedAt = now
        };
        ctx.Users.AddRange(s1User, s2User);

        var s1 = new Student
        {
            StudentId = student1Id,
            CenterId = _centerId,
            FullName = "Student 1",
            GradeLevel = 10,
            CreatedAt = now,
            UpdatedAt = now,
            User = s1User
        };
        var s2 = new Student
        {
            StudentId = student2Id,
            CenterId = _centerId,
            FullName = "Student 2",
            GradeLevel = 10,
            CreatedAt = now,
            UpdatedAt = now,
            User = s2User
        };
        ctx.Students.AddRange(s1, s2);

        ctx.ClassStudents.Add(new ClassStudent
        {
            ClassId = classId,
            StudentId = student1Id,
            CenterId = _centerId,
            Status = ClassStudentStatus.Active,
            GradeLevelAtEnrollment = 10,
            JoinedAt = now
        });

        var q10 = new Question
        {
            QuestionId = q10Id,
            CenterId = _centerId,
            SubjectId = subjectId,
            CreatedByTeacherId = _teacherUserId,
            QuestionText = "Question Grade 10",
            CorrectAnswer = "A",
            Solution = "Solution 10",
            LanguageCode = "vi",
            QuestionType = QuestionType.MultipleChoice,
            GradeLevel = 10,
            Difficulty = 1,
            MaxScore = 10,
            EstimatedTimeSeconds = 60,
            Status = QuestionStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
            RowVersion = 1
        };

        var q11 = new Question
        {
            QuestionId = q11Id,
            CenterId = _centerId,
            SubjectId = subjectId,
            CreatedByTeacherId = _teacherUserId,
            QuestionText = "Question Grade 11",
            CorrectAnswer = "B",
            Solution = "Solution 11",
            LanguageCode = "vi",
            QuestionType = QuestionType.MultipleChoice,
            GradeLevel = 11,
            Difficulty = 2,
            MaxScore = 10,
            EstimatedTimeSeconds = 60,
            Status = QuestionStatus.Active,
            CreatedAt = now,
            UpdatedAt = now,
            RowVersion = 1
        };
        ctx.Questions.AddRange(q10, q11);

        await ctx.SaveChangesAsync();
        return (subject, classEntity, s1, s2, q10, q11);
    }

    // ── Bug 1 Regression Tests: SelectedStudents cannot be empty or have invalid students ──

    [Fact]
    public async Task CreateAssignment_SelectedStudents_WithEmptyList_ReturnsValidationError()
    {
        await using var ctx = CreateContext();
        var (_, classEntity, _, _, q10, _) = await SeedBaseAsync(ctx);

        var sut = new CreateAssignmentUseCase(ctx, _tenantMock.Object, _timeProviderMock.Object);

        var request = new CreateAssignmentRequest
        {
            ClassId = classEntity.ClassId,
            Title = "Draft Assignment",
            TargetMode = "SelectedStudents",
            StudentIds = new List<string>(), // Empty list
            QuestionIds = new List<string> { q10.QuestionId.ToString() }
        };

        var result = await sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task CreateAssignment_SelectedStudents_StudentNotInClass_ReturnsValidationError()
    {
        await using var ctx = CreateContext();
        var (_, classEntity, _, s2, q10, _) = await SeedBaseAsync(ctx);

        var sut = new CreateAssignmentUseCase(ctx, _tenantMock.Object, _timeProviderMock.Object);

        // s2 is NOT in ClassStudents (only s1 is)
        var request = new CreateAssignmentRequest
        {
            ClassId = classEntity.ClassId,
            Title = "Draft Assignment",
            TargetMode = "SelectedStudents",
            StudentIds = new List<string> { s2.StudentId.ToString() },
            QuestionIds = new List<string> { q10.QuestionId.ToString() }
        };

        var result = await sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task UpdateAssignment_SelectedStudents_WithEmptyList_ReturnsValidationError()
    {
        await using var ctx = CreateContext();
        var (_, classEntity, s1, _, q10, _) = await SeedBaseAsync(ctx);

        var createSut = new CreateAssignmentUseCase(ctx, _tenantMock.Object, _timeProviderMock.Object);
        var createResult = await createSut.ExecuteAsync(new CreateAssignmentRequest
        {
            ClassId = classEntity.ClassId,
            Title = "Draft Assignment",
            TargetMode = "SelectedStudents",
            StudentIds = new List<string> { s1.StudentId.ToString() },
            QuestionIds = new List<string> { q10.QuestionId.ToString() }
        });
        Assert.True(createResult.IsSuccess);

        var updateSut = new UpdateAssignmentUseCase(ctx, _tenantMock.Object, _timeProviderMock.Object);

        // Update with empty studentIds in SelectedStudents mode
        var updateResult = await updateSut.ExecuteAsync(Guid.Parse(createResult.Data!.AssignmentId), new UpdateAssignmentRequest
        {
            Title = "Updated Draft",
            TargetMode = "SelectedStudents",
            StudentIds = new List<string>(), // Empty list
            QuestionIds = new List<string> { q10.QuestionId.ToString() },
            RowVersion = createResult.Data.RowVersion
        });

        Assert.False(updateResult.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, updateResult.ErrorCode);
    }

    [Fact]
    public async Task UpdateAssignment_SelectedStudents_StudentNotInClass_ReturnsValidationError()
    {
        await using var ctx = CreateContext();
        var (_, classEntity, s1, s2, q10, _) = await SeedBaseAsync(ctx);

        var createSut = new CreateAssignmentUseCase(ctx, _tenantMock.Object, _timeProviderMock.Object);
        var createResult = await createSut.ExecuteAsync(new CreateAssignmentRequest
        {
            ClassId = classEntity.ClassId,
            Title = "Draft Assignment",
            TargetMode = "SelectedStudents",
            StudentIds = new List<string> { s1.StudentId.ToString() },
            QuestionIds = new List<string> { q10.QuestionId.ToString() }
        });
        Assert.True(createResult.IsSuccess);

        var updateSut = new UpdateAssignmentUseCase(ctx, _tenantMock.Object, _timeProviderMock.Object);

        // Try to update with s2 who is not an active member of the class
        var updateResult = await updateSut.ExecuteAsync(Guid.Parse(createResult.Data!.AssignmentId), new UpdateAssignmentRequest
        {
            Title = "Updated Draft",
            TargetMode = "SelectedStudents",
            StudentIds = new List<string> { s2.StudentId.ToString() },
            QuestionIds = new List<string> { q10.QuestionId.ToString() },
            RowVersion = createResult.Data.RowVersion
        });

        Assert.False(updateResult.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, updateResult.ErrorCode);
    }

    [Fact]
    public async Task PublishAssignment_SelectedStudents_WithNoActiveTargets_ReturnsValidationError()
    {
        await using var ctx = CreateContext();
        var (_, classEntity, s1, _, q10, _) = await SeedBaseAsync(ctx);

        var createSut = new CreateAssignmentUseCase(ctx, _tenantMock.Object, _timeProviderMock.Object);
        var createResult = await createSut.ExecuteAsync(new CreateAssignmentRequest
        {
            ClassId = classEntity.ClassId,
            Title = "Draft Assignment",
            TargetMode = "SelectedStudents",
            StudentIds = new List<string> { s1.StudentId.ToString() },
            QuestionIds = new List<string> { q10.QuestionId.ToString() }
        });
        Assert.True(createResult.IsSuccess);

        // Remove student s1 from class before publishing
        var classStudent = await ctx.ClassStudents.FirstAsync(cs => cs.ClassId == classEntity.ClassId && cs.StudentId == s1.StudentId);
        classStudent.Status = ClassStudentStatus.Removed;
        await ctx.SaveChangesAsync();

        var publishSut = new PublishAssignmentUseCase(ctx, _tenantMock.Object, _timeProviderMock.Object);
        var publishResult = await publishSut.ExecuteAsync(Guid.Parse(createResult.Data!.AssignmentId), new PublishAssignmentRequest
        {
            RowVersion = createResult.Data.RowVersion
        });

        // Fail-closed: Cannot publish because selected student is no longer active in the class
        Assert.False(publishResult.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, publishResult.ErrorCode);
    }

    // ── Bug 2 Regression Test: Cannot publish into Archived Class ──

    [Fact]
    public async Task PublishAssignment_ClassArchived_ReturnsInvalidStateTransition()
    {
        await using var ctx = CreateContext();
        var (_, classEntity, s1, _, q10, _) = await SeedBaseAsync(ctx);

        var createSut = new CreateAssignmentUseCase(ctx, _tenantMock.Object, _timeProviderMock.Object);
        var createResult = await createSut.ExecuteAsync(new CreateAssignmentRequest
        {
            ClassId = classEntity.ClassId,
            Title = "Draft Assignment",
            TargetMode = "WholeClass",
            QuestionIds = new List<string> { q10.QuestionId.ToString() }
        });
        Assert.True(createResult.IsSuccess);

        // Archive the class after draft was created
        classEntity.Status = ClassStatus.Archived;
        await ctx.SaveChangesAsync();

        var publishSut = new PublishAssignmentUseCase(ctx, _tenantMock.Object, _timeProviderMock.Object);
        var publishResult = await publishSut.ExecuteAsync(Guid.Parse(createResult.Data!.AssignmentId), new PublishAssignmentRequest
        {
            RowVersion = createResult.Data.RowVersion
        });

        Assert.False(publishResult.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidStateTransition, publishResult.ErrorCode);
    }

    [Fact]
    public async Task AssignmentCannotUseAnotherTeachersPrivateQuestion_AndPublishRechecksSharing()
    {
        await using var ctx = CreateContext();
        var (_, classEntity, _, _, question, _) = await SeedBaseAsync(ctx);
        question.CreatedByTeacherId = Guid.NewGuid();
        await ctx.SaveChangesAsync();
        var create = new CreateAssignmentUseCase(ctx, _tenantMock.Object, _timeProviderMock.Object);
        var request = new CreateAssignmentRequest { ClassId = classEntity.ClassId, Title = "Library assignment",
            TargetMode = "WholeClass", QuestionIds = [question.QuestionId.ToString()] };
        Assert.False((await create.ExecuteAsync(request)).IsSuccess);
        question.Visibility = MaterialVisibility.Shared;
        await ctx.SaveChangesAsync();
        var draft = await create.ExecuteAsync(request);
        Assert.True(draft.IsSuccess);
        question.Visibility = MaterialVisibility.Private;
        await ctx.SaveChangesAsync();
        var publish = new PublishAssignmentUseCase(ctx, _tenantMock.Object, _timeProviderMock.Object);
        var command = new PublishAssignmentRequest { RowVersion = draft.Data!.RowVersion };
        Assert.False((await publish.ExecuteAsync(Guid.Parse(draft.Data.AssignmentId), command)).IsSuccess);
        question.Visibility = MaterialVisibility.Shared;
        await ctx.SaveChangesAsync();
        Assert.True((await publish.ExecuteAsync(Guid.Parse(draft.Data.AssignmentId), command)).IsSuccess);
    }

    // ── Academic Grade Level Constraints Tests ──

    [Fact]
    public async Task CreateAssignment_QuestionGradeMismatch_WithoutException_ReturnsValidationError()
    {
        await using var ctx = CreateContext();
        // Class is Grade 10, Question 11 is Grade 11
        var (_, classEntity, _, _, _, q11) = await SeedBaseAsync(ctx, classGrade: 10);

        var sut = new CreateAssignmentUseCase(ctx, _tenantMock.Object, _timeProviderMock.Object);

        var request = new CreateAssignmentRequest
        {
            ClassId = classEntity.ClassId,
            Title = "Grade Mismatch Assignment",
            TargetMode = "WholeClass",
            QuestionIds = new List<string> { q11.QuestionId.ToString() },
            AllowGradeMismatch = false
        };

        var result = await sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task CreateAssignment_QuestionGradeMismatch_WithExceptionAndReason_Succeeds()
    {
        await using var ctx = CreateContext();
        // Class is Grade 10, Question 11 is Grade 11
        var (_, classEntity, _, _, _, q11) = await SeedBaseAsync(ctx, classGrade: 10);

        var sut = new CreateAssignmentUseCase(ctx, _tenantMock.Object, _timeProviderMock.Object);

        var request = new CreateAssignmentRequest
        {
            ClassId = classEntity.ClassId,
            Title = "Grade Mismatch with Exception",
            TargetMode = "WholeClass",
            QuestionIds = new List<string> { q11.QuestionId.ToString() },
            AllowGradeMismatch = true,
            GradeMismatchReason = "Học trước chương trình nâng cao"
        };

        var result = await sut.ExecuteAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);

        var saved = await ctx.Assignments.FirstAsync(a => a.AssignmentId == Guid.Parse(result.Data.AssignmentId));
        Assert.True(saved.AllowGradeMismatch);
        Assert.Equal("Học trước chương trình nâng cao", saved.GradeMismatchReason);
    }

    [Fact]
    public async Task UpdateAssignment_QuestionGradeMismatch_WithoutException_ReturnsValidationError()
    {
        await using var ctx = CreateContext();
        var (_, classEntity, _, _, q10, q11) = await SeedBaseAsync(ctx, classGrade: 10);

        var createSut = new CreateAssignmentUseCase(ctx, _tenantMock.Object, _timeProviderMock.Object);
        var createResult = await createSut.ExecuteAsync(new CreateAssignmentRequest
        {
            ClassId = classEntity.ClassId,
            Title = "Draft Assignment",
            TargetMode = "WholeClass",
            QuestionIds = new List<string> { q10.QuestionId.ToString() }
        });
        Assert.True(createResult.IsSuccess);

        var updateSut = new UpdateAssignmentUseCase(ctx, _tenantMock.Object, _timeProviderMock.Object);

        // Update to include Grade 11 question without exception
        var updateResult = await updateSut.ExecuteAsync(Guid.Parse(createResult.Data!.AssignmentId), new UpdateAssignmentRequest
        {
            Title = "Updated Draft",
            TargetMode = "WholeClass",
            QuestionIds = new List<string> { q11.QuestionId.ToString() },
            AllowGradeMismatch = false,
            RowVersion = createResult.Data.RowVersion
        });

        Assert.False(updateResult.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, updateResult.ErrorCode);
    }

    [Fact]
    public async Task UpdateAssignment_QuestionGradeMismatch_WithExceptionAndReason_Succeeds()
    {
        await using var ctx = CreateContext();
        var (_, classEntity, _, _, q10, q11) = await SeedBaseAsync(ctx, classGrade: 10);

        var createSut = new CreateAssignmentUseCase(ctx, _tenantMock.Object, _timeProviderMock.Object);
        var createResult = await createSut.ExecuteAsync(new CreateAssignmentRequest
        {
            ClassId = classEntity.ClassId,
            Title = "Draft Assignment",
            TargetMode = "WholeClass",
            QuestionIds = new List<string> { q10.QuestionId.ToString() }
        });
        Assert.True(createResult.IsSuccess);

        var updateSut = new UpdateAssignmentUseCase(ctx, _tenantMock.Object, _timeProviderMock.Object);

        // Update to include Grade 11 question with explicit exception and reason
        var updateResult = await updateSut.ExecuteAsync(Guid.Parse(createResult.Data!.AssignmentId), new UpdateAssignmentRequest
        {
            Title = "Updated Draft with Exception",
            TargetMode = "WholeClass",
            QuestionIds = new List<string> { q11.QuestionId.ToString() },
            AllowGradeMismatch = true,
            GradeMismatchReason = "Ôn tập kiến thức bổ trợ",
            RowVersion = createResult.Data.RowVersion
        });

        Assert.True(updateResult.IsSuccess);
        Assert.NotNull(updateResult.Data);

        var saved = await ctx.Assignments.FirstAsync(a => a.AssignmentId == Guid.Parse(createResult.Data.AssignmentId));
        Assert.True(saved.AllowGradeMismatch);
        Assert.Equal("Ôn tập kiến thức bổ trợ", saved.GradeMismatchReason);
    }

    [Fact]
    public async Task CreateAssignment_GradeMismatchReasonExceeds500Chars_ReturnsValidationFailed()
    {
        await using var ctx = CreateContext();
        var (_, classEntity, _, _, _, q11) = await SeedBaseAsync(ctx, classGrade: 10);

        var sut = new CreateAssignmentUseCase(ctx, _tenantMock.Object, _timeProviderMock.Object);

        var request = new CreateAssignmentRequest
        {
            ClassId = classEntity.ClassId,
            Title = "Grade Mismatch Exceeding 500",
            TargetMode = "WholeClass",
            QuestionIds = new List<string> { q11.QuestionId.ToString() },
            AllowGradeMismatch = true,
            GradeMismatchReason = new string('X', 501)
        };

        var result = await sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task UpdateAssignment_GradeMismatchReasonExceeds500Chars_ReturnsValidationFailed()
    {
        await using var ctx = CreateContext();
        var (_, classEntity, _, _, q10, q11) = await SeedBaseAsync(ctx, classGrade: 10);

        var createSut = new CreateAssignmentUseCase(ctx, _tenantMock.Object, _timeProviderMock.Object);
        var createResult = await createSut.ExecuteAsync(new CreateAssignmentRequest
        {
            ClassId = classEntity.ClassId,
            Title = "Draft Assignment",
            TargetMode = "WholeClass",
            QuestionIds = new List<string> { q10.QuestionId.ToString() }
        });
        Assert.True(createResult.IsSuccess);

        var updateSut = new UpdateAssignmentUseCase(ctx, _tenantMock.Object, _timeProviderMock.Object);

        var updateResult = await updateSut.ExecuteAsync(Guid.Parse(createResult.Data!.AssignmentId), new UpdateAssignmentRequest
        {
            Title = "Updated Draft with Too Long Reason",
            TargetMode = "WholeClass",
            QuestionIds = new List<string> { q11.QuestionId.ToString() },
            AllowGradeMismatch = true,
            GradeMismatchReason = new string('Y', 501),
            RowVersion = createResult.Data.RowVersion
        });

        Assert.False(updateResult.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, updateResult.ErrorCode);
    }
}
