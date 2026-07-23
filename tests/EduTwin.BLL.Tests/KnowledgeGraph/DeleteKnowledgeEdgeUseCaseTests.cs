using System;
using System.IO;
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

public class DeleteKnowledgeEdgeUseCaseTests
{
    private readonly Mock<ITenantContext> _tenantContextMock;
    private readonly Mock<TimeProvider> _timeProviderMock;
    private readonly DateTimeOffset _fixedTime;
    private readonly Guid _centerId;
    private readonly Guid _userId;
    private readonly Guid _subjectId;

    public DeleteKnowledgeEdgeUseCaseTests()
    {
        _tenantContextMock = new Mock<ITenantContext>();
        _timeProviderMock = new Mock<TimeProvider>();
        _fixedTime = new DateTimeOffset(2026, 7, 23, 14, 0, 0, TimeSpan.Zero);

        _centerId = Guid.NewGuid();
        _userId = Guid.NewGuid();
        _subjectId = Guid.NewGuid();

        _timeProviderMock.Setup(x => x.GetUtcNow()).Returns(_fixedTime);

        _tenantContextMock.Setup(x => x.IsResolved).Returns(true);
        _tenantContextMock.Setup(x => x.CenterId).Returns(_centerId);
        _tenantContextMock.Setup(x => x.UserId).Returns(_userId);
        _tenantContextMock.Setup(x => x.Role).Returns(nameof(UserRole.CenterManager));
    }

    private EduTwinDbContext CreateDbContext(string dbName, Exception? exceptionToThrow = null)
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var tenantAccessorMock = new Mock<ITenantIdAccessor>();
        tenantAccessorMock.Setup(x => x.CenterId).Returns(() => _tenantContextMock.Object.CenterId);

        if (exceptionToThrow != null)
        {
            return new FaultyDbContext(options, tenantAccessorMock.Object, exceptionToThrow);
        }

