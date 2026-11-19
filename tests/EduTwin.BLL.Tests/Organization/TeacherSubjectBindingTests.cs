using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.KnowledgeGraph;
using EduTwin.BLL.Organization;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.Organization;

public sealed class TeacherSubjectBindingTests : IDisposable
{
    private readonly Guid _centerId = Guid.NewGuid();
    private readonly Guid _mathId = Guid.NewGuid();
    private readonly Guid _englishId = Guid.NewGuid();
    private readonly EduTwinDbContext _db;
    private readonly ITenantContext _tenant;

    public TeacherSubjectBindingTests()
    {
        var tenant = new Mock<ITenantContext>();
        tenant.SetupGet(t => t.IsResolved).Returns(true);
        tenant.SetupGet(t => t.CenterId).Returns(_centerId);
        tenant.SetupGet(t => t.UserId).Returns(Guid.NewGuid());
        tenant.SetupGet(t => t.Role).Returns(nameof(UserRole.CenterManager));
        _tenant = tenant.Object;
        var accessor = new Mock<ITenantIdAccessor>();
        accessor.SetupGet(t => t.CenterId).Returns(_centerId);
        _db = new EduTwinDbContext(new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, accessor.Object);
        _db.Centers.Add(new Center { CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, CenterId = _centerId, CenterCode = "CENTER", CenterName = "Center", Timezone = "UTC", Status = CenterStatus.Active });
        _db.Subjects.AddRange(
            new Subject { CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, CenterId = _centerId, SubjectId = _mathId, SubjectCode = "MATH", SubjectName = "Toán", IsActive = true },
            new Subject { CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, CenterId = _centerId, SubjectId = _englishId, SubjectCode = "ENG", SubjectName = "Tiếng Anh", IsActive = true });
        _db.SaveChanges();
    }

    private Teacher AddTeacher(Guid? subjectId, UserStatus status = UserStatus.Active, Guid? centerId = null, bool deleted = false)
    {
        var teacher = new Teacher { CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow,
            TeacherId = Guid.NewGuid(), CenterId = centerId ?? _centerId, SubjectId = subjectId, IsDeleted = deleted,
            User = new User { CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, UserId = Guid.NewGuid(), CenterId = centerId ?? _centerId, Username = Guid.NewGuid().ToString(),
                DisplayName = "Teacher", PasswordHash = "hash", RoleName = UserRole.Teacher, Status = status }
        };
        teacher.User.UserId = teacher.TeacherId;
        _db.Teachers.Add(teacher);
        return teacher;
    }

    private Class AddClass(Teacher teacher) {
        var entity = new Class { CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, ClassId = Guid.NewGuid(), CenterId = _centerId, SubjectId = _mathId,
            TeacherId = teacher.TeacherId, ClassName = "Math class", AcademicYear = "2026-2027", GradeLevel = 10, Status = ClassStatus.Active };
        _db.Classes.Add(entity);
        return entity;
    }

    [Theory]
    [InlineData("wrong-subject")]
    [InlineData("unassigned")]
    [InlineData("locked")]
    public async Task CreateClass_RejectsTeacherOutsideSubject(string scenario)
    {
        var teacher = AddTeacher(scenario == "unassigned" ? null : scenario == "wrong-subject" ? _englishId : _mathId,
            scenario == "locked" ? UserStatus.Locked : UserStatus.Active);
        await _db.SaveChangesAsync();
        var sut = new CreateClassUseCase(_db, _tenant, TimeProvider.System, NullLogger<CreateClassUseCase>.Instance);
        var result = await sut.ExecuteAsync(new CreateClassRequest { ClassName = "New", AcademicYear = "2026-2027",
            GradeLevel = 10, SubjectId = _mathId, TeacherId = teacher.TeacherId });
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Empty(await _db.Classes.ToListAsync());
    }

