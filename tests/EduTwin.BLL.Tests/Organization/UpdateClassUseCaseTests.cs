using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Query;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using EduTwin.BLL.Organization;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;

namespace EduTwin.BLL.Tests.Organization;

public class UpdateClassUseCaseTests
{
    private readonly Mock<ITenantContext> _mockTenantContext;
    private readonly Mock<TimeProvider> _mockTimeProvider;
    private readonly Mock<ILogger<UpdateClassUseCase>> _mockLogger;
    private static readonly DateTime SeedTimeUtc = new(2026, 7, 20, 0, 0, 0, DateTimeKind.Utc);

    public UpdateClassUseCaseTests()
    {
        _mockTenantContext = new Mock<ITenantContext>();
        _mockTenantContext.Setup(t => t.IsResolved).Returns(true);
        _mockTenantContext.Setup(t => t.CenterId).Returns(Guid.NewGuid());
        _mockTenantContext.Setup(t => t.UserId).Returns(Guid.NewGuid());
        _mockTenantContext.Setup(t => t.Role).Returns(nameof(UserRole.CenterManager));

        _mockTimeProvider = new Mock<TimeProvider>();
        _mockTimeProvider.Setup(t => t.GetUtcNow()).Returns(SeedTimeUtc);

        _mockLogger = new Mock<ILogger<UpdateClassUseCase>>();
    }

    private static EduTwinDbContext CreateContext(string dbName, Guid centerId, params IInterceptor[] interceptors)
    {
        var builder = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(dbName);

        if (interceptors != null && interceptors.Length > 0)
        {
            builder.EnableServiceProviderCaching(false);
            builder.AddInterceptors(interceptors);
        }

        var options = builder.Options;

        var tenantAccessorMock = new Mock<ITenantIdAccessor>();
        tenantAccessorMock.Setup(a => a.CenterId).Returns(centerId);

        return new EduTwinDbContext(options, tenantAccessorMock.Object);
    }

    private static async Task SeedDataAsync(
        EduTwinDbContext context,
        Guid centerId,
        Guid classId,
        Guid originalTeacherId,
        Guid newTeacherId,
        Guid subjectId,
        bool classDeleted = false,
        bool originalTeacherDeleted = false,
        bool newTeacherDeleted = false,
        bool originalUserDeleted = false,
        bool newUserDeleted = false,
        UserRole originalRole = UserRole.Teacher,
        UserRole newRole = UserRole.Teacher,
        Guid? classCenterId = null,
        Guid? newTeacherCenterId = null,
        Guid? newUserCenterId = null,
        CenterStatus centerStatus = CenterStatus.Active,
        bool centerDeleted = false,
        string academicYear = "2026",
        string className = "Old Class",
        ulong rowVersion = 1)
    {
        classCenterId ??= centerId;
        newTeacherCenterId ??= centerId;
        newUserCenterId ??= centerId;

        context.Centers.Add(new Center { CenterId = centerId, CenterName = "C", CenterCode="C", Timezone="UTC", Status = centerStatus, IsDeleted = centerDeleted, CreatedAt = SeedTimeUtc, UpdatedAt = SeedTimeUtc });
        context.Subjects.Add(new Subject { SubjectId = subjectId, CenterId = centerId, SubjectName = "S", SubjectCode="S", CreatedAt = SeedTimeUtc, UpdatedAt = SeedTimeUtc });

        var originalUser = new User { UserId = originalTeacherId, CenterId = centerId, DisplayName = "T1", RoleName = originalRole, Username="T1", PasswordHash="H", CreatedAt = SeedTimeUtc, UpdatedAt = SeedTimeUtc, IsDeleted = originalUserDeleted };
        var newUser = new User { UserId = newTeacherId, CenterId = newUserCenterId.Value, DisplayName = "T2", RoleName = newRole, Username="T2", PasswordHash="H", CreatedAt = SeedTimeUtc, UpdatedAt = SeedTimeUtc, IsDeleted = newUserDeleted };

        context.Users.Add(originalUser);
        context.Users.Add(newUser);

        context.Teachers.Add(new Teacher { TeacherId = originalTeacherId, CenterId = centerId, User = originalUser, CreatedAt = SeedTimeUtc, UpdatedAt = SeedTimeUtc, IsDeleted = originalTeacherDeleted });
        context.Teachers.Add(new Teacher { TeacherId = newTeacherId, CenterId = newTeacherCenterId.Value, User = newUser, CreatedAt = SeedTimeUtc, UpdatedAt = SeedTimeUtc, IsDeleted = newTeacherDeleted });

        var originalClass = new Class { ClassId = classId, CenterId = classCenterId.Value, ClassName = className, AcademicYear = academicYear, SubjectId = subjectId, TeacherId = originalTeacherId, CreatedAt = SeedTimeUtc, CreatedBy = Guid.NewGuid(), UpdatedAt = SeedTimeUtc, UpdatedBy = Guid.NewGuid(), Status = ClassStatus.Active, RowVersion = rowVersion, IsDeleted = classDeleted };
        context.Classes.Add(originalClass);

        await context.SaveChangesAsync();
    }

