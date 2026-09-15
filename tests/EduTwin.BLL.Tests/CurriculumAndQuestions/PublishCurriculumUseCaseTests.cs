using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using EduTwin.BLL.CurriculumAndQuestions;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;

namespace EduTwin.BLL.Tests.CurriculumAndQuestions;

public class PublishCurriculumUseCaseTests
{
    private readonly DbContextOptions<EduTwinDbContext> _dbOptions;
    private readonly Mock<ITenantContext> _tenantMock;
    private readonly Mock<ITenantIdAccessor> _tenantAccessorMock;
    private readonly Mock<TimeProvider> _timeProviderMock;

    public PublishCurriculumUseCaseTests()
    {
        _dbOptions = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _tenantMock = new Mock<ITenantContext>();
        _tenantMock.Setup(t => t.Role).Returns(nameof(UserRole.CenterManager));
        _tenantMock.Setup(t => t.Role).Returns(nameof(UserRole.CenterManager));
        _tenantAccessorMock = new Mock<ITenantIdAccessor>();
        _tenantAccessorMock.Setup(x => x.CenterId).Returns(() => _tenantMock.Object.CenterId);
        _timeProviderMock = new Mock<TimeProvider>();
        _timeProviderMock.Setup(x => x.GetUtcNow()).Returns(new DateTimeOffset(2026, 7, 24, 12, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task ExecuteAsync_TenantNotResolved_ReturnsNotFound()
    {
        using var dbContext = new EduTwinDbContext(_dbOptions, _tenantAccessorMock.Object);
        _tenantMock.Setup(t => t.IsResolved).Returns(false);

        var sut = new PublishCurriculumUseCase(dbContext, _tenantMock.Object, _timeProviderMock.Object);

        var result = await sut.ExecuteAsync(Guid.NewGuid(), new PublishCurriculumRequest { RowVersion = "1" });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_CurriculumNotFound_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _tenantMock.Setup(t => t.IsResolved).Returns(true);
        _tenantMock.Setup(t => t.CenterId).Returns(centerId);
        _tenantMock.Setup(t => t.UserId).Returns(userId);

        using var dbContext = new EduTwinDbContext(_dbOptions, _tenantAccessorMock.Object);
        var sut = new PublishCurriculumUseCase(dbContext, _tenantMock.Object, _timeProviderMock.Object);

        var result = await sut.ExecuteAsync(Guid.NewGuid(), new PublishCurriculumRequest { RowVersion = "1" });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_RowVersionMismatch_ReturnsConcurrencyConflict()
    {
        var centerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _tenantMock.Setup(t => t.IsResolved).Returns(true);
        _tenantMock.Setup(t => t.CenterId).Returns(centerId);
        _tenantMock.Setup(t => t.UserId).Returns(userId);

        var curriculumId = Guid.NewGuid();
        using (var dbContext = new EduTwinDbContext(_dbOptions, _tenantAccessorMock.Object))
        {
            dbContext.Curriculums.Add(new Curriculum
            {
                CurriculumId = curriculumId,
                CenterId = centerId,
                TeacherId = userId,
                SubjectId = Guid.NewGuid(),
                Title = "Draft Curriculum",
                ReviewStatus = ReviewStatus.Draft,
                RowVersion = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        using (var dbContext = new EduTwinDbContext(_dbOptions, _tenantAccessorMock.Object))
        {
            var sut = new PublishCurriculumUseCase(dbContext, _tenantMock.Object, _timeProviderMock.Object);

            var result = await sut.ExecuteAsync(curriculumId, new PublishCurriculumRequest
            {
                RowVersion = "2" // Mismatch
            });

            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorCodes.ConcurrencyConflict, result.ErrorCode);
        }
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyPublished_ReturnsInvalidStateTransition()
    {
        var centerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _tenantMock.Setup(t => t.IsResolved).Returns(true);
        _tenantMock.Setup(t => t.CenterId).Returns(centerId);
        _tenantMock.Setup(t => t.UserId).Returns(userId);

        var curriculumId = Guid.NewGuid();
        using (var dbContext = new EduTwinDbContext(_dbOptions, _tenantAccessorMock.Object))
        {
            dbContext.Curriculums.Add(new Curriculum
            {
                CurriculumId = curriculumId,
                CenterId = centerId,
                TeacherId = userId,
                SubjectId = Guid.NewGuid(),
                Title = "Already Published",
                ReviewStatus = ReviewStatus.Published,
                RowVersion = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        using (var dbContext = new EduTwinDbContext(_dbOptions, _tenantAccessorMock.Object))
        {
            var sut = new PublishCurriculumUseCase(dbContext, _tenantMock.Object, _timeProviderMock.Object);

            var result = await sut.ExecuteAsync(curriculumId, new PublishCurriculumRequest
            {
                RowVersion = "1"
            });

            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorCodes.InvalidStateTransition, result.ErrorCode);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ValidDraft_PublishesAndIncrementsRowVersion()
    {
        var centerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        _tenantMock.Setup(t => t.IsResolved).Returns(true);
        _tenantMock.Setup(t => t.CenterId).Returns(centerId);
        _tenantMock.Setup(t => t.UserId).Returns(userId);

        var curriculumId = Guid.NewGuid();
        using (var dbContext = new EduTwinDbContext(_dbOptions, _tenantAccessorMock.Object))
        {
            dbContext.Curriculums.Add(new Curriculum
            {
                CurriculumId = curriculumId,
                CenterId = centerId,
                TeacherId = userId,
                SubjectId = Guid.NewGuid(),
                Title = "Draft Curriculum",
                ReviewStatus = ReviewStatus.Draft,
                RowVersion = 1,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        using (var dbContext = new EduTwinDbContext(_dbOptions, _tenantAccessorMock.Object))
        {
            var sut = new PublishCurriculumUseCase(dbContext, _tenantMock.Object, _timeProviderMock.Object);

            var result = await sut.ExecuteAsync(curriculumId, new PublishCurriculumRequest
            {
                RowVersion = "1"
            });

            Assert.True(result.IsSuccess);
            Assert.NotNull(result.Data);
            Assert.Equal(ReviewStatus.Published.ToString(), result.Data.ReviewStatus);
            Assert.Equal("2", result.Data.RowVersion);

            var inDb = await dbContext.Curriculums.SingleAsync(c => c.CurriculumId == curriculumId);
            Assert.Equal(ReviewStatus.Published, inDb.ReviewStatus);
            Assert.Equal((uint)2, inDb.RowVersion);
        }
    }
}