        return new EduTwinDbContext(options, tenantAccessorMock.Object);
    }

    private async Task SeedBaseDataAsync(EduTwinDbContext dbContext, ulong edgeId = 1, ulong rowVersion = 1)
    {
        var center = new Center
        {
            CenterId = _centerId,
            CenterName = "Test Center",
            CenterCode = "TC01",
            Timezone = "UTC",
            Status = CenterStatus.Active,
            IsDeleted = false,
            CreatedAt = _fixedTime.UtcDateTime.AddDays(-1),
            UpdatedAt = _fixedTime.UtcDateTime.AddDays(-1)
        };

        var subject = new Subject
        {
            SubjectId = _subjectId,
            CenterId = _centerId,
            SubjectCode = "SUB01",
            SubjectName = "Test Subject",
            IsDeleted = false,
            CreatedAt = _fixedTime.UtcDateTime.AddDays(-1),
            UpdatedAt = _fixedTime.UtcDateTime.AddDays(-1)
        };

        var sourceNode = new KnowledgeNode
        {
            NodeId = 10,
            CenterId = _centerId,
            SubjectId = _subjectId,
            NodeCode = "N10",
            NodeName = "Node 10",
            NodeType = NodeType.Topic,
            IsDeleted = false,
            CreatedAt = _fixedTime.UtcDateTime.AddDays(-1),
            CreatedBy = _userId,
            UpdatedAt = _fixedTime.UtcDateTime.AddDays(-1),
            UpdatedBy = _userId
        };

        var targetNode = new KnowledgeNode
        {
            NodeId = 20,
            CenterId = _centerId,
            SubjectId = _subjectId,
            NodeCode = "N20",
            NodeName = "Node 20",
            NodeType = NodeType.Topic,
            IsDeleted = false,
            CreatedAt = _fixedTime.UtcDateTime.AddDays(-1),
            CreatedBy = _userId,
            UpdatedAt = _fixedTime.UtcDateTime.AddDays(-1),
            UpdatedBy = _userId
        };

        var edge = new KnowledgeEdge
        {
            EdgeId = edgeId,
            CenterId = _centerId,
            SubjectId = _subjectId,
            SourceNodeId = 10,
            TargetNodeId = 20,
            RelationType = RelationType.PrerequisiteOf,
            Weight = 0.5m,
            CreatedAt = _fixedTime.UtcDateTime.AddDays(-1),
            CreatedBy = _userId,
            UpdatedAt = _fixedTime.UtcDateTime.AddDays(-1),
            UpdatedBy = _userId,
            IsDeleted = false,
            RowVersion = rowVersion
        };

        dbContext.Centers.Add(center);
        dbContext.Subjects.Add(subject);
        dbContext.KnowledgeNodes.AddRange(sourceNode, targetNode);
        dbContext.KnowledgeEdges.Add(edge);
        await dbContext.SaveChangesAsync();
    }

    [Theory]
    [InlineData(nameof(UserRole.CenterManager))]
    [InlineData(nameof(UserRole.Teacher))]
    public async Task ExecuteAsync_CenterManagerOrTeacher_SoftDeleteSuccess(string role)
    {
        var dbName = Guid.NewGuid().ToString();
        using var dbContext = CreateDbContext(dbName);
        await SeedBaseDataAsync(dbContext, edgeId: 1, rowVersion: 1);

        _tenantContextMock.Setup(x => x.Role).Returns(role);

        var useCase = new DeleteKnowledgeEdgeUseCase(dbContext, _tenantContextMock.Object, _timeProviderMock.Object);

        var result = await useCase.ExecuteAsync("1");

        Assert.True(result.IsSuccess);
        Assert.Null(result.ErrorCode);

        var deletedEdge = await dbContext.KnowledgeEdges.IgnoreQueryFilters().FirstOrDefaultAsync(e => e.EdgeId == 1);
        Assert.NotNull(deletedEdge);
        Assert.True(deletedEdge!.IsDeleted);
        Assert.Equal(_fixedTime.UtcDateTime, deletedEdge.DeletedAt);
        Assert.Equal(_userId, deletedEdge.DeletedBy);
        Assert.Equal(_fixedTime.UtcDateTime, deletedEdge.UpdatedAt);
        Assert.Equal(_userId, deletedEdge.UpdatedBy);
        Assert.Equal(2uL, deletedEdge.RowVersion);

        var subject = await dbContext.Subjects.FirstOrDefaultAsync(s => s.SubjectId == _subjectId);
        Assert.NotNull(subject);
        Assert.False(subject!.IsDeleted);

        var sourceNode = await dbContext.KnowledgeNodes.FirstOrDefaultAsync(n => n.NodeId == 10);
        Assert.NotNull(sourceNode);
        Assert.False(sourceNode!.IsDeleted);

        var targetNode = await dbContext.KnowledgeNodes.FirstOrDefaultAsync(n => n.NodeId == 20);
        Assert.NotNull(targetNode);
        Assert.False(targetNode!.IsDeleted);
    }

    [Fact]
    public async Task ExecuteAsync_PassesExactCancellationToken()
    {
        var dbName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(databaseName: dbName)
            .Options;

        var tenantAccessorMock = new Mock<ITenantIdAccessor>();
        tenantAccessorMock.Setup(x => x.CenterId).Returns(() => _tenantContextMock.Object.CenterId);

        using var dbContext = new TokenCaptureDbContext(options, tenantAccessorMock.Object);
        await SeedBaseDataAsync(dbContext);

        dbContext.CaptureEnabled = true;

        using var cts = new CancellationTokenSource();
        var token = cts.Token;

        var useCase = new DeleteKnowledgeEdgeUseCase(dbContext, _tenantContextMock.Object, _timeProviderMock.Object);

        var result = await useCase.ExecuteAsync("1", token);

        Assert.True(result.IsSuccess);
        Assert.Equal(token, dbContext.CapturedSaveToken);
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

        var useCase = new DeleteKnowledgeEdgeUseCase(dbContext, _tenantContextMock.Object, _timeProviderMock.Object);

        var result = await useCase.ExecuteAsync("1");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Student")]
    [InlineData("Admin")]
    [InlineData("teacher")]
    [InlineData("CENTERMANAGER")]
    [InlineData("123")]
    public async Task ExecuteAsync_InvalidRole_ReturnsResourceNotFound(string? role)
    {
        var dbName = Guid.NewGuid().ToString();
        using var dbContext = CreateDbContext(dbName);

        _tenantContextMock.Setup(x => x.Role).Returns(role!);

        var useCase = new DeleteKnowledgeEdgeUseCase(dbContext, _tenantContextMock.Object, _timeProviderMock.Object);

        var result = await useCase.ExecuteAsync("1");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("+1")]
    [InlineData("1.0")]
    [InlineData("18446744073709551616")]
    [InlineData(" 1")]
    [InlineData("1 ")]
    [InlineData("abc")]
    public async Task ExecuteAsync_InvalidEdgeId_ReturnsValidationFailed(string? edgeId)
    {
        var dbName = Guid.NewGuid().ToString();
        using var dbContext = CreateDbContext(dbName);

        var useCase = new DeleteKnowledgeEdgeUseCase(dbContext, _tenantContextMock.Object, _timeProviderMock.Object);

        var result = await useCase.ExecuteAsync(edgeId!);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
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

        var useCase = new DeleteKnowledgeEdgeUseCase(dbContext, _tenantContextMock.Object, _timeProviderMock.Object);

        var result = await useCase.ExecuteAsync("1");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);

        var dbEdge = await dbContext.KnowledgeEdges.FirstAsync(e => e.EdgeId == 1);
        Assert.False(dbEdge.IsDeleted);
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

        var useCase = new DeleteKnowledgeEdgeUseCase(dbContext, _tenantContextMock.Object, _timeProviderMock.Object);

        var result = await useCase.ExecuteAsync("1");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);

        var dbEdge = await dbContext.KnowledgeEdges.FirstAsync(e => e.EdgeId == 1);
        Assert.False(dbEdge.IsDeleted);
    }

    [Fact]
    public async Task ExecuteAsync_MissingCenter_ReturnsResourceNotFound()
    {
        var dbName = Guid.NewGuid().ToString();
        using var dbContext = CreateDbContext(dbName);

        var useCase = new DeleteKnowledgeEdgeUseCase(dbContext, _tenantContextMock.Object, _timeProviderMock.Object);

        var result = await useCase.ExecuteAsync("1");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_MissingEdge_ReturnsResourceNotFound()
    {
        var dbName = Guid.NewGuid().ToString();
        using var dbContext = CreateDbContext(dbName);
        await SeedBaseDataAsync(dbContext);

        var useCase = new DeleteKnowledgeEdgeUseCase(dbContext, _tenantContextMock.Object, _timeProviderMock.Object);

        var result = await useCase.ExecuteAsync("999");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);

        var dbEdge = await dbContext.KnowledgeEdges.FirstAsync(e => e.EdgeId == 1);
        Assert.False(dbEdge.IsDeleted);
    }

    [Fact]
    public async Task ExecuteAsync_CrossTenantEdge_ReturnsResourceNotFound()
    {
        var dbName = Guid.NewGuid().ToString();
        using var dbContext = CreateDbContext(dbName);
        await SeedBaseDataAsync(dbContext);

        _tenantContextMock.Setup(x => x.CenterId).Returns(Guid.NewGuid());

        var useCase = new DeleteKnowledgeEdgeUseCase(dbContext, _tenantContextMock.Object, _timeProviderMock.Object);

        var result = await useCase.ExecuteAsync("1");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_SoftDeletedEdge_ReturnsResourceNotFound()
    {
        var dbName = Guid.NewGuid().ToString();
        using var dbContext = CreateDbContext(dbName);
        await SeedBaseDataAsync(dbContext);

        var edge = await dbContext.KnowledgeEdges.FirstAsync(e => e.EdgeId == 1);
        edge.IsDeleted = true;
        await dbContext.SaveChangesAsync();

        var useCase = new DeleteKnowledgeEdgeUseCase(dbContext, _tenantContextMock.Object, _timeProviderMock.Object);

        var result = await useCase.ExecuteAsync("1");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_DbUpdateConcurrencyException_ReturnsConcurrencyConflictAndClearsChangeTracker()
    {
        var dbName = Guid.NewGuid().ToString();
        var ex = new DbUpdateConcurrencyException("Concurrency conflict");
        using var dbContext = (FaultyDbContext)CreateDbContext(dbName, exceptionToThrow: ex);
        await SeedBaseDataAsync(dbContext);

        dbContext.StartThrowing();

        var useCase = new DeleteKnowledgeEdgeUseCase(dbContext, _tenantContextMock.Object, _timeProviderMock.Object);

        var result = await useCase.ExecuteAsync("1");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, result.ErrorCode);
        Assert.Empty(dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task ExecuteAsync_UnrelatedDbUpdateException_Rethrows()
    {
        var dbName = Guid.NewGuid().ToString();
        var ex = new DbUpdateException("Database disk failure");
        using var dbContext = (FaultyDbContext)CreateDbContext(dbName, exceptionToThrow: ex);
        await SeedBaseDataAsync(dbContext);

        dbContext.StartThrowing();

        var useCase = new DeleteKnowledgeEdgeUseCase(dbContext, _tenantContextMock.Object, _timeProviderMock.Object);

        await Assert.ThrowsAsync<DbUpdateException>(() => useCase.ExecuteAsync("1"));
    }

    [Fact]
    public async Task ExecuteAsync_UnexpectedDatabaseException_Rethrows()
    {
        var dbName = Guid.NewGuid().ToString();
        var ex = new InvalidOperationException("Unexpected system error");
        using var dbContext = (FaultyDbContext)CreateDbContext(dbName, exceptionToThrow: ex);
        await SeedBaseDataAsync(dbContext);

        dbContext.StartThrowing();

        var useCase = new DeleteKnowledgeEdgeUseCase(dbContext, _tenantContextMock.Object, _timeProviderMock.Object);

        await Assert.ThrowsAsync<InvalidOperationException>(() => useCase.ExecuteAsync("1"));
    }

    [Fact]
    public void Production_Source_Contains_No_IgnoreQueryFilters()
    {
        var sourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "src", "EduTwin.BLL", "KnowledgeGraph", "DeleteKnowledgeEdgeUseCase.cs");
        var fullPath = Path.GetFullPath(sourcePath);

        Assert.True(File.Exists(fullPath), $"Source file does not exist at expected path: {fullPath}");

        var content = File.ReadAllText(fullPath);
        Assert.DoesNotContain("IgnoreQueryFilters", content);
    }

    [Fact]
    public void Production_Source_Contains_No_Remove_Or_RemoveRange()
    {
        var sourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "src", "EduTwin.BLL", "KnowledgeGraph", "DeleteKnowledgeEdgeUseCase.cs");
        var fullPath = Path.GetFullPath(sourcePath);

        Assert.True(File.Exists(fullPath), $"Source file does not exist at expected path: {fullPath}");

        var content = File.ReadAllText(fullPath);
        Assert.DoesNotContain(".Remove(", content);
        Assert.DoesNotContain(".RemoveRange(", content);
    }

    private class FaultyDbContext : EduTwinDbContext
    {
        private readonly Exception _exceptionToThrow;
        private bool _throwNow;

        public FaultyDbContext(DbContextOptions<EduTwinDbContext> options, ITenantIdAccessor tenantAccessor, Exception exceptionToThrow)
            : base(options, tenantAccessor)
        {
            _exceptionToThrow = exceptionToThrow;
        }

        public void StartThrowing() => _throwNow = true;

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (_throwNow)
            {
                throw _exceptionToThrow;
            }
            return base.SaveChangesAsync(cancellationToken);
        }
    }

    private class TokenCaptureDbContext : EduTwinDbContext
    {
        public CancellationToken? CapturedSaveToken { get; private set; }
        public bool CaptureEnabled { get; set; }

        public TokenCaptureDbContext(DbContextOptions<EduTwinDbContext> options, ITenantIdAccessor tenantAccessor)
            : base(options, tenantAccessor)
        {
        }

        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (CaptureEnabled)
            {
                CapturedSaveToken = cancellationToken;
            }

            return base.SaveChangesAsync(cancellationToken);
        }
    }
}
