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

public class ListKnowledgeNodesUseCaseTests
{
    private readonly EduTwinDbContext _dbContext;
    private readonly Mock<ITenantContext> _tenantMock;
    private readonly ListKnowledgeNodesUseCase _sut;

    public ListKnowledgeNodesUseCaseTests()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var tenantAccessorMock = new Mock<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>();
        _tenantMock = new Mock<ITenantContext>();
        tenantAccessorMock.Setup(x => x.CenterId).Returns(() => _tenantMock.Object.CenterId);

        _dbContext = new EduTwinDbContext(options, tenantAccessorMock.Object);
        _sut = new ListKnowledgeNodesUseCase(_dbContext, _tenantMock.Object);
    }

    private async Task SetupActiveCenterAndSubject(Guid centerId, Guid subjectId)
    {
        _dbContext.Centers.Add(new Center
        {
            CenterId = centerId,
            CenterCode = "C" + centerId.ToString()[..4],
            CenterName = "Test Center",
            Timezone = "UTC",
            Status = EduTwin.Contracts.Organization.CenterStatus.Active,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        _dbContext.Subjects.Add(new Subject
        {
            SubjectId = subjectId,
            CenterId = centerId,
            SubjectCode = "S1",
            SubjectName = "Test Subject",
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    private void SetupValidTenant(Guid centerId)
    {
        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.Student));
    }

    [Theory]
    [InlineData(nameof(UserRole.Student))]
    [InlineData(nameof(UserRole.Teacher))]
    [InlineData(nameof(UserRole.CenterManager))]
    public async Task ExecuteAsync_ValidRoles_ReturnsSuccess(string role)
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        await SetupActiveCenterAndSubject(centerId, subjectId);

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(role);

        var result = await _sut.ExecuteAsync(new KnowledgeNodeListQuery { SubjectId = subjectId }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Empty(result.Data);
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("student")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public async Task ExecuteAsync_InvalidRole_ReturnsResourceNotFound(string? role)
    {
        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(role);

        var result = await _sut.ExecuteAsync(new KnowledgeNodeListQuery { SubjectId = Guid.NewGuid() }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_TenantUnresolved_ReturnsResourceNotFound()
    {
        _tenantMock.SetupGet(x => x.IsResolved).Returns(false);
        var result = await _sut.ExecuteAsync(new KnowledgeNodeListQuery { SubjectId = Guid.NewGuid() }, CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_CenterIdNullOrEmpty_ReturnsResourceNotFound()
    {
        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns((Guid?)null);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.Student));

        var result1 = await _sut.ExecuteAsync(new KnowledgeNodeListQuery { SubjectId = Guid.NewGuid() }, CancellationToken.None);
        Assert.False(result1.IsSuccess);

        _tenantMock.SetupGet(x => x.CenterId).Returns(Guid.Empty);
        var result2 = await _sut.ExecuteAsync(new KnowledgeNodeListQuery { SubjectId = Guid.NewGuid() }, CancellationToken.None);
        Assert.False(result2.IsSuccess);
    }

    [Fact]
    public async Task ExecuteAsync_UserIdNullOrEmpty_ReturnsResourceNotFound()
    {
        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.UserId).Returns((Guid?)null);
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.Student));

        var result1 = await _sut.ExecuteAsync(new KnowledgeNodeListQuery { SubjectId = Guid.NewGuid() }, CancellationToken.None);
        Assert.False(result1.IsSuccess);

        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.Empty);
        var result2 = await _sut.ExecuteAsync(new KnowledgeNodeListQuery { SubjectId = Guid.NewGuid() }, CancellationToken.None);
        Assert.False(result2.IsSuccess);
    }

    [Fact]
    public async Task ExecuteAsync_SubjectIdEmpty_ReturnsValidationFailed()
    {
        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.Student));

        var result = await _sut.ExecuteAsync(new KnowledgeNodeListQuery { SubjectId = Guid.Empty }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_CenterMissingDeletedOrSuspended_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        _dbContext.Centers.Add(new Center
        {
            CenterId = centerId,
            CenterCode = "C1",
            CenterName = "Test Center",
            Timezone = "UTC",
            Status = EduTwin.Contracts.Organization.CenterStatus.Suspended,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.Student));

        var result = await _sut.ExecuteAsync(new KnowledgeNodeListQuery { SubjectId = Guid.NewGuid() }, CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_SubjectMissingDeletedOrCrossTenant_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        var otherCenterId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        await SetupActiveCenterAndSubject(centerId, Guid.NewGuid());
        _dbContext.Subjects.Add(new Subject
        {
            SubjectId = subjectId,
            CenterId = otherCenterId,
            SubjectCode = "S1",
            SubjectName = "Test Subject",
            IsActive = true,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.Student));

        var result = await _sut.ExecuteAsync(new KnowledgeNodeListQuery { SubjectId = subjectId }, CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsProperNodesAndFiltersCorrectly()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var otherSubjectId = Guid.NewGuid();
        await SetupActiveCenterAndSubject(centerId, subjectId);
        _dbContext.Subjects.Add(new Subject { SubjectId = otherSubjectId, CenterId = centerId, SubjectCode = "S2", SubjectName = "S2", IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });

        // Node 1: Valid, order 2
        _dbContext.KnowledgeNodes.Add(new KnowledgeNode { NodeId = 1, CenterId = centerId, SubjectId = subjectId, NodeType = NodeType.Topic, NodeCode = "N1", NodeName = "N1", OrderIndex = 2, ParentNodeId = 10, IsActive = true, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, RowVersion = 123 });
        // Node 2: Valid, order 1
        _dbContext.KnowledgeNodes.Add(new KnowledgeNode { NodeId = 2, CenterId = centerId, SubjectId = subjectId, NodeType = NodeType.Skill, NodeCode = "N2", NodeName = "N2", OrderIndex = 1, ParentNodeId = null, IsActive = false, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, RowVersion = 456, ExamImportance = 10.5m, EstimatedLearningMinutes = 60, Description = "Desc" });
        // Node 3: Deleted
        _dbContext.KnowledgeNodes.Add(new KnowledgeNode { NodeId = 3, CenterId = centerId, SubjectId = subjectId, NodeType = NodeType.Topic, NodeCode = "N3", NodeName = "N3", OrderIndex = 3, IsActive = true, IsDeleted = true, DeletedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        // Node 4: Cross tenant / cross subject
        _dbContext.KnowledgeNodes.Add(new KnowledgeNode { NodeId = 4, CenterId = centerId, SubjectId = otherSubjectId, NodeType = NodeType.Topic, NodeCode = "N4", NodeName = "N4", OrderIndex = 4, IsActive = true, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        SetupValidTenant(centerId);

        // Under fr-FR culture, floats/decimals would use comma, but we enforce invariant culture for NodeId/ParentNodeId/SubjectId/RowVersion
        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            CultureInfo.CurrentUICulture = new CultureInfo("fr-FR");

            // Query with no filters -> Node 2 then Node 1
            var result = await _sut.ExecuteAsync(new KnowledgeNodeListQuery { SubjectId = subjectId }, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(2, result.Data!.Count);

            var n2 = result.Data[0];
            Assert.Equal("2", n2.NodeId);
            Assert.Null(n2.ParentNodeId);
            Assert.Equal(subjectId.ToString("D").ToLowerInvariant(), n2.SubjectId);
            Assert.NotNull(n2.RowVersion);
            Assert.Equal("Skill", n2.NodeType);
            Assert.Equal("N2", n2.NodeCode);
            Assert.Equal("Desc", n2.Description);
            Assert.Equal(1u, n2.OrderIndex);
            Assert.Equal(10.5m, n2.ExamImportance);
            Assert.Equal(60u, n2.EstimatedLearningMinutes);
            Assert.False(n2.IsActive);

            var n1 = result.Data[1];
            Assert.Equal("1", n1.NodeId);
            Assert.Equal("10", n1.ParentNodeId);
            Assert.Equal("Topic", n1.NodeType);
            Assert.Equal(2u, n1.OrderIndex);
            Assert.True(n1.IsActive);

            // Filter by nodeType
            var filterTypeResult = await _sut.ExecuteAsync(new KnowledgeNodeListQuery { SubjectId = subjectId, NodeType = "Topic" }, CancellationToken.None);
            Assert.True(filterTypeResult.IsSuccess);
            Assert.Single(filterTypeResult.Data!);
            Assert.Equal("1", filterTypeResult.Data![0].NodeId);

            // Filter by parentNodeId
            var filterParentResult = await _sut.ExecuteAsync(new KnowledgeNodeListQuery { SubjectId = subjectId, ParentNodeId = "10" }, CancellationToken.None);
            Assert.True(filterParentResult.IsSuccess);
            Assert.Single(filterParentResult.Data!);
            Assert.Equal("1", filterParentResult.Data![0].NodeId);

            // Filter by isActive
            var filterActiveResult = await _sut.ExecuteAsync(new KnowledgeNodeListQuery { SubjectId = subjectId, IsActive = false }, CancellationToken.None);
            Assert.True(filterActiveResult.IsSuccess);
            Assert.Single(filterActiveResult.Data!);
            Assert.Equal("2", filterActiveResult.Data![0].NodeId);

            // Query AsNoTracking
            Assert.Empty(_dbContext.ChangeTracker.Entries());
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    // ── NodeType strict validation ──

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Topic ")]
    [InlineData(" Topic")]
    [InlineData("topic")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("InvalidType")]
    public async Task ExecuteAsync_InvalidNodeType_ReturnsValidationFailed(string nodeType)
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        await SetupActiveCenterAndSubject(centerId, subjectId);
        SetupValidTenant(centerId);

        var result = await _sut.ExecuteAsync(new KnowledgeNodeListQuery { SubjectId = subjectId, NodeType = nodeType }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_NodeTypeNull_DoesNotFilter()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        await SetupActiveCenterAndSubject(centerId, subjectId);

        _dbContext.KnowledgeNodes.Add(new KnowledgeNode { NodeId = 100, CenterId = centerId, SubjectId = subjectId, NodeType = NodeType.Topic, NodeCode = "A", NodeName = "A", OrderIndex = 1, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.KnowledgeNodes.Add(new KnowledgeNode { NodeId = 101, CenterId = centerId, SubjectId = subjectId, NodeType = NodeType.Skill, NodeCode = "B", NodeName = "B", OrderIndex = 2, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        SetupValidTenant(centerId);

        var result = await _sut.ExecuteAsync(new KnowledgeNodeListQuery { SubjectId = subjectId, NodeType = null }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.Count);
    }

    // ── ParentNodeId strict validation ──

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData("1 ")]
    [InlineData(" 1")]
    [InlineData("1.0")]
    [InlineData("18446744073709551616")]
    [InlineData("abc")]
    [InlineData("\u0660")]
    public async Task ExecuteAsync_InvalidParentNodeId_ReturnsValidationFailed(string parentNodeId)
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        await SetupActiveCenterAndSubject(centerId, subjectId);
        SetupValidTenant(centerId);

        var result = await _sut.ExecuteAsync(new KnowledgeNodeListQuery { SubjectId = subjectId, ParentNodeId = parentNodeId }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_ParentNodeIdNull_DoesNotFilter()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        await SetupActiveCenterAndSubject(centerId, subjectId);

        _dbContext.KnowledgeNodes.Add(new KnowledgeNode { NodeId = 200, CenterId = centerId, SubjectId = subjectId, NodeType = NodeType.Chapter, NodeCode = "C1", NodeName = "C1", OrderIndex = 1, ParentNodeId = 5, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.KnowledgeNodes.Add(new KnowledgeNode { NodeId = 201, CenterId = centerId, SubjectId = subjectId, NodeType = NodeType.Chapter, NodeCode = "C2", NodeName = "C2", OrderIndex = 2, ParentNodeId = null, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        SetupValidTenant(centerId);

        var result = await _sut.ExecuteAsync(new KnowledgeNodeListQuery { SubjectId = subjectId, ParentNodeId = null }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.Count);
    }

    [Fact]
    public async Task ExecuteAsync_ParentNodeIdValid_FiltersCorrectly()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        await SetupActiveCenterAndSubject(centerId, subjectId);

        _dbContext.KnowledgeNodes.Add(new KnowledgeNode { NodeId = 300, CenterId = centerId, SubjectId = subjectId, NodeType = NodeType.Topic, NodeCode = "T1", NodeName = "T1", OrderIndex = 1, ParentNodeId = 10, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.KnowledgeNodes.Add(new KnowledgeNode { NodeId = 301, CenterId = centerId, SubjectId = subjectId, NodeType = NodeType.Topic, NodeCode = "T2", NodeName = "T2", OrderIndex = 2, ParentNodeId = 20, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _dbContext.KnowledgeNodes.Add(new KnowledgeNode { NodeId = 302, CenterId = centerId, SubjectId = subjectId, NodeType = NodeType.Topic, NodeCode = "T3", NodeName = "T3", OrderIndex = 3, ParentNodeId = null, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        SetupValidTenant(centerId);

        var result = await _sut.ExecuteAsync(new KnowledgeNodeListQuery { SubjectId = subjectId, ParentNodeId = "10" }, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!);
        Assert.Equal("300", result.Data![0].NodeId);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyParentNodeId_DoesNotBecomeRootFilter()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        await SetupActiveCenterAndSubject(centerId, subjectId);

        _dbContext.KnowledgeNodes.Add(new KnowledgeNode { NodeId = 400, CenterId = centerId, SubjectId = subjectId, NodeType = NodeType.Subject, NodeCode = "R1", NodeName = "R1", OrderIndex = 1, ParentNodeId = null, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        SetupValidTenant(centerId);

        // Empty string is invalid, not a root-node filter
        var result = await _sut.ExecuteAsync(new KnowledgeNodeListQuery { SubjectId = subjectId, ParentNodeId = "" }, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    // ── Cancellation ──

    [Fact]
    public async Task ExecuteAsync_CanceledToken_IsHonored()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        await SetupActiveCenterAndSubject(centerId, subjectId);
        SetupValidTenant(centerId);

        await Assert.ThrowsAsync<OperationCanceledException>(() => _sut.ExecuteAsync(new KnowledgeNodeListQuery { SubjectId = subjectId }, cts.Token));
    }
}
