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
    private readonly Mock<ICreateClassUseCase> _mockCreateClassUseCase;
    private readonly Mock<IUpdateClassUseCase> _mockUpdateClassUseCase;
    private readonly Mock<TimeProvider> _mockTimeProvider;
    private readonly ClassesController _controller;

    public ClassesControllerTests()
    {
        _mockListClassesUseCase = new Mock<IListClassesUseCase>();
        _mockGetClassUseCase = new Mock<IGetClassUseCase>();
        _mockCreateClassUseCase = new Mock<ICreateClassUseCase>();
        _mockUpdateClassUseCase = new Mock<IUpdateClassUseCase>();
        _mockTimeProvider = new Mock<TimeProvider>();

        var utcNow = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);
        _mockTimeProvider.Setup(t => t.GetUtcNow()).Returns(utcNow);

        _controller = new ClassesController(
            _mockListClassesUseCase.Object,
            _mockGetClassUseCase.Object,
            _mockCreateClassUseCase.Object,
            _mockUpdateClassUseCase.Object,
            _mockTimeProvider.Object)
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

    [Fact]
    public async Task CreateClass_Controller_Success_Returns201Created()
    {
        var request = new CreateClassRequest { ClassName = "Class 1", AcademicYear = "2026", SubjectId = Guid.NewGuid(), TeacherId = Guid.NewGuid() };
        var cancellationToken = new CancellationToken();
        var data = new ClassDto
        {
            ClassId = "class-1",
            ClassName = "Class 1",
            AcademicYear = "2026",
            Subject = new ClassSubjectDto { SubjectId = "sub-1", SubjectName = "Math" },
            Teacher = new ClassTeacherDto { TeacherId = "teach-1", DisplayName = "John" },
            Status = "Active",
            RowVersion = "1",
            StudentCount = 0
        };

        _mockCreateClassUseCase
            .Setup(u => u.ExecuteAsync(request, cancellationToken))
            .ReturnsAsync(CreateClassResult.Success(data));

        var result = await _controller.CreateClass(request, cancellationToken);

        var createdResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status201Created, createdResult.StatusCode);

        var responseType = createdResult.Value!.GetType();
        var returnedData = responseType.GetProperty("Data")!.GetValue(createdResult.Value) as ClassDto;
        Assert.NotNull(returnedData);
        Assert.Equal(data.ClassId, returnedData.ClassId);

        var meta = responseType.GetProperty("Meta")!.GetValue(createdResult.Value) as EduTwin.Contracts.IdentityAndTenancy.MetaDto;
        Assert.NotNull(meta);
        Assert.Equal("test-trace-id", meta.TraceId);
        Assert.Equal(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), meta.Timestamp);
    }

    [Fact]
    public async Task CreateClass_Controller_ValidationFailed_ReturnsBadRequest()
    {
        var request = new CreateClassRequest();
        _mockCreateClassUseCase
            .Setup(u => u.ExecuteAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClassResult.Failure(ErrorCodes.ValidationFailed));

        var result = await _controller.CreateClass(request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
    }

    [Fact]
    public async Task CreateClass_Controller_NotFound_ReturnsNotFound()
    {
        var request = new CreateClassRequest();
        _mockCreateClassUseCase
            .Setup(u => u.ExecuteAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClassResult.Failure(ErrorCodes.ResourceNotFound));

        var result = await _controller.CreateClass(request, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(notFound.Value);
        Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
    }

    [Fact]
    public async Task CreateClass_Controller_Duplicate_ReturnsConflict()
    {
        var request = new CreateClassRequest();
        _mockCreateClassUseCase
            .Setup(u => u.ExecuteAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClassResult.Failure(ErrorCodes.DuplicateResource));

        var result = await _controller.CreateClass(request, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(conflict.Value);
        Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
    }

    [Fact]
    public async Task CreateClass_Controller_UnexpectedErrorCode_Throws()
    {
        var request = new CreateClassRequest();
        _mockCreateClassUseCase
            .Setup(u => u.ExecuteAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateClassResult.Failure("UNKNOWN"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _controller.CreateClass(request, CancellationToken.None));
    }

    [Fact]
    public async Task CreateClass_Controller_PassesCancellationToken()
    {
        var request = new CreateClassRequest();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockCreateClassUseCase
            .Setup(u => u.ExecuteAsync(request, cts.Token))
            .ReturnsAsync(CreateClassResult.Failure(ErrorCodes.ValidationFailed));

        await _controller.CreateClass(request, cts.Token);

        _mockCreateClassUseCase.Verify(u => u.ExecuteAsync(request, cts.Token), Times.Once);
    }

    [Fact]
    public async Task UpdateClass_Controller_Success_Returns200Ok()
    {
        var classId = Guid.NewGuid();
        var request = new UpdateClassRequest { ClassName = "Class 1", TeacherId = Guid.NewGuid(), Status = ClassStatus.Active, RowVersion = "1" };
        var cancellationToken = new CancellationToken();
        var data = new ClassDto
        {
            ClassId = classId.ToString(),
            ClassName = "Class 1",
            AcademicYear = "2026",
            Subject = new ClassSubjectDto { SubjectId = "sub-1", SubjectName = "Math" },
            Teacher = new ClassTeacherDto { TeacherId = "teach-1", DisplayName = "John" },
            Status = "Active",
            RowVersion = "2",
            StudentCount = 0
        };

        _mockUpdateClassUseCase
            .Setup(u => u.ExecuteAsync(classId, request, cancellationToken))
            .ReturnsAsync(UpdateClassResult.Success(data));

        var result = await _controller.UpdateClass(classId, request, cancellationToken);

        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, okResult.StatusCode);

        var responseType = okResult.Value!.GetType();
        var returnedData = responseType.GetProperty("Data")!.GetValue(okResult.Value) as ClassDto;
        Assert.NotNull(returnedData);
        Assert.Equal(data.ClassId, returnedData.ClassId);

        var meta = responseType.GetProperty("Meta")!.GetValue(okResult.Value) as EduTwin.Contracts.IdentityAndTenancy.MetaDto;
        Assert.NotNull(meta);
        Assert.Equal("test-trace-id", meta.TraceId);
        Assert.Equal(new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc), meta.Timestamp);
    }

    [Fact]
    public async Task UpdateClass_Controller_ValidationFailed_ReturnsBadRequest()
    {
        var classId = Guid.NewGuid();
        var request = new UpdateClassRequest();
        _mockUpdateClassUseCase
            .Setup(u => u.ExecuteAsync(classId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateClassResult.Failure(ErrorCodes.ValidationFailed));

        var result = await _controller.UpdateClass(classId, request, CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
    }

    [Fact]
    public async Task UpdateClass_Controller_NotFound_ReturnsNotFound()
    {
        var classId = Guid.NewGuid();
        var request = new UpdateClassRequest();
        _mockUpdateClassUseCase
            .Setup(u => u.ExecuteAsync(classId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateClassResult.Failure(ErrorCodes.ResourceNotFound));

        var result = await _controller.UpdateClass(classId, request, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(notFound.Value);
        Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
    }

    [Fact]
    public async Task UpdateClass_Controller_Duplicate_ReturnsConflict()
    {
        var classId = Guid.NewGuid();
        var request = new UpdateClassRequest();
        _mockUpdateClassUseCase
            .Setup(u => u.ExecuteAsync(classId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateClassResult.Failure(ErrorCodes.DuplicateResource));

        var result = await _controller.UpdateClass(classId, request, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(conflict.Value);
        Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
    }

    [Fact]
    public async Task UpdateClass_Controller_ConcurrencyConflict_ReturnsConflict()
    {
        var classId = Guid.NewGuid();
        var request = new UpdateClassRequest();
        _mockUpdateClassUseCase
            .Setup(u => u.ExecuteAsync(classId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateClassResult.Failure(ErrorCodes.ConcurrencyConflict));

        var result = await _controller.UpdateClass(classId, request, CancellationToken.None);

        var conflict = Assert.IsType<ConflictObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(conflict.Value);
        Assert.Equal(StatusCodes.Status409Conflict, problem.Status);
    }

    [Fact]
    public async Task UpdateClass_Controller_UnexpectedErrorCode_Throws()
    {
        var classId = Guid.NewGuid();
        var request = new UpdateClassRequest();
        _mockUpdateClassUseCase
            .Setup(u => u.ExecuteAsync(classId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(UpdateClassResult.Failure("UNKNOWN"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _controller.UpdateClass(classId, request, CancellationToken.None));
    }

    [Fact]
    public async Task UpdateClass_Controller_PassesCancellationToken()
    {
        var classId = Guid.NewGuid();
        var request = new UpdateClassRequest();
        var cts = new CancellationTokenSource();
        cts.Cancel();

        _mockUpdateClassUseCase
            .Setup(u => u.ExecuteAsync(classId, request, cts.Token))
            .ReturnsAsync(UpdateClassResult.Failure(ErrorCodes.ValidationFailed));

        await _controller.UpdateClass(classId, request, cts.Token);

        _mockUpdateClassUseCase.Verify(u => u.ExecuteAsync(classId, request, cts.Token), Times.Once);
    }

    [Fact]
    public async Task AddStudents_Controller_Success_Returns200OK()
    {
        var classId = Guid.NewGuid();
        var request = new AddStudentsToClassRequest { StudentIds = new[] { Guid.NewGuid() } };
        var cancellationToken = new CancellationToken();

        var mockUseCase = new Mock<IAddStudentsToClassUseCase>();
        var dto = new AddStudentsToClassDto { ClassId = classId.ToString(), AddedCount = 1, AlreadyMemberCount = 0 };
        mockUseCase.Setup(u => u.ExecuteAsync(classId, request, cancellationToken))
            .ReturnsAsync(AddStudentsToClassResult.Success(dto));

        var result = await _controller.AddStudents(classId, request, mockUseCase.Object, cancellationToken);
        var objectResult = Assert.IsType<OkObjectResult>(result);
        Assert.Equal(StatusCodes.Status200OK, objectResult.StatusCode);

        var responseType = objectResult.Value!.GetType();

        var returnedData =
            responseType.GetProperty("Data")!.GetValue(objectResult.Value)
            as AddStudentsToClassDto;

        Assert.NotNull(returnedData);
        Assert.Equal(dto.ClassId, returnedData.ClassId);
        Assert.Equal(dto.AddedCount, returnedData.AddedCount);
        Assert.Equal(dto.AlreadyMemberCount, returnedData.AlreadyMemberCount);

        var meta =
            responseType.GetProperty("Meta")!.GetValue(objectResult.Value)
            as EduTwin.Contracts.IdentityAndTenancy.MetaDto;

        Assert.NotNull(meta);
        Assert.Equal("test-trace-id", meta.TraceId);
        Assert.Equal(
            new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            meta.Timestamp);
    }
    [Fact]
    public async Task AddStudents_Controller_ValidationFailed_ReturnsBadRequest()
    {
        var classId = Guid.NewGuid();
        var request = new AddStudentsToClassRequest { StudentIds = new[] { Guid.NewGuid() } };

        var mockUseCase = new Mock<IAddStudentsToClassUseCase>();
        mockUseCase.Setup(u => u.ExecuteAsync(classId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AddStudentsToClassResult.Failure(ErrorCodes.ValidationFailed));

        var result = await _controller.AddStudents(classId, request, mockUseCase.Object, CancellationToken.None);
        var objectResult = Assert.IsType<BadRequestObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
    }

    [Fact]
    public async Task AddStudents_Controller_Forbidden_Returns403()
    {
        var classId = Guid.NewGuid();
        var request = new AddStudentsToClassRequest { StudentIds = new[] { Guid.NewGuid() } };

        var mockUseCase = new Mock<IAddStudentsToClassUseCase>();
        mockUseCase.Setup(u => u.ExecuteAsync(classId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AddStudentsToClassResult.Failure(ErrorCodes.ForbiddenResource));

        var result = await _controller.AddStudents(classId, request, mockUseCase.Object, CancellationToken.None);
        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status403Forbidden, objectResult.StatusCode);
    }

    [Fact]
    public async Task AddStudents_Controller_NotFound_Returns404()
    {
        var classId = Guid.NewGuid();
        var request = new AddStudentsToClassRequest { StudentIds = new[] { Guid.NewGuid() } };

        var mockUseCase = new Mock<IAddStudentsToClassUseCase>();
        mockUseCase.Setup(u => u.ExecuteAsync(classId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AddStudentsToClassResult.Failure(ErrorCodes.ResourceNotFound));

        var result = await _controller.AddStudents(classId, request, mockUseCase.Object, CancellationToken.None);
        Assert.IsType<NotFoundObjectResult>(result);
    }

    [Fact]
    public async Task AddStudents_Controller_UnexpectedError_Throws()
    {
        var classId = Guid.NewGuid();
        var request = new AddStudentsToClassRequest { StudentIds = new[] { Guid.NewGuid() } };

        var mockUseCase = new Mock<IAddStudentsToClassUseCase>();
        mockUseCase.Setup(u => u.ExecuteAsync(classId, request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(AddStudentsToClassResult.Failure("UNKNOWN"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _controller.AddStudents(classId, request, mockUseCase.Object, CancellationToken.None));
    }

    [Fact]
    public async Task AddStudents_Controller_PassesExactCancellationToken()
    {
        var classId = Guid.NewGuid();
        var request = new AddStudentsToClassRequest
        {
            StudentIds = new[] { Guid.NewGuid() }
        };

        using var cts = new CancellationTokenSource();
        var exactToken = cts.Token;

        var mockUseCase = new Mock<IAddStudentsToClassUseCase>();
        mockUseCase
            .Setup(u => u.ExecuteAsync(classId, request, exactToken))
            .ReturnsAsync(
                AddStudentsToClassResult.Failure(
                    ErrorCodes.ResourceNotFound));

        await _controller.AddStudents(
            classId,
            request,
            mockUseCase.Object,
            exactToken);

        mockUseCase.Verify(
            u => u.ExecuteAsync(classId, request, exactToken),
            Times.Once);
    }
}
