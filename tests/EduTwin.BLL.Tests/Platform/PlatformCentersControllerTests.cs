using System;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Moq;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using EduTwin.API.Controllers;
using EduTwin.BLL.Platform;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.Platform;

namespace EduTwin.BLL.Tests.Platform;

public class PlatformCentersControllerTests
{
    private readonly Mock<IPlatformCenterService> _mockService;
    private readonly Mock<TimeProvider> _mockTimeProvider;
    private readonly PlatformCentersController _controller;
    private static readonly DateTime FixedUtcNow = new(2026, 9, 12, 12, 0, 0, DateTimeKind.Utc);

    public PlatformCentersControllerTests()
    {
        _mockService = new Mock<IPlatformCenterService>();
        _mockTimeProvider = new Mock<TimeProvider>();
        _mockTimeProvider
            .Setup(t => t.GetUtcNow())
            .Returns(new DateTimeOffset(FixedUtcNow, TimeSpan.Zero));

        _controller = new PlatformCentersController(_mockService.Object, _mockTimeProvider.Object);

        _controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };
    }

    [Fact]
    public async Task ListCenters_OnSuccess_ReturnsOk()
    {
        var data = new PlatformCentersListData
        {
            Items = [],
            TotalCount = 0,
            Page = 1,
            PageSize = 20
        };

        _mockService
            .Setup(s => s.ListCentersAsync(1, 20, null, null, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PlatformResult<PlatformCentersListData>.Success(data));

        var result = await _controller.ListCenters(1, 20, null, null);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public async Task CreateCenter_WhenDuplicate_ReturnsConflict409()
    {
        var request = new CreatePlatformCenterRequest
        {
            CenterCode = "DUPLICATE",
            CenterName = "Duplicate Center"
        };

        _mockService
            .Setup(s => s.CreateCenterAsync(request, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PlatformResult<PlatformCenterListItemDto>.Failure(ErrorCodes.DuplicateResource, "Duplicate center"));

        var result = await _controller.CreateCenter(request);

        var conflictResult = Assert.IsType<ConflictObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(conflictResult.Value);
        Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
    }

    [Fact]
    public async Task UpdateCenterStatus_WhenConcurrencyConflict_ReturnsConflict409()
    {
        var centerId = Guid.NewGuid();
        var request = new UpdatePlatformCenterStatusRequest
        {
            Status = "Suspended",
            RowVersion = "1"
        };

        _mockService
            .Setup(s => s.UpdateCenterStatusAsync(centerId, request, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PlatformResult<PlatformCenterListItemDto>.Failure(ErrorCodes.ConcurrencyConflict, "Version mismatch"));

        var result = await _controller.UpdateCenterStatus(centerId, request);

        var conflictResult = Assert.IsType<ConflictObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(conflictResult.Value);
        Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
    }

    [Fact]
    public async Task ResetCenterManagerPassword_OnSuccess_ReturnsOk()
    {
        var centerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        var request = new ResetCenterManagerPasswordRequest
        {
            NewPassword = "NewPassword123!",
            ExpectedUserRowVersion = "1"
        };

        var responseData = new ResetCenterManagerPasswordData
        {
            CenterId = centerId,
            ManagerUserId = managerId,
            NewUserRowVersion = "2",
            ResetAtUtc = FixedUtcNow,
            Success = true
        };

        _mockService
            .Setup(s => s.ResetCenterManagerPasswordAsync(centerId, managerId, request, It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(PlatformResult<ResetCenterManagerPasswordData>.Success(responseData));

        var result = await _controller.ResetCenterManagerPassword(centerId, managerId, request);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }
}
