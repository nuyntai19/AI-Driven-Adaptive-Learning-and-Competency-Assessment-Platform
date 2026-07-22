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
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;

namespace EduTwin.BLL.Tests.KnowledgeGraph;

public class MockEduTwinDbContext : EduTwinDbContext
{
    public Func<CancellationToken, Task<int>>? SaveChangesAsyncCallback { get; set; }

    public MockEduTwinDbContext(DbContextOptions<EduTwinDbContext> options, ITenantIdAccessor tenantIdAccessor)
        : base(options, tenantIdAccessor)
    {
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        if (SaveChangesAsyncCallback != null)
        {
            return SaveChangesAsyncCallback(cancellationToken);
        }
        return base.SaveChangesAsync(cancellationToken);
    }
}

public class UpdateSubjectUseCaseTests
{
    private readonly MockEduTwinDbContext _dbContext;
    private readonly Mock<ITenantContext> _tenantMock;
    private readonly Mock<TimeProvider> _timeProviderMock;
    private readonly UpdateSubjectUseCase _sut;

    public UpdateSubjectUseCaseTests()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var tenantIdAccessorMock = new Mock<ITenantIdAccessor>();
        _tenantMock = new Mock<ITenantContext>();

        tenantIdAccessorMock.SetupGet(x => x.CenterId).Returns(() => _tenantMock.Object.CenterId);

        _dbContext = new MockEduTwinDbContext(options, tenantIdAccessorMock.Object);

        _timeProviderMock = new Mock<TimeProvider>();
        var utcNow = new DateTimeOffset(2026, 7, 22, 10, 0, 0, TimeSpan.Zero);
        _timeProviderMock.Setup(x => x.GetUtcNow()).Returns(utcNow);

        _sut = new UpdateSubjectUseCase(_dbContext, _tenantMock.Object, _timeProviderMock.Object);
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
    [InlineData(nameof(UserRole.Teacher))]
    [InlineData(nameof(UserRole.CenterManager))]
    public async Task Update_ValidRoles_ReturnsSuccess(string role)
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await SetupActiveCenter(centerId);

        var originalCreatedAt = DateTime.UtcNow.AddDays(-1);
        var originalCreatedBy = Guid.NewGuid();
        _dbContext.Subjects.Add(new Subject
        {
            SubjectId = subjectId,
            CenterId = centerId,
            SubjectCode = "S1",
            SubjectName = "Subject 1",
            IsActive = true,
            IsDeleted = false,
            RowVersion = 1,
            CreatedAt = originalCreatedAt,
            CreatedBy = originalCreatedBy,
            UpdatedAt = originalCreatedAt,
            UpdatedBy = originalCreatedBy
        });
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(userId);
        _tenantMock.SetupGet(x => x.Role).Returns(role);

        var request = new UpdateSubjectRequest
        {
            SubjectCode = "S2",
            SubjectName = "Subject 2",
            Description = "Desc",
            IsActive = false,
            RowVersion = "1"
        };

        var result = await _sut.ExecuteAsync(subjectId, request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal("S2", result.Data.SubjectCode);

        var updatedSubject = await _dbContext.Subjects.FindAsync(subjectId);
        Assert.NotNull(updatedSubject);
        Assert.Equal("S2", updatedSubject.SubjectCode);
        Assert.Equal(userId, updatedSubject.UpdatedBy);
        Assert.Equal(new DateTimeOffset(2026, 7, 22, 10, 0, 0, TimeSpan.Zero).UtcDateTime, updatedSubject.UpdatedAt);
        Assert.Equal(originalCreatedBy, updatedSubject.CreatedBy);
        Assert.Equal(originalCreatedAt, updatedSubject.CreatedAt);
        Assert.Equal(2UL, updatedSubject.RowVersion);
        Assert.Equal(subjectId, updatedSubject.SubjectId);
        Assert.Equal(centerId, updatedSubject.CenterId);
    }

