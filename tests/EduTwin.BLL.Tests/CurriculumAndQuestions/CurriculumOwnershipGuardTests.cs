using EduTwin.BLL.CurriculumAndQuestions;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.CurriculumAndQuestions;

public sealed class CurriculumOwnershipGuardTests
{
    [Fact]
    public async Task TeacherCannotReadOrMutateAnotherTeachersCurriculum()
    {
        var centerId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var curriculumId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var accessor = new Mock<ITenantIdAccessor>();
        accessor.SetupGet(x => x.CenterId).Returns(centerId);
        var tenant = CreateTenant(centerId, actorId);
        var time = new Mock<TimeProvider>();
        time.Setup(x => x.GetUtcNow()).Returns(new DateTimeOffset(2026, 9, 15, 0, 0, 0, TimeSpan.Zero));

        await using var db = new EduTwinDbContext(options, accessor.Object);
        db.Curriculums.Add(new Curriculum
        {
            CurriculumId = curriculumId,
            CenterId = centerId,
            TeacherId = Guid.NewGuid(),
            SubjectId = Guid.NewGuid(),
            Title = "Other teacher curriculum",
            ReviewStatus = ReviewStatus.Draft,
            RowVersion = 1,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await db.SaveChangesAsync();

        var get = await new GetCurriculumUseCase(db, tenant.Object)
            .ExecuteAsync(new GetCurriculumRequest { CurriculumId = curriculumId });
        var update = await new UpdateCurriculumUseCase(db, tenant.Object, time.Object)
            .ExecuteAsync(curriculumId, new UpdateCurriculumRequest { Title = "Changed", RowVersion = "1" });
        var publish = await new PublishCurriculumUseCase(db, tenant.Object, time.Object)
            .ExecuteAsync(curriculumId, new PublishCurriculumRequest { RowVersion = "1" });
        var classes = await new AssignCurriculumClassesUseCase(db, tenant.Object, time.Object)
            .ExecuteAsync(curriculumId, new AssignCurriculumClassesRequest { ClassIds = [], RowVersion = "1" });
        var nodes = await new AssignCurriculumNodesUseCase(db, tenant.Object, time.Object)
            .ExecuteAsync(curriculumId, new AssignCurriculumNodesRequest { NodeIds = [], RowVersion = "1" });

        Assert.All(new[] { get.ErrorCode, update.ErrorCode, publish.ErrorCode, classes.ErrorCode, nodes.ErrorCode },
            error => Assert.Equal(ErrorCodes.ResourceNotFound, error));
        Assert.Equal("Other teacher curriculum", (await db.Curriculums.SingleAsync()).Title);
        Assert.Equal(ReviewStatus.Draft, (await db.Curriculums.SingleAsync()).ReviewStatus);
    }

    [Theory]
    [InlineData(" 1")]
    [InlineData("1 ")]
    [InlineData("+1")]
    [InlineData("0")]
    public async Task CurriculumMutationRejectsNonCanonicalRowVersion(string rowVersion)
    {
        var centerId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var accessor = new Mock<ITenantIdAccessor>();
        accessor.SetupGet(x => x.CenterId).Returns(centerId);
        var tenant = CreateTenant(centerId, actorId, UserRole.CenterManager);
        var time = new Mock<TimeProvider>();
        await using var db = new EduTwinDbContext(options, accessor.Object);

        var result = await new UpdateCurriculumUseCase(db, tenant.Object, time.Object)
            .ExecuteAsync(Guid.NewGuid(), new UpdateCurriculumRequest { Title = "Valid", RowVersion = rowVersion });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    private static Mock<ITenantContext> CreateTenant(
        Guid centerId,
        Guid userId,
        UserRole role = UserRole.Teacher)
    {
        var tenant = new Mock<ITenantContext>();
        tenant.SetupGet(x => x.IsResolved).Returns(true);
        tenant.SetupGet(x => x.CenterId).Returns(centerId);
        tenant.SetupGet(x => x.UserId).Returns(userId);
        tenant.SetupGet(x => x.Role).Returns(role.ToString());
        return tenant;
    }
}
