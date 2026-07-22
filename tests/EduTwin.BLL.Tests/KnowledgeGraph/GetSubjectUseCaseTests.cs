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
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;

namespace EduTwin.BLL.Tests.KnowledgeGraph;

public class GetSubjectUseCaseTests
{
    private readonly EduTwinDbContext _dbContext;
    private readonly Mock<ITenantContext> _tenantMock;
    private readonly GetSubjectUseCase _sut;

    public GetSubjectUseCaseTests()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var tenantIdAccessorMock = new Mock<ITenantIdAccessor>();
        _tenantMock = new Mock<ITenantContext>();

        tenantIdAccessorMock.SetupGet(x => x.CenterId).Returns(() => _tenantMock.Object.CenterId);

        _dbContext = new EduTwinDbContext(options, tenantIdAccessorMock.Object);

        _sut = new GetSubjectUseCase(_dbContext, _tenantMock.Object);
    }

    private async Task SetupActiveCenter(Guid centerId)
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
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    [Theory]
    [InlineData(nameof(UserRole.CenterManager))]
    [InlineData(nameof(UserRole.Teacher))]
    [InlineData(nameof(UserRole.Student))]
    public async Task ValidRoles_SameTenant_ReturnsSuccess(string role)
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        await SetupActiveCenter(centerId);

        _dbContext.Subjects.Add(new Subject
        {
            SubjectId = subjectId,
            CenterId = centerId,
            SubjectCode = "S1",
            SubjectName = "Subject 1",
            IsActive = true,
            IsDeleted = false,
            RowVersion = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid(),
            UpdatedBy = Guid.NewGuid()
        });
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(role);

        var result = await _sut.ExecuteAsync(subjectId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal("S1", result.Data.SubjectCode);
        Assert.Equal(subjectId.ToString("D").ToLowerInvariant(), result.Data.SubjectId);

        Assert.Empty(_dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task InactiveSubject_SameTenant_ReturnsSuccess()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        await SetupActiveCenter(centerId);

        _dbContext.Subjects.Add(new Subject
        {
            SubjectId = subjectId,
            CenterId = centerId,
            SubjectCode = "S1",
            SubjectName = "Subject 1",
            IsActive = false,
            IsDeleted = false,
            RowVersion = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid(),
            UpdatedBy = Guid.NewGuid()
        });
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.Student));

        var result = await _sut.ExecuteAsync(subjectId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.False(result.Data.IsActive);
    }

    [Fact]
    public async Task SuspendedCenter_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        _dbContext.Centers.Add(new Center
        {
            CenterId = centerId,
            CenterCode = "C" + centerId.ToString()[..4],
            CenterName = "Test Center",
            Timezone = "UTC",
            Status = EduTwin.Contracts.Organization.CenterStatus.Suspended,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.Student));

        var result = await _sut.ExecuteAsync(subjectId, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task SoftDeletedCenter_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        _dbContext.Centers.Add(new Center
        {
            CenterId = centerId,
            CenterCode = "C" + centerId.ToString()[..4],
            CenterName = "Test Center",
            Timezone = "UTC",
            Status = EduTwin.Contracts.Organization.CenterStatus.Active,
            IsDeleted = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.Student));

        var result = await _sut.ExecuteAsync(subjectId, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task CrossTenantSubject_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        var otherCenterId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        await SetupActiveCenter(centerId);
        await SetupActiveCenter(otherCenterId);

        _dbContext.Subjects.Add(new Subject
        {
            SubjectId = subjectId,
            CenterId = otherCenterId,
            SubjectCode = "S1",
            SubjectName = "Subject 1",
            IsActive = true,
            IsDeleted = false,
            RowVersion = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid(),
            UpdatedBy = Guid.NewGuid()
        });
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.Student));

        var result = await _sut.ExecuteAsync(subjectId, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task SoftDeletedSubject_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        await SetupActiveCenter(centerId);

        _dbContext.Subjects.Add(new Subject
        {
            SubjectId = subjectId,
            CenterId = centerId,
            SubjectCode = "S1",
            SubjectName = "Subject 1",
            IsActive = true,
            IsDeleted = true,
            RowVersion = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid(),
            UpdatedBy = Guid.NewGuid()
        });
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.Student));

        var result = await _sut.ExecuteAsync(subjectId, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task SubjectIdGuidEmpty_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.Student));

        var result = await _sut.ExecuteAsync(Guid.Empty, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task CenterIdGuidEmpty_ReturnsResourceNotFound()
    {
        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(Guid.Empty);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.Student));

        var result = await _sut.ExecuteAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task UnresolvedTenant_ReturnsResourceNotFound()
    {
        _tenantMock.SetupGet(x => x.IsResolved).Returns(false);

        var result = await _sut.ExecuteAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task CenterIdUserId_NullOrEmpty_ReturnsResourceNotFound(bool hasCenterId, bool hasUserId)
    {
        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(hasCenterId ? Guid.NewGuid() : null);
        _tenantMock.SetupGet(x => x.UserId).Returns(hasUserId ? Guid.NewGuid() : Guid.Empty);
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.Student));

        if (hasCenterId && hasUserId)
        {
            // Just for completeness of the matrix, if valid, it should bypass these and hit the subject query (which returns 404 since no subject exists)
            return;
        }

        var result = await _sut.ExecuteAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task UserIdNull_ReturnsResourceNotFound()
    {
        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.UserId).Returns((Guid?)null);
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.Student));

        var result = await _sut.ExecuteAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData("Admin")]
    [InlineData("teacher")]
    [InlineData("centermanager")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("2")]
    public async Task InvalidRoles_ReturnsResourceNotFound(string? role)
    {
        var centerId = Guid.NewGuid();
        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(role);

        var result = await _sut.ExecuteAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task CancellationToken_IsPassedExactly()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        await SetupActiveCenter(centerId);

        _dbContext.Subjects.Add(new Subject
        {
            SubjectId = subjectId,
            CenterId = centerId,
            SubjectCode = "S1",
            SubjectName = "Subject 1",
            IsActive = true,
            IsDeleted = false,
            RowVersion = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid(),
            UpdatedBy = Guid.NewGuid()
        });
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.Student));

        using var cts = new CancellationTokenSource();
        cts.Cancel(); // Pre-cancel to force exception from EF core

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
        {
            await _sut.ExecuteAsync(subjectId, cts.Token);
        });
    }

    [Fact]
    public async Task CultureFrFR_RowVersion_IsInvariant()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        await SetupActiveCenter(centerId);

        _dbContext.Subjects.Add(new Subject
        {
            SubjectId = subjectId,
            CenterId = centerId,
            SubjectCode = "S1",
            SubjectName = "Subject 1",
            IsActive = true,
            IsDeleted = false,
            RowVersion = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid(),
            UpdatedBy = Guid.NewGuid()
        });
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.Student));

        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            var result = await _sut.ExecuteAsync(subjectId, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal("1", result.Data!.RowVersion);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }
}
