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

namespace EduTwin.BLL.Tests.CurriculumAndQuestions;

public class GetCurriculumUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_TenantNotResolved_ReturnsNotFound()
    {
        var dbOptions = new DbContextOptionsBuilder<EduTwinDbContext>().UseInMemoryDatabase("GetCurriculum_TenantNotResolved").Options;
        using var dbContext = new EduTwinDbContext(dbOptions);
        var tenantMock = new Mock<ITenantContext>();
        tenantMock.Setup(t => t.IsResolved).Returns(false);

        var sut = new GetCurriculumUseCase(dbContext, tenantMock.Object);

        var result = await sut.ExecuteAsync(new GetCurriculumRequest { CurriculumId = Guid.NewGuid() });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }
}
