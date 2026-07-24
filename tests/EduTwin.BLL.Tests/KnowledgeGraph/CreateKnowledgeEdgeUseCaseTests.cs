using System;
using System.Collections.Generic;
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
using EduTwin.Contracts.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Organization;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.KnowledgeGraph;

namespace EduTwin.BLL.Tests.KnowledgeGraph;

public class CreateKnowledgeEdgeUseCaseTests
{
    private readonly Mock<ITenantContext> _tenantContextMock;
    private readonly Mock<ITenantIdAccessor> _tenantIdAccessorMock;
    private readonly Mock<TimeProvider> _timeProviderMock;
    private readonly KnowledgeGraphValidator _validator;

    public CreateKnowledgeEdgeUseCaseTests()
    {
        _tenantContextMock = new Mock<ITenantContext>();
        _tenantIdAccessorMock = new Mock<ITenantIdAccessor>();
        _timeProviderMock = new Mock<TimeProvider>();
        _timeProviderMock.Setup(x => x.GetUtcNow()).Returns(DateTimeOffset.UtcNow);
        _validator = new KnowledgeGraphValidator();
    }

    private EduTwinDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new EduTwinDbContext(options, _tenantIdAccessorMock.Object);
    }

    private void SetupValidTenant()
    {
        var centerId = Guid.NewGuid();
        _tenantContextMock.Setup(x => x.IsResolved).Returns(true);
        _tenantContextMock.Setup(x => x.CenterId).Returns(centerId);
        _tenantContextMock.Setup(x => x.UserId).Returns(Guid.NewGuid());
        _tenantContextMock.Setup(x => x.Role).Returns("Teacher");

        _tenantIdAccessorMock.Setup(x => x.CenterId).Returns(centerId);
    }

    private async Task SeedValidDataAsync(EduTwinDbContext context, Guid centerId, Guid subjectId, ulong sourceNodeId, ulong targetNodeId)
    {
        context.Centers.Add(new Center { CenterCode = "C1", CenterName = "C1", Timezone = "UTC", CenterId = centerId, Status = CenterStatus.Active, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        context.Subjects.Add(new Subject { SubjectCode = "S1", SubjectName = "S1", CenterId = centerId, SubjectId = subjectId, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        context.KnowledgeNodes.Add(new KnowledgeNode { NodeCode = "N1", NodeName = "N1", CenterId = centerId, SubjectId = subjectId, NodeId = sourceNodeId, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        context.KnowledgeNodes.Add(new KnowledgeNode { NodeCode = "N1", NodeName = "N1", CenterId = centerId, SubjectId = subjectId, NodeId = targetNodeId, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();
    }

    private CreateKnowledgeEdgeRequest CreateValidRequest(Guid subjectId, ulong sourceNodeId, ulong targetNodeId, string relationType = "PrerequisiteOf")
    {
        return new CreateKnowledgeEdgeRequest
        {
            SubjectId = subjectId,
            SourceNodeId = sourceNodeId.ToString(CultureInfo.InvariantCulture),
            TargetNodeId = targetNodeId.ToString(CultureInfo.InvariantCulture),
            RelationType = relationType,
            Weight = 1.0m
        };
    }

    [Fact]
    public async Task Teacher_Creates_Edge_Successfully()
    {
        SetupValidTenant();
        _tenantContextMock.Setup(x => x.Role).Returns("Teacher");
        using var context = CreateDbContext();
        var sut = new CreateKnowledgeEdgeUseCase(context, _tenantContextMock.Object, _timeProviderMock.Object, _validator);

        var subjectId = Guid.NewGuid();
        await SeedValidDataAsync(context, _tenantContextMock.Object.CenterId!.Value, subjectId, 100, 101);
        var request = CreateValidRequest(subjectId, 100, 101);

        var result = await sut.ExecuteAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        var edgeInDb = await context.KnowledgeEdges.SingleOrDefaultAsync();
        Assert.NotNull(edgeInDb);
    }

    [Fact]
    public async Task CenterManager_Creates_Edge_Successfully()
    {
        SetupValidTenant();
        _tenantContextMock.Setup(x => x.Role).Returns("CenterManager");
        using var context = CreateDbContext();
        var sut = new CreateKnowledgeEdgeUseCase(context, _tenantContextMock.Object, _timeProviderMock.Object, _validator);

        var subjectId = Guid.NewGuid();
        await SeedValidDataAsync(context, _tenantContextMock.Object.CenterId!.Value, subjectId, 100, 101);
        var request = CreateValidRequest(subjectId, 100, 101);

        var result = await sut.ExecuteAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
    }

    [Fact]
    public async Task Audit_Fields_And_RowVersion_Exact()
    {
        SetupValidTenant();
        var now = new DateTime(2026, 7, 23, 10, 0, 0, DateTimeKind.Utc);
        _timeProviderMock.Setup(x => x.GetUtcNow()).Returns(new DateTimeOffset(now));
        using var context = CreateDbContext();
        var sut = new CreateKnowledgeEdgeUseCase(context, _tenantContextMock.Object, _timeProviderMock.Object, _validator);

        var subjectId = Guid.NewGuid();
        await SeedValidDataAsync(context, _tenantContextMock.Object.CenterId!.Value, subjectId, 100, 101);
        var request = CreateValidRequest(subjectId, 100, 101);

        await sut.ExecuteAsync(request, CancellationToken.None);

        var edgeInDb = await context.KnowledgeEdges.SingleAsync();
        Assert.Equal(now, edgeInDb.CreatedAt);
        Assert.Equal(now, edgeInDb.UpdatedAt);
        Assert.Equal(_tenantContextMock.Object.UserId!.Value, edgeInDb.CreatedBy);
        Assert.Equal(_tenantContextMock.Object.UserId!.Value, edgeInDb.UpdatedBy);
        Assert.False(edgeInDb.IsDeleted);
        Assert.Equal(1UL, edgeInDb.RowVersion);
        Assert.Equal(1.0m, edgeInDb.Weight);
    }

    [Fact]
    public async Task DTO_Serialization_Exact_Invariant()
    {
        SetupValidTenant();
        using var context = CreateDbContext();
        var sut = new CreateKnowledgeEdgeUseCase(context, _tenantContextMock.Object, _timeProviderMock.Object, _validator);

        var subjectId = Guid.NewGuid();
        await SeedValidDataAsync(context, _tenantContextMock.Object.CenterId!.Value, subjectId, 100, 101);
        var request = CreateValidRequest(subjectId, 100, 101);

        var result = await sut.ExecuteAsync(request, CancellationToken.None);

        var edgeInDb = await context.KnowledgeEdges.SingleAsync();

        Assert.Equal(edgeInDb.EdgeId.ToString(CultureInfo.InvariantCulture), result.Data!.EdgeId);
        Assert.Equal(subjectId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(), result.Data.SubjectId);
        Assert.Equal("100", result.Data.SourceNodeId);
        Assert.Equal("101", result.Data.TargetNodeId);
        Assert.Equal("PrerequisiteOf", result.Data.RelationType);
        Assert.Equal("1", result.Data.RowVersion);
        Assert.Equal(1.0m, result.Data.Weight);
    }

    [Fact]
    public async Task Invalid_Unresolved_Tenant_Fail_Closed()
    {
        SetupValidTenant();
        _tenantContextMock.Setup(x => x.IsResolved).Returns(false);
        using var context = CreateDbContext();
        var sut = new CreateKnowledgeEdgeUseCase(context, _tenantContextMock.Object, _timeProviderMock.Object, _validator);

        var result = await sut.ExecuteAsync(CreateValidRequest(Guid.NewGuid(), 100, 101), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData("00000000-0000-0000-0000-000000000000", "d6b5e024-5d51-419b-ab66-b2569ef81622")]
    [InlineData("d6b5e024-5d51-419b-ab66-b2569ef81622", "00000000-0000-0000-0000-000000000000")]
    public async Task Missing_Empty_CenterId_UserId(string centerIdStr, string userIdStr)
    {
        SetupValidTenant();
        var centerId = Guid.Parse(centerIdStr);
        _tenantContextMock.Setup(x => x.CenterId).Returns(centerId);
        _tenantIdAccessorMock.Setup(x => x.CenterId).Returns(centerId);
        _tenantContextMock.Setup(x => x.UserId).Returns(Guid.Parse(userIdStr));
        using var context = CreateDbContext();
        var sut = new CreateKnowledgeEdgeUseCase(context, _tenantContextMock.Object, _timeProviderMock.Object, _validator);

        var result = await sut.ExecuteAsync(CreateValidRequest(Guid.NewGuid(), 100, 101), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("Admin")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("teacher")]
    public async Task Invalid_Role_Matrix(string? role)
    {
        SetupValidTenant();
        _tenantContextMock.Setup(x => x.Role).Returns(role);
        using var context = CreateDbContext();
        var sut = new CreateKnowledgeEdgeUseCase(context, _tenantContextMock.Object, _timeProviderMock.Object, _validator);

        var result = await sut.ExecuteAsync(CreateValidRequest(Guid.NewGuid(), 100, 101), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Raw_Request_Validation_Occurs_Before_DB_Queries()
    {
        SetupValidTenant();
        var mockContext = new Mock<EduTwinDbContext>(new DbContextOptions<EduTwinDbContext>());
        var sut = new CreateKnowledgeEdgeUseCase(mockContext.Object, _tenantContextMock.Object, _timeProviderMock.Object, _validator);

        var request = CreateValidRequest(Guid.Empty, 100, 101); // Invalid subject ID

        var result = await sut.ExecuteAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        // We know it didn't hit DB if it doesn't throw about Mock DbContext not set up.
    }

    [Fact]
    public async Task Missing_Inactive_Deleted_Center()
    {
        SetupValidTenant();
        using var context = CreateDbContext();
        var sut = new CreateKnowledgeEdgeUseCase(context, _tenantContextMock.Object, _timeProviderMock.Object, _validator);

        var subjectId = Guid.NewGuid();
        context.Centers.Add(new Center { CenterCode = "C1", CenterName = "C1", Timezone = "UTC", CenterId = _tenantContextMock.Object.CenterId!.Value, Status = CenterStatus.Suspended, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        context.Subjects.Add(new Subject { SubjectCode = "S1", SubjectName = "S1", CenterId = _tenantContextMock.Object.CenterId!.Value, SubjectId = subjectId, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        context.KnowledgeNodes.Add(new KnowledgeNode { NodeCode = "N1", NodeName = "N1", CenterId = _tenantContextMock.Object.CenterId!.Value, SubjectId = subjectId, NodeId = 100, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        context.KnowledgeNodes.Add(new KnowledgeNode { NodeCode = "N1", NodeName = "N1", CenterId = _tenantContextMock.Object.CenterId!.Value, SubjectId = subjectId, NodeId = 101, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var request = CreateValidRequest(subjectId, 100, 101);
        var result = await sut.ExecuteAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Missing_Deleted_Cross_Tenant_Subject()
    {
        SetupValidTenant();
        using var context = CreateDbContext();
        var sut = new CreateKnowledgeEdgeUseCase(context, _tenantContextMock.Object, _timeProviderMock.Object, _validator);

        var subjectId = Guid.NewGuid();
        context.Centers.Add(new Center { CenterCode = "C1", CenterName = "C1", Timezone = "UTC", CenterId = _tenantContextMock.Object.CenterId!.Value, Status = CenterStatus.Active, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        context.Subjects.Add(new Subject { SubjectCode = "S1", SubjectName = "S1", CenterId = Guid.NewGuid(), SubjectId = subjectId, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }); // Cross tenant
        context.KnowledgeNodes.Add(new KnowledgeNode { NodeCode = "N1", NodeName = "N1", CenterId = _tenantContextMock.Object.CenterId!.Value, SubjectId = subjectId, NodeId = 100, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        context.KnowledgeNodes.Add(new KnowledgeNode { NodeCode = "N1", NodeName = "N1", CenterId = _tenantContextMock.Object.CenterId!.Value, SubjectId = subjectId, NodeId = 101, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var request = CreateValidRequest(subjectId, 100, 101);
        var result = await sut.ExecuteAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Missing_Deleted_Cross_Tenant_Source_Node()
    {
        SetupValidTenant();
        using var context = CreateDbContext();
        var sut = new CreateKnowledgeEdgeUseCase(context, _tenantContextMock.Object, _timeProviderMock.Object, _validator);

        var subjectId = Guid.NewGuid();
        context.Centers.Add(new Center { CenterCode = "C1", CenterName = "C1", Timezone = "UTC", CenterId = _tenantContextMock.Object.CenterId!.Value, Status = CenterStatus.Active, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        context.Subjects.Add(new Subject { SubjectCode = "S1", SubjectName = "S1", CenterId = _tenantContextMock.Object.CenterId!.Value, SubjectId = subjectId, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        context.KnowledgeNodes.Add(new KnowledgeNode { NodeCode = "N1", NodeName = "N1", CenterId = _tenantContextMock.Object.CenterId!.Value, SubjectId = subjectId, NodeId = 100, IsDeleted = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }); // deleted
        context.KnowledgeNodes.Add(new KnowledgeNode { NodeCode = "N1", NodeName = "N1", CenterId = _tenantContextMock.Object.CenterId!.Value, SubjectId = subjectId, NodeId = 101, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var request = CreateValidRequest(subjectId, 100, 101);
        var result = await sut.ExecuteAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Missing_Deleted_Cross_Tenant_Target_Node()
    {
        SetupValidTenant();
        using var context = CreateDbContext();
        var sut = new CreateKnowledgeEdgeUseCase(context, _tenantContextMock.Object, _timeProviderMock.Object, _validator);

        var subjectId = Guid.NewGuid();
        context.Centers.Add(new Center { CenterCode = "C1", CenterName = "C1", Timezone = "UTC", CenterId = _tenantContextMock.Object.CenterId!.Value, Status = CenterStatus.Active, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        context.Subjects.Add(new Subject { SubjectCode = "S1", SubjectName = "S1", CenterId = _tenantContextMock.Object.CenterId!.Value, SubjectId = subjectId, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        context.KnowledgeNodes.Add(new KnowledgeNode { NodeCode = "N1", NodeName = "N1", CenterId = _tenantContextMock.Object.CenterId!.Value, SubjectId = subjectId, NodeId = 100, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        context.KnowledgeNodes.Add(new KnowledgeNode { NodeCode = "N1", NodeName = "N1", CenterId = Guid.NewGuid(), SubjectId = subjectId, NodeId = 101, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }); // cross tenant
        await context.SaveChangesAsync();

        var request = CreateValidRequest(subjectId, 100, 101);
        var result = await sut.ExecuteAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Source_Or_Target_Belonging_To_Another_Subject_Returns_RESOURCE_NOT_FOUND()
    {
        SetupValidTenant();
        using var context = CreateDbContext();
        var sut = new CreateKnowledgeEdgeUseCase(context, _tenantContextMock.Object, _timeProviderMock.Object, _validator);

        var subjectId = Guid.NewGuid();
        var otherSubjectId = Guid.NewGuid();
        context.Centers.Add(new Center { CenterCode = "C1", CenterName = "C1", Timezone = "UTC", CenterId = _tenantContextMock.Object.CenterId!.Value, Status = CenterStatus.Active, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        context.Subjects.Add(new Subject { SubjectCode = "S1", SubjectName = "S1", CenterId = _tenantContextMock.Object.CenterId!.Value, SubjectId = subjectId, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        context.KnowledgeNodes.Add(new KnowledgeNode { NodeCode = "N1", NodeName = "N1", CenterId = _tenantContextMock.Object.CenterId!.Value, SubjectId = subjectId, NodeId = 100, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        context.KnowledgeNodes.Add(new KnowledgeNode { NodeCode = "N1", NodeName = "N1", CenterId = _tenantContextMock.Object.CenterId!.Value, SubjectId = otherSubjectId, NodeId = 101, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }); // other subject
        await context.SaveChangesAsync();

        var request = CreateValidRequest(subjectId, 100, 101);
        var result = await sut.ExecuteAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Existing_Duplicate_Returns_DUPLICATE_RESOURCE()
    {
        SetupValidTenant();
        using var context = CreateDbContext();
        var sut = new CreateKnowledgeEdgeUseCase(context, _tenantContextMock.Object, _timeProviderMock.Object, _validator);

        var subjectId = Guid.NewGuid();
        await SeedValidDataAsync(context, _tenantContextMock.Object.CenterId!.Value, subjectId, 100, 101);

        context.KnowledgeEdges.Add(new KnowledgeEdge
        {
            CenterId = _tenantContextMock.Object.CenterId!.Value,
            SubjectId = subjectId,
            SourceNodeId = 100,
            TargetNodeId = 101,
            RelationType = EduTwin.Contracts.KnowledgeGraph.RelationType.PrerequisiteOf,
            IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var request = CreateValidRequest(subjectId, 100, 101);
        var result = await sut.ExecuteAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.DuplicateResource, result.ErrorCode);
        Assert.Empty(context.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).ToList());
    }

    [Fact]
    public async Task Reverse_Edge_Is_Not_Automatically_Duplicate()
    {
        SetupValidTenant();
        using var context = CreateDbContext();
        var sut = new CreateKnowledgeEdgeUseCase(context, _tenantContextMock.Object, _timeProviderMock.Object, _validator);

        var subjectId = Guid.NewGuid();
        await SeedValidDataAsync(context, _tenantContextMock.Object.CenterId!.Value, subjectId, 100, 101);

        context.KnowledgeEdges.Add(new KnowledgeEdge
        {
            CenterId = _tenantContextMock.Object.CenterId!.Value,
            SubjectId = subjectId,
            SourceNodeId = 100, // 100 -> 101
            TargetNodeId = 101,
            RelationType = EduTwin.Contracts.KnowledgeGraph.RelationType.RelatedTo, // RelatedTo allows cycles
            IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var request = CreateValidRequest(subjectId, 101, 100, "RelatedTo"); // 101 -> 100
        var result = await sut.ExecuteAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task PrerequisiteOf_Direct_Cycle_Rejected()
    {
        SetupValidTenant();
        using var context = CreateDbContext();
        var sut = new CreateKnowledgeEdgeUseCase(context, _tenantContextMock.Object, _timeProviderMock.Object, _validator);

        var subjectId = Guid.NewGuid();
        await SeedValidDataAsync(context, _tenantContextMock.Object.CenterId!.Value, subjectId, 100, 101);

        context.KnowledgeEdges.Add(new KnowledgeEdge
        {
            CenterId = _tenantContextMock.Object.CenterId!.Value,
            SubjectId = subjectId,
            SourceNodeId = 101, // 101 -> 100
            TargetNodeId = 100,
            RelationType = EduTwin.Contracts.KnowledgeGraph.RelationType.PrerequisiteOf,
            IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var request = CreateValidRequest(subjectId, 100, 101, "PrerequisiteOf"); // 100 -> 101 creates cycle
        var result = await sut.ExecuteAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.DagCycleDetected, result.ErrorCode);
    }

    [Fact]
    public async Task PrerequisiteOf_Transitive_Cycle_Rejected()
    {
        SetupValidTenant();
        using var context = CreateDbContext();
        var sut = new CreateKnowledgeEdgeUseCase(context, _tenantContextMock.Object, _timeProviderMock.Object, _validator);

        var subjectId = Guid.NewGuid();
        context.Centers.Add(new Center { CenterCode = "C1", CenterName = "C1", Timezone = "UTC", CenterId = _tenantContextMock.Object.CenterId!.Value, Status = CenterStatus.Active, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        context.Subjects.Add(new Subject { SubjectCode = "S1", SubjectName = "S1", CenterId = _tenantContextMock.Object.CenterId!.Value, SubjectId = subjectId, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        context.KnowledgeNodes.Add(new KnowledgeNode { NodeCode = "N1", NodeName = "N1", CenterId = _tenantContextMock.Object.CenterId!.Value, SubjectId = subjectId, NodeId = 100, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        context.KnowledgeNodes.Add(new KnowledgeNode { NodeCode = "N1", NodeName = "N1", CenterId = _tenantContextMock.Object.CenterId!.Value, SubjectId = subjectId, NodeId = 101, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        context.KnowledgeNodes.Add(new KnowledgeNode { NodeCode = "N1", NodeName = "N1", CenterId = _tenantContextMock.Object.CenterId!.Value, SubjectId = subjectId, NodeId = 102, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });

        context.KnowledgeEdges.Add(new KnowledgeEdge { CenterId = _tenantContextMock.Object.CenterId!.Value, SubjectId = subjectId, SourceNodeId = 101, TargetNodeId = 102, RelationType = EduTwin.Contracts.KnowledgeGraph.RelationType.PrerequisiteOf, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        context.KnowledgeEdges.Add(new KnowledgeEdge { CenterId = _tenantContextMock.Object.CenterId!.Value, SubjectId = subjectId, SourceNodeId = 102, TargetNodeId = 100, RelationType = EduTwin.Contracts.KnowledgeGraph.RelationType.PrerequisiteOf, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        // Adding 100 -> 101 creates 100 -> 101 -> 102 -> 100
        var request = CreateValidRequest(subjectId, 100, 101, "PrerequisiteOf");
        var result = await sut.ExecuteAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.DagCycleDetected, result.ErrorCode);
    }

    [Fact]
    public async Task PrerequisiteOf_A_To_B_To_C_Then_C_To_A_ReturnsDagCycleDetected()
    {
        SetupValidTenant();
        using var context = CreateDbContext();
        var sut = new CreateKnowledgeEdgeUseCase(context, _tenantContextMock.Object, _timeProviderMock.Object, _validator);

        var subjectId = Guid.NewGuid();
        context.Centers.Add(new Center { CenterCode = "C1", CenterName = "C1", Timezone = "UTC", CenterId = _tenantContextMock.Object.CenterId!.Value, Status = CenterStatus.Active, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        context.Subjects.Add(new Subject { SubjectCode = "S1", SubjectName = "S1", CenterId = _tenantContextMock.Object.CenterId!.Value, SubjectId = subjectId, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        context.KnowledgeNodes.Add(new KnowledgeNode { NodeCode = "A", NodeName = "Node A", CenterId = _tenantContextMock.Object.CenterId!.Value, SubjectId = subjectId, NodeId = 100, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        context.KnowledgeNodes.Add(new KnowledgeNode { NodeCode = "B", NodeName = "Node B", CenterId = _tenantContextMock.Object.CenterId!.Value, SubjectId = subjectId, NodeId = 101, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        context.KnowledgeNodes.Add(new KnowledgeNode { NodeCode = "C", NodeName = "Node C", CenterId = _tenantContextMock.Object.CenterId!.Value, SubjectId = subjectId, NodeId = 102, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });

        // Seed A -> B (100 -> 101) and B -> C (101 -> 102)
        context.KnowledgeEdges.Add(new KnowledgeEdge { CenterId = _tenantContextMock.Object.CenterId!.Value, SubjectId = subjectId, SourceNodeId = 100, TargetNodeId = 101, RelationType = EduTwin.Contracts.KnowledgeGraph.RelationType.PrerequisiteOf, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        context.KnowledgeEdges.Add(new KnowledgeEdge { CenterId = _tenantContextMock.Object.CenterId!.Value, SubjectId = subjectId, SourceNodeId = 101, TargetNodeId = 102, RelationType = EduTwin.Contracts.KnowledgeGraph.RelationType.PrerequisiteOf, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        // Attempting to create C -> A (102 -> 100) creates cycle A -> B -> C -> A
        var request = CreateValidRequest(subjectId, 102, 100, "PrerequisiteOf");
        var result = await sut.ExecuteAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.DagCycleDetected, result.ErrorCode);

        var persistedEdges = await context.KnowledgeEdges
            .Where(e => !e.IsDeleted)
            .ToListAsync();

        Assert.Equal(2, persistedEdges.Count);
        Assert.DoesNotContain(
            persistedEdges,
            e => e.SourceNodeId == 102 && e.TargetNodeId == 100);

        Assert.Empty(context.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).ToList());
    }

    [Fact]
    public async Task PartOf_Direct_Transitive_Cycle_Rejected()
    {
        SetupValidTenant();
        using var context = CreateDbContext();
        var sut = new CreateKnowledgeEdgeUseCase(context, _tenantContextMock.Object, _timeProviderMock.Object, _validator);

        var subjectId = Guid.NewGuid();
        context.Centers.Add(new Center { CenterCode = "C1", CenterName = "C1", Timezone = "UTC", CenterId = _tenantContextMock.Object.CenterId!.Value, Status = CenterStatus.Active, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        context.Subjects.Add(new Subject { SubjectCode = "S1", SubjectName = "S1", CenterId = _tenantContextMock.Object.CenterId!.Value, SubjectId = subjectId, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        context.KnowledgeNodes.Add(new KnowledgeNode { NodeCode = "N1", NodeName = "N1", CenterId = _tenantContextMock.Object.CenterId!.Value, SubjectId = subjectId, NodeId = 100, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        context.KnowledgeNodes.Add(new KnowledgeNode { NodeCode = "N1", NodeName = "N1", CenterId = _tenantContextMock.Object.CenterId!.Value, SubjectId = subjectId, NodeId = 101, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        context.KnowledgeNodes.Add(new KnowledgeNode { NodeCode = "N1", NodeName = "N1", CenterId = _tenantContextMock.Object.CenterId!.Value, SubjectId = subjectId, NodeId = 102, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });

        context.KnowledgeEdges.Add(new KnowledgeEdge { CenterId = _tenantContextMock.Object.CenterId!.Value, SubjectId = subjectId, SourceNodeId = 101, TargetNodeId = 102, RelationType = EduTwin.Contracts.KnowledgeGraph.RelationType.PartOf, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        context.KnowledgeEdges.Add(new KnowledgeEdge { CenterId = _tenantContextMock.Object.CenterId!.Value, SubjectId = subjectId, SourceNodeId = 102, TargetNodeId = 100, RelationType = EduTwin.Contracts.KnowledgeGraph.RelationType.PartOf, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var request = CreateValidRequest(subjectId, 100, 101, "PartOf");
        var result = await sut.ExecuteAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.DagCycleDetected, result.ErrorCode);
    }

    [Fact]
    public async Task RelatedTo_Reverse_Cycle_Like_Edges_Allowed()
    {
        SetupValidTenant();
        using var context = CreateDbContext();
        var sut = new CreateKnowledgeEdgeUseCase(context, _tenantContextMock.Object, _timeProviderMock.Object, _validator);

        var subjectId = Guid.NewGuid();
        await SeedValidDataAsync(context, _tenantContextMock.Object.CenterId!.Value, subjectId, 100, 101);

        context.KnowledgeEdges.Add(new KnowledgeEdge { CenterId = _tenantContextMock.Object.CenterId!.Value, SubjectId = subjectId, SourceNodeId = 101, TargetNodeId = 100, RelationType = EduTwin.Contracts.KnowledgeGraph.RelationType.RelatedTo, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var request = CreateValidRequest(subjectId, 100, 101, "RelatedTo");
        var result = await sut.ExecuteAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task CausesErrorIn_Reverse_Cycle_Like_Edges_Allowed()
    {
        SetupValidTenant();
        using var context = CreateDbContext();
        var sut = new CreateKnowledgeEdgeUseCase(context, _tenantContextMock.Object, _timeProviderMock.Object, _validator);

        var subjectId = Guid.NewGuid();
        await SeedValidDataAsync(context, _tenantContextMock.Object.CenterId!.Value, subjectId, 100, 101);

        context.KnowledgeEdges.Add(new KnowledgeEdge { CenterId = _tenantContextMock.Object.CenterId!.Value, SubjectId = subjectId, SourceNodeId = 101, TargetNodeId = 100, RelationType = EduTwin.Contracts.KnowledgeGraph.RelationType.CausesErrorIn, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var request = CreateValidRequest(subjectId, 100, 101, "CausesErrorIn");
        var result = await sut.ExecuteAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Deleted_Edges_Excluded_From_Cycle_Calculation()
    {
        SetupValidTenant();
        using var context = CreateDbContext();
        var sut = new CreateKnowledgeEdgeUseCase(context, _tenantContextMock.Object, _timeProviderMock.Object, _validator);

        var subjectId = Guid.NewGuid();
        await SeedValidDataAsync(context, _tenantContextMock.Object.CenterId!.Value, subjectId, 100, 101);

        context.KnowledgeEdges.Add(new KnowledgeEdge
        {
            CenterId = _tenantContextMock.Object.CenterId!.Value,
            SubjectId = subjectId,
            SourceNodeId = 101,
            TargetNodeId = 100,
            RelationType = EduTwin.Contracts.KnowledgeGraph.RelationType.PrerequisiteOf,
            IsDeleted = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow // Deleted!
        });
        await context.SaveChangesAsync();

        var request = CreateValidRequest(subjectId, 100, 101, "PrerequisiteOf");
        var result = await sut.ExecuteAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess); // Cycle is broken because edge is deleted
    }

    [Fact]
    public async Task Cross_Tenant_And_Cross_Subject_Edges_Excluded()
    {
        SetupValidTenant();
        using var context = CreateDbContext();
        var sut = new CreateKnowledgeEdgeUseCase(context, _tenantContextMock.Object, _timeProviderMock.Object, _validator);

        var subjectId = Guid.NewGuid();
        await SeedValidDataAsync(context, _tenantContextMock.Object.CenterId!.Value, subjectId, 100, 101);

        context.KnowledgeEdges.Add(new KnowledgeEdge
        {
            CenterId = Guid.NewGuid(), // Cross tenant
            SubjectId = subjectId,
            SourceNodeId = 101,
            TargetNodeId = 100,
            RelationType = EduTwin.Contracts.KnowledgeGraph.RelationType.PrerequisiteOf,
            IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow
        });
        await context.SaveChangesAsync();

        var request = CreateValidRequest(subjectId, 100, 101, "PrerequisiteOf");
        var result = await sut.ExecuteAsync(request, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Deterministic_Result_Regardless_Input_Edge_Insertion_Order()
    {
        SetupValidTenant();
        var subjectId = Guid.NewGuid();

        // Run 1
        using var context1 = CreateDbContext();
        var sut1 = new CreateKnowledgeEdgeUseCase(context1, _tenantContextMock.Object, _timeProviderMock.Object, _validator);
        await SeedValidDataAsync(context1, _tenantContextMock.Object.CenterId!.Value, subjectId, 100, 101);
        context1.KnowledgeNodes.Add(new KnowledgeNode { NodeCode = "N1", NodeName = "N1", CenterId = _tenantContextMock.Object.CenterId!.Value, SubjectId = subjectId, NodeId = 102, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        context1.KnowledgeEdges.Add(new KnowledgeEdge { CenterId = _tenantContextMock.Object.CenterId!.Value, SubjectId = subjectId, SourceNodeId = 101, TargetNodeId = 102, RelationType = EduTwin.Contracts.KnowledgeGraph.RelationType.PrerequisiteOf, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        context1.KnowledgeEdges.Add(new KnowledgeEdge { CenterId = _tenantContextMock.Object.CenterId!.Value, SubjectId = subjectId, SourceNodeId = 102, TargetNodeId = 100, RelationType = EduTwin.Contracts.KnowledgeGraph.RelationType.PrerequisiteOf, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await context1.SaveChangesAsync();

        var request1 = CreateValidRequest(subjectId, 100, 101, "PrerequisiteOf");
        var result1 = await sut1.ExecuteAsync(request1, CancellationToken.None);

        // Run 2
        using var context2 = CreateDbContext();
        var sut2 = new CreateKnowledgeEdgeUseCase(context2, _tenantContextMock.Object, _timeProviderMock.Object, _validator);
        await SeedValidDataAsync(context2, _tenantContextMock.Object.CenterId!.Value, subjectId, 100, 101);
        context2.KnowledgeNodes.Add(new KnowledgeNode { NodeCode = "N1", NodeName = "N1", CenterId = _tenantContextMock.Object.CenterId!.Value, SubjectId = subjectId, NodeId = 102, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        context2.KnowledgeEdges.Add(new KnowledgeEdge { CenterId = _tenantContextMock.Object.CenterId!.Value, SubjectId = subjectId, SourceNodeId = 102, TargetNodeId = 100, RelationType = EduTwin.Contracts.KnowledgeGraph.RelationType.PrerequisiteOf, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        context2.KnowledgeEdges.Add(new KnowledgeEdge { CenterId = _tenantContextMock.Object.CenterId!.Value, SubjectId = subjectId, SourceNodeId = 101, TargetNodeId = 102, RelationType = EduTwin.Contracts.KnowledgeGraph.RelationType.PrerequisiteOf, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await context2.SaveChangesAsync();

        var request2 = CreateValidRequest(subjectId, 100, 101, "PrerequisiteOf");
        var result2 = await sut2.ExecuteAsync(request2, CancellationToken.None);

        Assert.False(result1.IsSuccess);
        Assert.Equal(ErrorCodes.DagCycleDetected, result1.ErrorCode);

        Assert.False(result2.IsSuccess);
        Assert.Equal(ErrorCodes.DagCycleDetected, result2.ErrorCode);
    }

    [Fact]
    public async Task Duplicate_Race_Maps_Exact_Constraint()
    {
        SetupValidTenant();
        var subjectId = Guid.NewGuid();
        var innerEx = new Exception("ux_knowledge_edges_center_id_source_node_id_target_node_id_relation_type");
        var dbEx = new DbUpdateException("outer", innerEx);
        using var context = CreateTestDbContext(null);
        var sut = new CreateKnowledgeEdgeUseCase(context, _tenantContextMock.Object, _timeProviderMock.Object, _validator);

        await SeedValidDataAsync(context, _tenantContextMock.Object.CenterId!.Value, subjectId, 100, 101);

        var request = CreateValidRequest(subjectId, 100, 101);
        ((TestDbContext)context).ExceptionToThrowOnSave = dbEx;
        var result = await sut.ExecuteAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.DuplicateResource, result.ErrorCode);
        Assert.Empty(context.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).ToList());
    }

    [Fact]
    public async Task Canonical_And_Physical_Constraint_Aliases_Recognized()
    {
        SetupValidTenant();
        var subjectId = Guid.NewGuid();
        var deepestEx = new Exception("some random physical alias ux_knowledge_edges_center_id_source_id_target_id_relation_type violated");
        var nestedEx = new Exception("middle", deepestEx);
        var dbEx = new DbUpdateException("outer", nestedEx);
        using var context = CreateTestDbContext(null);
        var sut = new CreateKnowledgeEdgeUseCase(context, _tenantContextMock.Object, _timeProviderMock.Object, _validator);

        await SeedValidDataAsync(context, _tenantContextMock.Object.CenterId!.Value, subjectId, 100, 101);

        var request = CreateValidRequest(subjectId, 100, 101);
        ((TestDbContext)context).ExceptionToThrowOnSave = dbEx;
        var result = await sut.ExecuteAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.DuplicateResource, result.ErrorCode);
        Assert.Empty(context.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).ToList());
    }

    [Fact]
    public async Task Unrelated_Primary_Unique_DB_Exception_Rethrows()
    {
        SetupValidTenant();
        var subjectId = Guid.NewGuid();
        var innerEx = new Exception("PK_SomeTable violated");
        var dbEx = new DbUpdateException("outer", innerEx);
        using var context = CreateTestDbContext(null);
        var sut = new CreateKnowledgeEdgeUseCase(context, _tenantContextMock.Object, _timeProviderMock.Object, _validator);

        await SeedValidDataAsync(context, _tenantContextMock.Object.CenterId!.Value, subjectId, 100, 101);

        var request = CreateValidRequest(subjectId, 100, 101);

        // Throw exception on SaveChangesAsync after data is seeded
        ((TestDbContext)context).ExceptionToThrowOnSave = dbEx;

        await Assert.ThrowsAsync<DbUpdateException>(() => sut.ExecuteAsync(request, CancellationToken.None));
    }

    [Fact]
    public async Task Exact_CancellationToken_Passed()
    {
        SetupValidTenant();
        using var context = CreateDbContext();
        var sut = new CreateKnowledgeEdgeUseCase(context, _tenantContextMock.Object, _timeProviderMock.Object, _validator);

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // cancelled token will throw TaskCanceledException when SaveChangesAsync is called

        var subjectId = Guid.NewGuid();
        await SeedValidDataAsync(context, _tenantContextMock.Object.CenterId!.Value, subjectId, 100, 101);

        var request = CreateValidRequest(subjectId, 100, 101);

        var ex = await Assert.ThrowsAsync<OperationCanceledException>(() => sut.ExecuteAsync(request, cts.Token));
        Assert.Equal(cts.Token, ex.CancellationToken);
    }

    [Fact]
    public async Task Failure_Paths_Do_Not_Add_Entity_Or_Persist()
    {
        SetupValidTenant();
        using var context = CreateDbContext();
        var sut = new CreateKnowledgeEdgeUseCase(context, _tenantContextMock.Object, _timeProviderMock.Object, _validator);

        var subjectId = Guid.NewGuid();
        await SeedValidDataAsync(context, _tenantContextMock.Object.CenterId!.Value, subjectId, 100, 101);

        // Force DAG cycle failure
        context.KnowledgeEdges.Add(new KnowledgeEdge { CenterId = _tenantContextMock.Object.CenterId!.Value, SubjectId = subjectId, SourceNodeId = 101, TargetNodeId = 100, RelationType = EduTwin.Contracts.KnowledgeGraph.RelationType.PrerequisiteOf, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var request = CreateValidRequest(subjectId, 100, 101, "PrerequisiteOf");
        var result = await sut.ExecuteAsync(request, CancellationToken.None);

        Assert.False(result.IsSuccess);

        // Verify entity wasn't persisted
        var edges = await context.KnowledgeEdges.ToListAsync();
        Assert.Single(edges); // only the one we seeded

        // Verify ChangeTracker doesn't hold Added candidate
        Assert.Empty(context.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).ToList());
    }

    [Fact]
    public void Production_Source_Contains_No_IgnoreQueryFilters()
    {
        var basePath = AppDomain.CurrentDomain.BaseDirectory;
        var srcPath = Path.Combine(basePath, "../../../../../src/EduTwin.BLL/KnowledgeGraph/CreateKnowledgeEdgeUseCase.cs");
        Assert.True(
            File.Exists(srcPath),
            $"Production source file was not found: {srcPath}");

        var content = File.ReadAllText(srcPath);
        Assert.DoesNotContain("IgnoreQueryFilters", content);
    }

    private TestDbContext CreateTestDbContext(Exception? exceptionToThrow)
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        var context = new TestDbContext(options, _tenantIdAccessorMock.Object)
        {
            ExceptionToThrowOnSave = exceptionToThrow
        };
        return context;
    }

    private class TestDbContext : EduTwinDbContext
    {
        public Exception? ExceptionToThrowOnSave { get; set; }
        public TestDbContext(DbContextOptions<EduTwinDbContext> options, ITenantIdAccessor tenantIdAccessor) : base(options, tenantIdAccessor) { }
        public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            if (ExceptionToThrowOnSave != null)
                throw ExceptionToThrowOnSave;
            return base.SaveChangesAsync(cancellationToken);
        }
    }
}
