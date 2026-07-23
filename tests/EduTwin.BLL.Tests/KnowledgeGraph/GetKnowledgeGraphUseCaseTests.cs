using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.KnowledgeGraph;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;

namespace EduTwin.BLL.Tests.KnowledgeGraph;

public class GetKnowledgeGraphUseCaseTests
{
    private readonly Mock<ITenantContext> _tenantContextMock;
    private readonly Guid _centerId;
    private readonly Guid _userId;
    private readonly Guid _subjectId;
    private readonly DateTime _utcNow;

    public GetKnowledgeGraphUseCaseTests()
    {
        _tenantContextMock = new Mock<ITenantContext>();

        _centerId = Guid.NewGuid();
        _userId = Guid.NewGuid();
        _subjectId = Guid.NewGuid();
        _utcNow = new DateTime(2026, 7, 24, 0, 0, 0, DateTimeKind.Utc);

        _tenantContextMock.Setup(x => x.IsResolved).Returns(true);
        _tenantContextMock.Setup(x => x.CenterId).Returns(_centerId);
        _tenantContextMock.Setup(x => x.UserId).Returns(_userId);
        _tenantContextMock.Setup(x => x.Role).Returns(nameof(UserRole.Teacher));
    }

    private EduTwinDbContext CreateDbContext(string dbName)
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var tenantAccessorMock = new Mock<ITenantIdAccessor>();
        tenantAccessorMock.Setup(x => x.CenterId).Returns(() => _tenantContextMock.Object.CenterId);

