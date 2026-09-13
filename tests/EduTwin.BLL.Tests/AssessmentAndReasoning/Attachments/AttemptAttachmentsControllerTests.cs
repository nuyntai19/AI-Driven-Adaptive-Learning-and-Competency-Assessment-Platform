using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.API.AssessmentAndReasoning.Attachments;
using EduTwin.API.Controllers;
using EduTwin.BLL.AssessmentAndReasoning.Attachments;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Common;
using EduTwin.DAL.AssessmentAndReasoning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Attachments;

public sealed class AttemptAttachmentsControllerTests
{
    private readonly Mock<IPrepareAttemptAttachmentUploadUseCase> _prepareUploadMock = new();
    private readonly Mock<IGetAttemptAttachmentUseCase> _getAttachmentMock = new();
    private readonly Mock<IAttemptAttachmentStorage> _storageMock = new();
    private readonly Mock<TimeProvider> _timeProviderMock = new();
    private static readonly DateTime FixedUtcNow = new(2026, 9, 13, 10, 0, 0, DateTimeKind.Utc);

    private readonly AttemptAttachmentsController _controller;

    public AttemptAttachmentsControllerTests()
    {
        _timeProviderMock.Setup(t => t.GetUtcNow()).Returns(new DateTimeOffset(FixedUtcNow));
        _controller = new AttemptAttachmentsController(
            _prepareUploadMock.Object,
            _getAttachmentMock.Object,
            _storageMock.Object,
            _timeProviderMock.Object);
    }

    [Fact]
    public async Task PrepareUpload_NonMultipart_Returns400BadRequest()
    {
        var httpContext = new DefaultHttpContext();
        httpContext.Request.ContentType = "application/json";
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await _controller.PrepareUpload(CancellationToken.None);

        var badRequest = Assert.IsType<BadRequestObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(badRequest.Value);
        Assert.Equal(ErrorCodes.ValidationFailed, problem.Extensions["errorCode"]?.ToString());
    }