    [Fact]
    public async Task UpdateClass_CannotReopenWhileSubjectIsInactive()
    {
        var teacher = AddTeacher(_mathId);
        var entity = AddClass(teacher);
        entity.Status = ClassStatus.Archived;
        entity.LearningScope = ClassLearningScope.History;
        (await _db.Subjects.SingleAsync(s => s.SubjectId == _mathId)).IsActive = false;
        await _db.SaveChangesAsync();
        var result = await new UpdateClassUseCase(_db, _tenant, TimeProvider.System, NullLogger<UpdateClassUseCase>.Instance)
            .ExecuteAsync(entity.ClassId, new UpdateClassRequest { ClassName = entity.ClassName, TeacherId = teacher.TeacherId,
                GradeLevel = 10, Status = ClassStatus.Active, LifecycleReason = "Mở lại", RowVersion = entity.RowVersion.ToString() });
        Assert.Equal(ErrorCodes.InvalidStateTransition, result.ErrorCode);
        Assert.Equal(ClassStatus.Archived, (await _db.Classes.SingleAsync()).Status);
    }

    [Fact]
    public async Task UpdateClass_RejectsReplacementTeacherFromDifferentSubject()
    {
        var original = AddTeacher(_mathId);
        var replacement = AddTeacher(_englishId);
        var entity = AddClass(original);
        await _db.SaveChangesAsync();
        var sut = new UpdateClassUseCase(_db, _tenant, TimeProvider.System, NullLogger<UpdateClassUseCase>.Instance);
        var result = await sut.ExecuteAsync(entity.ClassId, new UpdateClassRequest { ClassName = entity.ClassName,
            GradeLevel = 10, TeacherId = replacement.TeacherId, Status = ClassStatus.Active, RowVersion = entity.RowVersion.ToString() });
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Equal(original.TeacherId, (await _db.Classes.SingleAsync()).TeacherId);
    }

    [Fact]
    public async Task UpdateTeacher_RejectsSubjectChangeConflictingWithActiveClasses()
    {
        var teacher = AddTeacher(_mathId);
        AddClass(teacher);
        await _db.SaveChangesAsync();
        var result = await new UpdateTeacherUseCase(_db, _tenant, TimeProvider.System).ExecuteAsync(teacher.TeacherId,
            new UpdateTeacherRequest { DisplayName = "Changed", SubjectId = _englishId, Status = UserStatus.Active, RowVersion = teacher.RowVersion.ToString() });
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Equal(_mathId, (await _db.Teachers.SingleAsync()).SubjectId);
        Assert.Equal("Teacher", teacher.User.DisplayName);
    }

    [Fact]
    public async Task ListTeachers_SubjectFilterIsAppliedBeforePaginationAndCounts()
    {
        var expected = AddTeacher(_mathId);
        AddTeacher(_mathId);
        AddTeacher(_englishId);
        AddTeacher(null);
        AddTeacher(_mathId, UserStatus.Locked);
        AddTeacher(_mathId, deleted: true);
        AddTeacher(_mathId, centerId: Guid.NewGuid());
        await _db.SaveChangesAsync();
        var result = await new ListTeachersUseCase(_db, _tenant).ExecuteAsync(new TeacherListQuery {
            SubjectId = _mathId, Status = UserStatus.Active, PageSize = 1 });
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.TotalItems);
        Assert.Equal(2, result.TotalPages);
        Assert.Single(result.Data!);
        Assert.All(result.Data!, t => { Assert.Equal(_mathId, t.SubjectId); Assert.Equal("Toán", t.Department); });
        var secondPage = await new ListTeachersUseCase(_db, _tenant).ExecuteAsync(new TeacherListQuery {
            SubjectId = _mathId, Status = UserStatus.Active, PageSize = 1, Page = 2 });
        Assert.NotEqual(result.Data![0].TeacherId, secondPage.Data![0].TeacherId);
        Assert.Contains(expected.TeacherId.ToString(), result.Data.Concat(secondPage.Data).Select(t => t.TeacherId));
    }

    [Fact]
    public async Task DeleteSubject_BlocksSubjectStillAssignedToTeacher()
    {
        AddTeacher(_mathId);
        await _db.SaveChangesAsync();
        var result = await new DeleteSubjectUseCase(_db, _tenant, TimeProvider.System).ExecuteAsync(_mathId);
        Assert.Equal(ErrorCodes.InvalidStateTransition, result.ErrorCode);
        Assert.False((await _db.Subjects.SingleAsync(s => s.SubjectId == _mathId)).IsDeleted);
    }

    public void Dispose() => _db.Dispose();
}