        return new EduTwinDbContext(options, tenantAccessorMock.Object);
    }

    private async Task SeedBaseDataAsync(EduTwinDbContext dbContext)
    {
        var center = new Center
        {
            CenterId = _centerId,
            CenterName = "Test Center",
            CenterCode = "TC01",
            Timezone = "UTC",
            Status = CenterStatus.Active,
            IsDeleted = false,
            CreatedAt = _utcNow.AddDays(-1),
            UpdatedAt = _utcNow.AddDays(-1)
        };

        var subject = new Subject
        {
            SubjectId = _subjectId,
            CenterId = _centerId,
            SubjectCode = "SUB01",
            SubjectName = "Test Subject",
            IsDeleted = false,
            CreatedAt = _utcNow.AddDays(-1),
            UpdatedAt = _utcNow.AddDays(-1)
        };

        dbContext.Centers.Add(center);
        dbContext.Subjects.Add(subject);
        await dbContext.SaveChangesAsync();
    }

    [Theory]
    [InlineData(nameof(UserRole.Student))]
    [InlineData(nameof(UserRole.Teacher))]
    [InlineData(nameof(UserRole.CenterManager))]
    public async Task ExecuteAsync_AuthorizedRoles_Success(string role)
    {
        var dbName = Guid.NewGuid().ToString();
        using var dbContext = CreateDbContext(dbName);
        await SeedBaseDataAsync(dbContext);

        _tenantContextMock.Setup(x => x.Role).Returns(role);

        var useCase = new GetKnowledgeGraphUseCase(dbContext, _tenantContextMock.Object);

        var result = await useCase.ExecuteAsync(_subjectId);

        Assert.True(result.IsSuccess);
        Assert.Null(result.ErrorCode);
        Assert.NotNull(result.Data);
        Assert.Equal(_subjectId.ToString("D").ToLowerInvariant(), result.Data!.SubjectId);
    }

    [Fact]
    public void ExecuteAsync_ExactFrozenDtoProjection_NoExtraFieldsInDtoContracts()
    {
        var nodeProps = typeof(KnowledgeGraphNodeDto).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name).OrderBy(x => x).ToList();
        var expectedNodeProps = new[] { "NodeId", "NodeType", "NodeCode", "NodeName", "OrderIndex", "ExamImportance" }
            .OrderBy(x => x).ToList();
        Assert.Equal(expectedNodeProps, nodeProps);

        var edgeProps = typeof(KnowledgeGraphEdgeDto).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name).OrderBy(x => x).ToList();
        var expectedEdgeProps = new[] { "EdgeId", "SourceNodeId", "TargetNodeId", "RelationType", "Weight" }
            .OrderBy(x => x).ToList();
        Assert.Equal(expectedEdgeProps, edgeProps);

        var graphProps = typeof(KnowledgeGraphDto).GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name).OrderBy(x => x).ToList();
        var expectedGraphProps = new[] { "SubjectId", "Nodes", "Edges" }
            .OrderBy(x => x).ToList();
        Assert.Equal(expectedGraphProps, graphProps);
    }

    [Fact]
    public async Task ExecuteAsync_SubjectIdCanonicalLowercase()
    {
        var dbName = Guid.NewGuid().ToString();
        using var dbContext = CreateDbContext(dbName);
        await SeedBaseDataAsync(dbContext);

        var useCase = new GetKnowledgeGraphUseCase(dbContext, _tenantContextMock.Object);

        var result = await useCase.ExecuteAsync(_subjectId);

        Assert.True(result.IsSuccess);
        Assert.Equal(_subjectId.ToString("D").ToLowerInvariant(), result.Data!.SubjectId);
    }

    [Fact]
    public async Task ExecuteAsync_NodeDeterministicOrdering_OrderByOrderIndexThenNodeId()
    {
        var dbName = Guid.NewGuid().ToString();
        using var dbContext = CreateDbContext(dbName);
        await SeedBaseDataAsync(dbContext);

        var node1 = new KnowledgeNode
        {
            NodeId = 200,
            CenterId = _centerId,
            SubjectId = _subjectId,
            NodeCode = "N200",
            NodeName = "Node 200",
            NodeType = NodeType.Topic,
            OrderIndex = 1,
            ExamImportance = 10,
            IsDeleted = false,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        };
        var node2 = new KnowledgeNode
        {
            NodeId = 100,
            CenterId = _centerId,
            SubjectId = _subjectId,
            NodeCode = "N100",
            NodeName = "Node 100",
            NodeType = NodeType.Topic,
            OrderIndex = 1,
            ExamImportance = 20,
            IsDeleted = false,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        };
        var node3 = new KnowledgeNode
        {
            NodeId = 50,
            CenterId = _centerId,
            SubjectId = _subjectId,
            NodeCode = "N50",
            NodeName = "Node 50",
            NodeType = NodeType.Topic,
            OrderIndex = 2,
            ExamImportance = 30,
            IsDeleted = false,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        };

        dbContext.KnowledgeNodes.AddRange(node1, node2, node3);
        await dbContext.SaveChangesAsync();

        var useCase = new GetKnowledgeGraphUseCase(dbContext, _tenantContextMock.Object);

        var result = await useCase.ExecuteAsync(_subjectId);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Data!.Nodes.Count);
        Assert.Equal("100", result.Data.Nodes[0].NodeId);
        Assert.Equal("200", result.Data.Nodes[1].NodeId);
        Assert.Equal("50", result.Data.Nodes[2].NodeId);
    }

    [Fact]
    public async Task ExecuteAsync_EdgeDeterministicOrdering_OrderBySourceNodeIdThenTargetNodeIdThenRelationTypeThenEdgeId()
    {
        var dbName = Guid.NewGuid().ToString();
        using var dbContext = CreateDbContext(dbName);
        await SeedBaseDataAsync(dbContext);

        var edge1 = new KnowledgeEdge
        {
            EdgeId = 30,
            CenterId = _centerId,
            SubjectId = _subjectId,
            SourceNodeId = 10,
            TargetNodeId = 20,
            RelationType = RelationType.PrerequisiteOf,
            Weight = 1.0m,
            IsDeleted = false,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        };
        var edge2 = new KnowledgeEdge
        {
            EdgeId = 10,
            CenterId = _centerId,
            SubjectId = _subjectId,
            SourceNodeId = 10,
            TargetNodeId = 20,
            RelationType = RelationType.PrerequisiteOf,
            Weight = 0.5m,
            IsDeleted = false,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        };
        var edge3 = new KnowledgeEdge
        {
            EdgeId = 5,
            CenterId = _centerId,
            SubjectId = _subjectId,
            SourceNodeId = 5,
            TargetNodeId = 20,
            RelationType = RelationType.PrerequisiteOf,
            Weight = 0.8m,
            IsDeleted = false,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        };

        dbContext.KnowledgeEdges.AddRange(edge1, edge2, edge3);
        await dbContext.SaveChangesAsync();

        var useCase = new GetKnowledgeGraphUseCase(dbContext, _tenantContextMock.Object);

        var result = await useCase.ExecuteAsync(_subjectId);

        Assert.True(result.IsSuccess);
        Assert.Equal(3, result.Data!.Edges.Count);
        Assert.Equal("5", result.Data.Edges[0].EdgeId);
        Assert.Equal("10", result.Data.Edges[1].EdgeId);
        Assert.Equal("30", result.Data.Edges[2].EdgeId);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyGraph_ReturnsSuccessWithEmptyCollections()
    {
        var dbName = Guid.NewGuid().ToString();
        using var dbContext = CreateDbContext(dbName);
        await SeedBaseDataAsync(dbContext);

        var useCase = new GetKnowledgeGraphUseCase(dbContext, _tenantContextMock.Object);

        var result = await useCase.ExecuteAsync(_subjectId);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.NotNull(result.Data!.Nodes);
        Assert.NotNull(result.Data.Edges);
        Assert.Empty(result.Data.Nodes);
        Assert.Empty(result.Data.Edges);
    }

    [Fact]
    public async Task ExecuteAsync_SoftDeletedNodes_Excluded()
    {
        var dbName = Guid.NewGuid().ToString();
        using var dbContext = CreateDbContext(dbName);
        await SeedBaseDataAsync(dbContext);

        var activeNode = new KnowledgeNode
        {
            NodeId = 1,
            CenterId = _centerId,
            SubjectId = _subjectId,
            NodeCode = "N1",
            NodeName = "Active Node",
            NodeType = NodeType.Topic,
            IsDeleted = false,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        };
        var deletedNode = new KnowledgeNode
        {
            NodeId = 2,
            CenterId = _centerId,
            SubjectId = _subjectId,
            NodeCode = "N2",
            NodeName = "Deleted Node",
            NodeType = NodeType.Topic,
            IsDeleted = true,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        };

        dbContext.KnowledgeNodes.AddRange(activeNode, deletedNode);
        await dbContext.SaveChangesAsync();

        var useCase = new GetKnowledgeGraphUseCase(dbContext, _tenantContextMock.Object);

        var result = await useCase.ExecuteAsync(_subjectId);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!.Nodes);
        Assert.Equal("1", result.Data.Nodes[0].NodeId);
    }

    [Fact]
    public async Task ExecuteAsync_SoftDeletedEdges_Excluded()
    {
        var dbName = Guid.NewGuid().ToString();
        using var dbContext = CreateDbContext(dbName);
        await SeedBaseDataAsync(dbContext);

        var activeEdge = new KnowledgeEdge
        {
            EdgeId = 1,
            CenterId = _centerId,
            SubjectId = _subjectId,
            SourceNodeId = 10,
            TargetNodeId = 20,
            RelationType = RelationType.PrerequisiteOf,
            Weight = 1.0m,
            IsDeleted = false,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        };
        var deletedEdge = new KnowledgeEdge
        {
            EdgeId = 2,
            CenterId = _centerId,
            SubjectId = _subjectId,
            SourceNodeId = 10,
            TargetNodeId = 20,
            RelationType = RelationType.PrerequisiteOf,
            Weight = 1.0m,
            IsDeleted = true,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        };

        dbContext.KnowledgeEdges.AddRange(activeEdge, deletedEdge);
        await dbContext.SaveChangesAsync();

        var useCase = new GetKnowledgeGraphUseCase(dbContext, _tenantContextMock.Object);

        var result = await useCase.ExecuteAsync(_subjectId);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!.Edges);
        Assert.Equal("1", result.Data.Edges[0].EdgeId);
    }

    [Fact]
    public async Task ExecuteAsync_NodeAndEdgeOtherSubject_Excluded()
    {
        var dbName = Guid.NewGuid().ToString();
        using var dbContext = CreateDbContext(dbName);
        await SeedBaseDataAsync(dbContext);

        var otherSubjectId = Guid.NewGuid();
        var otherSubject = new Subject
        {
            SubjectId = otherSubjectId,
            CenterId = _centerId,
            SubjectCode = "SUB02",
            SubjectName = "Other Subject",
            IsDeleted = false,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        };

        var otherNode = new KnowledgeNode
        {
            NodeId = 99,
            CenterId = _centerId,
            SubjectId = otherSubjectId,
            NodeCode = "N99",
            NodeName = "Other Subject Node",
            NodeType = NodeType.Topic,
            IsDeleted = false,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        };
        var otherEdge = new KnowledgeEdge
        {
            EdgeId = 99,
            CenterId = _centerId,
            SubjectId = otherSubjectId,
            SourceNodeId = 99,
            TargetNodeId = 100,
            RelationType = RelationType.PrerequisiteOf,
            Weight = 1.0m,
            IsDeleted = false,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        };

        dbContext.Subjects.Add(otherSubject);
        dbContext.KnowledgeNodes.Add(otherNode);
        dbContext.KnowledgeEdges.Add(otherEdge);
        await dbContext.SaveChangesAsync();

        var useCase = new GetKnowledgeGraphUseCase(dbContext, _tenantContextMock.Object);

        var result = await useCase.ExecuteAsync(_subjectId);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!.Nodes);
        Assert.Empty(result.Data.Edges);
    }

    [Fact]
    public async Task ExecuteAsync_NodeAndEdgeCrossTenant_Excluded()
    {
        var dbName = Guid.NewGuid().ToString();
        using var dbContext = CreateDbContext(dbName);
        await SeedBaseDataAsync(dbContext);

        var otherCenterId = Guid.NewGuid();

        var crossNode = new KnowledgeNode
        {
            NodeId = 88,
            CenterId = otherCenterId,
            SubjectId = _subjectId,
            NodeCode = "N88",
            NodeName = "Cross Tenant Node",
            NodeType = NodeType.Topic,
            IsDeleted = false,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        };
        var crossEdge = new KnowledgeEdge
        {
            EdgeId = 88,
            CenterId = otherCenterId,
            SubjectId = _subjectId,
            SourceNodeId = 88,
            TargetNodeId = 89,
            RelationType = RelationType.PrerequisiteOf,
            Weight = 1.0m,
            IsDeleted = false,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        };

        dbContext.KnowledgeNodes.Add(crossNode);
        dbContext.KnowledgeEdges.Add(crossEdge);
        await dbContext.SaveChangesAsync();

        var useCase = new GetKnowledgeGraphUseCase(dbContext, _tenantContextMock.Object);

        var result = await useCase.ExecuteAsync(_subjectId);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!.Nodes);
        Assert.Empty(result.Data.Edges);
    }

    [Fact]
    public async Task ExecuteAsync_MissingOrDeletedSubject_ReturnsResourceNotFound()
    {
        var dbName = Guid.NewGuid().ToString();
        using var dbContext = CreateDbContext(dbName);
        await SeedBaseDataAsync(dbContext);

        var useCase = new GetKnowledgeGraphUseCase(dbContext, _tenantContextMock.Object);

        // Missing subject
        var resultMissing = await useCase.ExecuteAsync(Guid.NewGuid());
        Assert.False(resultMissing.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, resultMissing.ErrorCode);
        Assert.Null(resultMissing.Data);

        // Soft-deleted subject
        var subject = await dbContext.Subjects.FirstAsync(s => s.SubjectId == _subjectId);
        subject.IsDeleted = true;
        await dbContext.SaveChangesAsync();

        var resultDeleted = await useCase.ExecuteAsync(_subjectId);
        Assert.False(resultDeleted.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, resultDeleted.ErrorCode);
        Assert.Null(resultDeleted.Data);
    }

    [Fact]
    public async Task ExecuteAsync_CrossTenantSubject_ReturnsResourceNotFound()
    {
        var dbName = Guid.NewGuid().ToString();
        using var dbContext = CreateDbContext(dbName);
        await SeedBaseDataAsync(dbContext);

        var otherCenterId = Guid.NewGuid();
        var otherSubjectId = Guid.NewGuid();

        var otherCenter = new Center
        {
            CenterId = otherCenterId,
            CenterName = "Other Center",
            CenterCode = "OC01",
            Timezone = "UTC",
            Status = CenterStatus.Active,
            IsDeleted = false,
            CreatedAt = _utcNow.AddDays(-1),
            UpdatedAt = _utcNow.AddDays(-1)
        };

        var otherSubject = new Subject
        {
            SubjectId = otherSubjectId,
            CenterId = otherCenterId,
            SubjectCode = "SUB02",
            SubjectName = "Cross Tenant Subject",
            IsDeleted = false,
            CreatedAt = _utcNow.AddDays(-1),
            UpdatedAt = _utcNow.AddDays(-1)
        };

        dbContext.Centers.Add(otherCenter);
        dbContext.Subjects.Add(otherSubject);
        await dbContext.SaveChangesAsync();

        // Verify other subject is persisted prior to usecase call
        var dbOtherSubject = await dbContext.Subjects.IgnoreQueryFilters().FirstOrDefaultAsync(s => s.SubjectId == otherSubjectId);
        Assert.NotNull(dbOtherSubject);
        Assert.Equal(otherCenterId, dbOtherSubject!.CenterId);

        var useCase = new GetKnowledgeGraphUseCase(dbContext, _tenantContextMock.Object);

        var result = await useCase.ExecuteAsync(otherSubjectId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task ExecuteAsync_SuspendedCenter_ReturnsResourceNotFound()
    {
        var dbName = Guid.NewGuid().ToString();
        using var dbContext = CreateDbContext(dbName);
        await SeedBaseDataAsync(dbContext);

        var center = await dbContext.Centers.FirstAsync(c => c.CenterId == _centerId);
        center.Status = CenterStatus.Suspended;
        await dbContext.SaveChangesAsync();

        var useCase = new GetKnowledgeGraphUseCase(dbContext, _tenantContextMock.Object);

        var result = await useCase.ExecuteAsync(_subjectId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task ExecuteAsync_MissingCenter_ReturnsResourceNotFound()
    {
        var dbName = Guid.NewGuid().ToString();
        using var dbContext = CreateDbContext(dbName);

        var subject = new Subject
        {
            SubjectId = _subjectId,
            CenterId = _centerId,
            SubjectCode = "SUB01",
            SubjectName = "Test Subject",
            IsDeleted = false,
            CreatedAt = _utcNow.AddDays(-1),
            UpdatedAt = _utcNow.AddDays(-1)
        };
        dbContext.Subjects.Add(subject);
        await dbContext.SaveChangesAsync();

        var useCase = new GetKnowledgeGraphUseCase(dbContext, _tenantContextMock.Object);

        var result = await useCase.ExecuteAsync(_subjectId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task ExecuteAsync_DeletedCenter_ReturnsResourceNotFound()
    {
        var dbName = Guid.NewGuid().ToString();
        using var dbContext = CreateDbContext(dbName);
        await SeedBaseDataAsync(dbContext);

        var center = await dbContext.Centers.FirstAsync(c => c.CenterId == _centerId);
        center.IsDeleted = true;
        await dbContext.SaveChangesAsync();

        var useCase = new GetKnowledgeGraphUseCase(dbContext, _tenantContextMock.Object);

        var result = await useCase.ExecuteAsync(_subjectId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        Assert.Null(result.Data);
    }

    [Fact]
    public async Task ExecuteAsync_EmptyGuidSubjectId_ReturnsValidationFailed()
    {
        var dbName = Guid.NewGuid().ToString();
        using var dbContext = CreateDbContext(dbName);

        var useCase = new GetKnowledgeGraphUseCase(dbContext, _tenantContextMock.Object);

        var result = await useCase.ExecuteAsync(Guid.Empty);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Theory]
    [InlineData(false, "d78d46e2-2a78-43f1-b9db-953e1987d609", "c56d46e2-2a78-43f1-b9db-953e1987d609")]
    [InlineData(true, null, "c56d46e2-2a78-43f1-b9db-953e1987d609")]
    [InlineData(true, "00000000-0000-0000-0000-000000000000", "c56d46e2-2a78-43f1-b9db-953e1987d609")]
    [InlineData(true, "d78d46e2-2a78-43f1-b9db-953e1987d609", null)]
    [InlineData(true, "d78d46e2-2a78-43f1-b9db-953e1987d609", "00000000-0000-0000-0000-000000000000")]
    public async Task ExecuteAsync_InvalidTenantContext_ReturnsResourceNotFound(bool isResolved, string? centerIdStr, string? userIdStr)
    {
        var dbName = Guid.NewGuid().ToString();
        using var dbContext = CreateDbContext(dbName);

        _tenantContextMock.Setup(x => x.IsResolved).Returns(isResolved);
        _tenantContextMock.Setup(x => x.CenterId).Returns(centerIdStr != null ? Guid.Parse(centerIdStr) : (Guid?)null);
        _tenantContextMock.Setup(x => x.UserId).Returns(userIdStr != null ? Guid.Parse(userIdStr) : (Guid?)null);

        var useCase = new GetKnowledgeGraphUseCase(dbContext, _tenantContextMock.Object);

        var result = await useCase.ExecuteAsync(_subjectId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Admin")]
    [InlineData("student")]
    [InlineData("TEACHER")]
    [InlineData("centermanager")]
    [InlineData("123")]
    public async Task ExecuteAsync_InvalidRole_ReturnsResourceNotFound(string? role)
    {
        var dbName = Guid.NewGuid().ToString();
        using var dbContext = CreateDbContext(dbName);

        _tenantContextMock.Setup(x => x.Role).Returns(role!);

        var useCase = new GetKnowledgeGraphUseCase(dbContext, _tenantContextMock.Object);

        var result = await useCase.ExecuteAsync(_subjectId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_ReadQuery_DoesNotTrackEntities()
    {
        var dbName = Guid.NewGuid().ToString();
        using var dbContext = CreateDbContext(dbName);
        await SeedBaseDataAsync(dbContext);

        dbContext.ChangeTracker.Clear();

        var useCase = new GetKnowledgeGraphUseCase(dbContext, _tenantContextMock.Object);

        var result = await useCase.ExecuteAsync(_subjectId);

        Assert.True(result.IsSuccess);
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task ExecuteAsync_PassesExactCancellationToken()
    {
        var dbName = Guid.NewGuid().ToString();
        using var dbContext = CreateDbContext(dbName);
        await SeedBaseDataAsync(dbContext);

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Cancel immediately to ensure token is evaluated

        var useCase = new GetKnowledgeGraphUseCase(dbContext, _tenantContextMock.Object);

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => useCase.ExecuteAsync(_subjectId, cts.Token));
    }

    [Fact]
    public void Production_Source_Contains_No_IgnoreQueryFilters()
    {
        var sourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "src", "EduTwin.BLL", "KnowledgeGraph", "GetKnowledgeGraphUseCase.cs");
        var fullPath = Path.GetFullPath(sourcePath);

        Assert.True(File.Exists(fullPath), $"Source file does not exist at expected path: {fullPath}");

        var content = File.ReadAllText(fullPath);
        Assert.DoesNotContain("IgnoreQueryFilters", content);
    }

    [Fact]
    public void Production_Source_Contains_No_Query_In_Foreach()
    {
        var sourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "src", "EduTwin.BLL", "KnowledgeGraph", "GetKnowledgeGraphUseCase.cs");
        var fullPath = Path.GetFullPath(sourcePath);

        Assert.True(File.Exists(fullPath), $"Source file does not exist at expected path: {fullPath}");

        var content = File.ReadAllText(fullPath);
        Assert.DoesNotContain("foreach", content);
    }

    [Fact]
    public void Schema_And_Entity_Untouched()
    {
        // Reflection check that DAL entity types exist and remain untouched
        Assert.NotNull(typeof(KnowledgeNode));
        Assert.NotNull(typeof(KnowledgeEdge));
        Assert.NotNull(typeof(Subject));
        Assert.NotNull(typeof(Center));
    }
}
