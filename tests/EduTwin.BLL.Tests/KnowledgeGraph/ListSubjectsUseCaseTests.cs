using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using EduTwin.BLL.KnowledgeGraph;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Organization;

namespace EduTwin.BLL.Tests.KnowledgeGraph;

public class ListSubjectsUseCaseTests : IDisposable
{
    private readonly EduTwinDbContext _dbContext;
    private readonly Mock<ITenantContext> _tenantContextMock;
    private readonly Mock<ITenantIdAccessor> _tenantIdAccessorMock;
    private readonly ListSubjectsUseCase _useCase;

    public ListSubjectsUseCaseTests()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _tenantIdAccessorMock = new Mock<ITenantIdAccessor>();
        _dbContext = new EduTwinDbContext(options, _tenantIdAccessorMock.Object);
        _tenantContextMock = new Mock<ITenantContext>();
        _useCase = new ListSubjectsUseCase(_dbContext, _tenantContextMock.Object);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    private void SetupTenantContext(Guid? centerId, Guid? userId, string? role, bool isResolved = true)
    {
        _tenantContextMock.Setup(x => x.IsResolved).Returns(isResolved);
        _tenantContextMock.Setup(x => x.CenterId).Returns(centerId);
        _tenantContextMock.Setup(x => x.UserId).Returns(userId);
        _tenantContextMock.Setup(x => x.Role).Returns(role);

        // Also mock tenantIdAccessor for Global Query Filters in DbContext
        if (centerId.HasValue)
        {
            _tenantIdAccessorMock.Setup(x => x.CenterId).Returns(centerId.Value);
        }
    }

    private Center CreateCenter(Guid centerId, string code, string name, EduTwin.Contracts.Organization.CenterStatus status, bool isDeleted = false)
    {
        return new Center
        {
            CenterId = centerId,
            CenterCode = code,
            CenterName = name,
            Timezone = "UTC",
            Status = status,
            IsDeleted = isDeleted,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            DeletedAt = isDeleted ? DateTime.UtcNow : null
        };
    }

    private Subject CreateSubject(Guid centerId, string code, string name, bool isActive, bool isDeleted = false)
    {
        return new Subject
        {
            SubjectId = Guid.NewGuid(),
            CenterId = centerId,
            SubjectCode = code,
            SubjectName = name,
            IsActive = isActive,
            IsDeleted = isDeleted,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            DeletedAt = isDeleted ? DateTime.UtcNow : null
        };
    }

