using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.API.Controllers;
using EduTwin.BLL.Organization;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.Organization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.Organization;

public class ClassesControllerTests
{
    private readonly Mock<IListClassesUseCase> _mockListClassesUseCase;
    private readonly Mock<IGetClassUseCase> _mockGetClassUseCase;
    private readonly Mock<TimeProvider> _mockTimeProvider;
    private readonly ClassesController _controller;

    public ClassesControllerTests()
    {
        _mockListClassesUseCase = new Mock<IListClassesUseCase>();
        _mockGetClassUseCase = new Mock<IGetClassUseCase>();
        _mockTimeProvider = new Mock<TimeProvider>();

        var utcNow = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        _mockTimeProvider.Setup(t => t.GetUtcNow()).Returns(utcNow);

        _controller = new ClassesController(_mockListClassesUseCase.Object, _mockGetClassUseCase.Object, _mockTimeProvider.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    TraceIdentifier = "test-trace-id"
                }
            }
        };
    }

    [Fact]
    public async Task Controller_SuccessMetaContainsTraceIdAndTimestamp()
    {
        var query = new ClassListQuery { Page = 2, PageSize = 10 };
        var cancellationToken = new CancellationToken();
        var data = new List<ClassDto> {
            new ClassDto
            {
                ClassId = "class-1",
                ClassName = "Class 1",
                AcademicYear = "2026-2027",
                Subject = new ClassSubjectDto { SubjectId = "sub-1", SubjectName = "Math" },
                Teacher = new ClassTeacherDto { TeacherId = "teach-1", DisplayName = "John" },
                Status = "Active",
                RowVersion = "1"
            }
        };
        var totalItems = 25;
        var totalPages = 3;

        var successResult = ListClassesResult.Success(data, totalItems, totalPages);

        _mockListClassesUseCase
            .Setup(u => u.ExecuteAsync(query, cancellationToken))
            .ReturnsAsync(successResult);

        var result = await _controller.ListClasses(query, cancellationToken);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<ClassListResponse>(okResult.Value);

        Assert.NotNull(response.Meta);
        Assert.Equal(2, response.Meta.Page);
        Assert.Equal(10, response.Meta.PageSize);
        Assert.Equal(25, response.Meta.TotalItems);
        Assert.Equal(3, response.Meta.TotalPages);

        Assert.False(string.IsNullOrWhiteSpace(response.Meta.TraceId));
        Assert.Equal("test-trace-id", response.Meta.TraceId);

        Assert.Equal(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), response.Meta.Timestamp);
    }

    [Fact]
    public async Task Controller_PassesCancellationToken()
    {
        var query = new ClassListQuery();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockListClassesUseCase
            .Setup(u => u.ExecuteAsync(query, cts.Token))
            .ReturnsAsync(ListClassesResult.Failure(ErrorCodes.ResourceNotFound));

        var result = await _controller.ListClasses(query, cts.Token);

        _mockListClassesUseCase.Verify(u => u.ExecuteAsync(query, cts.Token), Times.Once);
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetClass_Controller_SuccessMetaContainsTraceIdAndTimestamp()
    {
        var classId = Guid.NewGuid();
        var cancellationToken = new CancellationToken();
        var data = new ClassDto
        {
            ClassId = "class-1",
            ClassName = "Class 1",
            AcademicYear = "2026-2027",
            Subject = new ClassSubjectDto { SubjectId = "sub-1", SubjectName = "Math" },
            Teacher = new ClassTeacherDto { TeacherId = "teach-1", DisplayName = "John" },
            Status = "Active",
            RowVersion = "1"
        };

        var successResult = GetClassResult.Success(data);

        _mockGetClassUseCase
            .Setup(u => u.ExecuteAsync(classId, cancellationToken))
            .ReturnsAsync(successResult);

        var result = await _controller.GetClass(classId, cancellationToken);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var responseType = okResult.Value!.GetType();

        var returnedData = responseType.GetProperty("Data")!.GetValue(okResult.Value) as ClassDto;
        Assert.NotNull(returnedData);
        Assert.Equal(data.ClassId, returnedData.ClassId);
        Assert.Equal(data.ClassName, returnedData.ClassName);
        Assert.Equal(data.AcademicYear, returnedData.AcademicYear);
        Assert.Equal(data.Status, returnedData.Status);

        var meta = responseType.GetProperty("Meta")!.GetValue(okResult.Value) as EduTwin.Contracts.IdentityAndTenancy.MetaDto;
        Assert.NotNull(meta);
        Assert.False(string.IsNullOrWhiteSpace(meta.TraceId));
        Assert.Equal("test-trace-id", meta.TraceId);
        Assert.Equal(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), meta.Timestamp);
    }

    [Fact]
    public async Task GetClass_Controller_NotFound_ReturnsNotFoundObjectResult()
    {
        var classId = Guid.NewGuid();

        _mockGetClassUseCase
            .Setup(u => u.ExecuteAsync(classId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetClassResult.Failure(ErrorCodes.ResourceNotFound));

        var result = await _controller.GetClass(classId, CancellationToken.None);

        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task GetClass_Controller_MapsForbiddenResult()
    {
        var classId = Guid.NewGuid();

        _mockGetClassUseCase
            .Setup(u => u.ExecuteAsync(classId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetClassResult.Failure(ErrorCodes.ForbiddenResource));

        var result = await _controller.GetClass(classId, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    [Fact]
    public async Task GetClass_Controller_UnexpectedErrorCode_ThrowsInvalidOperationException()
    {
        var classId = Guid.NewGuid();

        _mockGetClassUseCase
            .Setup(u => u.ExecuteAsync(classId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetClassResult.Failure("UNKNOWN_CODE"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _controller.GetClass(classId, CancellationToken.None));
    }

    [Fact]
    public async Task GetClass_Controller_PassesCancellationToken()
    {
        var classId = Guid.NewGuid();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockGetClassUseCase
            .Setup(u => u.ExecuteAsync(classId, cts.Token))
            .ReturnsAsync(GetClassResult.Failure(ErrorCodes.ResourceNotFound));

        var result = await _controller.GetClass(classId, cts.Token);

        _mockGetClassUseCase.Verify(u => u.ExecuteAsync(classId, cts.Token), Times.Once);
        Assert.IsType<NotFoundObjectResult>(result);
    }
}
