using EduTwin.API.Controllers;
using EduTwin.BLL.Organization;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.Organization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.Organization;

public class TeacherSubjectChangeControllerTests
{
    [Theory]
    [InlineData(ErrorCodes.TeacherSubjectChangeBlocked, 422)]
    [InlineData(ErrorCodes.ValidationFailed, 400)]
    public async Task UpdateTeacher_DistinguishesClassAssignmentsFromInvalidInput(string code, int status)
    {
        var useCase = new Mock<IUpdateTeacherUseCase>();
        useCase.Setup(u => u.ExecuteAsync(It.IsAny<Guid>(), It.IsAny<UpdateTeacherRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateTeacherResult.Failure(code));
        var controller = new TeachersController(null!, null!, null!, useCase.Object, null!, TimeProvider.System)
        { ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() } };
        var response = Assert.IsAssignableFrom<ObjectResult>(await controller.UpdateTeacher(Guid.NewGuid(), new UpdateTeacherRequest(), default));
        Assert.Equal(status, response.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(response.Value);
        Assert.Equal(code, problem.Extensions["errorCode"]);
        if (status == 422)
        {
            Assert.Contains("lớp đang hoạt động", problem.Detail);
            Assert.Contains("chuyển giao", problem.Detail);
        }
    }
}