    [Fact]
    public async Task PrepareUpload_ValidPngMultipart_Returns200WithToken()
    {
        var boundary = "---------------------------974767299852498929531610575";
        var dummyPng = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R' };

        var memoryStream = new MemoryStream();
        var header = $"--{boundary}\r\nContent-Disposition: form-data; name=\"file\"; filename=\"scratchpad.png\"\r\nContent-Type: image/png\r\n\r\n";
        memoryStream.Write(Encoding.UTF8.GetBytes(header));
        memoryStream.Write(dummyPng);
        var footer = $"\r\n--{boundary}--\r\n";
        memoryStream.Write(Encoding.UTF8.GetBytes(footer));
        memoryStream.Position = 0;

        var httpContext = new DefaultHttpContext();
        httpContext.Request.ContentType = $"multipart/form-data; boundary={boundary}";
        httpContext.Request.Body = memoryStream;
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        _prepareUploadMock
            .Setup(u => u.ExecuteAsync(It.IsAny<Stream>(), "scratchpad.png", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PrepareAttemptAttachmentUploadResult.Success("test-token-123", FixedUtcNow.AddHours(24)));

        var result = await _controller.PrepareUpload(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<PrepareAttemptAttachmentUploadResponse>(okResult.Value);
        Assert.Equal("test-token-123", response.Data.DrawingUploadToken);
    }

    [Fact]
    public async Task PrepareUpload_PipeStreamMultipart_Returns200WithToken()
    {
        var boundary = "---------------------------974767299852498929531610575";
        var dummyPng = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, (byte)'I', (byte)'H', (byte)'D', (byte)'R' };

        var memoryStream = new MemoryStream();
        var header = $"--{boundary}\r\nContent-Disposition: form-data; name=\"file\"; filename=\"scratchpad.png\"\r\nContent-Type: image/png\r\n\r\n";
        memoryStream.Write(Encoding.UTF8.GetBytes(header));
        memoryStream.Write(dummyPng);
        var footer = $"\r\n--{boundary}--\r\n";
        memoryStream.Write(Encoding.UTF8.GetBytes(footer));

        var pipe = new System.IO.Pipelines.Pipe();
        await pipe.Writer.WriteAsync(memoryStream.ToArray());
        await pipe.Writer.CompleteAsync();

        var httpContext = new DefaultHttpContext();
        httpContext.Request.ContentType = $"multipart/form-data; boundary={boundary}";
        httpContext.Request.Body = pipe.Reader.AsStream();
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        _prepareUploadMock
            .Setup(u => u.ExecuteAsync(It.IsAny<Stream>(), "scratchpad.png", It.IsAny<CancellationToken>()))
            .ReturnsAsync(PrepareAttemptAttachmentUploadResult.Success("test-token-123", FixedUtcNow.AddHours(24)));

        var result = await _controller.PrepareUpload(CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<PrepareAttemptAttachmentUploadResponse>(okResult.Value);
        Assert.Equal("test-token-123", response.Data.DrawingUploadToken);
    }

    [Fact]
    public async Task PrepareUpload_Exceeds5Mb_Returns413PayloadTooLarge()
    {
        var boundary = "---------------------------974767299852498929531610575";
        // Create payload exceeding 5 MB
        var oversizedPng = new byte[FileSystemAttemptAttachmentStorage.MaxFileBytes + 1024];

        var memoryStream = new MemoryStream();
        var header = $"--{boundary}\r\nContent-Disposition: form-data; name=\"file\"; filename=\"large.png\"\r\nContent-Type: image/png\r\n\r\n";
        memoryStream.Write(Encoding.UTF8.GetBytes(header));
        memoryStream.Write(oversizedPng);
        var footer = $"\r\n--{boundary}--\r\n";
        memoryStream.Write(Encoding.UTF8.GetBytes(footer));
        memoryStream.Position = 0;

        var httpContext = new DefaultHttpContext();
        httpContext.Request.ContentType = $"multipart/form-data; boundary={boundary}";
        httpContext.Request.Body = memoryStream;
        _controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var result = await _controller.PrepareUpload(CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status413PayloadTooLarge, objectResult.StatusCode);
    }

    [Fact]
    public async Task Download_ZeroAttemptId_Returns404NotFound()
    {
        _controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var result = await _controller.Download(0, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(notFound.Value);
        Assert.Equal(ErrorCodes.ResourceNotFound, problem.Extensions["errorCode"]?.ToString());
    }

    [Fact]
    public async Task Download_NotFoundAttachment_Returns404NotFound()
    {
        _controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        _getAttachmentMock
            .Setup(g => g.ExecuteAsync(42UL, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetAttemptAttachmentResult.NotFoundResult());

        var result = await _controller.Download(42UL, CancellationToken.None);

        var notFound = Assert.IsType<NotFoundObjectResult>(result);
        var problem = Assert.IsType<ProblemDetails>(notFound.Value);
        Assert.Equal(ErrorCodes.ResourceNotFound, problem.Extensions["errorCode"]?.ToString());
    }

    [Fact]
    public async Task Download_StorageFileNotFound_Returns503ServiceUnavailable()
    {
        _controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var descriptor = new AttemptAttachmentDescriptor("tenants/x/perm/y.png", "scratchpad.png", "image/png");

        _getAttachmentMock
            .Setup(g => g.ExecuteAsync(42UL, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetAttemptAttachmentResult.Success(descriptor));

        _storageMock
            .Setup(s => s.OpenPermanentReadAsync(descriptor.StorageKey, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new FileNotFoundException("Missing blob"));

        var result = await _controller.Download(42UL, CancellationToken.None);

        var objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, objectResult.StatusCode);
        var problem = Assert.IsType<ProblemDetails>(objectResult.Value);
        Assert.Equal(ErrorCodes.StorageUnavailable, problem.Extensions["errorCode"]?.ToString());
    }

    [Fact]
    public async Task Download_ValidAttachment_ReturnsFileStreamResult()
    {
        _controller.ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() };

        var descriptor = new AttemptAttachmentDescriptor("tenants/x/perm/y.png", "scratchpad.png", "image/png");

        _getAttachmentMock
            .Setup(g => g.ExecuteAsync(42UL, It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetAttemptAttachmentResult.Success(descriptor));

        var fakeStream = new MemoryStream(new byte[] { 1, 2, 3 });
        _storageMock
            .Setup(s => s.OpenPermanentReadAsync(descriptor.StorageKey, It.IsAny<CancellationToken>()))
            .ReturnsAsync(fakeStream);

        var result = await _controller.Download(42UL, CancellationToken.None);

        var fileResult = Assert.IsType<FileStreamResult>(result);
        Assert.Equal("image/png", fileResult.ContentType);
        Assert.Equal("scratchpad.png", fileResult.FileDownloadName);
    }
}