    [Theory]
    [InlineData(nameof(UserRole.Student))]
    [InlineData("Admin")]
    [InlineData("teacher")]
    [InlineData("centermanager")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Update_InvalidRoles_ReturnsResourceNotFound(string? role)
    {
        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(role);

        var request = new UpdateSubjectRequest { SubjectCode = "S", SubjectName = "N", IsActive = true, RowVersion = "1" };
        var result = await _sut.ExecuteAsync(Guid.NewGuid(), request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Update_InvalidTenantContext_ReturnsResourceNotFound()
    {
        _tenantMock.SetupGet(x => x.IsResolved).Returns(false);
        var request = new UpdateSubjectRequest { SubjectCode = "S", SubjectName = "N", IsActive = true, RowVersion = "1" };
        var result = await _sut.ExecuteAsync(Guid.NewGuid(), request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Update_CenterIdNull_ReturnsResourceNotFound()
    {
        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns((Guid?)null);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.Teacher));

        var request = new UpdateSubjectRequest { SubjectCode = "S", SubjectName = "N", IsActive = true, RowVersion = "1" };
        var result = await _sut.ExecuteAsync(Guid.NewGuid(), request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Update_CenterIdEmpty_ReturnsResourceNotFound()
    {
        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(Guid.Empty);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.Teacher));

        var request = new UpdateSubjectRequest { SubjectCode = "S", SubjectName = "N", IsActive = true, RowVersion = "1" };
        var result = await _sut.ExecuteAsync(Guid.NewGuid(), request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Update_UserIdNull_ReturnsResourceNotFound()
    {
        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.UserId).Returns((Guid?)null);
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.Teacher));

        var request = new UpdateSubjectRequest { SubjectCode = "S", SubjectName = "N", IsActive = true, RowVersion = "1" };
        var result = await _sut.ExecuteAsync(Guid.NewGuid(), request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Update_UserIdEmpty_ReturnsResourceNotFound()
    {
        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.Empty);
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.Teacher));

        var request = new UpdateSubjectRequest { SubjectCode = "S", SubjectName = "N", IsActive = true, RowVersion = "1" };
        var result = await _sut.ExecuteAsync(Guid.NewGuid(), request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Update_SubjectIdEmpty_ReturnsResourceNotFound()
    {
        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.Teacher));

        var request = new UpdateSubjectRequest { SubjectCode = "S", SubjectName = "N", IsActive = true, RowVersion = "1" };
        var result = await _sut.ExecuteAsync(Guid.Empty, request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Update_RawValidation_BeforeTrim_ReturnsValidationFailed()
    {
        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.Teacher));

        var request = new UpdateSubjectRequest
        {
            SubjectCode = new string('A', 33),
            SubjectName = "Valid Name",
            IsActive = true,
            RowVersion = "1"
        };
        var result = await _sut.ExecuteAsync(Guid.NewGuid(), request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task Update_SuspendedCenter_ReturnsResourceNotFound()
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
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.Teacher));

        var request = new UpdateSubjectRequest { SubjectCode = "S", SubjectName = "N", IsActive = true, RowVersion = "1" };
        var result = await _sut.ExecuteAsync(Guid.NewGuid(), request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Update_SoftDeletedCenter_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        _dbContext.Centers.Add(new Center
        {
            CenterId = centerId,
            CenterCode = "C1",
            CenterName = "Test Center",
            Timezone = "UTC",
            Status = EduTwin.Contracts.Organization.CenterStatus.Active,
            IsDeleted = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.Teacher));

        var request = new UpdateSubjectRequest { SubjectCode = "S", SubjectName = "N", IsActive = true, RowVersion = "1" };
        var result = await _sut.ExecuteAsync(Guid.NewGuid(), request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Update_CrossTenantSubject_ReturnsResourceNotFound()
    {
        var myCenterId = Guid.NewGuid();
        var otherCenterId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        await SetupActiveCenter(myCenterId);
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

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(myCenterId);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.Teacher));

        var request = new UpdateSubjectRequest { SubjectCode = "S2", SubjectName = "N", IsActive = true, RowVersion = "1" };
        var result = await _sut.ExecuteAsync(subjectId, request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Update_SoftDeletedSubject_ReturnsResourceNotFound()
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

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.Teacher));

        var request = new UpdateSubjectRequest { SubjectCode = "S2", SubjectName = "N", IsActive = true, RowVersion = "1" };
        var result = await _sut.ExecuteAsync(subjectId, request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Update_InactiveSubject_CanBeReactivated()
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

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.Teacher));

