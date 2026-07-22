using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.EntityFrameworkCore;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.BLL.KnowledgeGraph;
using EduTwin.Contracts.Common;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Organization;

namespace EduTwin.BLL.Tests.KnowledgeGraph;

public class UpdateKnowledgeNodeUseCaseTests
{
    private readonly UpdateKnowledgeNodeUseCase _sut;
    private readonly Mock<ITenantContext> _tenantMock;
    private readonly EduTwinDbContext _dbContext;
    private readonly Mock<IKnowledgeNodeHierarchyCycleDetector> _cycleDetectorMock;
    private readonly Mock<TimeProvider> _timeProviderMock;
    private readonly DateTimeOffset _fixedTime = new DateTimeOffset(2026, 7, 22, 10, 0, 0, TimeSpan.Zero);

    public UpdateKnowledgeNodeUseCaseTests()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var tenantAccessorMock = new Mock<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>();
        _tenantMock = new Mock<ITenantContext>();
        tenantAccessorMock.Setup(x => x.CenterId).Returns(() => _tenantMock.Object.CenterId);

        _dbContext = new EduTwinDbContext(options, tenantAccessorMock.Object);
        _cycleDetectorMock = new Mock<IKnowledgeNodeHierarchyCycleDetector>();

        _timeProviderMock = new Mock<TimeProvider>();
        _timeProviderMock.Setup(x => x.GetUtcNow()).Returns(_fixedTime);

