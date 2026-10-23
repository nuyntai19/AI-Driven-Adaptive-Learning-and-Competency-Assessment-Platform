using EduTwin.BLL.CurriculumAndQuestions;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.CurriculumAndQuestions;

public sealed class CurriculumGradeScopeTests : IDisposable
{
    private readonly Guid _center = Guid.NewGuid(), _actor = Guid.NewGuid(), _subject = Guid.NewGuid();
    private readonly Guid _version10 = Guid.NewGuid(), _version12 = Guid.NewGuid();
    private readonly EduTwinDbContext _db;
    private readonly ITenantContext _tenant;

    public CurriculumGradeScopeTests()
    {
        var tenant = new Mock<ITenantContext>();
        tenant.SetupGet(x => x.IsResolved).Returns(true);
        tenant.SetupGet(x => x.CenterId).Returns(_center);
        tenant.SetupGet(x => x.UserId).Returns(_actor);
        tenant.SetupGet(x => x.Role).Returns(nameof(UserRole.Teacher));
        _tenant = tenant.Object;
        var accessor = new Mock<ITenantIdAccessor>();
        accessor.SetupGet(x => x.CenterId).Returns(_center);
        _db = new(new DbContextOptionsBuilder<EduTwinDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options, accessor.Object);
        _db.Centers.Add(new Center { CenterId = _center, CenterCode = "SCOPE", CenterName = "Scope", Timezone = "UTC", Status = CenterStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _db.Users.Add(new User { CenterId = _center, UserId = _actor, Username = "teacher", DisplayName = "Teacher", PasswordHash = "test", RoleName = UserRole.Teacher, Status = UserStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _db.Teachers.Add(new Teacher { CenterId = _center, TeacherId = _actor, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _db.Subjects.Add(new Subject { CenterId = _center, SubjectId = _subject, SubjectCode = "MATH", SubjectName = "Toán", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        foreach (var (id, grade) in new[] { (_version10, (byte)10), (_version12, (byte)12) })
            _db.KnowledgeGraphVersions.Add(new KnowledgeGraphVersion { CenterId = _center, GraphVersionId = id, SubjectId = _subject, GradeLevel = grade, Status = "Published", IsCurrent = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        for (ulong id = 1; id <= 3; id++)
            _db.KnowledgeNodes.Add(new KnowledgeNode { CenterId = _center, NodeId = id, SubjectId = _subject, NodeCode = $"NODE-{id}", NodeName = $"Node {id}", NodeType = NodeType.Topic, IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _db.KnowledgeNodeGrades.AddRange(
            new KnowledgeNodeGrade { CenterId = _center, GraphVersionId = _version10, NodeId = 1, CreatedAt = DateTime.UtcNow },
            new KnowledgeNodeGrade { CenterId = _center, GraphVersionId = _version12, NodeId = 2, CreatedAt = DateTime.UtcNow });
        _db.SaveChanges();
    }

    private Curriculum Draft(Guid? version = null, byte grade = 10, ulong node = 1)
    {
        var c = new Curriculum { CenterId = _center, CurriculumId = Guid.NewGuid(), TeacherId = _actor,
            SubjectId = _subject, Title = "Scope curriculum", GradeLevel = grade, GraphVersionId = version ?? _version10, ReviewStatus = ReviewStatus.Draft, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _db.Curriculums.Add(c);
        _db.CurriculumNodes.Add(new CurriculumNode { CenterId = _center, CurriculumId = c.CurriculumId, NodeId = node, OrderIndex = 1, CreatedAt = DateTime.UtcNow });
        _db.SaveChanges();
        return c;
    }

    private Class Class(byte grade, string year = "2026-2027", Guid? teacher = null)
    {
        var c = new Class { ClassId = Guid.NewGuid(), CenterId = _center, TeacherId = teacher ?? _actor,
            SubjectId = _subject, ClassName = $"Class {grade}", GradeLevel = grade, AcademicYear = year, Status = ClassStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        _db.Classes.Add(c);
        _db.SaveChanges();
        return c;
    }

    [Theory]
    [InlineData("2")]
    [InlineData("3")]
    public async Task Create_RejectsOtherGradeAndUnclassifiedNode(string node)
    {
        var result = await new CreateCurriculumUseCase(_db, _tenant, TimeProvider.System).ExecuteAsync(new()
        { SubjectId = _subject, Title = "New", GradeLevel = 10, NodeIds = [node] });
        Assert.Equal(ErrorCodes.AcademicScopeConflict, result.ErrorCode);
        Assert.Equal([node], result.ScopeConflict!.InvalidNodeIds);
        Assert.Empty(await _db.Curriculums.ToListAsync());
    }

    [Fact]
    public async Task Create_PinsApprovedVersionAndReturnsGrade()
    {
        var result = await new CreateCurriculumUseCase(_db, _tenant, TimeProvider.System).ExecuteAsync(new()
        { SubjectId = _subject, Title = "New", GradeLevel = 12, NodeIds = ["2"] });
        Assert.True(result.IsSuccess);
        Assert.Equal(_version12.ToString("D"), result.Data!.GraphVersionId);
        Assert.Equal((byte)12, result.Data.GradeLevel);
    }

    [Fact]
    public async Task Update_GradeChangeCannotRetainIncompatibleNodes()
    {
        var c = Draft();
        var result = await new UpdateCurriculumUseCase(_db, _tenant, TimeProvider.System).ExecuteAsync(c.CurriculumId,
            new() { Title = "Changed", GradeLevel = 12, RowVersion = "1" });
        Assert.Equal(ErrorCodes.AcademicScopeConflict, result.ErrorCode);
        Assert.Equal(["1"], result.ScopeConflict!.InvalidNodeIds);
        Assert.Equal((byte)10, c.GradeLevel);
        Assert.Equal(_version10, c.GraphVersionId);
    }

    [Fact]
    public async Task Update_RepairsGradeNodesAndPlannedClassesAtomically()
    {
        var c = Draft(); var oldClass = Class(10); var nextClass = Class(12);
        _db.CurriculumClasses.Add(new CurriculumClass { CenterId = _center, CurriculumId = c.CurriculumId, ClassId = oldClass.ClassId, AssignedBy = _actor });
        await _db.SaveChangesAsync();
        var result = await new UpdateCurriculumUseCase(_db, _tenant, TimeProvider.System).ExecuteAsync(c.CurriculumId,
            new() { Title = "Changed", GradeLevel = 12, NodeIds = ["2"], ClassIds = [nextClass.ClassId.ToString()], RowVersion = "1" });
        Assert.True(result.IsSuccess);
        Assert.Equal(_version12.ToString(), result.Data!.GraphVersionId);
        Assert.Equal(["2"], result.Data.NodeIds);
        Assert.Equal([nextClass.ClassId.ToString()], result.Data.ClassIds);
        Assert.Equal("2", result.Data.RowVersion);
        Assert.Equal([2UL], await _db.CurriculumNodes.Where(n => n.CurriculumId == c.CurriculumId).Select(n => n.NodeId).ToListAsync());
    }

    [Fact]
    public async Task Update_ReportsPlannedClassConflictAndLeavesSelection()
    {
        var c = Draft(); var cls = Class(10);
        _db.CurriculumClasses.Add(new CurriculumClass { CenterId = _center, CurriculumId = c.CurriculumId, ClassId = cls.ClassId, AssignedBy = _actor });
        await _db.SaveChangesAsync();
        var result = await new UpdateCurriculumUseCase(_db, _tenant, TimeProvider.System).ExecuteAsync(c.CurriculumId,
            new() { Title = "Changed", GradeLevel = 12, NodeIds = ["2"], RowVersion = "1" });
        Assert.Equal([cls.ClassId.ToString()], result.ScopeConflict!.InvalidClassIds);
        Assert.Equal((byte)10, c.GradeLevel);
        Assert.Equal(1UL, (await _db.CurriculumNodes.SingleAsync()).NodeId);
    }

    [Fact]
    public async Task Update_CannotClearRequiredGrade()
    {
        var c = Draft();
        var result = await new UpdateCurriculumUseCase(_db, _tenant, TimeProvider.System).ExecuteAsync(c.CurriculumId,
            new() { Title = "Changed", RowVersion = "1" });
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.Equal((byte)10, c.GradeLevel);
    }

    [Fact]
    public async Task AssignNodes_RejectsOtherGradeAndPreservesExistingComposition()
    {
        var c = Draft();
        var result = await new AssignCurriculumNodesUseCase(_db, _tenant, TimeProvider.System).ExecuteAsync(c.CurriculumId,
            new() { NodeIds = ["2"], RowVersion = "1" });
        Assert.Equal(ErrorCodes.AcademicScopeConflict, result.ErrorCode);
        Assert.Equal(1UL, (await _db.CurriculumNodes.SingleAsync()).NodeId);
    }

    [Fact]
    public async Task AssignNodes_ReturnsGradeAndStablePin()
    {
        var c = Draft();
        var result = await new AssignCurriculumNodesUseCase(_db, _tenant, TimeProvider.System).ExecuteAsync(c.CurriculumId,
            new() { NodeIds = ["1"], RowVersion = "1" });
        Assert.True(result.IsSuccess);
        Assert.Equal((byte)10, result.Data!.GradeLevel);
        Assert.Equal(_version10.ToString(), result.Data.GraphVersionId);
    }

    [Fact]
    public async Task Publish_DoesNotPromoteUnreviewedLegacyComposition()
    {
        var legacy = new KnowledgeGraphVersion { CenterId = _center, GraphVersionId = Guid.NewGuid(), SubjectId = _subject,
            GradeLevel = 10, Status = "Published", IsLegacy = true };
        _db.KnowledgeGraphVersions.Add(legacy);
        _db.KnowledgeGraphVersions.Single(v => v.GraphVersionId == _version10).IsCurrent = false;
        await _db.SaveChangesAsync();
        var c = Draft(legacy.GraphVersionId);
        var result = await new PublishCurriculumUseCase(_db, _tenant, TimeProvider.System).ExecuteAsync(c.CurriculumId, new() { RowVersion = "1" });
        Assert.Equal(ErrorCodes.AcademicScopeConflict, result.ErrorCode);
        Assert.Equal(ReviewStatus.Draft, c.ReviewStatus);
    }

    [Fact]
    public async Task Publish_KeepsPreviouslyApprovedNonCurrentPin()
    {
        var c = Draft();
        _db.KnowledgeGraphVersions.Single(v => v.GraphVersionId == _version10).IsCurrent = false;
        await _db.SaveChangesAsync();
        var result = await new PublishCurriculumUseCase(_db, _tenant, TimeProvider.System).ExecuteAsync(c.CurriculumId, new() { RowVersion = "1" });
        Assert.True(result.IsSuccess);
        Assert.Equal(_version10.ToString(), result.Data!.GraphVersionId);
    }

    [Fact]
    public async Task List_FiltersClassAndYearWithoutExposingOtherTeachersClasses()
    {
        var c = Draft(); c.Visibility = MaterialVisibility.Shared; c.ReviewStatus = ReviewStatus.Published;
        var own = Class(10); var foreign = Class(10, teacher: Guid.NewGuid());
        foreach (var cls in new[] { own, foreign })
            _db.ClassCurriculumApplications.Add(new ClassCurriculumApplication { CenterId = _center, ApplicationId = Guid.NewGuid(), ClassId = cls.ClassId,
                CurriculumId = c.CurriculumId, SubjectId = _subject, AssignedBy = cls.TeacherId, StartedAt = DateTime.UtcNow, ApplicationRole = "Primary" });
        await _db.SaveChangesAsync();
        var useCase = new ListCurriculumsUseCase(_db, _tenant);
        var result = await useCase.ExecuteAsync(new() { ClassId = own.ClassId, AcademicYear = "2026-2027" });
        Assert.True(result.IsSuccess);
        var dto = Assert.Single(result.Data!);
        Assert.Equal(own.ClassId.ToString(), Assert.Single(dto.ApplicationClasses).ClassId);
        Assert.Equal("2026-2027", dto.ApplicationClasses[0].AcademicYear);
        Assert.Empty((await useCase.ExecuteAsync(new() { AcademicYear = "2027-2028" })).Data!);
        Assert.Equal(ErrorCodes.ResourceNotFound, (await useCase.ExecuteAsync(new() { ClassId = foreign.ClassId })).ErrorCode);
    }

    public void Dispose() => _db.Dispose();
}
