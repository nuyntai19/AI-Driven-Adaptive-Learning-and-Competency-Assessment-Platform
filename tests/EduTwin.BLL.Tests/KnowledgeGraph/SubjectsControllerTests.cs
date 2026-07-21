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
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.KnowledgeGraph;

namespace EduTwin.BLL.Tests.KnowledgeGraph;

public class SubjectsControllerTests
{
    private readonly Mock<IListSubjectsUseCase> _listUseCaseMock;
    private readonly Mock<ICreateSubjectUseCase> _createUseCaseMock;
    private readonly SubjectsController _controller;

    public SubjectsControllerTests()
    {
        _listUseCaseMock = new Mock<IListSubjectsUseCase>();
        _createUseCaseMock = new Mock<ICreateSubjectUseCase>();
        _controller = new SubjectsController(_listUseCaseMock.Object, _createUseCaseMock.Object, TimeProvider.System)
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
        _listUseCaseMock.Setup(x => x.ExecuteAsync(It.IsAny<SubjectListQuery>(), It.IsAny<CancellationToken>()))
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
        _listUseCaseMock.Setup(x => x.ExecuteAsync(It.IsAny<SubjectListQuery>(), It.IsAny<CancellationToken>()))
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
        _listUseCaseMock.Setup(x => x.ExecuteAsync(query, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ListSubjectsResult.Success(new List<SubjectDto>()));

        await _controller.ListSubjects(query, CancellationToken.None);

        _listUseCaseMock.Verify(x => x.ExecuteAsync(query, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancellationToken_IsPassedExactly()
    {
        using var cts = new CancellationTokenSource();
        _listUseCaseMock.Setup(x => x.ExecuteAsync(It.IsAny<SubjectListQuery>(), cts.Token))
            .ReturnsAsync(ListSubjectsResult.Success(new List<SubjectDto>()));

        await _controller.ListSubjects(new SubjectListQuery(), cts.Token);

        _listUseCaseMock.Verify(x => x.ExecuteAsync(It.IsAny<SubjectListQuery>(), cts.Token), Times.Once);
    }

    [Fact]
    public async Task ResourceNotFound_Returns404ProblemDetails()
    {
        _listUseCaseMock.Setup(x => x.ExecuteAsync(It.IsAny<SubjectListQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(ListSubjectsResult.Failure(ErrorCodes.ResourceNotFound));

        var result = await _controller.ListSubjects(new SubjectListQuery(), CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var problemDetails = Assert.IsType<ProblemDetails>(notFoundResult.Value);
        Assert.Equal(404, problemDetails.Status);
        Assert.Equal("Không tìm thấy dữ liệu", problemDetails.Title);
        Assert.Equal("Dữ liệu không tồn tại hoặc bạn không có quyền truy cập.", problemDetails.Detail);
    }

    [Fact]
    public async Task ProblemDetails_ContainsTraceIdAndErrorCode()
    {
        _listUseCaseMock.Setup(x => x.ExecuteAsync(It.IsAny<SubjectListQuery>(), It.IsAny<CancellationToken>()))
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

    [Fact]
    public async Task CreateSubject_Success_Returns201Created()
    {
        var request = new CreateSubjectRequest { SubjectCode = "A", SubjectName = "A" };
        var data = new SubjectDto { SubjectCode = "A" };

        _createUseCaseMock.Setup(x => x.ExecuteAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSubjectResult.Success(data));

        var result = await _controller.CreateSubject(request, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, objectResult.StatusCode);

        var type = objectResult.Value!.GetType();
        var dataProp = type.GetProperty("Data")?.GetValue(objectResult.Value) as SubjectDto;
        Assert.NotNull(dataProp);
        Assert.Equal("A", dataProp.SubjectCode);
    }

    [Fact]
    public async Task CreateSubject_Success_MetaContainsTraceIdAndTimestamp()
    {
        var request = new CreateSubjectRequest { SubjectCode = "A", SubjectName = "A" };
        var data = new SubjectDto { SubjectCode = "A" };

        _createUseCaseMock.Setup(x => x.ExecuteAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSubjectResult.Success(data));

        var result = await _controller.CreateSubject(request, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        var type = objectResult.Value!.GetType();
        var metaProp = type.GetProperty("Meta")?.GetValue(objectResult.Value) as EduTwin.Contracts.IdentityAndTenancy.MetaDto;

        Assert.NotNull(metaProp);
        Assert.Equal("test-trace-id", metaProp.TraceId);
        Assert.NotEqual(default, metaProp.Timestamp);
    }

    [Fact]
    public async Task CreateSubject_RequestAndToken_PassedToUseCase()
    {
        var request = new CreateSubjectRequest { SubjectCode = "A", SubjectName = "A" };
        using var cts = new CancellationTokenSource();
        var data = new SubjectDto { SubjectCode = "A" };

        _createUseCaseMock.Setup(x => x.ExecuteAsync(request, cts.Token))
            .ReturnsAsync(CreateSubjectResult.Success(data));

        await _controller.CreateSubject(request, cts.Token);

        _createUseCaseMock.Verify(x => x.ExecuteAsync(request, cts.Token), Times.Once);
    }

    [Fact]
    public async Task CreateSubject_ValidationFailed_Returns400()
    {
        var request = new CreateSubjectRequest();
        _createUseCaseMock.Setup(x => x.ExecuteAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSubjectResult.Failure(ErrorCodes.ValidationFailed));

        var result = await _controller.CreateSubject(request, CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var problemDetails = Assert.IsType<ProblemDetails>(badRequestResult.Value);
        Assert.Equal(400, problemDetails.Status);
        Assert.Equal(ErrorCodes.ValidationFailed, problemDetails.Extensions["errorCode"]);
        Assert.Equal("test-trace-id", problemDetails.Extensions["traceId"]);
        Assert.Equal("Dữ liệu không hợp lệ", problemDetails.Title);
        Assert.Equal("Thông tin môn học cung cấp không hợp lệ.", problemDetails.Detail);
    }

    [Fact]
    public async Task CreateSubject_ResourceNotFound_Returns404()
    {
        var request = new CreateSubjectRequest();
        _createUseCaseMock.Setup(x => x.ExecuteAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSubjectResult.Failure(ErrorCodes.ResourceNotFound));

        var result = await _controller.CreateSubject(request, CancellationToken.None);

        var notFoundResult = Assert.IsType<NotFoundObjectResult>(result);
        var problemDetails = Assert.IsType<ProblemDetails>(notFoundResult.Value);
        Assert.Equal(404, problemDetails.Status);
        Assert.Equal(ErrorCodes.ResourceNotFound, problemDetails.Extensions["errorCode"]);
        Assert.Equal("test-trace-id", problemDetails.Extensions["traceId"]);
        Assert.Equal("Không tìm thấy dữ liệu", problemDetails.Title);
        Assert.Equal("Dữ liệu không tồn tại hoặc bạn không có quyền truy cập.", problemDetails.Detail);
    }

    [Fact]
    public async Task CreateSubject_DuplicateResource_Returns409()
    {
        var request = new CreateSubjectRequest();
        _createUseCaseMock.Setup(x => x.ExecuteAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateSubjectResult.Failure(ErrorCodes.DuplicateResource));

        var result = await _controller.CreateSubject(request, CancellationToken.None);

        var conflictResult = Assert.IsType<ConflictObjectResult>(result);
        var problemDetails = Assert.IsType<ProblemDetails>(conflictResult.Value);
        Assert.Equal(409, problemDetails.Status);
        Assert.Equal(ErrorCodes.DuplicateResource, problemDetails.Extensions["errorCode"]);
        Assert.Equal("test-trace-id", problemDetails.Extensions["traceId"]);
        Assert.Equal("Dữ liệu đã tồn tại", problemDetails.Title);
        Assert.Equal("Môn học này đã tồn tại trong trung tâm.", problemDetails.Detail);
    }

    [Fact]
    public void CreateSubject_HasAuthorizeAttributeWithPolicy()
    {
        var method = typeof(SubjectsController).GetMethod("CreateSubject");
        var attr = method?.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true)
            .FirstOrDefault() as Microsoft.AspNetCore.Authorization.AuthorizeAttribute;

        Assert.NotNull(attr);
        Assert.Equal(AuthorizationPolicies.TeacherOrCenterManager, attr.Policy);
    }
}