        _sut = new UpdateKnowledgeNodeUseCase(_dbContext, _tenantMock.Object, _timeProviderMock.Object, _cycleDetectorMock.Object);
    }

    private void SetupValidTenant(Guid centerId, string role = nameof(UserRole.Teacher))
    {
        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(role);
    }

    private UpdateKnowledgeNodeRequest GetValidRequest(string? parentId = null) => new()
    {
        ParentNodeId = parentId,
        NodeName = "Updated Node",
        Description = "Updated Desc",
        OrderIndex = 2,
        ExamImportance = 50,
        EstimatedLearningMinutes = 45,
        IsActive = true,
        RowVersion = "1"
    };

    [Theory]
    [InlineData(nameof(UserRole.Teacher))]
    [InlineData(nameof(UserRole.CenterManager))]
    public async Task ExecuteAsync_ValidRole_UpdatesNode(string role)
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        SetupValidTenant(centerId, role);

        _dbContext.Centers.Add(new Center { CenterId = centerId, CenterCode = "C01", CenterName = "C", Timezone = "UTC", Status = EduTwin.Contracts.Organization.CenterStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.Subjects.Add(new Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "S01", SubjectName = "S01", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });

        var node = new KnowledgeNode
        {
            NodeId = 1, CenterId = centerId, SubjectId = subjectId, NodeType = NodeType.Topic,
            NodeCode = "T01", NodeName = "Old", IsDeleted = false, RowVersion = 1,
            CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), UpdatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            CreatedBy = Guid.NewGuid(), UpdatedBy = Guid.NewGuid()
        };
        _dbContext.KnowledgeNodes.Add(node);
        await _dbContext.SaveChangesAsync();

        var request = GetValidRequest();
        var result = await _sut.ExecuteAsync("1", request);

        Assert.True(result.IsSuccess);

        var nodeInDb = await _dbContext.KnowledgeNodes.FirstAsync();
        Assert.Equal("Updated Node", nodeInDb.NodeName);
        Assert.Equal("Updated Desc", nodeInDb.Description);
        Assert.Equal(2u, nodeInDb.OrderIndex);
        Assert.Equal(50m, nodeInDb.ExamImportance);
        Assert.Equal(45u, nodeInDb.EstimatedLearningMinutes);
        Assert.True(nodeInDb.IsActive);

        // Audit and immutability checks
        Assert.Equal(_fixedTime.UtcDateTime, nodeInDb.UpdatedAt);
        Assert.Equal(DateTimeKind.Utc, nodeInDb.UpdatedAt.Kind);
        Assert.Equal(_tenantMock.Object.UserId, nodeInDb.UpdatedBy);

        Assert.Equal(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), nodeInDb.CreatedAt);
        Assert.NotEqual(_tenantMock.Object.UserId, nodeInDb.CreatedBy);
        Assert.Equal(NodeType.Topic, nodeInDb.NodeType);
        Assert.Equal("T01", nodeInDb.NodeCode);
        Assert.Equal(subjectId, nodeInDb.SubjectId);
    }

    [Fact]
    public async Task TenantUnresolved_ReturnsResourceNotFound()
    {
        _tenantMock.SetupGet(x => x.IsResolved).Returns(false);
        var result = await _sut.ExecuteAsync("1", GetValidRequest());
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task InvalidRole_ReturnsResourceNotFound()
    {
        SetupValidTenant(Guid.NewGuid(), "Student");
        var result = await _sut.ExecuteAsync("1", GetValidRequest());
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task EmptyCenterId_ReturnsResourceNotFound()
    {
        SetupValidTenant(Guid.Empty);
        var result = await _sut.ExecuteAsync("1", GetValidRequest());
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task MissingCenter_ReturnsResourceNotFound()
    {
        SetupValidTenant(Guid.NewGuid());
        // No center in DB
        var result = await _sut.ExecuteAsync("1", GetValidRequest());
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task MissingTargetNode_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        SetupValidTenant(centerId);
        _dbContext.Centers.Add(new Center { CenterId = centerId, CenterCode = "C01", CenterName = "C", Timezone = "UTC", Status = EduTwin.Contracts.Organization.CenterStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ExecuteAsync("1", GetValidRequest());
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ValidSameSubjectParent_Succeeds()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        SetupValidTenant(centerId);

        _dbContext.Centers.Add(new Center { CenterId = centerId, CenterCode = "C01", CenterName = "C", Timezone = "UTC", Status = EduTwin.Contracts.Organization.CenterStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.Subjects.Add(new Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "S01", SubjectName = "S01", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.KnowledgeNodes.Add(new KnowledgeNode { NodeId = 1, CenterId = centerId, SubjectId = subjectId, RowVersion = 1, NodeType = NodeType.Topic, NodeCode = "T01", NodeName = "T01", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.KnowledgeNodes.Add(new KnowledgeNode { NodeId = 2, CenterId = centerId, SubjectId = subjectId, RowVersion = 1, NodeType = NodeType.Topic, NodeCode = "T02", NodeName = "T02", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        _cycleDetectorMock.Setup(x => x.HasCycle(1, 2, It.IsAny<IReadOnlyDictionary<ulong, ulong?>>())).Returns(false);

        var request = GetValidRequest("2");
        var result = await _sut.ExecuteAsync("1", request);

        Assert.True(result.IsSuccess);
        var node1 = await _dbContext.KnowledgeNodes.FirstAsync(x => x.NodeId == 1);
        Assert.Equal(2ul, node1.ParentNodeId);
    }

    [Fact]
    public async Task NullParent_ClearsParent_Succeeds()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        SetupValidTenant(centerId);

        _dbContext.Centers.Add(new Center { CenterId = centerId, CenterCode = "C01", CenterName = "C", Timezone = "UTC", Status = EduTwin.Contracts.Organization.CenterStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.Subjects.Add(new Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "S01", SubjectName = "S01", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.KnowledgeNodes.Add(new KnowledgeNode { NodeId = 1, ParentNodeId = 2, CenterId = centerId, SubjectId = subjectId, RowVersion = 1, NodeType = NodeType.Topic, NodeCode = "T01", NodeName = "T01", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.KnowledgeNodes.Add(new KnowledgeNode { NodeId = 2, CenterId = centerId, SubjectId = subjectId, RowVersion = 1, NodeType = NodeType.Topic, NodeCode = "T02", NodeName = "T02", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        var request = GetValidRequest(null);
        var result = await _sut.ExecuteAsync("1", request);

        Assert.True(result.IsSuccess);
        var node1 = await _dbContext.KnowledgeNodes.FirstAsync(x => x.NodeId == 1);
        Assert.Null(node1.ParentNodeId);
    }

    [Fact]
    public async Task CrossSubjectParent_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        var subjectId1 = Guid.NewGuid();
        var subjectId2 = Guid.NewGuid();
        SetupValidTenant(centerId);

        _dbContext.Centers.Add(new Center { CenterId = centerId, CenterCode = "C01", CenterName = "C", Timezone = "UTC", Status = EduTwin.Contracts.Organization.CenterStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.Subjects.Add(new Subject { SubjectId = subjectId1, CenterId = centerId, SubjectCode = "S01", SubjectName = "S01", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.Subjects.Add(new Subject { SubjectId = subjectId2, CenterId = centerId, SubjectCode = "S02", SubjectName = "S02", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.KnowledgeNodes.Add(new KnowledgeNode { NodeId = 1, CenterId = centerId, SubjectId = subjectId1, RowVersion = 1, NodeType = NodeType.Topic, NodeCode = "T01", NodeName = "T01", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.KnowledgeNodes.Add(new KnowledgeNode { NodeId = 2, CenterId = centerId, SubjectId = subjectId2, RowVersion = 1, NodeType = NodeType.Topic, NodeCode = "T02", NodeName = "T02", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }); // Parent in different subject
        await _dbContext.SaveChangesAsync();

        var request = GetValidRequest("2");
        var result = await _sut.ExecuteAsync("1", request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task CycleDetectorReturnsTrue_ReturnsDagCycleDetected_DoesNotMutateTrackedEntity()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        SetupValidTenant(centerId);

        _dbContext.Centers.Add(new Center { CenterId = centerId, CenterCode = "C01", CenterName = "C", Timezone = "UTC", Status = EduTwin.Contracts.Organization.CenterStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.Subjects.Add(new Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "S01", SubjectName = "S01", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.KnowledgeNodes.Add(new KnowledgeNode { NodeId = 1, CenterId = centerId, SubjectId = subjectId, RowVersion = 1, NodeType = NodeType.Topic, NodeCode = "T01", NodeName = "Original", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        _cycleDetectorMock.Setup(x => x.HasCycle(It.IsAny<ulong>(), It.IsAny<ulong?>(), It.IsAny<IReadOnlyDictionary<ulong, ulong?>>())).Returns(true);

        var request = GetValidRequest();
        request.ParentNodeId = "1"; // self parent
        var result = await _sut.ExecuteAsync("1", request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.DagCycleDetected, result.ErrorCode);

        var nodeInDb = await _dbContext.KnowledgeNodes.FirstAsync();
        Assert.Equal("Original", nodeInDb.NodeName); // Not mutated
    }

    [Fact]
    public async Task StaleRowVersion_ReturnsConcurrencyConflict()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        SetupValidTenant(centerId);

        _dbContext.Centers.Add(new Center { CenterId = centerId, CenterCode = "C01", CenterName = "C", Timezone = "UTC", Status = EduTwin.Contracts.Organization.CenterStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.Subjects.Add(new Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "S01", SubjectName = "S01", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.KnowledgeNodes.Add(new KnowledgeNode { NodeId = 1, CenterId = centerId, SubjectId = subjectId, RowVersion = 2, NodeType = NodeType.Topic, NodeCode = "T01", NodeName = "T01", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        var request = GetValidRequest();
        request.RowVersion = "99"; // Stale

        var result = await _sut.ExecuteAsync("1", request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, result.ErrorCode);
    }

    [Fact]
    public async Task DbUpdateConcurrencyException_MapsToConcurrencyConflict()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var tenantAccessorMock = new Mock<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>();
        var tenantMock = new Mock<ITenantContext>();
        tenantAccessorMock.Setup(x => x.CenterId).Returns(Guid.NewGuid());
        var faultyDb = new FaultyDbContext(options, tenantAccessorMock.Object, new DbUpdateConcurrencyException());

        tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        tenantMock.SetupGet(x => x.CenterId).Returns(tenantAccessorMock.Object.CenterId);
        tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.Teacher));

        var sut = new UpdateKnowledgeNodeUseCase(faultyDb, tenantMock.Object, _timeProviderMock.Object, _cycleDetectorMock.Object);

        faultyDb.Centers.Add(new Center { CenterId = tenantAccessorMock.Object.CenterId ?? Guid.Empty, CenterCode = "C", CenterName = "C", Timezone = "UTC", Status = EduTwin.Contracts.Organization.CenterStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        faultyDb.Subjects.Add(new Subject { SubjectId = Guid.NewGuid(), CenterId = tenantAccessorMock.Object.CenterId ?? Guid.Empty, SubjectCode = "S01", SubjectName = "S01", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        faultyDb.KnowledgeNodes.Add(new KnowledgeNode { NodeId = 1, CenterId = tenantAccessorMock.Object.CenterId ?? Guid.Empty, SubjectId = Guid.NewGuid(), RowVersion = 1, NodeType = NodeType.Topic, NodeCode = "T01", NodeName = "T01", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await faultyDb.SaveChangesAsync();

        faultyDb.StartThrowing();
        var request = GetValidRequest();
        var result = await sut.ExecuteAsync("1", request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, result.ErrorCode);
    }

    [Fact]
    public async Task UnrelatedDbUpdateException_Rethrows()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var tenantAccessorMock = new Mock<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>();
        var tenantMock = new Mock<ITenantContext>();
        tenantAccessorMock.Setup(x => x.CenterId).Returns(Guid.NewGuid());
        var faultyDb = new FaultyDbContext(options, tenantAccessorMock.Object, new DbUpdateException("Other error"));

        tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        tenantMock.SetupGet(x => x.CenterId).Returns(tenantAccessorMock.Object.CenterId);
        tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.Teacher));

        var sut = new UpdateKnowledgeNodeUseCase(faultyDb, tenantMock.Object, _timeProviderMock.Object, _cycleDetectorMock.Object);

        faultyDb.Centers.Add(new Center { CenterId = tenantAccessorMock.Object.CenterId ?? Guid.Empty, CenterCode = "C", CenterName = "C", Timezone = "UTC", Status = EduTwin.Contracts.Organization.CenterStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        faultyDb.Subjects.Add(new Subject { SubjectId = Guid.NewGuid(), CenterId = tenantAccessorMock.Object.CenterId ?? Guid.Empty, SubjectCode = "S01", SubjectName = "S01", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        faultyDb.KnowledgeNodes.Add(new KnowledgeNode { NodeId = 1, CenterId = tenantAccessorMock.Object.CenterId ?? Guid.Empty, SubjectId = Guid.NewGuid(), RowVersion = 1, NodeType = NodeType.Topic, NodeCode = "T01", NodeName = "T01", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await faultyDb.SaveChangesAsync();

        faultyDb.StartThrowing();
        var request = GetValidRequest();

        await Assert.ThrowsAsync<DbUpdateException>(() => sut.ExecuteAsync("1", request));
    }


    [Fact]
    public async Task DeletedCenter_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        SetupValidTenant(centerId);
        _dbContext.Centers.Add(new Center { CenterId = centerId, IsDeleted = true, CenterCode = "C", CenterName = "C", Timezone = "UTC", Status = EduTwin.Contracts.Organization.CenterStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();
        var result = await _sut.ExecuteAsync("1", GetValidRequest());
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task SuspendedCenter_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        SetupValidTenant(centerId);
        _dbContext.Centers.Add(new Center { CenterId = centerId, CenterCode = "C", CenterName = "C", Timezone = "UTC", Status = EduTwin.Contracts.Organization.CenterStatus.Suspended, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();
        var result = await _sut.ExecuteAsync("1", GetValidRequest());
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task EmptyUserId_ReturnsResourceNotFound()
    {
        SetupValidTenant(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.Empty);
        var result = await _sut.ExecuteAsync("1", GetValidRequest());
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task NullCenterId_ReturnsResourceNotFound()
    {
        SetupValidTenant(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.CenterId).Returns((Guid?)null);
        var result = await _sut.ExecuteAsync("1", GetValidRequest());
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task NullUserId_ReturnsResourceNotFound()
    {
        SetupValidTenant(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.UserId).Returns((Guid?)null);
        var result = await _sut.ExecuteAsync("1", GetValidRequest());
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Student")]
    [InlineData("teacher")]
    [InlineData("CENTERMANAGER")]
    [InlineData("123")]
    [InlineData("Admin")]
    public async Task InvalidRole_ReturnsResourceNotFoundTheory(string? role)
    {
        SetupValidTenant(Guid.NewGuid(), role!);
        var result = await _sut.ExecuteAsync("1", GetValidRequest());
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task DeletedTarget_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        SetupValidTenant(centerId);
        _dbContext.Centers.Add(new Center { CenterId = centerId, CenterCode = "C01", CenterName = "C", Timezone = "UTC", Status = EduTwin.Contracts.Organization.CenterStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.KnowledgeNodes.Add(new KnowledgeNode { NodeId = 1, CenterId = centerId, SubjectId = Guid.NewGuid(), IsDeleted = true, RowVersion = 1, NodeType = NodeType.Topic, NodeCode = "T01", NodeName = "T01", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();
        var result = await _sut.ExecuteAsync("1", GetValidRequest());
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task CrossTenantTarget_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        SetupValidTenant(centerId);
        _dbContext.Centers.Add(new Center { CenterId = centerId, CenterCode = "C01", CenterName = "C", Timezone = "UTC", Status = EduTwin.Contracts.Organization.CenterStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.KnowledgeNodes.Add(new KnowledgeNode { NodeId = 1, CenterId = Guid.NewGuid(), SubjectId = Guid.NewGuid(), RowVersion = 1, NodeType = NodeType.Topic, NodeCode = "T01", NodeName = "T01", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();
        var result = await _sut.ExecuteAsync("1", GetValidRequest());
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task MissingParent_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        SetupValidTenant(centerId);
        _dbContext.Centers.Add(new Center { CenterId = centerId, CenterCode = "C01", CenterName = "C", Timezone = "UTC", Status = EduTwin.Contracts.Organization.CenterStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.KnowledgeNodes.Add(new KnowledgeNode { NodeId = 1, CenterId = centerId, SubjectId = Guid.NewGuid(), RowVersion = 1, NodeType = NodeType.Topic, NodeCode = "T01", NodeName = "T01", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();
        var request = GetValidRequest("999");
        var result = await _sut.ExecuteAsync("1", request);
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
        _dbContext.KnowledgeNodes.Add(new KnowledgeNode { NodeId = 1, CenterId = centerId, SubjectId = subjectId, RowVersion = 1, NodeType = NodeType.Topic, NodeCode = "T01", NodeName = "T01", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.KnowledgeNodes.Add(new KnowledgeNode { NodeId = 2, CenterId = centerId, SubjectId = subjectId, IsDeleted = true, RowVersion = 1, NodeType = NodeType.Topic, NodeCode = "T02", NodeName = "T02", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();
        var request = GetValidRequest("2");
        var result = await _sut.ExecuteAsync("1", request);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task CrossTenantParent_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        SetupValidTenant(centerId);
        _dbContext.Centers.Add(new Center { CenterId = centerId, CenterCode = "C01", CenterName = "C", Timezone = "UTC", Status = EduTwin.Contracts.Organization.CenterStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.KnowledgeNodes.Add(new KnowledgeNode { NodeId = 1, CenterId = centerId, SubjectId = subjectId, RowVersion = 1, NodeType = NodeType.Topic, NodeCode = "T01", NodeName = "T01", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.KnowledgeNodes.Add(new KnowledgeNode { NodeId = 2, CenterId = Guid.NewGuid(), SubjectId = subjectId, RowVersion = 1, NodeType = NodeType.Topic, NodeCode = "T02", NodeName = "T02", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();
        var request = GetValidRequest("2");
        var result = await _sut.ExecuteAsync("1", request);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Success_IncrementsRowVersionExactlyOnce()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        SetupValidTenant(centerId);
        _dbContext.Centers.Add(new Center { CenterId = centerId, CenterCode = "C01", CenterName = "C", Timezone = "UTC", Status = EduTwin.Contracts.Organization.CenterStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.Subjects.Add(new Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "S01", SubjectName = "S", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.KnowledgeNodes.Add(new KnowledgeNode { NodeId = 1, CenterId = centerId, SubjectId = subjectId, RowVersion = 10, NodeType = NodeType.Topic, NodeCode = "T01", NodeName = "Old", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        var request = GetValidRequest();
        var nodeBefore = await _dbContext.KnowledgeNodes.FirstAsync();
        request.RowVersion = nodeBefore.RowVersion.ToString();
        var expectedNewVersion = nodeBefore.RowVersion + 1;
        var result = await _sut.ExecuteAsync("1", request);

        Assert.True(result.IsSuccess);
        var nodeInDb = await _dbContext.KnowledgeNodes.FirstAsync();
        Assert.Equal(expectedNewVersion, nodeInDb.RowVersion);
        Assert.Equal(expectedNewVersion.ToString(), result.Data!.RowVersion);
    }

    [Fact]
    public async Task Success_PreservesExactImmutableAndCreatedAuditFields()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        SetupValidTenant(centerId);
        var origCreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var origCreatedBy = Guid.NewGuid();

        _dbContext.Centers.Add(new Center { CenterId = centerId, CenterCode = "C01", CenterName = "C", Timezone = "UTC", Status = EduTwin.Contracts.Organization.CenterStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.Subjects.Add(new Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "S01", SubjectName = "S", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.KnowledgeNodes.Add(new KnowledgeNode { NodeId = 1, CenterId = centerId, SubjectId = subjectId, RowVersion = 1, NodeType = NodeType.Topic, NodeCode = "T01", NodeName = "Old", CreatedAt = origCreatedAt, CreatedBy = origCreatedBy, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        var request = GetValidRequest();
        var result = await _sut.ExecuteAsync("1", request);

        Assert.True(result.IsSuccess);
        var nodeInDb = await _dbContext.KnowledgeNodes.FirstAsync();
        Assert.Equal(origCreatedAt, nodeInDb.CreatedAt);
        Assert.Equal(origCreatedBy, nodeInDb.CreatedBy);
        Assert.Equal(subjectId, nodeInDb.SubjectId);
        Assert.Equal(NodeType.Topic, nodeInDb.NodeType);
        Assert.Equal("T01", nodeInDb.NodeCode);
    }

    [Fact]
    public async Task CycleResult_DoesNotMutateAnyMutableField()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        SetupValidTenant(centerId);

        var origCreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var origUpdatedAt = new DateTime(2025, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        var origCreatedBy = Guid.NewGuid();
        var origUpdatedBy = Guid.NewGuid();

        _dbContext.Centers.Add(new Center { CenterId = centerId, CenterCode = "C01", CenterName = "C", Timezone = "UTC", Status = EduTwin.Contracts.Organization.CenterStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.Subjects.Add(new Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "S01", SubjectName = "S", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });

        var originalNode = new KnowledgeNode
        {
            NodeId = 1,
            CenterId = centerId,
            SubjectId = subjectId,
            ParentNodeId = null,
            RowVersion = 1,
            NodeType = NodeType.Topic,
            NodeCode = "T01",
            NodeName = "OldName",
            Description = "OldDesc",
            OrderIndex = 1,
            ExamImportance = 10,
            EstimatedLearningMinutes = 10,
            IsActive = false,
            CreatedAt = origCreatedAt,
            CreatedBy = origCreatedBy,
            UpdatedAt = origUpdatedAt,
            UpdatedBy = origUpdatedBy
        };
        _dbContext.KnowledgeNodes.Add(originalNode);
        await _dbContext.SaveChangesAsync();

        // 1. Snapshot scalar values independently
        ulong snapNodeId = 1;
        Guid snapCenterId = centerId;
        Guid snapSubjectId = subjectId;
        ulong? snapParentNodeId = null;
        ulong snapRowVersion = 1;
        NodeType snapNodeType = NodeType.Topic;
        string snapNodeCode = "T01";
        string snapNodeName = "OldName";
        string snapDescription = "OldDesc";
        uint snapOrderIndex = 1;
        decimal snapExamImportance = 10m;
        uint snapEstimatedLearningMinutes = 10;
        bool snapIsActive = false;
        DateTime snapCreatedAt = origCreatedAt;
        Guid snapCreatedBy = origCreatedBy;
        DateTime snapUpdatedAt = origUpdatedAt;
        Guid snapUpdatedBy = origUpdatedBy;

        _cycleDetectorMock.Setup(x => x.HasCycle(It.IsAny<ulong>(), It.IsAny<ulong?>(), It.IsAny<IReadOnlyDictionary<ulong, ulong?>>())).Returns(true);

        var request = GetValidRequest();
        request.ParentNodeId = "1";
        var result = await _sut.ExecuteAsync("1", request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.DagCycleDetected, result.ErrorCode);

        var nodeInDb = await _dbContext.KnowledgeNodes.FirstAsync();

        // 2. Assert tracked entity against scalar snapshots
        Assert.Equal(snapParentNodeId, nodeInDb.ParentNodeId);
        Assert.Equal(snapNodeName, nodeInDb.NodeName);
        Assert.Equal(snapDescription, nodeInDb.Description);
        Assert.Equal(snapOrderIndex, nodeInDb.OrderIndex);
        Assert.Equal(snapExamImportance, nodeInDb.ExamImportance);
        Assert.Equal(snapEstimatedLearningMinutes, nodeInDb.EstimatedLearningMinutes);
        Assert.Equal(snapIsActive, nodeInDb.IsActive);
        Assert.Equal(snapUpdatedAt, nodeInDb.UpdatedAt);
        Assert.Equal(snapUpdatedBy, nodeInDb.UpdatedBy);
        Assert.Equal(snapRowVersion, nodeInDb.RowVersion);
        Assert.Equal(snapCreatedAt, nodeInDb.CreatedAt);
        Assert.Equal(snapCreatedBy, nodeInDb.CreatedBy);
        Assert.Equal(snapSubjectId, nodeInDb.SubjectId);
        Assert.Equal(snapNodeType, nodeInDb.NodeType);
        Assert.Equal(snapNodeCode, nodeInDb.NodeCode);
        Assert.Equal(snapNodeId, nodeInDb.NodeId);
        Assert.Equal(snapCenterId, nodeInDb.CenterId);

        // 3. Clear tracker and fetch AsNoTracking
        _dbContext.ChangeTracker.Clear();
        var nodeNoTracking = await _dbContext.KnowledgeNodes.AsNoTracking().FirstAsync(n => n.NodeId == 1);

        // 4. Assert again to prove database is unchanged
        Assert.Equal(snapParentNodeId, nodeNoTracking.ParentNodeId);
        Assert.Equal(snapNodeName, nodeNoTracking.NodeName);
        Assert.Equal(snapDescription, nodeNoTracking.Description);
        Assert.Equal(snapOrderIndex, nodeNoTracking.OrderIndex);
        Assert.Equal(snapExamImportance, nodeNoTracking.ExamImportance);
        Assert.Equal(snapEstimatedLearningMinutes, nodeNoTracking.EstimatedLearningMinutes);
        Assert.Equal(snapIsActive, nodeNoTracking.IsActive);
        Assert.Equal(snapUpdatedAt, nodeNoTracking.UpdatedAt);
        Assert.Equal(snapUpdatedBy, nodeNoTracking.UpdatedBy);
        Assert.Equal(snapRowVersion, nodeNoTracking.RowVersion);
        Assert.Equal(snapCreatedAt, nodeNoTracking.CreatedAt);
        Assert.Equal(snapCreatedBy, nodeNoTracking.CreatedBy);
        Assert.Equal(snapSubjectId, nodeNoTracking.SubjectId);
        Assert.Equal(snapNodeType, nodeNoTracking.NodeType);
        Assert.Equal(snapNodeCode, nodeNoTracking.NodeCode);
        Assert.Equal(snapNodeId, nodeNoTracking.NodeId);
        Assert.Equal(snapCenterId, nodeNoTracking.CenterId);
    }

    [Fact]
    public async Task ExactCancellationToken_IsPassedToSaveChangesAsync()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        SetupValidTenant(centerId);

        var cts = new CancellationTokenSource();
        var expectedToken = cts.Token;

        var options = new DbContextOptionsBuilder<EduTwinDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var localTenantAccessorMock = new Mock<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>();
        localTenantAccessorMock.Setup(x => x.CenterId).Returns(centerId);
        var captureDb = new TokenCaptureDbContext(options, localTenantAccessorMock.Object);
        captureDb.Centers.Add(new Center { CenterId = centerId, CenterCode = "C01", CenterName = "C", Timezone = "UTC", Status = EduTwin.Contracts.Organization.CenterStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        captureDb.Subjects.Add(new Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "S01", SubjectName = "S", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        captureDb.KnowledgeNodes.Add(new KnowledgeNode { NodeId = 1, CenterId = centerId, SubjectId = subjectId, RowVersion = 1, NodeType = NodeType.Topic, NodeCode = "T01", NodeName = "Old", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await captureDb.SaveChangesAsync();

        // Reset the token after seeding database
        captureDb.ResetCapturedToken();

        var captureSut = new UpdateKnowledgeNodeUseCase(captureDb, _tenantMock.Object, _timeProviderMock.Object, _cycleDetectorMock.Object);
        var request = GetValidRequest();

        var result = await captureSut.ExecuteAsync("1", request, expectedToken);

        Assert.True(result.IsSuccess);
        Assert.True(captureDb.CapturedToken.HasValue);
        Assert.Equal(expectedToken, captureDb.CapturedToken.Value);
    }

    private class TokenCaptureDbContext : EduTwinDbContext
    {
        public CancellationToken? CapturedToken { get; private set; }

        public void ResetCapturedToken() => CapturedToken = null;

        public TokenCaptureDbContext(DbContextOptions<EduTwinDbContext> options, EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor tenantAccessor)
            : base(options, tenantAccessor)
        {
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            CapturedToken = cancellationToken;
            return base.SaveChangesAsync(cancellationToken);
        }
    }
    private class FaultyDbContext : EduTwinDbContext
    {
        private readonly Exception _exceptionToThrow;
        private bool _throwNow;

        public FaultyDbContext(DbContextOptions<EduTwinDbContext> options, EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor tenantAccessor, Exception exceptionToThrow)
            : base(options, tenantAccessor)
        {
            _exceptionToThrow = exceptionToThrow;
        }

        public void StartThrowing() => _throwNow = true;

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (_throwNow) throw _exceptionToThrow;
            return base.SaveChangesAsync(cancellationToken);
        }
    }
}
