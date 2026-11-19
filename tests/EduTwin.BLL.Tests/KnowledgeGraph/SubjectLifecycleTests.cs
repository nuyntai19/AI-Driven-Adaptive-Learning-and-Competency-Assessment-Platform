using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.KnowledgeGraph;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.KnowledgeGraph;

public sealed class SubjectLifecycleTests : IDisposable
{
    private readonly Guid _centerId = Guid.NewGuid();
    private readonly Guid _subjectId = Guid.NewGuid();
    private readonly Guid _actorId = Guid.NewGuid();
    private readonly Mock<ITenantContext> _tenant = new();
    private readonly EduTwinDbContext _db;
    private static readonly DateTime Now = new(2026, 11, 20, 0, 0, 0, DateTimeKind.Utc);

    public SubjectLifecycleTests()
    {
        _tenant.SetupGet(t => t.IsResolved).Returns(true);
        _tenant.SetupGet(t => t.CenterId).Returns(_centerId);
        _tenant.SetupGet(t => t.UserId).Returns(_actorId);
        _tenant.SetupGet(t => t.Role).Returns(nameof(UserRole.CenterManager));
        var accessor = new Mock<ITenantIdAccessor>();
        accessor.SetupGet(t => t.CenterId).Returns(_centerId);
        _db = new EduTwinDbContext(new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, accessor.Object);
        _db.Centers.Add(new Center { CenterId = _centerId, CenterCode = "TEST", CenterName = "Center", Timezone = "UTC",
            Status = CenterStatus.Active, CreatedAt = Now, UpdatedAt = Now });
        _db.Subjects.Add(new Subject { SubjectId = _subjectId, CenterId = _centerId, SubjectCode = "MATH", SubjectName = "Toán",
            IsActive = true, CreatedAt = Now, UpdatedAt = Now });
        _db.SaveChanges();
        _db.ChangeTracker.Clear();
    }

    private Class AddClass(ClassStatus status = ClassStatus.Active, bool deleted = false, Guid? subject = null, Guid? center = null)
    {
        var entity = new Class { ClassId = Guid.NewGuid(), CenterId = center ?? _centerId, SubjectId = subject ?? _subjectId,
            TeacherId = _actorId, ClassName = Guid.NewGuid().ToString(), AcademicYear = "2026-2027", GradeLevel = 12,
            Status = status, IsDeleted = deleted, CreatedAt = Now, UpdatedAt = Now };
        _db.Classes.Add(entity);
        return entity;
    }

    private async Task<UpdateSubjectResult> Update(bool active, string name = "Toán")
    {
        var version = await _db.Subjects.AsNoTracking().Where(s => s.SubjectId == _subjectId).Select(s => s.RowVersion).SingleAsync();
        _db.ChangeTracker.Clear();
        return await new UpdateSubjectUseCase(_db, _tenant.Object, TimeProvider.System).ExecuteAsync(_subjectId,
            new UpdateSubjectRequest { SubjectCode = "MATH", SubjectName = name, IsActive = active, RowVersion = version.ToString() }, CancellationToken.None);
    }

    [Fact]
    public async Task Deactivate_WithActiveClassesWithoutStudents_BlocksAllSubjectChanges()
    {
        AddClass(); AddClass();
        AddClass(ClassStatus.Archived);
        AddClass(deleted: true);
        AddClass(subject: Guid.NewGuid());
        AddClass(center: Guid.NewGuid());
        await _db.SaveChangesAsync();
        var result = await Update(false, "Tên mới");
        Assert.Equal(ErrorCodes.InvalidStateTransition, result.ErrorCode);
        Assert.Equal(2, result.ActiveClassCount);
        Assert.Contains("2 lớp đang hoạt động", result.ErrorMessage);
        _db.ChangeTracker.Clear();
        var subject = await _db.Subjects.SingleAsync();
        Assert.True(subject.IsActive);
        Assert.Equal("Toán", subject.SubjectName);
        Assert.Equal(1ul, subject.RowVersion);
        Assert.Empty(await _db.ClassStudents.ToListAsync());
    }

    [Theory]
    [InlineData("none")]
    [InlineData("archived")]
    [InlineData("deleted")]
    [InlineData("other-subject")]
    [InlineData("other-center")]
    public async Task Deactivate_WithoutActiveClassesInThisSubject_Succeeds(string scenario)
    {
        if (scenario == "archived") AddClass(ClassStatus.Archived);
        if (scenario == "deleted") AddClass(deleted: true);
        if (scenario == "other-subject") AddClass(subject: Guid.NewGuid());
        if (scenario == "other-center") AddClass(center: Guid.NewGuid());
        await _db.SaveChangesAsync();
        var result = await Update(false);
        Assert.True(result.IsSuccess);
        Assert.False(result.Data!.IsActive);
        Assert.Equal(0, result.Data.ActiveClassCount);
    }

    [Fact]
    public async Task Reactivate_AndRenameWhileActive_AreAllowedEvenWithActiveClasses()
    {
        var subject = await _db.Subjects.SingleAsync();
        subject.IsActive = false;
        AddClass();
        await _db.SaveChangesAsync();
        var result = await Update(true);
        Assert.True(result.IsSuccess);
        Assert.True(result.Data!.IsActive);
        var renamed = await Update(true, "Toán học");
        Assert.True(renamed.IsSuccess);
        Assert.Equal("Toán học", renamed.Data!.SubjectName);
        Assert.Equal(1, renamed.Data.ActiveClassCount);
    }

    [Fact]
    public async Task ManagerListAndDetail_ReturnSameCount_WhileStudentCatalogueStillIncludesInactiveHistorySubjects()
    {
        AddClass(); AddClass(ClassStatus.Archived); AddClass(deleted: true); AddClass(subject: Guid.NewGuid());
        await _db.SaveChangesAsync();
        var list = await new ListSubjectsUseCase(_db, _tenant.Object).ExecuteAsync(new SubjectListQuery(), CancellationToken.None);
        var detail = await new GetSubjectUseCase(_db, _tenant.Object).ExecuteAsync(_subjectId, CancellationToken.None);
        Assert.Equal(1, Assert.Single(list.Data!).ActiveClassCount);
        Assert.Equal(1, detail.Data!.ActiveClassCount);

        var subject = await _db.Subjects.SingleAsync();
        subject.IsActive = false;
        await _db.SaveChangesAsync();
        _tenant.SetupGet(t => t.Role).Returns(nameof(UserRole.Student));
        var historical = await new ListSubjectsUseCase(_db, _tenant.Object).ExecuteAsync(new SubjectListQuery(), CancellationToken.None);
        Assert.False(Assert.Single(historical.Data!).IsActive);
        Assert.Null(Assert.Single(historical.Data!).ActiveClassCount);
        var current = await new ListSubjectsUseCase(_db, _tenant.Object).ExecuteAsync(new SubjectListQuery { IsActive = true }, CancellationToken.None);
        Assert.Empty(current.Data!);
        var historicalDetail = await new GetSubjectUseCase(_db, _tenant.Object).ExecuteAsync(_subjectId, CancellationToken.None);
        Assert.True(historicalDetail.IsSuccess);
        Assert.False(historicalDetail.Data!.IsActive);
    }

    public void Dispose() => _db.Dispose();
}
