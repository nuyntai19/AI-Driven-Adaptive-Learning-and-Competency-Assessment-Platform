using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.KnowledgeGraph;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Tests.KnowledgeGraph;

public class CreateKnowledgeNodeUseCaseTests
{
    private readonly EduTwinDbContext _dbContext;
    private readonly Mock<ITenantContext> _tenantMock;
    private readonly Mock<TimeProvider> _timeProviderMock;
    private readonly CreateKnowledgeNodeUseCase _sut;

    private readonly DbContextOptions<EduTwinDbContext> _options;
    private readonly EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor _tenantAccessor;

    public CreateKnowledgeNodeUseCaseTests()
    {
        _options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var tenantAccessorMock = new Mock<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>();
        _tenantMock = new Mock<ITenantContext>();
        tenantAccessorMock.Setup(x => x.CenterId).Returns(() => _tenantMock.Object.CenterId);
        _tenantAccessor = tenantAccessorMock.Object;

        _timeProviderMock = new Mock<TimeProvider>();
        var utcNow = new DateTimeOffset(2026, 7, 22, 10, 0, 0, TimeSpan.Zero);
        _timeProviderMock.Setup(x => x.GetUtcNow()).Returns(utcNow);

        _dbContext = new EduTwinDbContext(_options, _tenantAccessor);
        _sut = new CreateKnowledgeNodeUseCase(_dbContext, _tenantMock.Object, _timeProviderMock.Object);
    }

    private void SetupValidTenant(Guid centerId, string role = nameof(UserRole.Teacher))
    {
        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(role);
    }

    private CreateKnowledgeNodeRequest GetValidRequest(Guid subjectId, string? parentId = null) => new()
    {
        SubjectId = subjectId,
        ParentNodeId = parentId,
        NodeType = "Topic",
        NodeCode = "T01",
        NodeName = "Topic 1",
        Description = "Desc",
        OrderIndex = 1,
        ExamImportance = 10m,
        EstimatedLearningMinutes = 60,
        IsActive = true
    };

    [Theory]
    [InlineData(nameof(UserRole.Teacher))]
    [InlineData(nameof(UserRole.CenterManager))]
    public async Task ExecuteAsync_ValidRole_CreatesNode(string role)
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        SetupValidTenant(centerId, role);

