using System;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.API.Controllers;
using EduTwin.BLL.Assignments;
using EduTwin.Contracts.Assignments;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.Assignments;

public class AssignmentsControllerTests
{
    [Fact]
    public async Task GetStudentAssignment_Success_CreatesResponseMetadata()
    {
        var assignmentId = Guid.NewGuid();
        var fixedTime = new DateTimeOffset(2026, 8, 9, 10, 30, 0, TimeSpan.Zero);
        var responseWithoutMetadata = new StudentAssignmentDetailResponse
        {
            Data = new StudentAssignmentDetailDto
            {
                AssignmentId = assignmentId.ToString(),
                Title = "Bài tập kiểm thử"
            }
        };

        var getStudentAssignmentUseCase = new Mock<IGetStudentAssignmentUseCase>();
        getStudentAssignmentUseCase
            .Setup(useCase => useCase.ExecuteAsync(assignmentId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetStudentAssignmentResult.Success(responseWithoutMetadata));

        var timeProvider = new Mock<TimeProvider>();
        timeProvider.Setup(provider => provider.GetUtcNow()).Returns(fixedTime);

        var controller = CreateController(getStudentAssignmentUseCase.Object, timeProvider.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                TraceIdentifier = "student-assignment-detail-trace"
            }
        };

        var actionResult = await controller.GetStudentAssignment(assignmentId, CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(actionResult);
        var response = Assert.IsType<StudentAssignmentDetailResponse>(okResult.Value);
        Assert.Same(responseWithoutMetadata, response);
        Assert.NotNull(response.Meta);
        Assert.Equal("student-assignment-detail-trace", response.Meta.TraceId);
        Assert.Equal(fixedTime.UtcDateTime, response.Meta.Timestamp);
    }

    private static AssignmentsController CreateController(
        IGetStudentAssignmentUseCase getStudentAssignmentUseCase,
        TimeProvider timeProvider)
    {
        return new AssignmentsController(
            Mock.Of<ICreateAssignmentUseCase>(),
            Mock.Of<IGetAssignmentUseCase>(),
            Mock.Of<IListAssignmentsUseCase>(),
            Mock.Of<IUpdateAssignmentUseCase>(),
            Mock.Of<IPublishAssignmentUseCase>(),
            Mock.Of<ICloseAssignmentUseCase>(),
            Mock.Of<IGetAssignmentProgressUseCase>(),
            Mock.Of<IListStudentAssignmentsUseCase>(),
            getStudentAssignmentUseCase,
            timeProvider);
    }
}