    [Fact]
    public async Task UpdateClass_Success_TrimsAndUpdatesCorrectly()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var currentUserId = _mockTenantContext.Object.UserId!.Value;
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName, centerId);

        var classId = Guid.NewGuid();
        var originalTeacherId = Guid.NewGuid();
        var newTeacherId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        await SeedDataAsync(context, centerId, classId, originalTeacherId, newTeacherId, subjectId, rowVersion: 1);

        // Seed ClassStudents fully relationally valid
        var student1Id = Guid.NewGuid();
        var student2Id = Guid.NewGuid();
        var student3Id = Guid.NewGuid(); // Removed
        var student4Id = Guid.NewGuid(); // Other class
        var secondClassId = Guid.NewGuid();

        // Users
        context.Users.Add(new User { UserId = student1Id, CenterId = centerId, DisplayName = "S1", RoleName = UserRole.Student, Username="S1", PasswordHash="H", CreatedAt = SeedTimeUtc, UpdatedAt = SeedTimeUtc });
        context.Users.Add(new User { UserId = student2Id, CenterId = centerId, DisplayName = "S2", RoleName = UserRole.Student, Username="S2", PasswordHash="H", CreatedAt = SeedTimeUtc, UpdatedAt = SeedTimeUtc });
        context.Users.Add(new User { UserId = student3Id, CenterId = centerId, DisplayName = "S3", RoleName = UserRole.Student, Username="S3", PasswordHash="H", CreatedAt = SeedTimeUtc, UpdatedAt = SeedTimeUtc });
        context.Users.Add(new User { UserId = student4Id, CenterId = centerId, DisplayName = "S4", RoleName = UserRole.Student, Username="S4", PasswordHash="H", CreatedAt = SeedTimeUtc, UpdatedAt = SeedTimeUtc });

        // Students (GradeLevel 10)
        context.Students.Add(new Student { StudentId = student1Id, CenterId = centerId, FullName = "S1", GradeLevel = 10, User = context.Users.Local.First(u => u.UserId == student1Id), CreatedAt = SeedTimeUtc, UpdatedAt = SeedTimeUtc });
        context.Students.Add(new Student { StudentId = student2Id, CenterId = centerId, FullName = "S2", GradeLevel = 10, User = context.Users.Local.First(u => u.UserId == student2Id), CreatedAt = SeedTimeUtc, UpdatedAt = SeedTimeUtc });
        context.Students.Add(new Student { StudentId = student3Id, CenterId = centerId, FullName = "S3", GradeLevel = 10, User = context.Users.Local.First(u => u.UserId == student3Id), CreatedAt = SeedTimeUtc, UpdatedAt = SeedTimeUtc });
        context.Students.Add(new Student { StudentId = student4Id, CenterId = centerId, FullName = "S4", GradeLevel = 10, User = context.Users.Local.First(u => u.UserId == student4Id), CreatedAt = SeedTimeUtc, UpdatedAt = SeedTimeUtc });

        // Second Class
        context.Classes.Add(new Class { ClassId = secondClassId, CenterId = centerId, ClassName = "Second Class", AcademicYear = "2026", SubjectId = subjectId, TeacherId = originalTeacherId, CreatedAt = SeedTimeUtc, UpdatedAt = SeedTimeUtc, Status = ClassStatus.Active, RowVersion = 1 });

        // ClassStudents
        var joinedUtc = DateTime.SpecifyKind(SeedTimeUtc, DateTimeKind.Utc);
        var removedUtc = joinedUtc.AddDays(1);
        context.ClassStudents.Add(new ClassStudent { CenterId = centerId, ClassId = classId, StudentId = student1Id, JoinedAt = joinedUtc, Status = ClassStudentStatus.Active });
        context.ClassStudents.Add(new ClassStudent { CenterId = centerId, ClassId = classId, StudentId = student2Id, JoinedAt = joinedUtc, Status = ClassStudentStatus.Active });
        context.ClassStudents.Add(new ClassStudent { CenterId = centerId, ClassId = classId, StudentId = student3Id, JoinedAt = joinedUtc, RemovedAt = removedUtc, Status = ClassStudentStatus.Removed });
        context.ClassStudents.Add(new ClassStudent { CenterId = centerId, ClassId = secondClassId, StudentId = student4Id, JoinedAt = joinedUtc, Status = ClassStudentStatus.Active }); // Other class

        await context.SaveChangesAsync();

        // Grab original values for immutable assertions
        var originalClass = await context.Classes.AsNoTracking().FirstAsync(c => c.ClassId == classId);

        var updateTime = SeedTimeUtc.AddDays(1);
        _mockTimeProvider.Setup(t => t.GetUtcNow()).Returns(updateTime);

        var sut = new UpdateClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var request = new UpdateClassRequest
        {
            ClassName = "  New Math 101  ",
            TeacherId = newTeacherId,
            Status = ClassStatus.Archived,
            RowVersion = "1"
        };

        var result = await sut.ExecuteAsync(classId, request);

        Assert.True(result.IsSuccess, result.ErrorCode);
        Assert.NotNull(result.Data);
        Assert.Equal("New Math 101", result.Data.ClassName);
        Assert.Equal("2", result.Data.RowVersion); // Incremented
        Assert.Equal("Archived", result.Data.Status);
        Assert.Equal(newTeacherId.ToString().ToLowerInvariant(), result.Data.Teacher.TeacherId);
        Assert.Equal(subjectId.ToString().ToLowerInvariant(), result.Data.Subject.SubjectId);
        Assert.Equal("2026", result.Data.AcademicYear);
        Assert.Equal(2, result.Data.StudentCount); // 2 Active same-tenant students

        var updatedClass = await context.Classes.FirstAsync(c => c.ClassId == classId);

        // Assert mutated properties
        Assert.Equal("New Math 101", updatedClass.ClassName);
        Assert.Equal(newTeacherId, updatedClass.TeacherId);
        Assert.Equal(ClassStatus.Archived, updatedClass.Status);
        Assert.Equal(2ul, updatedClass.RowVersion);

        // Assert audit properties
        Assert.Equal(updateTime, updatedClass.UpdatedAt);
        Assert.Equal(currentUserId, updatedClass.UpdatedBy);

        // Assert immutable properties
        Assert.Equal(originalClass.CreatedAt, updatedClass.CreatedAt);
        Assert.Equal(originalClass.CreatedBy, updatedClass.CreatedBy);
        Assert.Equal(originalClass.SubjectId, updatedClass.SubjectId);
        Assert.Equal(originalClass.AcademicYear, updatedClass.AcademicYear);
    }

    private class ClassStudentQueryVisitor : ExpressionVisitor
    {
        public bool HasClassStudentQuery { get; private set; }

        protected override Expression VisitExtension(Expression node)
        {
            if (node is QueryRootExpression queryRoot &&
                queryRoot.ElementType == typeof(ClassStudent))
            {
                HasClassStudentQuery = true;
            }
            return base.VisitExtension(node);
        }
    }

    private class StudentCountCancellationInterceptor : IQueryExpressionInterceptor
    {
        public bool WasTriggered { get; private set; }

        public Expression QueryCompilationStarting(Expression queryExpression, QueryExpressionEventData eventData)
        {
            var visitor = new ClassStudentQueryVisitor();
            visitor.Visit(queryExpression);

            if (visitor.HasClassStudentQuery)
            {
                WasTriggered = true;
                throw new OperationCanceledException();
            }
            return queryExpression;
        }
    }

    private class SaveChangesCounterInterceptor : ISaveChangesInterceptor
    {
        public int CallCount { get; private set; }

        public ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return new ValueTask<InterceptionResult<int>>(result);
        }

        public InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
        {
            CallCount++;
            return result;
        }
    }

    [Fact]
    public async Task StudentCountQuery_Cancellation_DoesNotMutateEntity()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var dbName = Guid.NewGuid().ToString();
        var cancellationInterceptor = new StudentCountCancellationInterceptor();
        var saveChangesInterceptor = new SaveChangesCounterInterceptor();

        var context = CreateContext(dbName, centerId, cancellationInterceptor, saveChangesInterceptor);

        var classId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var newTeacherId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        await SeedDataAsync(context, centerId, classId, teacherId, newTeacherId, subjectId, rowVersion: 1);

        var originalClass = await context.Classes.AsNoTracking().FirstAsync(c => c.ClassId == classId);
        int baselineSaveChanges = saveChangesInterceptor.CallCount;

        var sut = new UpdateClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var request = new UpdateClassRequest { ClassName = "New Name", TeacherId = newTeacherId, Status = ClassStatus.Archived, RowVersion = "1" };

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.ExecuteAsync(classId, request, CancellationToken.None));

        Assert.True(cancellationInterceptor.WasTriggered);
        Assert.Equal(baselineSaveChanges, saveChangesInterceptor.CallCount);

        var classAfter = await context.Classes.FirstAsync(c => c.ClassId == classId);
        Assert.Equal(originalClass.ClassName, classAfter.ClassName);
        Assert.Equal(originalClass.TeacherId, classAfter.TeacherId);
        Assert.Equal(originalClass.Status, classAfter.Status);
        Assert.Equal(originalClass.RowVersion, classAfter.RowVersion);
        Assert.Equal(originalClass.CreatedAt, classAfter.CreatedAt);
        Assert.Equal(originalClass.CreatedBy, classAfter.CreatedBy);
        Assert.Equal(originalClass.UpdatedAt, classAfter.UpdatedAt);
        Assert.Equal(originalClass.UpdatedBy, classAfter.UpdatedBy);
        Assert.Equal(originalClass.SubjectId, classAfter.SubjectId);
        Assert.Equal(originalClass.AcademicYear, classAfter.AcademicYear);
        Assert.Equal(EntityState.Unchanged, context.Entry(classAfter).State);
    }

    [Fact]
    public async Task StaleRowVersion_ReturnsConcurrencyConflict()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName, centerId);

        var classId = Guid.NewGuid();
        var originalTeacherId = Guid.NewGuid();
        var newTeacherId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        await SeedDataAsync(context, centerId, classId, originalTeacherId, newTeacherId, subjectId, rowVersion: 2);

        var sut = new UpdateClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var request = new UpdateClassRequest { ClassName = "New Name", TeacherId = newTeacherId, Status = ClassStatus.Active, RowVersion = "999" /* Stale */ };

        var result = await sut.ExecuteAsync(classId, request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, result.ErrorCode);
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1 ")]
    [InlineData(" 1")]
    [InlineData("+1")]
    [InlineData("-1")]
    [InlineData("1.0")]
    [InlineData("abc")]
    [InlineData("18446744073709551616")] // Overflow ulong
    [InlineData("")]
    [InlineData(null)]
    public async Task InvalidRowVersion_ReturnsValidationFailed(string? invalidRowVersion)
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName, centerId);

        var sut = new UpdateClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var request = new UpdateClassRequest { ClassName = "Name", TeacherId = Guid.NewGuid(), Status = ClassStatus.Active, RowVersion = invalidRowVersion! };

        var result = await sut.ExecuteAsync(Guid.NewGuid(), request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task InvalidClassName_ReturnsValidationFailed(string? className)
    {
        var sut = new UpdateClassUseCase(null!, _mockTenantContext.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var request = new UpdateClassRequest { ClassName = className!, TeacherId = Guid.NewGuid(), Status = ClassStatus.Active, RowVersion = "1" };
        var result = await sut.ExecuteAsync(Guid.NewGuid(), request);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task ClassNameExceedsRawLength_ReturnsValidationFailed()
    {
        var sut = new UpdateClassUseCase(null!, _mockTenantContext.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var request = new UpdateClassRequest { ClassName = new string('a', 150) + " ", TeacherId = Guid.NewGuid(), Status = ClassStatus.Active, RowVersion = "1" };
        var result = await sut.ExecuteAsync(Guid.NewGuid(), request);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task EmptyGuid_ReturnsValidationFailed()
    {
        var sut = new UpdateClassUseCase(null!, _mockTenantContext.Object, _mockTimeProvider.Object, _mockLogger.Object);

        var request1 = new UpdateClassRequest { ClassName = "A", TeacherId = Guid.NewGuid(), Status = ClassStatus.Active, RowVersion = "1" };
        var result1 = await sut.ExecuteAsync(Guid.Empty, request1);
        Assert.Equal(ErrorCodes.ValidationFailed, result1.ErrorCode);

        var request2 = new UpdateClassRequest { ClassName = "A", TeacherId = Guid.Empty, Status = ClassStatus.Active, RowVersion = "1" };
        var result2 = await sut.ExecuteAsync(Guid.NewGuid(), request2);
        Assert.Equal(ErrorCodes.ValidationFailed, result2.ErrorCode);
    }

    [Theory]
    [InlineData(false, "c", "u", "CenterManager")]
    [InlineData(true, null, "u", "CenterManager")]
    [InlineData(true, "00000000-0000-0000-0000-000000000000", "u", "CenterManager")]
    [InlineData(true, "c", null, "CenterManager")]
    [InlineData(true, "c", "00000000-0000-0000-0000-000000000000", "CenterManager")]
    [InlineData(true, "c", "u", "Teacher")]
    [InlineData(true, "c", "u", "Student")]
    [InlineData(true, "c", "u", "Admin")]
    [InlineData(true, "c", "u", "centerManager")] // Case sensitive
    [InlineData(true, "c", "u", "")]
    [InlineData(true, "c", "u", "   ")]
    [InlineData(true, "c", "u", "0")]
    [InlineData(true, "c", "u", "1")]
    [InlineData(true, "c", "u", "2")]
    [InlineData(true, "c", "u", null)]
    public async Task InvalidTenantContext_ReturnsNotFound(bool isResolved, string? centerIdStr, string? userIdStr, string? role)
    {
        _mockTenantContext.Setup(t => t.IsResolved).Returns(isResolved);
        if (centerIdStr == "c") _mockTenantContext.Setup(t => t.CenterId).Returns(Guid.NewGuid());
        else if (centerIdStr != null) _mockTenantContext.Setup(t => t.CenterId).Returns(Guid.Parse(centerIdStr));
        else _mockTenantContext.Setup(t => t.CenterId).Returns((Guid?)null);

        if (userIdStr == "u") _mockTenantContext.Setup(t => t.UserId).Returns(Guid.NewGuid());
        else if (userIdStr != null) _mockTenantContext.Setup(t => t.UserId).Returns(Guid.Parse(userIdStr));
        else _mockTenantContext.Setup(t => t.UserId).Returns((Guid?)null);

        _mockTenantContext.Setup(t => t.Role).Returns(role);

        var sut = new UpdateClassUseCase(null!, _mockTenantContext.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var request = new UpdateClassRequest { ClassName = "A", TeacherId = Guid.NewGuid(), Status = ClassStatus.Active, RowVersion = "1" };
        var result = await sut.ExecuteAsync(Guid.NewGuid(), request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData(true, false, false, false, false, false, UserRole.Teacher)] // Class soft deleted
    [InlineData(false, true, false, false, false, false, UserRole.Teacher)] // Class cross tenant
    [InlineData(false, false, true, false, false, false, UserRole.Teacher)] // Teacher deleted
    [InlineData(false, false, false, true, false, true, UserRole.Teacher)] // Teacher cross tenant (requires user cross tenant too)
    [InlineData(false, false, false, false, true, false, UserRole.Teacher)] // User deleted
    [InlineData(false, false, false, false, false, true, UserRole.Teacher)] // User cross tenant
    [InlineData(false, false, false, false, false, false, UserRole.Student)] // Wrong role
    public async Task InvalidRelatedEntities_ReturnsNotFound(
        bool classDeleted, bool classCrossTenant,
        bool teacherDeleted, bool teacherCrossTenant,
        bool userDeleted, bool userCrossTenant, UserRole userRole)
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName, centerId);

        var classId = Guid.NewGuid();
        var originalTeacherId = Guid.NewGuid();
        var newTeacherId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        await SeedDataAsync(context, centerId, classId, originalTeacherId, newTeacherId, subjectId,
            classDeleted: classDeleted,
            classCenterId: classCrossTenant ? Guid.NewGuid() : centerId,
            newTeacherDeleted: teacherDeleted,
            newTeacherCenterId: teacherCrossTenant ? Guid.NewGuid() : centerId,
            newUserDeleted: userDeleted,
            newUserCenterId: userCrossTenant ? Guid.NewGuid() : centerId,
            newRole: userRole,
            rowVersion: 1);

        var sut = new UpdateClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var request = new UpdateClassRequest { ClassName = "A", TeacherId = newTeacherId, Status = ClassStatus.Active, RowVersion = "1" };

        var result = await sut.ExecuteAsync(classId, request);

        Assert.False(result.IsSuccess, result.ErrorCode);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task SuspendedOrDeletedCenter_ReturnsNotFound()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var classId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var newTeacherId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        // Suspended Center
        var dbNameSuspended = Guid.NewGuid().ToString();
        var contextSuspended = CreateContext(dbNameSuspended, centerId);
        await SeedDataAsync(contextSuspended, centerId, classId, teacherId, newTeacherId, subjectId, centerStatus: CenterStatus.Suspended, rowVersion: 1);
        var sutSuspended = new UpdateClassUseCase(contextSuspended, _mockTenantContext.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var request = new UpdateClassRequest { ClassName = "New Name", TeacherId = newTeacherId, Status = ClassStatus.Active, RowVersion = "1" };
        var resultSuspended = await sutSuspended.ExecuteAsync(classId, request);
        Assert.Equal(ErrorCodes.ResourceNotFound, resultSuspended.ErrorCode);

        var classSuspended = await contextSuspended.Classes.FirstAsync(c => c.ClassId == classId);
        Assert.Equal("Old Class", classSuspended.ClassName);
        Assert.Equal(1ul, classSuspended.RowVersion);

        // Deleted Center
        var dbNameDeleted = Guid.NewGuid().ToString();
        var contextDeleted = CreateContext(dbNameDeleted, centerId);
        await SeedDataAsync(contextDeleted, centerId, classId, teacherId, newTeacherId, subjectId, centerDeleted: true, rowVersion: 1);
        var sutDeleted = new UpdateClassUseCase(contextDeleted, _mockTenantContext.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var resultDeleted = await sutDeleted.ExecuteAsync(classId, request);
        Assert.Equal(ErrorCodes.ResourceNotFound, resultDeleted.ErrorCode);

        var classDeleted = await contextDeleted.Classes.FirstAsync(c => c.ClassId == classId);
        Assert.Equal("Old Class", classDeleted.ClassName);
        Assert.Equal(1ul, classDeleted.RowVersion);
    }

    [Fact]
    public async Task InvalidSubject_ReturnsNotFound()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;

        // Subject Soft Deleted
        var dbNameDeleted = Guid.NewGuid().ToString();
        var ctxDeleted = CreateContext(dbNameDeleted, centerId);
        var classId1 = Guid.NewGuid();
        var teacherId1 = Guid.NewGuid();
        var newTeacherId1 = Guid.NewGuid();
        var subjectId1 = Guid.NewGuid();

        await SeedDataAsync(ctxDeleted, centerId, classId1, teacherId1, newTeacherId1, subjectId1, rowVersion: 1);
        var subject1 = await ctxDeleted.Subjects.FirstAsync(s => s.SubjectId == subjectId1);
        subject1.IsDeleted = true;
        await ctxDeleted.SaveChangesAsync();

        var originalClass1 = await ctxDeleted.Classes.AsNoTracking().FirstAsync(c => c.ClassId == classId1);

        var sutDeleted = new UpdateClassUseCase(ctxDeleted, _mockTenantContext.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var req1 = new UpdateClassRequest { ClassName = "New Name", TeacherId = newTeacherId1, Status = ClassStatus.Active, RowVersion = "1" };
        var result1 = await sutDeleted.ExecuteAsync(classId1, req1);

        Assert.False(result1.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result1.ErrorCode);
        Assert.Null(result1.Data);

        var class1 = await ctxDeleted.Classes.FirstAsync(c => c.ClassId == classId1);
        Assert.Equal(originalClass1.ClassName, class1.ClassName);
        Assert.Equal(originalClass1.TeacherId, class1.TeacherId);
        Assert.Equal(originalClass1.Status, class1.Status);
        Assert.Equal(originalClass1.RowVersion, class1.RowVersion);
        Assert.Equal(originalClass1.CreatedAt, class1.CreatedAt);
        Assert.Equal(originalClass1.CreatedBy, class1.CreatedBy);
        Assert.Equal(originalClass1.UpdatedAt, class1.UpdatedAt);
        Assert.Equal(originalClass1.UpdatedBy, class1.UpdatedBy);
        Assert.Equal(originalClass1.SubjectId, class1.SubjectId);
        Assert.Equal(originalClass1.AcademicYear, class1.AcademicYear);

        // Subject Cross Tenant
        var dbNameCross = Guid.NewGuid().ToString();
        var ctxCross = CreateContext(dbNameCross, centerId);
        var classId2 = Guid.NewGuid();
        var teacherId2 = Guid.NewGuid();
        var newTeacherId2 = Guid.NewGuid();
        var subjectId2 = Guid.NewGuid();

        ctxCross.Centers.Add(new Center { CenterId = centerId, CenterName = "C", CenterCode="C", Timezone="UTC", Status = CenterStatus.Active, CreatedAt = SeedTimeUtc, UpdatedAt = SeedTimeUtc });
        var crossCenterId = Guid.NewGuid();
        ctxCross.Subjects.Add(new Subject { SubjectId = subjectId2, CenterId = crossCenterId, SubjectName = "S", SubjectCode="S", CreatedAt = SeedTimeUtc, UpdatedAt = SeedTimeUtc });

        var originalUser2 = new User { UserId = teacherId2, CenterId = centerId, DisplayName = "T1", RoleName = UserRole.Teacher, Username="T1", PasswordHash="H", CreatedAt = SeedTimeUtc, UpdatedAt = SeedTimeUtc };
        var newUser2 = new User { UserId = newTeacherId2, CenterId = centerId, DisplayName = "T2", RoleName = UserRole.Teacher, Username="T2", PasswordHash="H", CreatedAt = SeedTimeUtc, UpdatedAt = SeedTimeUtc };
        ctxCross.Users.Add(originalUser2);
        ctxCross.Users.Add(newUser2);

        ctxCross.Teachers.Add(new Teacher { TeacherId = teacherId2, CenterId = centerId, User = originalUser2, CreatedAt = SeedTimeUtc, UpdatedAt = SeedTimeUtc });
        ctxCross.Teachers.Add(new Teacher { TeacherId = newTeacherId2, CenterId = centerId, User = newUser2, CreatedAt = SeedTimeUtc, UpdatedAt = SeedTimeUtc });

        ctxCross.Classes.Add(new Class { ClassId = classId2, CenterId = centerId, ClassName = "Old Class", AcademicYear = "2026", SubjectId = subjectId2, TeacherId = teacherId2, CreatedAt = SeedTimeUtc, CreatedBy = Guid.NewGuid(), UpdatedAt = SeedTimeUtc, UpdatedBy = Guid.NewGuid(), Status = ClassStatus.Active, RowVersion = 1 });
        await ctxCross.SaveChangesAsync();

        var originalClass2 = await ctxCross.Classes.AsNoTracking().FirstAsync(c => c.ClassId == classId2);

        var sutCross = new UpdateClassUseCase(ctxCross, _mockTenantContext.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var req2 = new UpdateClassRequest { ClassName = "New Name", TeacherId = newTeacherId2, Status = ClassStatus.Active, RowVersion = "1" };
        var result2 = await sutCross.ExecuteAsync(classId2, req2);

        Assert.False(result2.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result2.ErrorCode);
        Assert.Null(result2.Data);

        var class2 = await ctxCross.Classes.FirstAsync(c => c.ClassId == classId2);
        Assert.Equal(originalClass2.ClassName, class2.ClassName);
        Assert.Equal(originalClass2.TeacherId, class2.TeacherId);
        Assert.Equal(originalClass2.Status, class2.Status);
        Assert.Equal(originalClass2.RowVersion, class2.RowVersion);
        Assert.Equal(originalClass2.CreatedAt, class2.CreatedAt);
        Assert.Equal(originalClass2.CreatedBy, class2.CreatedBy);
        Assert.Equal(originalClass2.UpdatedAt, class2.UpdatedAt);
        Assert.Equal(originalClass2.UpdatedBy, class2.UpdatedBy);
        Assert.Equal(originalClass2.SubjectId, class2.SubjectId);
        Assert.Equal(originalClass2.AcademicYear, class2.AcademicYear);
    }

    [Fact]
    public async Task DuplicateSameTenant_ReturnsDuplicateResource()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName, centerId);

        var classId1 = Guid.NewGuid();
        var classId2 = Guid.NewGuid(); // Existing duplicate
        var originalTeacherId = Guid.NewGuid();
        var newTeacherId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        await SeedDataAsync(context, centerId, classId1, originalTeacherId, newTeacherId, subjectId, rowVersion: 1);

        context.Classes.Add(new Class { ClassId = classId2, CenterId = centerId, ClassName = "DuplicateName", AcademicYear = "2026", SubjectId = subjectId, TeacherId = originalTeacherId, CreatedAt = SeedTimeUtc, UpdatedAt = SeedTimeUtc, Status = ClassStatus.Active, RowVersion = 1 });
        await context.SaveChangesAsync();

        var sut = new UpdateClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var request = new UpdateClassRequest { ClassName = "DuplicateName", TeacherId = newTeacherId, Status = ClassStatus.Active, RowVersion = "1" };

        var result = await sut.ExecuteAsync(classId1, request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.DuplicateResource, result.ErrorCode);
    }

    [Fact]
    public async Task DuplicateCrossTenant_ReturnsSuccess()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var otherCenterId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName, centerId);

        var classId1 = Guid.NewGuid();
        var originalTeacherId = Guid.NewGuid();
        var newTeacherId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        await SeedDataAsync(context, centerId, classId1, originalTeacherId, newTeacherId, subjectId, rowVersion: 1);

        context.Classes.Add(new Class { ClassId = Guid.NewGuid(), CenterId = otherCenterId, ClassName = "CrossTenantName", AcademicYear = "2026", SubjectId = subjectId, TeacherId = originalTeacherId, CreatedAt = SeedTimeUtc, UpdatedAt = SeedTimeUtc, Status = ClassStatus.Active, RowVersion = 1 });
        await context.SaveChangesAsync();

        var sut = new UpdateClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var request = new UpdateClassRequest { ClassName = "CrossTenantName", TeacherId = newTeacherId, Status = ClassStatus.Active, RowVersion = "1" };

        var result = await sut.ExecuteAsync(classId1, request);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task DbUpdateConcurrencyException_Mapping_ReturnsConcurrencyConflict()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<EduTwinDbContext>().UseInMemoryDatabase(dbName).Options;
        var mockAccessor = new Mock<ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(centerId);

        var classId = Guid.NewGuid();
        var originalTeacherId = Guid.NewGuid();
        var newTeacherId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        var setupContext = new EduTwinDbContext(options, mockAccessor.Object);
        await SeedDataAsync(setupContext, centerId, classId, originalTeacherId, newTeacherId, subjectId, rowVersion: 1);

        var context = new TestConcurrencyExceptionDbContext(options, mockAccessor.Object);
        var sut = new UpdateClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var request = new UpdateClassRequest { ClassName = "New Name", TeacherId = newTeacherId, Status = ClassStatus.Active, RowVersion = "1" };

        var result = await sut.ExecuteAsync(classId, request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, result.ErrorCode);
    }

    [Fact]
    public async Task DbUpdateException_DuplicateRaceMapping_ReturnsDuplicateResource()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<EduTwinDbContext>().UseInMemoryDatabase(dbName).Options;
        var mockAccessor = new Mock<ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(centerId);

        var classId = Guid.NewGuid();
        var originalTeacherId = Guid.NewGuid();
        var newTeacherId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        var setupContext = new EduTwinDbContext(options, mockAccessor.Object);
        await SeedDataAsync(setupContext, centerId, classId, originalTeacherId, newTeacherId, subjectId, rowVersion: 1);

        var context = new TestRaceConditionDbContext(options, mockAccessor.Object, true, "duplicate key value violates unique constraint \"ux_classes_center_id_class_name_academic_year\"");
        var sut = new UpdateClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var request = new UpdateClassRequest { ClassName = "New Name", TeacherId = newTeacherId, Status = ClassStatus.Active, RowVersion = "1" };

        var result = await sut.ExecuteAsync(classId, request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.DuplicateResource, result.ErrorCode);
        Assert.Equal(1, context.SaveChangesCallCount);
    }

    [Fact]
    public async Task DuplicateSoftDeleted_DbUpdateException_ReturnsDuplicateResource()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<EduTwinDbContext>().UseInMemoryDatabase(dbName).Options;
        var mockAccessor = new Mock<ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(centerId);

        var classId = Guid.NewGuid();
        var originalTeacherId = Guid.NewGuid();
        var newTeacherId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        var setupContext = new EduTwinDbContext(options, mockAccessor.Object);
        await SeedDataAsync(setupContext, centerId, classId, originalTeacherId, newTeacherId, subjectId, rowVersion: 1);

        setupContext.Classes.Add(new Class { ClassId = Guid.NewGuid(), CenterId = centerId, ClassName = "SoftDeletedDuplicate", AcademicYear = "2026", SubjectId = subjectId, TeacherId = originalTeacherId, CreatedAt = SeedTimeUtc, UpdatedAt = SeedTimeUtc, Status = ClassStatus.Active, RowVersion = 1, IsDeleted = true });
        await setupContext.SaveChangesAsync();

        var context = new TestRaceConditionDbContext(options, mockAccessor.Object, true, "ux_classes_center_id_class_name_academic_year");
        var sut = new UpdateClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var request = new UpdateClassRequest { ClassName = "SoftDeletedDuplicate", TeacherId = newTeacherId, Status = ClassStatus.Active, RowVersion = "1" };

        var result = await sut.ExecuteAsync(classId, request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.DuplicateResource, result.ErrorCode);
        Assert.Equal(1, context.SaveChangesCallCount);
    }

    [Fact]
    public async Task DbUpdateException_UnrelatedConstraint_Rethrows()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<EduTwinDbContext>().UseInMemoryDatabase(dbName).Options;
        var mockAccessor = new Mock<ITenantIdAccessor>();
        mockAccessor.Setup(a => a.CenterId).Returns(centerId);

        var classId = Guid.NewGuid();
        var originalTeacherId = Guid.NewGuid();
        var newTeacherId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        var setupContext = new EduTwinDbContext(options, mockAccessor.Object);
        await SeedDataAsync(setupContext, centerId, classId, originalTeacherId, newTeacherId, subjectId, rowVersion: 1);

        var context = new TestRaceConditionDbContext(options, mockAccessor.Object, true, "some_other_fk_constraint");
        var sut = new UpdateClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var request = new UpdateClassRequest { ClassName = "New Name", TeacherId = newTeacherId, Status = ClassStatus.Active, RowVersion = "1" };

        await Assert.ThrowsAsync<DbUpdateException>(() => sut.ExecuteAsync(classId, request));
    }

    [Fact]
    public async Task PassesCancellationToken_ToDependencies()
    {
        var centerId = _mockTenantContext.Object.CenterId!.Value;
        var dbName = Guid.NewGuid().ToString();
        var context = CreateContext(dbName, centerId);

        var cts = new CancellationTokenSource();
        cts.Cancel();

        var sut = new UpdateClassUseCase(context, _mockTenantContext.Object, _mockTimeProvider.Object, _mockLogger.Object);
        var request = new UpdateClassRequest { ClassName = "A", TeacherId = Guid.NewGuid(), Status = ClassStatus.Active, RowVersion = "1" };

        await Assert.ThrowsAsync<OperationCanceledException>(() => sut.ExecuteAsync(Guid.NewGuid(), request, cts.Token));
    }
}

public class TestConcurrencyExceptionDbContext : EduTwinDbContext
{
    public TestConcurrencyExceptionDbContext(DbContextOptions<EduTwinDbContext> options, ITenantIdAccessor tenantIdAccessor)
        : base(options, tenantIdAccessor)
    {
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        throw new DbUpdateConcurrencyException("Mock concurrency");
    }
}