        _dbContext.Centers.Add(new Center { CenterId = centerId, CenterCode = "C01", CenterName = "C", Timezone = "UTC", Status = EduTwin.Contracts.Organization.CenterStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.Subjects.Add(new EduTwin.DAL.Organization.Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "S01", SubjectName = "S01", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        var request = GetValidRequest(subjectId);
        var result = await _sut.ExecuteAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(request.NodeCode, result.Data.NodeCode);

        var nodeInDb = await _dbContext.KnowledgeNodes.SingleOrDefaultAsync();
        Assert.NotNull(nodeInDb);
        Assert.NotEqual(0ul, nodeInDb.NodeId);
        Assert.Equal(centerId, nodeInDb.CenterId);
        Assert.Equal(subjectId, nodeInDb.SubjectId);
        Assert.Null(nodeInDb.ParentNodeId);
        Assert.Equal(NodeType.Topic, nodeInDb.NodeType);
        Assert.Equal(request.NodeCode, nodeInDb.NodeCode);
        Assert.Equal(request.NodeName, nodeInDb.NodeName);
        Assert.Equal(request.Description, nodeInDb.Description);
        Assert.Equal(request.OrderIndex, nodeInDb.OrderIndex);
        Assert.Equal(request.ExamImportance, nodeInDb.ExamImportance);
        Assert.Equal(request.EstimatedLearningMinutes, nodeInDb.EstimatedLearningMinutes);
        Assert.Equal(request.IsActive, nodeInDb.IsActive);
        Assert.False(nodeInDb.IsDeleted);
        Assert.Equal(1ul, nodeInDb.RowVersion);
        Assert.Equal(_tenantMock.Object.UserId, nodeInDb.CreatedBy);
        Assert.Equal(_tenantMock.Object.UserId, nodeInDb.UpdatedBy);
        Assert.Equal(new DateTimeOffset(2026, 7, 22, 10, 0, 0, TimeSpan.Zero).UtcDateTime, nodeInDb.CreatedAt);
        Assert.Equal(new DateTimeOffset(2026, 7, 22, 10, 0, 0, TimeSpan.Zero).UtcDateTime, nodeInDb.UpdatedAt);
        Assert.Equal(DateTimeKind.Utc, nodeInDb.CreatedAt.Kind);
        Assert.Equal(DateTimeKind.Utc, nodeInDb.UpdatedAt.Kind);

        // Ensure DTO invariant parsing
        Assert.Equal(nodeInDb.NodeId.ToString(CultureInfo.InvariantCulture), result.Data.NodeId);
        Assert.Equal(subjectId.ToString("D").ToLowerInvariant(), result.Data.SubjectId);
    }

    [Fact]
    public async Task ExecuteAsync_TenantUnresolved_ReturnsResourceNotFound()
    {
        _tenantMock.SetupGet(x => x.IsResolved).Returns(false);
        var result = await _sut.ExecuteAsync(GetValidRequest(Guid.NewGuid()));
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("Student")]
    [InlineData("teacher")]
    public async Task ExecuteAsync_InvalidRole_ReturnsResourceNotFound(string role)
    {
        SetupValidTenant(Guid.NewGuid(), role);
        var result = await _sut.ExecuteAsync(GetValidRequest(Guid.NewGuid()));
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_CenterMissingOrSuspended_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        SetupValidTenant(centerId);

        _dbContext.Centers.Add(new Center { CenterId = centerId, CenterCode = "C01", CenterName = "C", Timezone = "UTC", Status = EduTwin.Contracts.Organization.CenterStatus.Suspended, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.Subjects.Add(new EduTwin.DAL.Organization.Subject { SubjectId = Guid.NewGuid(), CenterId = centerId, SubjectCode = "S01", SubjectName = "S01", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ExecuteAsync(GetValidRequest(Guid.NewGuid()));
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_SubjectCrossTenant_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        SetupValidTenant(centerId);

        _dbContext.Centers.Add(new Center { CenterId = centerId, CenterCode = "C01", CenterName = "C", Timezone = "UTC", Status = EduTwin.Contracts.Organization.CenterStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.Subjects.Add(new EduTwin.DAL.Organization.Subject { SubjectId = subjectId, CenterId = Guid.NewGuid(), SubjectCode = "S01", SubjectName = "S01", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ExecuteAsync(GetValidRequest(subjectId));
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_ValidParent_CreatesNode()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        SetupValidTenant(centerId);

        _dbContext.Centers.Add(new Center { CenterId = centerId, CenterCode = "C01", CenterName = "C", Timezone = "UTC", Status = EduTwin.Contracts.Organization.CenterStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.Subjects.Add(new EduTwin.DAL.Organization.Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "S01", SubjectName = "S01", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.KnowledgeNodes.Add(new KnowledgeNode { NodeId = 10, CenterId = centerId, SubjectId = subjectId, NodeType = NodeType.Topic, NodeCode = "P01", NodeName = "P", IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        var request = GetValidRequest(subjectId, "10");
        var result = await _sut.ExecuteAsync(request);

        Assert.True(result.IsSuccess);
        Assert.Equal("10", result.Data!.ParentNodeId);
    }

    [Fact]
    public async Task ExecuteAsync_CrossTenantParent_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        SetupValidTenant(centerId);

        _dbContext.Centers.Add(new Center { CenterId = centerId, CenterCode = "C01", CenterName = "C", Timezone = "UTC", Status = EduTwin.Contracts.Organization.CenterStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.Subjects.Add(new EduTwin.DAL.Organization.Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "S01", SubjectName = "S01", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.KnowledgeNodes.Add(new KnowledgeNode { NodeId = 10, CenterId = Guid.NewGuid(), SubjectId = subjectId, NodeType = NodeType.Topic, NodeCode = "P01", NodeName = "P", IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        var request = GetValidRequest(subjectId, "10");
        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_DuplicateNodeCodeRace_ReturnsDuplicateResource()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        SetupValidTenant(centerId);

        _dbContext.Centers.Add(new Center { CenterId = centerId, CenterCode = "C01", CenterName = "C", Timezone = "UTC", Status = EduTwin.Contracts.Organization.CenterStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.Subjects.Add(new EduTwin.DAL.Organization.Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "S01", SubjectName = "S01", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        // Simulate DbUpdateException that matches the unique index for node code
        var innerEx = new Exception("Duplicate entry for key 'ux_knowledge_nodes_center_id_subject_id_node_code'");
        var dbEx = new DbUpdateException("Error", innerEx);

        var sut = new CreateKnowledgeNodeUseCase(new FaultyDbContext(_options, _tenantAccessor, dbEx), _tenantMock.Object, _timeProviderMock.Object);

        var request = GetValidRequest(subjectId);
        var result = await sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.DuplicateResource, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_UnrelatedDbUpdateException_Rethrows()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        SetupValidTenant(centerId);

        _dbContext.Centers.Add(new Center { CenterId = centerId, CenterCode = "C01", CenterName = "C", Timezone = "UTC", Status = EduTwin.Contracts.Organization.CenterStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.Subjects.Add(new EduTwin.DAL.Organization.Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "S01", SubjectName = "S01", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        var innerEx = new Exception("Some other constraint failed");
        var dbEx = new DbUpdateException("Error", innerEx);

        var sut = new CreateKnowledgeNodeUseCase(new FaultyDbContext(_options, _tenantAccessor, dbEx), _tenantMock.Object, _timeProviderMock.Object);

        var request = GetValidRequest(subjectId);
        await Assert.ThrowsAsync<DbUpdateException>(() => sut.ExecuteAsync(request));
    }

    [Fact]
    public async Task ExecuteAsync_CanceledToken_IsHonored()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        SetupValidTenant(centerId);

        _dbContext.Centers.Add(new Center { CenterId = centerId, CenterCode = "C01", CenterName = "C", Timezone = "UTC", Status = EduTwin.Contracts.Organization.CenterStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.Subjects.Add(new EduTwin.DAL.Organization.Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "S01", SubjectName = "S01", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => _sut.ExecuteAsync(GetValidRequest(subjectId), cts.Token));
    }

    [Fact]
    public async Task ExistingDuplicateSameCenterAndSubject_ReturnsDuplicateResource()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        SetupValidTenant(centerId);

        _dbContext.Centers.Add(new Center { CenterId = centerId, CenterCode = "C01", CenterName = "C", Timezone = "UTC", Status = EduTwin.Contracts.Organization.CenterStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.Subjects.Add(new EduTwin.DAL.Organization.Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "S01", SubjectName = "S01", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.KnowledgeNodes.Add(new KnowledgeNode { NodeId = 1, CenterId = centerId, SubjectId = subjectId, NodeType = NodeType.Topic, NodeCode = "T01", NodeName = "Existing", IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        var request = GetValidRequest(subjectId);
        request.NodeCode = "T01";

        var result = await _sut.ExecuteAsync(request);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.DuplicateResource, result.ErrorCode);
    }

    [Fact]
    public async Task ExistingDuplicate_DoesNotAddOrPersistSecondNode()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        SetupValidTenant(centerId);

        _dbContext.Centers.Add(new Center { CenterId = centerId, CenterCode = "C01", CenterName = "C", Timezone = "UTC", Status = EduTwin.Contracts.Organization.CenterStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.Subjects.Add(new EduTwin.DAL.Organization.Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "S01", SubjectName = "S01", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.KnowledgeNodes.Add(new KnowledgeNode { NodeId = 1, CenterId = centerId, SubjectId = subjectId, NodeType = NodeType.Topic, NodeCode = "T01", NodeName = "Existing", IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        var request = GetValidRequest(subjectId);
        request.NodeCode = "T01";

        await _sut.ExecuteAsync(request);

        var nodes = await _dbContext.KnowledgeNodes.ToListAsync();
        Assert.Single(nodes);
    }

    [Fact]
    public async Task SameCodeDifferentSubject_IsAllowed()
    {
        var centerId = Guid.NewGuid();
        var subjectId1 = Guid.NewGuid();
        var subjectId2 = Guid.NewGuid();
        SetupValidTenant(centerId);

        _dbContext.Centers.Add(new Center { CenterId = centerId, CenterCode = "C01", CenterName = "C", Timezone = "UTC", Status = EduTwin.Contracts.Organization.CenterStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.Subjects.Add(new EduTwin.DAL.Organization.Subject { SubjectId = subjectId1, CenterId = centerId, SubjectCode = "S01", SubjectName = "S01", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.Subjects.Add(new EduTwin.DAL.Organization.Subject { SubjectId = subjectId2, CenterId = centerId, SubjectCode = "S02", SubjectName = "S02", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.KnowledgeNodes.Add(new KnowledgeNode { NodeId = 1, CenterId = centerId, SubjectId = subjectId1, NodeType = NodeType.Topic, NodeCode = "T01", NodeName = "Existing", IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        var request = GetValidRequest(subjectId2);
        request.NodeCode = "T01";

        var result = await _sut.ExecuteAsync(request);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task SameCodeDifferentCenter_IsAllowed()
    {
        var centerId1 = Guid.NewGuid();
        var centerId2 = Guid.NewGuid();
        var subjectId1 = Guid.NewGuid();
        var subjectId2 = Guid.NewGuid();
        SetupValidTenant(centerId2);

        _dbContext.Centers.Add(new Center { CenterId = centerId1, CenterCode = "C01", CenterName = "C1", Timezone = "UTC", Status = EduTwin.Contracts.Organization.CenterStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.Centers.Add(new Center { CenterId = centerId2, CenterCode = "C02", CenterName = "C2", Timezone = "UTC", Status = EduTwin.Contracts.Organization.CenterStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.Subjects.Add(new EduTwin.DAL.Organization.Subject { SubjectId = subjectId1, CenterId = centerId1, SubjectCode = "S01", SubjectName = "S01", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.Subjects.Add(new EduTwin.DAL.Organization.Subject { SubjectId = subjectId2, CenterId = centerId2, SubjectCode = "S01", SubjectName = "S01", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.KnowledgeNodes.Add(new KnowledgeNode { NodeId = 1, CenterId = centerId1, SubjectId = subjectId1, NodeType = NodeType.Topic, NodeCode = "T01", NodeName = "Existing", IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        var request = GetValidRequest(subjectId2);
        request.NodeCode = "T01";

        var result = await _sut.ExecuteAsync(request);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task InvalidSubjectWithExistingCode_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        var subjectId1 = Guid.NewGuid();
        var subjectId2 = Guid.NewGuid();
        SetupValidTenant(centerId);

        _dbContext.Centers.Add(new Center { CenterId = centerId, CenterCode = "C01", CenterName = "C", Timezone = "UTC", Status = EduTwin.Contracts.Organization.CenterStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.Subjects.Add(new EduTwin.DAL.Organization.Subject { SubjectId = subjectId1, CenterId = centerId, SubjectCode = "S01", SubjectName = "S01", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.KnowledgeNodes.Add(new KnowledgeNode { NodeId = 1, CenterId = centerId, SubjectId = subjectId1, NodeType = NodeType.Topic, NodeCode = "T01", NodeName = "Existing", IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        var request = GetValidRequest(subjectId2); // Does not exist
        request.NodeCode = "T01";

        var result = await _sut.ExecuteAsync(request);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task InvalidParentWithExistingCode_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        SetupValidTenant(centerId);

        _dbContext.Centers.Add(new Center { CenterId = centerId, CenterCode = "C01", CenterName = "C", Timezone = "UTC", Status = EduTwin.Contracts.Organization.CenterStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.Subjects.Add(new EduTwin.DAL.Organization.Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "S01", SubjectName = "S01", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.KnowledgeNodes.Add(new KnowledgeNode { NodeId = 1, CenterId = centerId, SubjectId = subjectId, NodeType = NodeType.Topic, NodeCode = "T01", NodeName = "Existing", IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        var request = GetValidRequest(subjectId, "999"); // Invalid parent
        request.NodeCode = "T01"; // Duplicate code

        var result = await _sut.ExecuteAsync(request);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode); // Parent validation takes precedence
    }

    [Fact]
    public async Task MissingParent_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        SetupValidTenant(centerId);

        _dbContext.Centers.Add(new Center { CenterId = centerId, CenterCode = "C01", CenterName = "C", Timezone = "UTC", Status = EduTwin.Contracts.Organization.CenterStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.Subjects.Add(new EduTwin.DAL.Organization.Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "S01", SubjectName = "S01", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        var request = GetValidRequest(subjectId, "999");
        var result = await _sut.ExecuteAsync(request);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task DeletedParent_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        SetupValidTenant(centerId);

        _dbContext.Centers.Add(new Center { CenterId = centerId, CenterCode = "C01", CenterName = "C", Timezone = "UTC", Status = EduTwin.Contracts.Organization.CenterStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.Subjects.Add(new EduTwin.DAL.Organization.Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "S01", SubjectName = "S01", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.KnowledgeNodes.Add(new KnowledgeNode { NodeId = 1, CenterId = centerId, SubjectId = subjectId, NodeType = NodeType.Topic, NodeCode = "P01", NodeName = "P", IsDeleted = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        var request = GetValidRequest(subjectId, "1");
        var result = await _sut.ExecuteAsync(request);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task CrossSubjectParent_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        var subjectId1 = Guid.NewGuid();
        var subjectId2 = Guid.NewGuid();
        SetupValidTenant(centerId);

        _dbContext.Centers.Add(new Center { CenterId = centerId, CenterCode = "C01", CenterName = "C", Timezone = "UTC", Status = EduTwin.Contracts.Organization.CenterStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.Subjects.Add(new EduTwin.DAL.Organization.Subject { SubjectId = subjectId1, CenterId = centerId, SubjectCode = "S01", SubjectName = "S01", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.Subjects.Add(new EduTwin.DAL.Organization.Subject { SubjectId = subjectId2, CenterId = centerId, SubjectCode = "S02", SubjectName = "S02", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.KnowledgeNodes.Add(new KnowledgeNode { NodeId = 1, CenterId = centerId, SubjectId = subjectId1, NodeType = NodeType.Topic, NodeCode = "P01", NodeName = "P", IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        var request = GetValidRequest(subjectId2, "1");
        var result = await _sut.ExecuteAsync(request);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task DeletedSubject_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        SetupValidTenant(centerId);

        _dbContext.Centers.Add(new Center { CenterId = centerId, CenterCode = "C01", CenterName = "C", Timezone = "UTC", Status = EduTwin.Contracts.Organization.CenterStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.Subjects.Add(new EduTwin.DAL.Organization.Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "S01", SubjectName = "S01", IsDeleted = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        var request = GetValidRequest(subjectId);
        var result = await _sut.ExecuteAsync(request);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task MissingCenter_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        SetupValidTenant(centerId);

        _dbContext.Subjects.Add(new EduTwin.DAL.Organization.Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "S01", SubjectName = "S01", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        var request = GetValidRequest(subjectId);
        var result = await _sut.ExecuteAsync(request);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task DeletedCenter_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        SetupValidTenant(centerId);

        _dbContext.Centers.Add(new Center { CenterId = centerId, CenterCode = "C01", CenterName = "C", Timezone = "UTC", Status = EduTwin.Contracts.Organization.CenterStatus.Active, IsDeleted = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.Subjects.Add(new EduTwin.DAL.Organization.Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "S01", SubjectName = "S01", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        var request = GetValidRequest(subjectId);
        var result = await _sut.ExecuteAsync(request);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task CenterIdUserId_Missing_ReturnsResourceNotFound(bool hasCenter, bool hasUser)
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(hasCenter ? centerId : null);
        _tenantMock.SetupGet(x => x.UserId).Returns(hasUser ? Guid.NewGuid() : null);
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.Teacher));

        var request = GetValidRequest(subjectId);
        var result = await _sut.ExecuteAsync(request);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("123")]
    [InlineData("TEACHER")]
    [InlineData("teacher")]
    public async Task Role_Invalid_ReturnsResourceNotFound(string? role)
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(role);

        var request = GetValidRequest(subjectId);
        var result = await _sut.ExecuteAsync(request);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }


    [Fact]
    public async Task ExecuteAsync_EmptyCenterId_ReturnsResourceNotFound()
    {
        var centerId = Guid.Empty;
        var subjectId = Guid.NewGuid();

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.Teacher));

        var request = GetValidRequest(subjectId);
        var result = await _sut.ExecuteAsync(request);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyUserId_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.Empty);
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.Teacher));

        var request = GetValidRequest(subjectId);
        var result = await _sut.ExecuteAsync(request);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }
    private class FaultyDbContext : EduTwinDbContext
    {
        private readonly Exception _exceptionToThrow;

        public FaultyDbContext(DbContextOptions<EduTwinDbContext> options, EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor tenantAccessor, Exception exceptionToThrow)
            : base(options, tenantAccessor)
        {
            _exceptionToThrow = exceptionToThrow;
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            throw _exceptionToThrow;
        }
    }
}