        var request = new UpdateSubjectRequest { SubjectCode = "S1", SubjectName = "N", IsActive = true, RowVersion = "1" };
        var result = await _sut.ExecuteAsync(subjectId, request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(result.Data!.IsActive);
    }

    [Fact]
    public async Task Update_StaleRowVersion_ReturnsConcurrencyConflict_NoPersistence()
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
            RowVersion = 2,
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
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.Teacher));

        var request = new UpdateSubjectRequest { SubjectCode = "S2", SubjectName = "N", IsActive = true, RowVersion = "999" };
        var result = await _sut.ExecuteAsync(subjectId, request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, result.ErrorCode);

        var subject = await _dbContext.Subjects.FindAsync(subjectId);
        Assert.Equal("S1", subject!.SubjectCode);
    }

    [Fact]
    public async Task Update_DuplicateCodeSameTenant_ReturnsDuplicateResource()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var otherSubjectId = Guid.NewGuid();
        await SetupActiveCenter(centerId);

        _dbContext.Subjects.AddRange(
            new Subject
            {
                SubjectId = subjectId,
                CenterId = centerId,
                SubjectCode = "S1",
                SubjectName = "Subject 1",
                IsActive = true,
                RowVersion = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid(),
                UpdatedBy = Guid.NewGuid()
            },
            new Subject
            {
                SubjectId = otherSubjectId,
                CenterId = centerId,
                SubjectCode = "S2",
                SubjectName = "Subject 2",
                IsActive = true,
                RowVersion = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = Guid.NewGuid(),
                UpdatedBy = Guid.NewGuid()
            }
        );
        await _dbContext.SaveChangesAsync();

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.Teacher));

        var request = new UpdateSubjectRequest { SubjectCode = "S2", SubjectName = "N", IsActive = true, RowVersion = "1" };
        var result = await _sut.ExecuteAsync(subjectId, request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.DuplicateResource, result.ErrorCode);
    }

    [Fact]
    public async Task Update_SameCodeForSameSubject_ReturnsSuccess()
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
            RowVersion = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid(),
            UpdatedBy = Guid.NewGuid()
        });
        await _dbContext.SaveChangesAsync();

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.Teacher));

        var request = new UpdateSubjectRequest { SubjectCode = "S1", SubjectName = "New Name", IsActive = true, RowVersion = "1" };
        var result = await _sut.ExecuteAsync(subjectId, request, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("New Name", result.Data!.SubjectName);
    }

    [Fact]
    public async Task Update_SameCodeCrossTenant_ReturnsSuccess()
    {
        var centerId = Guid.NewGuid();
        var otherCenterId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        await SetupActiveCenter(centerId);
        await SetupActiveCenter(otherCenterId);

        _dbContext.Subjects.Add(new Subject
        {
            SubjectId = Guid.NewGuid(),
            CenterId = otherCenterId,
            SubjectCode = "S2",
            SubjectName = "Subject 2",
            IsActive = true,
            RowVersion = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid(),
            UpdatedBy = Guid.NewGuid()
        });

        _dbContext.Subjects.Add(new Subject
        {
            SubjectId = subjectId,
            CenterId = centerId,
            SubjectCode = "S1",
            SubjectName = "Subject 1",
            IsActive = true,
            RowVersion = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid(),
            UpdatedBy = Guid.NewGuid()
        });
        await _dbContext.SaveChangesAsync();

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.Teacher));

        var request = new UpdateSubjectRequest { SubjectCode = "S2", SubjectName = "New Name", IsActive = true, RowVersion = "1" };
        var result = await _sut.ExecuteAsync(subjectId, request, CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Update_DbUpdateConcurrencyException_ReturnsConcurrencyConflict()
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
            RowVersion = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid(),
            UpdatedBy = Guid.NewGuid()
        });
        await _dbContext.SaveChangesAsync();

        _dbContext.SaveChangesAsyncCallback = _ => throw new DbUpdateConcurrencyException();

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.Teacher));

        var request = new UpdateSubjectRequest { SubjectCode = "S2", SubjectName = "Name", IsActive = true, RowVersion = "1" };
        var result = await _sut.ExecuteAsync(subjectId, request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, result.ErrorCode);
        Assert.Empty(_dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Update_UniqueConstraintOnRootException_ReturnsDuplicateResource()
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
            RowVersion = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid(),
            UpdatedBy = Guid.NewGuid()
        });
        await _dbContext.SaveChangesAsync();

        _dbContext.SaveChangesAsyncCallback = _ => throw new DbUpdateException("Duplicate key value violates unique constraint ux_subjects_center_id_subject_code");

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.Teacher));

        var request = new UpdateSubjectRequest { SubjectCode = "S2", SubjectName = "Name", IsActive = true, RowVersion = "1" };
        var result = await _sut.ExecuteAsync(subjectId, request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal(ErrorCodes.DuplicateResource, result.ErrorCode);
        Assert.Empty(_dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Update_UniqueConstraintDeepNested_ReturnsDuplicateResource()
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
            RowVersion = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid(),
            UpdatedBy = Guid.NewGuid()
        });
        await _dbContext.SaveChangesAsync();

        var inner2 = new Exception("ux_subjects_center_id_subject_code constraint violation");
        var inner1 = new Exception("Wrapper", inner2);
        _dbContext.SaveChangesAsyncCallback = _ => throw new DbUpdateException("Top level", inner1);

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.Teacher));

        var request = new UpdateSubjectRequest { SubjectCode = "S2", SubjectName = "Name", IsActive = true, RowVersion = "1" };
        var result = await _sut.ExecuteAsync(subjectId, request, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Data);
        Assert.Equal(ErrorCodes.DuplicateResource, result.ErrorCode);
        Assert.Empty(_dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Update_UnrelatedDbUpdateException_Rethrows()
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
            RowVersion = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid(),
            UpdatedBy = Guid.NewGuid()
        });
        await _dbContext.SaveChangesAsync();

        var expectedException = new DbUpdateException("Database connection failed");
        _dbContext.SaveChangesAsyncCallback = _ => throw expectedException;

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.Teacher));

        var request = new UpdateSubjectRequest { SubjectCode = "S2", SubjectName = "Name", IsActive = true, RowVersion = "1" };

        var ex = await Assert.ThrowsAsync<DbUpdateException>(() => _sut.ExecuteAsync(subjectId, request, CancellationToken.None));
        Assert.Same(expectedException, ex);
        Assert.Empty(_dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task CultureFrFR_Response_IsInvariant()
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
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.Teacher));

        var originalCulture = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("fr-FR");
            var request = new UpdateSubjectRequest { SubjectCode = "S2", SubjectName = "Name", IsActive = true, RowVersion = "1" };
            var result = await _sut.ExecuteAsync(subjectId, request, CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal("2", result.Data!.RowVersion);
            Assert.Equal(subjectId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(), result.Data.SubjectId);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public async Task Update_ExactCancellationToken_IsPassedToSaveChangesAsync()
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
            RowVersion = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = Guid.NewGuid(),
            UpdatedBy = Guid.NewGuid()
        });
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var tokenPassed = CancellationToken.None;
        _dbContext.SaveChangesAsyncCallback = token =>
        {
            tokenPassed = token;
            return Task.FromResult(1);
        };

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.Teacher));

        using var cts = new CancellationTokenSource();
        var exactToken = cts.Token;
        var request = new UpdateSubjectRequest { SubjectCode = "S2", SubjectName = "Name", IsActive = true, RowVersion = "1" };

        await _sut.ExecuteAsync(subjectId, request, exactToken);

        Assert.Equal(exactToken, tokenPassed);
    }
}
