using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Organization;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.Common;
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
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.Organization;

public class DeleteClassUseCaseTests
{
    private static readonly DateTime SeedTimeUtc = new(2026, 8, 1, 0, 0, 0, DateTimeKind.Utc);

    private readonly Mock<ITenantContext> _mockTenantContext;
    private readonly Mock<ILogger<DeleteClassUseCase>> _mockLogger;
    private readonly Mock<TimeProvider> _mockTimeProvider;
    private readonly Guid _centerId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();

    public DeleteClassUseCaseTests()
    {
        _mockTenantContext = new Mock<ITenantContext>();
        _mockLogger = new Mock<ILogger<DeleteClassUseCase>>();
        _mockTimeProvider = new Mock<TimeProvider>();

        _mockTenantContext.Setup(t => t.IsResolved).Returns(true);
        _mockTenantContext.Setup(t => t.CenterId).Returns(_centerId);
        _mockTenantContext.Setup(t => t.UserId).Returns(_userId);
        _mockTenantContext.Setup(t => t.Role).Returns(nameof(UserRole.CenterManager));

        _mockTimeProvider.Setup(t => t.GetUtcNow()).Returns(new DateTimeOffset(SeedTimeUtc));
    }

    private EduTwinDbContext CreateContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(dbName)
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        var mockAccessor = new Mock<ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(_centerId);
        return new EduTwinDbContext(options, mockAccessor.Object);
    }

    private Class CreateTestClass(Guid classId, string className = "10A1", string academicYear = "2026-2027", Guid? centerId = null)
    {
        return new Class
        {
            ClassId = classId,
            CenterId = centerId ?? _centerId,
            ClassName = className,
            AcademicYear = academicYear,
            GradeLevel = 10,
            Status = ClassStatus.Active,
            RowVersion = 1,
            IsDeleted = false,
            CreatedAt = SeedTimeUtc,
            UpdatedAt = SeedTimeUtc,
            CreatedBy = _userId,
            UpdatedBy = _userId
        };
    }

    private Center CreateTestCenter(Guid? centerId = null)
    {
        return new Center
        {
            CenterId = centerId ?? _centerId,
            CenterName = "Test Center",
            CenterCode = "TC01",
            Timezone = "UTC",
            Status = CenterStatus.Active,
            IsDeleted = false,
            CreatedAt = SeedTimeUtc,
            UpdatedAt = SeedTimeUtc
        };
    }

    [Fact]
    public async Task ExecuteAsync_CleanClass_SoftDeletesClass_AppendsSuffix_AndWritesAuditLog()
    {
        var dbName = Guid.NewGuid().ToString();
        var classId = Guid.NewGuid();
        await using (var ctx = CreateContext(dbName))
        {
            ctx.Centers.Add(CreateTestCenter());
            ctx.Classes.Add(CreateTestClass(classId, "10A1", "2026-2027"));
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = CreateContext(dbName))
        {
            var sut = new DeleteClassUseCase(ctx, _mockTenantContext.Object, _mockTimeProvider.Object, _mockLogger.Object);
            var result = await sut.ExecuteAsync(classId);

            Assert.True(result.IsSuccess);
            Assert.Null(result.ErrorCode);

            var deletedClass = await ctx.Classes.IgnoreQueryFilters().FirstAsync(c => c.ClassId == classId);
            Assert.True(deletedClass.IsDeleted);
            Assert.Contains($"#del#{classId:N}", deletedClass.ClassName);
            Assert.Equal(_userId, deletedClass.UpdatedBy);

            var auditLog = await ctx.AuthorizationAuditLogs.FirstOrDefaultAsync(a => a.TargetId == classId.ToString("D"));
            Assert.NotNull(auditLog);
            Assert.Equal("ClassDeleted", auditLog.ActionType);
            Assert.Equal("Class", auditLog.TargetType);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ClassWithActiveStudents_ReturnsInvalidStateTransition()
    {
        var dbName = Guid.NewGuid().ToString();
        var classId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        await using (var ctx = CreateContext(dbName))
        {
            ctx.Centers.Add(CreateTestCenter());
            ctx.Classes.Add(CreateTestClass(classId));
            ctx.ClassStudents.Add(new ClassStudent
            {
                ClassId = classId,
                StudentId = studentId,
                CenterId = _centerId,
                Status = ClassStudentStatus.Active,
                JoinedAt = SeedTimeUtc
            });
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = CreateContext(dbName))
        {
            var sut = new DeleteClassUseCase(ctx, _mockTenantContext.Object, _mockTimeProvider.Object, _mockLogger.Object);
            var result = await sut.ExecuteAsync(classId);

            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorCodes.InvalidStateTransition, result.ErrorCode);
            Assert.Contains("thành viên", result.ErrorMessage);
            Assert.Contains("Lưu trữ", result.ErrorMessage);

            var existingClass = await ctx.Classes.FirstAsync(c => c.ClassId == classId);
            Assert.False(existingClass.IsDeleted);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ClassWithRemovedStudents_ReturnsInvalidStateTransition()
    {
        var dbName = Guid.NewGuid().ToString();
        var classId = Guid.NewGuid();
        var studentId = Guid.NewGuid();

        await using (var ctx = CreateContext(dbName))
        {
            ctx.Centers.Add(CreateTestCenter());
            ctx.Classes.Add(CreateTestClass(classId));
            ctx.ClassStudents.Add(new ClassStudent
            {
                ClassId = classId,
                StudentId = studentId,
                CenterId = _centerId,
                Status = ClassStudentStatus.Removed,
                JoinedAt = SeedTimeUtc,
                RemovedAt = SeedTimeUtc.AddDays(1)
            });
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = CreateContext(dbName))
        {
            var sut = new DeleteClassUseCase(ctx, _mockTenantContext.Object, _mockTimeProvider.Object, _mockLogger.Object);
            var result = await sut.ExecuteAsync(classId);

            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorCodes.InvalidStateTransition, result.ErrorCode);
            Assert.Contains("thành viên", result.ErrorMessage);
            Assert.Contains("Lưu trữ", result.ErrorMessage);

            var existingClass = await ctx.Classes.FirstAsync(c => c.ClassId == classId);
            Assert.False(existingClass.IsDeleted);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ClassWithAssignments_ReturnsInvalidStateTransition()
    {
        var dbName = Guid.NewGuid().ToString();
        var classId = Guid.NewGuid();

        await using (var ctx = CreateContext(dbName))
        {
            ctx.Centers.Add(CreateTestCenter());
            ctx.Classes.Add(CreateTestClass(classId));
            ctx.Assignments.Add(new Assignment
            {
                AssignmentId = Guid.NewGuid(),
                CenterId = _centerId,
                ClassId = classId,
                CreatedByTeacherId = Guid.NewGuid(),
                Title = "Test Assignment",
                Status = AssignmentStatus.Draft,
                CreatedAt = SeedTimeUtc,
                UpdatedAt = SeedTimeUtc,
                RowVersion = 1
            });
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = CreateContext(dbName))
        {
            var sut = new DeleteClassUseCase(ctx, _mockTenantContext.Object, _mockTimeProvider.Object, _mockLogger.Object);
            var result = await sut.ExecuteAsync(classId);

            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorCodes.InvalidStateTransition, result.ErrorCode);
            Assert.Contains("bài tập", result.ErrorMessage);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ClassWithCurriculumClasses_ReturnsInvalidStateTransition()
    {
        var dbName = Guid.NewGuid().ToString();
        var classId = Guid.NewGuid();

        await using (var ctx = CreateContext(dbName))
        {
            ctx.Centers.Add(CreateTestCenter());
            ctx.Classes.Add(CreateTestClass(classId));
            ctx.CurriculumClasses.Add(new CurriculumClass
            {
                CurriculumId = Guid.NewGuid(),
                ClassId = classId,
                CenterId = _centerId,
                AssignedAt = SeedTimeUtc
            });
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = CreateContext(dbName))
        {
            var sut = new DeleteClassUseCase(ctx, _mockTenantContext.Object, _mockTimeProvider.Object, _mockLogger.Object);
            var result = await sut.ExecuteAsync(classId);

            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorCodes.InvalidStateTransition, result.ErrorCode);
            Assert.Contains("giáo trình", result.ErrorMessage);
        }
    }

    [Fact]
    public async Task ExecuteAsync_NonCenterManager_ReturnsForbiddenError()
    {
        var dbName = Guid.NewGuid().ToString();
        var classId = Guid.NewGuid();

        await using (var ctx = CreateContext(dbName))
        {
            ctx.Centers.Add(CreateTestCenter());
            ctx.Classes.Add(CreateTestClass(classId));
            await ctx.SaveChangesAsync();
        }

        _mockTenantContext.Setup(t => t.Role).Returns(nameof(UserRole.Teacher));

        await using (var ctx = CreateContext(dbName))
        {
            var sut = new DeleteClassUseCase(ctx, _mockTenantContext.Object, _mockTimeProvider.Object, _mockLogger.Object);
            var result = await sut.ExecuteAsync(classId);

            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorCodes.ForbiddenResource, result.ErrorCode);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ClassFromDifferentCenter_ReturnsNotFound()
    {
        var dbName = Guid.NewGuid().ToString();
        var classId = Guid.NewGuid();
        var otherCenterId = Guid.NewGuid();

        await using (var ctx = CreateContext(dbName))
        {
            ctx.Centers.Add(CreateTestCenter());
            ctx.Classes.Add(CreateTestClass(classId, centerId: otherCenterId));
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = CreateContext(dbName))
        {
            var sut = new DeleteClassUseCase(ctx, _mockTenantContext.Object, _mockTimeProvider.Object, _mockLogger.Object);
            var result = await sut.ExecuteAsync(classId);

            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        }
    }

    [Fact]
    public async Task ExecuteAsync_RecreatingClassWithSameNameAfterDeletion_Succeeds()
    {
        var dbName = Guid.NewGuid().ToString();
        var classId1 = Guid.NewGuid();
        var classId2 = Guid.NewGuid();

        await using (var ctx = CreateContext(dbName))
        {
            ctx.Centers.Add(CreateTestCenter());
            ctx.Classes.Add(CreateTestClass(classId1, "10A1", "2026-2027"));
            await ctx.SaveChangesAsync();
        }

        await using (var ctx = CreateContext(dbName))
        {
            var sut = new DeleteClassUseCase(ctx, _mockTenantContext.Object, _mockTimeProvider.Object, _mockLogger.Object);
            var result = await sut.ExecuteAsync(classId1);
            Assert.True(result.IsSuccess);
        }

        await using (var ctx = CreateContext(dbName))
        {
            // Now create a new class with the exact same name and academic year
            var newClass = CreateTestClass(classId2, "10A1", "2026-2027");
            ctx.Classes.Add(newClass);
            await ctx.SaveChangesAsync();

            var activeClass = await ctx.Classes.FirstOrDefaultAsync(c => c.ClassName == "10A1" && c.AcademicYear == "2026-2027");
            Assert.NotNull(activeClass);
            Assert.Equal(classId2, activeClass.ClassId);

            var oldClass = await ctx.Classes.IgnoreQueryFilters().FirstOrDefaultAsync(c => c.ClassId == classId1);
            Assert.NotNull(oldClass);
            Assert.True(oldClass.IsDeleted);
            Assert.Contains($"#del#{classId1:N}", oldClass.ClassName);
        }
    }
}