    [Fact]
    public async Task UnresolvedTenant_ReturnsResourceNotFound()
    {
        SetupTenantContext(Guid.NewGuid(), Guid.NewGuid(), "Teacher", false);
        var result = await _useCase.ExecuteAsync(new SubjectListQuery(), CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task CenterIdNull_ReturnsResourceNotFound()
    {
        SetupTenantContext(null, Guid.NewGuid(), "Teacher");
        var result = await _useCase.ExecuteAsync(new SubjectListQuery(), CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task CenterIdEmpty_ReturnsResourceNotFound()
    {
        SetupTenantContext(Guid.Empty, Guid.NewGuid(), "Teacher");
        var result = await _useCase.ExecuteAsync(new SubjectListQuery(), CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task UserIdNull_ReturnsResourceNotFound()
    {
        SetupTenantContext(Guid.NewGuid(), null, "Teacher");
        var result = await _useCase.ExecuteAsync(new SubjectListQuery(), CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task UserIdEmpty_ReturnsResourceNotFound()
    {
        SetupTenantContext(Guid.NewGuid(), Guid.Empty, "Teacher");
        var result = await _useCase.ExecuteAsync(new SubjectListQuery(), CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task RoleNull_ReturnsResourceNotFound()
    {
        SetupTenantContext(Guid.NewGuid(), Guid.NewGuid(), null);
        var result = await _useCase.ExecuteAsync(new SubjectListQuery(), CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task RoleEmpty_ReturnsResourceNotFound()
    {
        SetupTenantContext(Guid.NewGuid(), Guid.NewGuid(), "");
        var result = await _useCase.ExecuteAsync(new SubjectListQuery(), CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task RoleWhitespace_ReturnsResourceNotFound()
    {
        SetupTenantContext(Guid.NewGuid(), Guid.NewGuid(), "   ");
        var result = await _useCase.ExecuteAsync(new SubjectListQuery(), CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task InvalidRole_ReturnsResourceNotFound()
    {
        SetupTenantContext(Guid.NewGuid(), Guid.NewGuid(), "Admin");
        var result = await _useCase.ExecuteAsync(new SubjectListQuery(), CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task WrongCasingRole_ReturnsResourceNotFound()
    {
        SetupTenantContext(Guid.NewGuid(), Guid.NewGuid(), "teacher");
        var result = await _useCase.ExecuteAsync(new SubjectListQuery(), CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task NumericRole_ReturnsResourceNotFound()
    {
        SetupTenantContext(Guid.NewGuid(), Guid.NewGuid(), "1");
        var result = await _useCase.ExecuteAsync(new SubjectListQuery(), CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task SuspendedCenter_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        SetupTenantContext(centerId, Guid.NewGuid(), "Teacher");

        _dbContext.Centers.Add(CreateCenter(centerId, "C1", "Center 1", EduTwin.Contracts.Organization.CenterStatus.Suspended));
        await _dbContext.SaveChangesAsync();

        var result = await _useCase.ExecuteAsync(new SubjectListQuery(), CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task SoftDeletedCenter_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        SetupTenantContext(centerId, Guid.NewGuid(), "Teacher");

        _dbContext.Centers.Add(CreateCenter(centerId, "C1", "Center 1", EduTwin.Contracts.Organization.CenterStatus.Active, isDeleted: true));
        await _dbContext.SaveChangesAsync();

        var result = await _useCase.ExecuteAsync(new SubjectListQuery(), CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("Teacher")]
    [InlineData("CenterManager")]
    public async Task Roles_AreAllowed(string role)
    {
        var centerId = Guid.NewGuid();
        SetupTenantContext(centerId, Guid.NewGuid(), role);
        _dbContext.Centers.Add(CreateCenter(centerId, "C1", "Center 1", EduTwin.Contracts.Organization.CenterStatus.Active));
        await _dbContext.SaveChangesAsync();

        var result = await _useCase.ExecuteAsync(new SubjectListQuery(), CancellationToken.None);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task CenterA_ReturnsOnlyCenterASubjects()
    {
        var centerIdA = Guid.NewGuid();
        var centerIdB = Guid.NewGuid();
        SetupTenantContext(centerIdA, Guid.NewGuid(), "CenterManager");

        _dbContext.Centers.Add(CreateCenter(centerIdA, "C1", "Center 1", EduTwin.Contracts.Organization.CenterStatus.Active));
        _dbContext.Centers.Add(CreateCenter(centerIdB, "C2", "Center 2", EduTwin.Contracts.Organization.CenterStatus.Active));

        _dbContext.Subjects.Add(CreateSubject(centerIdA, "A1", "Sub A1", true));
        _dbContext.Subjects.Add(CreateSubject(centerIdB, "B1", "Sub B1", true));
        await _dbContext.SaveChangesAsync();

        var result = await _useCase.ExecuteAsync(new SubjectListQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!);
        Assert.Equal("A1", result.Data![0].SubjectCode);
    }

    [Fact]
    public async Task CrossTenantSubjects_AreExcluded()
    {
        var centerIdA = Guid.NewGuid();
        var centerIdB = Guid.NewGuid();
        SetupTenantContext(centerIdA, Guid.NewGuid(), "CenterManager");

        _dbContext.Centers.Add(CreateCenter(centerIdA, "C1", "Center 1", EduTwin.Contracts.Organization.CenterStatus.Active));
        _dbContext.Centers.Add(CreateCenter(centerIdB, "C2", "Center 2", EduTwin.Contracts.Organization.CenterStatus.Active));

        _dbContext.Subjects.Add(CreateSubject(centerIdA, "A1", "Sub A1", true));
        _dbContext.Subjects.Add(CreateSubject(centerIdB, "B1", "Sub B1", true));
        await _dbContext.SaveChangesAsync();

        var allSubjects = await _dbContext.Subjects.IgnoreQueryFilters().ToListAsync();
        Assert.Equal(2, allSubjects.Count);

        var result = await _useCase.ExecuteAsync(new SubjectListQuery(), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.DoesNotContain(result.Data!, s => s.SubjectCode == "B1");
    }

    [Fact]
    public async Task SoftDeletedSubjects_AreExcluded()
    {
        var centerId = Guid.NewGuid();
        SetupTenantContext(centerId, Guid.NewGuid(), "CenterManager");
        _dbContext.Centers.Add(CreateCenter(centerId, "C1", "Center 1", EduTwin.Contracts.Organization.CenterStatus.Active));

        _dbContext.Subjects.Add(CreateSubject(centerId, "A1", "A1", true));
        _dbContext.Subjects.Add(CreateSubject(centerId, "A2", "A2", true, true));
        await _dbContext.SaveChangesAsync();

        var result = await _useCase.ExecuteAsync(new SubjectListQuery(), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!);
        Assert.Equal("A1", result.Data![0].SubjectCode);
    }

    [Fact]
    public async Task IsActiveTrue_ReturnsOnlyActive()
    {
        var centerId = Guid.NewGuid();
        SetupTenantContext(centerId, Guid.NewGuid(), "CenterManager");
        _dbContext.Centers.Add(CreateCenter(centerId, "C1", "Center 1", EduTwin.Contracts.Organization.CenterStatus.Active));

        _dbContext.Subjects.Add(CreateSubject(centerId, "A1", "A1", true));
        _dbContext.Subjects.Add(CreateSubject(centerId, "A2", "A2", false));
        await _dbContext.SaveChangesAsync();

        var result = await _useCase.ExecuteAsync(new SubjectListQuery { IsActive = true }, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!);
        Assert.Equal("A1", result.Data![0].SubjectCode);
    }

    [Fact]
    public async Task IsActiveFalse_ReturnsOnlyInactive()
    {
        var centerId = Guid.NewGuid();
        SetupTenantContext(centerId, Guid.NewGuid(), "CenterManager");
        _dbContext.Centers.Add(CreateCenter(centerId, "C1", "Center 1", EduTwin.Contracts.Organization.CenterStatus.Active));

        _dbContext.Subjects.Add(CreateSubject(centerId, "A1", "A1", true));
        _dbContext.Subjects.Add(CreateSubject(centerId, "A2", "A2", false));
        await _dbContext.SaveChangesAsync();

        var result = await _useCase.ExecuteAsync(new SubjectListQuery { IsActive = false }, CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!);
        Assert.Equal("A2", result.Data![0].SubjectCode);
    }

    [Fact]
    public async Task NoFilter_ReturnsActiveAndInactive()
    {
        var centerId = Guid.NewGuid();
        SetupTenantContext(centerId, Guid.NewGuid(), "CenterManager");
        _dbContext.Centers.Add(CreateCenter(centerId, "C1", "Center 1", EduTwin.Contracts.Organization.CenterStatus.Active));

        _dbContext.Subjects.Add(CreateSubject(centerId, "A1", "A1", true));
        _dbContext.Subjects.Add(CreateSubject(centerId, "A2", "A2", false));
        await _dbContext.SaveChangesAsync();

        var result = await _useCase.ExecuteAsync(new SubjectListQuery(), CancellationToken.None);
        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Data!.Count);
    }

    [Fact]
    public async Task DeterministicOrder_BySubjectCodeThenId()
    {
        var centerId = Guid.NewGuid();
        SetupTenantContext(centerId, Guid.NewGuid(), "CenterManager");
        _dbContext.Centers.Add(CreateCenter(centerId, "C1", "Center 1", EduTwin.Contracts.Organization.CenterStatus.Active));

        var id1 = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var id2 = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var id3 = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");

        var sub1 = CreateSubject(centerId, "MATH", "M1", true); sub1.SubjectId = id1;
        var sub2 = CreateSubject(centerId, "MATH", "M2", true); sub2.SubjectId = id2;
        var sub3 = CreateSubject(centerId, "BIO", "B1", true); sub3.SubjectId = id3;

        _dbContext.Subjects.Add(sub1);
        _dbContext.Subjects.Add(sub2);
        _dbContext.Subjects.Add(sub3);

        await _dbContext.SaveChangesAsync();

        var result = await _useCase.ExecuteAsync(new SubjectListQuery(), CancellationToken.None);
        Assert.True(result.IsSuccess);

        Assert.Equal("BIO", result.Data![0].SubjectCode);

        Assert.Equal("MATH", result.Data![1].SubjectCode);
        Assert.Equal(id2.ToString("D").ToLowerInvariant(), result.Data![1].SubjectId); // 'a' comes before 'b'

        Assert.Equal("MATH", result.Data![2].SubjectCode);
        Assert.Equal(id1.ToString("D").ToLowerInvariant(), result.Data![2].SubjectId);
    }

    [Fact]
    public async Task RowVersion_UsesInvariantCulture()
    {
        var centerId = Guid.NewGuid();
        SetupTenantContext(centerId, Guid.NewGuid(), "CenterManager");
        _dbContext.Centers.Add(CreateCenter(centerId, "C1", "Center 1", EduTwin.Contracts.Organization.CenterStatus.Active));

        var subject = CreateSubject(centerId, "A1", "A1", true);
        _dbContext.Subjects.Add(subject);
        await _dbContext.SaveChangesAsync();

        var persistedSubject = await _dbContext.Subjects.FirstAsync();
        var expectedRowVersion = persistedSubject.RowVersion.ToString(CultureInfo.InvariantCulture);

        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            var result = await _useCase.ExecuteAsync(new SubjectListQuery(), CancellationToken.None);
            Assert.True(result.IsSuccess);
            Assert.Equal(expectedRowVersion, result.Data![0].RowVersion);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public async Task ReadQuery_DoesNotTrackEntities()
    {
        var centerId = Guid.NewGuid();
        SetupTenantContext(centerId, Guid.NewGuid(), "CenterManager");
        _dbContext.Centers.Add(CreateCenter(centerId, "C1", "Center 1", EduTwin.Contracts.Organization.CenterStatus.Active));

        _dbContext.Subjects.Add(CreateSubject(centerId, "A1", "A1", true));
        await _dbContext.SaveChangesAsync();

        _dbContext.ChangeTracker.Clear();

        var result = await _useCase.ExecuteAsync(new SubjectListQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(_dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task ExactCancellationToken_IsPassed()
    {
        var centerId = Guid.NewGuid();
        SetupTenantContext(centerId, Guid.NewGuid(), "CenterManager");

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await _useCase.ExecuteAsync(new SubjectListQuery(), cts.Token));
    }
}
