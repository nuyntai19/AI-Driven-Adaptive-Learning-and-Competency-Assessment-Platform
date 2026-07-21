using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using EduTwin.API.Controllers;
using EduTwin.BLL.KnowledgeGraph;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.KnowledgeGraph;

namespace EduTwin.BLL.Tests.KnowledgeGraph;

public class SubjectsControllerTests
{
    private readonly Mock<IListSubjectsUseCase> _useCaseMock;
    private readonly SubjectsController _controller;

    public SubjectsControllerTests()
    {
        _useCaseMock = new Mock<IListSubjectsUseCase>();
        _controller = new SubjectsController(_useCaseMock.Object, TimeProvider.System)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext()
                {
                    TraceIdentifier = "test-trace-id"
                }
            }
        };
    }

    [Fact]
    public async Task Success_Returns200SubjectListResponse()
    {
        var data = new List<SubjectDto>
        {
            new SubjectDto { SubjectCode = "A" }
        };
        _useCaseMock.Setup(x => x.ExecuteAsync(It.IsAny<SubjectListQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ListSubjectsResult.Success(data));

        var result = await _controller.ListSubjects(new SubjectListQuery(), CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<SubjectListResponse>(okResult.Value);
        Assert.Single(response.Data);
        Assert.Equal("A", response.Data[0].SubjectCode);
    }

    [Fact]
    public async Task Success_MetaContainsTraceIdAndTimestamp()
    {
        var data = new List<SubjectDto>();
        _useCaseMock.Setup(x => x.ExecuteAsync(It.IsAny<SubjectListQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ListSubjectsResult.Success(data));

        var result = await _controller.ListSubjects(new SubjectListQuery(), CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<SubjectListResponse>(okResult.Value);
        Assert.Equal("test-trace-id", response.Meta.TraceId);
        Assert.NotEqual(default, response.Meta.Timestamp);
    }

    [Fact]
    public async Task Query_IsPassedToUseCase()
    {
        var query = new SubjectListQuery { IsActive = true };
        _useCaseMock.Setup(x => x.ExecuteAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ListSubjectsResult.Success(new List<SubjectDto>()));

        await _controller.ListSubjects(query, CancellationToken.None);

        _useCaseMock.Verify(x => x.ExecuteAsync(query, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancellationToken_IsPassedExactly()
    {
        using var cts = new CancellationTokenSource();
        _useCaseMock.Setup(x => x.ExecuteAsync(It.IsAny<SubjectListQuery>(), cts.Token))
            .ReturnsAsync(ListSubjectsResult.Success(new List<SubjectDto>()));

        await _controller.ListSubjects(new SubjectListQuery(), cts.Token);

        _useCaseMock.Verify(x => x.ExecuteAsync(It.IsAny<SubjectListQuery>(), cts.Token), Times.Once);
    }

    [Fact]
    public async Task ResourceNotFound_Returns404ProblemDetails()
    {
        _useCaseMock.Setup(x => x.ExecuteAsync(It.IsAny<SubjectListQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ListSubjectsResult.Failure(ErrorCodes.ResourceNotFound));

        var result = await _controller.ListSubjects(new SubjectListQuery(), CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var problemDetails = Assert.IsType<ProblemDetails>(notFoundResult.Value);
        Assert.Equal(404, problemDetails.Status);
    }

    [Fact]
    public async Task ProblemDetails_ContainsTraceIdAndErrorCode()
    {
        _useCaseMock.Setup(x => x.ExecuteAsync(It.IsAny<SubjectListQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ListSubjectsResult.Failure(ErrorCodes.ResourceNotFound));

        var result = await _controller.ListSubjects(new SubjectListQuery(), CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var problemDetails = Assert.IsType<ProblemDetails>(notFoundResult.Value);
        Assert.Equal("test-trace-id", problemDetails.Extensions["traceId"]);
        Assert.Equal(ErrorCodes.ResourceNotFound, problemDetails.Extensions["errorCode"]);
    }

    [Fact]
    public void Endpoint_HasAuthorizeAttribute()
    {
        var attributes = typeof(SubjectsController).GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true);
        Assert.NotEmpty(attributes);
    }

    [Fact]
    public void Route_IsApiV1Subjects()
    {
        var routeAttr = typeof(SubjectsController).GetCustomAttributes(typeof(Microsoft.AspNetCore.Mvc.RouteAttribute), true)
            .FirstOrDefault() as Microsoft.AspNetCore.Mvc.RouteAttribute;

        Assert.NotNull(routeAttr);
        Assert.Equal("api/v1/subjects", routeAttr.Template);
    }
}
