using System;
using System.Globalization;
using System.IO;
using System.Linq;
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

public class UpdateKnowledgeEdgeUseCaseTests
{
    private readonly Mock<ITenantContext> _tenantContextMock;
    private readonly Mock<TimeProvider> _timeProviderMock;
    private readonly DateTimeOffset _fixedTime;
    private readonly Guid _centerId;
    private readonly Guid _userId;
    private readonly Guid _subjectId;

    public UpdateKnowledgeEdgeUseCaseTests()
    {
        _tenantContextMock = new Mock<ITenantContext>();
        _timeProviderMock = new Mock<TimeProvider>();
        _fixedTime = new DateTimeOffset(2026, 7, 23, 12, 0, 0, TimeSpan.Zero);

        _centerId = Guid.NewGuid();
        _userId = Guid.NewGuid();
        _subjectId = Guid.NewGuid();

        _timeProviderMock.Setup(x => x.GetUtcNow()).Returns(_fixedTime);

        _tenantContextMock.Setup(x => x.IsResolved).Returns(true);
        _tenantContextMock.Setup(x => x.CenterId).Returns(_centerId);
        _tenantContextMock.Setup(x => x.UserId).Returns(_userId);
        _tenantContextMock.Setup(x => x.Role).Returns(nameof(UserRole.Teacher));
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

    private async Task SeedBaseDataAsync(EduTwinDbContext dbContext, ulong edgeId = 1, ulong rowVersion = 1, decimal weight = 0.5m)
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
            Weight = weight,
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
    [InlineData(nameof(UserRole.Teacher))]
    [InlineData(nameof(UserRole.CenterManager))]
    public async Task ExecuteAsync_TeacherOrCenterManager_UpdateSuccess(string role)
    {
        var dbName = Guid.NewGuid().ToString();
        using var dbContext = CreateDbContext(dbName);
        await SeedBaseDataAsync(dbContext);

        _tenantContextMock.Setup(x => x.Role).Returns(role);

        var useCase = new UpdateKnowledgeEdgeUseCase(dbContext, _tenantContextMock.Object, _timeProviderMock.Object);
        var request = new UpdateKnowledgeEdgeRequest
        {
            Weight = 0.8m,
            RowVersion = "1"
        };

        var result = await useCase.ExecuteAsync("1", request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal("1", result.Data.EdgeId);
        Assert.Equal(_subjectId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(), result.Data.SubjectId);
        Assert.Equal("10", result.Data.SourceNodeId);
        Assert.Equal("20", result.Data.TargetNodeId);
        Assert.Equal("PrerequisiteOf", result.Data.RelationType);
        Assert.Equal(0.8m, result.Data.Weight);
        Assert.Equal("2", result.Data.RowVersion);

        var updatedEdge = await dbContext.KnowledgeEdges.FindAsync(1uL);
        Assert.NotNull(updatedEdge);
        Assert.Equal(0.8m, updatedEdge.Weight);
        Assert.Equal(2uL, updatedEdge.RowVersion);
        Assert.Equal(_fixedTime.UtcDateTime, updatedEdge.UpdatedAt);
        Assert.Equal(_userId, updatedEdge.UpdatedBy);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    public async Task ExecuteAsync_WeightBoundaries0And1_Success(double weightValue)
    {
        var dbName = Guid.NewGuid().ToString();
        using var dbContext = CreateDbContext(dbName);
        await SeedBaseDataAsync(dbContext);

        var useCase = new UpdateKnowledgeEdgeUseCase(dbContext, _tenantContextMock.Object, _timeProviderMock.Object);
        var request = new UpdateKnowledgeEdgeRequest
        {
            Weight = (decimal)weightValue,
            RowVersion = "1"
        };

        var result = await useCase.ExecuteAsync("1", request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal((decimal)weightValue, result.Data.Weight);
    }

    [Fact]
    public async Task ExecuteAsync_ImmutableFields_Unchanged()
    {
        var dbName = Guid.NewGuid().ToString();
        using var dbContext = CreateDbContext(dbName);
        await SeedBaseDataAsync(dbContext, edgeId: 1, rowVersion: 1, weight: 0.5m);

        var originalEdge = await dbContext.KnowledgeEdges.AsNoTracking().FirstAsync(e => e.EdgeId == 1);

        var useCase = new UpdateKnowledgeEdgeUseCase(dbContext, _tenantContextMock.Object, _timeProviderMock.Object);
        var request = new UpdateKnowledgeEdgeRequest
        {
            Weight = 0.9m,
            RowVersion = "1"
        };

        var result = await useCase.ExecuteAsync("1", request);

        var updatedEdge = await dbContext.KnowledgeEdges.AsNoTracking().FirstAsync(e => e.EdgeId == 1);

        Assert.True(result.IsSuccess);
        Assert.Equal(0.9m, updatedEdge.Weight);
        Assert.Equal(2uL, updatedEdge.RowVersion);

        Assert.Equal(originalEdge.EdgeId, updatedEdge.EdgeId);
        Assert.Equal(originalEdge.CenterId, updatedEdge.CenterId);
        Assert.Equal(originalEdge.SubjectId, updatedEdge.SubjectId);
        Assert.Equal(originalEdge.SourceNodeId, updatedEdge.SourceNodeId);
        Assert.Equal(originalEdge.TargetNodeId, updatedEdge.TargetNodeId);
        Assert.Equal(originalEdge.RelationType, updatedEdge.RelationType);
        Assert.Equal(originalEdge.CreatedAt, updatedEdge.CreatedAt);
        Assert.Equal(originalEdge.CreatedBy, updatedEdge.CreatedBy);
        Assert.Equal(originalEdge.IsDeleted, updatedEdge.IsDeleted);
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

        var useCase = new UpdateKnowledgeEdgeUseCase(dbContext, _tenantContextMock.Object, _timeProviderMock.Object);
        var request = new UpdateKnowledgeEdgeRequest { Weight = 0.5m, RowVersion = "1" };

        var result = await useCase.ExecuteAsync("1", request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Student")]
    [InlineData("teacher")]
    [InlineData("CENTERMANAGER")]
    public async Task ExecuteAsync_InvalidRoleMatrix_ReturnsResourceNotFound(string? role)
    {
        var dbName = Guid.NewGuid().ToString();
        using var dbContext = CreateDbContext(dbName);

        _tenantContextMock.Setup(x => x.Role).Returns(role!);

        var useCase = new UpdateKnowledgeEdgeUseCase(dbContext, _tenantContextMock.Object, _timeProviderMock.Object);
        var request = new UpdateKnowledgeEdgeRequest { Weight = 0.5m, RowVersion = "1" };

        var result = await useCase.ExecuteAsync("1", request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" 1")]
    [InlineData("1 ")]
    [InlineData("+1")]
    [InlineData("-1")]
    [InlineData("1.0")]
    [InlineData("0")]
    [InlineData("18446744073709551616")]
    [InlineData("abc")]
    public async Task ExecuteAsync_InvalidEdgeIdRawMatrix_ReturnsValidationFailed(string? edgeId)
    {
        var dbName = Guid.NewGuid().ToString();
        using var dbContext = CreateDbContext(dbName);

        var useCase = new UpdateKnowledgeEdgeUseCase(dbContext, _tenantContextMock.Object, _timeProviderMock.Object);
        var request = new UpdateKnowledgeEdgeRequest { Weight = 0.5m, RowVersion = "1" };

        var result = await useCase.ExecuteAsync(edgeId!, request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_InvalidRequest_RejectedBeforeDbAccess()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateDbContext(dbName);
        dbContext.Dispose();

        var useCase = new UpdateKnowledgeEdgeUseCase(dbContext, _tenantContextMock.Object, _timeProviderMock.Object);

        var result = await useCase.ExecuteAsync("1", new UpdateKnowledgeEdgeRequest { Weight = -0.1m, RowVersion = "1" });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_NullRequest_ReturnsValidationFailed()
    {
        var dbName = Guid.NewGuid().ToString();
        var dbContext = CreateDbContext(dbName);
        dbContext.Dispose();

        var useCase = new UpdateKnowledgeEdgeUseCase(dbContext, _tenantContextMock.Object, _timeProviderMock.Object);

        var result = await useCase.ExecuteAsync("1", null!);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_InactiveCenter_ReturnsResourceNotFound()
    {
        var dbName = Guid.NewGuid().ToString();
        using var dbContext = CreateDbContext(dbName);
        await SeedBaseDataAsync(dbContext);

        var center = await dbContext.Centers.FirstAsync(c => c.CenterId == _centerId);
        center.Status = CenterStatus.Suspended;
        await dbContext.SaveChangesAsync();

        var useCase = new UpdateKnowledgeEdgeUseCase(dbContext, _tenantContextMock.Object, _timeProviderMock.Object);
        var request = new UpdateKnowledgeEdgeRequest { Weight = 0.9m, RowVersion = "1" };

        var result = await useCase.ExecuteAsync("1", request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);

        var dbEdge = await dbContext.KnowledgeEdges.FirstAsync(e => e.EdgeId == 1);
        Assert.Equal(0.5m, dbEdge.Weight);
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

        var useCase = new UpdateKnowledgeEdgeUseCase(dbContext, _tenantContextMock.Object, _timeProviderMock.Object);
        var request = new UpdateKnowledgeEdgeRequest { Weight = 0.9m, RowVersion = "1" };

        var result = await useCase.ExecuteAsync("1", request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);

        var dbEdge = await dbContext.KnowledgeEdges.FirstAsync(e => e.EdgeId == 1);
        Assert.Equal(0.5m, dbEdge.Weight);
    }

    [Fact]
    public async Task ExecuteAsync_MissingCenter_ReturnsResourceNotFound()
    {
        var dbName = Guid.NewGuid().ToString();
        using var dbContext = CreateDbContext(dbName);

        var useCase = new UpdateKnowledgeEdgeUseCase(dbContext, _tenantContextMock.Object, _timeProviderMock.Object);
        var request = new UpdateKnowledgeEdgeRequest { Weight = 0.5m, RowVersion = "1" };

        var result = await useCase.ExecuteAsync("1", request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_MissingEdge_ReturnsResourceNotFound()
    {
        var dbName = Guid.NewGuid().ToString();
        using var dbContext = CreateDbContext(dbName);
        await SeedBaseDataAsync(dbContext);

        var useCase = new UpdateKnowledgeEdgeUseCase(dbContext, _tenantContextMock.Object, _timeProviderMock.Object);
        var request = new UpdateKnowledgeEdgeRequest { Weight = 0.9m, RowVersion = "1" };

        var result = await useCase.ExecuteAsync("9999", request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);

        var dbEdge = await dbContext.KnowledgeEdges.FirstAsync(e => e.EdgeId == 1);
        Assert.Equal(0.5m, dbEdge.Weight);
    }

    [Fact]
    public async Task ExecuteAsync_DeletedEdge_ReturnsResourceNotFound()
    {
        var dbName = Guid.NewGuid().ToString();
        using var dbContext = CreateDbContext(dbName);
        await SeedBaseDataAsync(dbContext);

        var edge = await dbContext.KnowledgeEdges.FirstAsync(e => e.EdgeId == 1);
        edge.IsDeleted = true;
        await dbContext.SaveChangesAsync();

        var useCase = new UpdateKnowledgeEdgeUseCase(dbContext, _tenantContextMock.Object, _timeProviderMock.Object);
        var request = new UpdateKnowledgeEdgeRequest { Weight = 0.5m, RowVersion = "1" };

        var result = await useCase.ExecuteAsync("1", request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);

        var dbEdge = await dbContext.KnowledgeEdges.IgnoreQueryFilters().FirstAsync(e => e.EdgeId == 1);
        Assert.True(dbEdge.IsDeleted);
        Assert.Equal(0.5m, dbEdge.Weight);
        Assert.Equal(2uL, dbEdge.RowVersion);
    }

    [Fact]
    public async Task ExecuteAsync_CrossTenantEdge_ReturnsResourceNotFound()
    {
        var dbName = Guid.NewGuid().ToString();
        using var dbContext = CreateDbContext(dbName);
        await SeedBaseDataAsync(dbContext);

        _tenantContextMock.Setup(x => x.CenterId).Returns(Guid.NewGuid());

        var useCase = new UpdateKnowledgeEdgeUseCase(dbContext, _tenantContextMock.Object, _timeProviderMock.Object);
        var request = new UpdateKnowledgeEdgeRequest { Weight = 0.5m, RowVersion = "1" };

        var result = await useCase.ExecuteAsync("1", request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_StaleRowVersion_ReturnsConcurrencyConflictAndNoPersistence()
    {
        var dbName = Guid.NewGuid().ToString();
        using var dbContext = CreateDbContext(dbName);
        await SeedBaseDataAsync(dbContext, edgeId: 1, rowVersion: 1);

        var dbEdgeBefore = await dbContext.KnowledgeEdges.FirstAsync(e => e.EdgeId == 1);
        dbEdgeBefore.RowVersion = 2;
        await dbContext.SaveChangesAsync();

        var useCase = new UpdateKnowledgeEdgeUseCase(dbContext, _tenantContextMock.Object, _timeProviderMock.Object);
        var request = new UpdateKnowledgeEdgeRequest { Weight = 0.9m, RowVersion = "1" };

        var result = await useCase.ExecuteAsync("1", request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, result.ErrorCode);

        var dbEdge = await dbContext.KnowledgeEdges.FirstAsync(e => e.EdgeId == 1);
        Assert.Equal(0.5m, dbEdge.Weight);
        Assert.Equal(2uL, dbEdge.RowVersion);
    }

    [Fact]
    public async Task ExecuteAsync_DbUpdateConcurrencyException_ReturnsConcurrencyConflictAndClearsChangeTracker()
    {
        var dbName = Guid.NewGuid().ToString();
        var ex = new DbUpdateConcurrencyException("Concurrency conflict");
        using var dbContext = (FaultyDbContext)CreateDbContext(dbName, exceptionToThrow: ex);
        await SeedBaseDataAsync(dbContext);

        dbContext.StartThrowing();

        var useCase = new UpdateKnowledgeEdgeUseCase(dbContext, _tenantContextMock.Object, _timeProviderMock.Object);
        var request = new UpdateKnowledgeEdgeRequest { Weight = 0.9m, RowVersion = "1" };

        var result = await useCase.ExecuteAsync("1", request);

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

        var useCase = new UpdateKnowledgeEdgeUseCase(dbContext, _tenantContextMock.Object, _timeProviderMock.Object);
        var request = new UpdateKnowledgeEdgeRequest { Weight = 0.9m, RowVersion = "1" };

        await Assert.ThrowsAsync<DbUpdateException>(() => useCase.ExecuteAsync("1", request));
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

        var useCase = new UpdateKnowledgeEdgeUseCase(dbContext, _tenantContextMock.Object, _timeProviderMock.Object);
        var request = new UpdateKnowledgeEdgeRequest { Weight = 0.9m, RowVersion = "1" };

        var result = await useCase.ExecuteAsync("1", request, token);

        Assert.True(result.IsSuccess);
        Assert.Equal(token, dbContext.CapturedSaveToken);
    }

    [Fact]
    public void Production_Source_Contains_No_IgnoreQueryFilters()
    {
        var sourcePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "..", "src", "EduTwin.BLL", "KnowledgeGraph", "UpdateKnowledgeEdgeUseCase.cs");
        var fullPath = Path.GetFullPath(sourcePath);

        Assert.True(File.Exists(fullPath), $"Source file does not exist at expected path: {fullPath}");

        var content = File.ReadAllText(fullPath);
        Assert.DoesNotContain("IgnoreQueryFilters", content);
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
