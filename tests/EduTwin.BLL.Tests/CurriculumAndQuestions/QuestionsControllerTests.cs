using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Xunit;
using EduTwin.API.Controllers;
using EduTwin.BLL.CurriculumAndQuestions;
using EduTwin.BLL.CurriculumAndQuestions.Import;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.BLL.Tests.CurriculumAndQuestions;

public class QuestionsControllerTests
{
    private readonly Mock<ICreateQuestionUseCase> _createUseCaseMock;
    private readonly Mock<IGetQuestionUseCase> _getUseCaseMock;
    private readonly Mock<IListQuestionsUseCase> _listUseCaseMock;
    private readonly Mock<IUpdateQuestionUseCase> _updateUseCaseMock;
    private readonly Mock<IActivateQuestionUseCase> _activateUseCaseMock;
    private readonly Mock<IArchiveQuestionUseCase> _archiveUseCaseMock;
    private readonly Mock<IDeleteQuestionUseCase> _deleteUseCaseMock;
    private readonly Mock<TimeProvider> _timeProviderMock;
    private readonly Mock<IQuestionImportUseCase> _importUseCaseMock = new();
    private readonly QuestionsController _sut;
    private readonly DateTimeOffset _fixedTime = new DateTimeOffset(2026, 7, 24, 14, 0, 0, TimeSpan.Zero);

    public QuestionsControllerTests()
    {
        _createUseCaseMock = new Mock<ICreateQuestionUseCase>();
        _getUseCaseMock = new Mock<IGetQuestionUseCase>();
        _listUseCaseMock = new Mock<IListQuestionsUseCase>();
        _updateUseCaseMock = new Mock<IUpdateQuestionUseCase>();
        _activateUseCaseMock = new Mock<IActivateQuestionUseCase>();
        _archiveUseCaseMock = new Mock<IArchiveQuestionUseCase>();
        _deleteUseCaseMock = new Mock<IDeleteQuestionUseCase>();
        _timeProviderMock = new Mock<TimeProvider>();
        _timeProviderMock.Setup(t => t.GetUtcNow()).Returns(_fixedTime);

        _sut = new QuestionsController(
            _createUseCaseMock.Object,
            _getUseCaseMock.Object,
            _listUseCaseMock.Object,
            _updateUseCaseMock.Object,
            _activateUseCaseMock.Object,
            _archiveUseCaseMock.Object,
            _deleteUseCaseMock.Object,
            _timeProviderMock.Object,
            _importUseCaseMock.Object);

        var httpContext = new DefaultHttpContext
        {
            TraceIdentifier = "test-trace-id"
        };
        httpContext.Request.Path = "/api/v1/questions";

        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };
    }

    [Fact]
    public async Task Create_Success_Returns201Envelope()
    {
        var request = new CreateQuestionRequest();
        var expectedDto = new QuestionDto { QuestionId = "123" };
        _createUseCaseMock.Setup(x => x.ExecuteAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(CreateQuestionResult.Success(expectedDto));

        var result = await _sut.Create(request, CancellationToken.None);

        var createdResult = Assert.IsType<CreatedResult>(result);
        var response = Assert.IsType<QuestionResponse>(createdResult.Value);
        Assert.Same(expectedDto, response.Data);
        Assert.Equal("test-trace-id", response.Meta.TraceId);
    }

    [Fact]
    public async Task Get_Success_Returns200Envelope()
    {
        var expectedDto = new QuestionDto { QuestionId = "123" };
        _getUseCaseMock.Setup(x => x.ExecuteAsync("123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetQuestionResult.Success(expectedDto));

        var result = await _sut.Get("123", CancellationToken.None);

        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<QuestionResponse>(okResult.Value);
        Assert.Same(expectedDto, response.Data);
    }

    [Fact]
    public async Task Delete_Success_Returns204NoContent()
    {
        _deleteUseCaseMock.Setup(x => x.ExecuteAsync("123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(DeleteQuestionResult.Success());

        var result = await _sut.Delete("123", CancellationToken.None);

        Assert.IsType<NoContentResult>(result);
    }

    [Fact]
    public async Task Error_ValidationFailed_Returns400()
    {
        _getUseCaseMock.Setup(x => x.ExecuteAsync("123", It.IsAny<CancellationToken>()))
            .ReturnsAsync(GetQuestionResult.Failure(ErrorCodes.ValidationFailed));

        var result = await _sut.Get("123", CancellationToken.None);

        var badRequestResult = Assert.IsType<BadRequestObjectResult>(result);
        var problemDetails = Assert.IsType<ProblemDetails>(badRequestResult.Value);
        Assert.Equal(400, problemDetails.Status);
    }

    [Fact]
    public async Task Error_InvalidStateTransition_Returns422()
    {
        var request = new ActivateQuestionRequest { RowVersion = "1" };
        _activateUseCaseMock.Setup(x => x.ExecuteAsync("123", request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(ActivateQuestionResult.Failure(ErrorCodes.InvalidStateTransition));

        var result = await _sut.Activate("123", request, CancellationToken.None);

        var unprocessableResult = Assert.IsType<UnprocessableEntityObjectResult>(result);
        var problemDetails = Assert.IsType<ProblemDetails>(unprocessableResult.Value);
        Assert.Equal(422, problemDetails.Status);
    }

    [Fact]
    public async Task PreviewImport_BindsBatchContextAndReturnsEnvelope()
    {
        var request = new QuestionImportPreviewRequest { SubjectId = Guid.NewGuid(), GradeLevel = 12 };
        using var bytes = new MemoryStream(Encoding.UTF8.GetBytes("data"));
        var file = new FormFile(bytes, 0, bytes.Length, "file", "questions.csv");
        var data = new QuestionImportPreviewDataDto { PreviewToken = "bound-token", SubjectId = request.SubjectId.ToString() };
        _importUseCaseMock.Setup(u => u.PreviewAsync(It.IsAny<Stream>(), "questions.csv", request, It.IsAny<CancellationToken>())).ReturnsAsync(QuestionImportPreviewResult.Success(data));
        var response = Assert.IsType<QuestionImportPreviewResponse>(Assert.IsType<OkObjectResult>(await _sut.PreviewImport(file, request, default)).Value);
        Assert.Same(data, response.Data); Assert.Equal("test-trace-id", response.Meta.TraceId);
    }

    [Fact]
    public async Task PreviewImport_OversizedFileRejectedBeforeOpeningStream()
    {
        var file = new Mock<IFormFile>(); file.SetupGet(f => f.Length).Returns(10 * 1024 * 1024 + 1);
        Assert.IsType<BadRequestObjectResult>(await _sut.PreviewImport(file.Object, new(), default));
        file.Verify(f => f.OpenReadStream(), Times.Never);
        _importUseCaseMock.Verify(u => u.PreviewAsync(It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<QuestionImportPreviewRequest>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PreviewImport_ForbiddenActorReturns403()
    {
        using var bytes = new MemoryStream(Encoding.UTF8.GetBytes("data"));
        var request = new QuestionImportPreviewRequest(); var file = new FormFile(bytes, 0, bytes.Length, "file", "questions.csv");
        _importUseCaseMock.Setup(u => u.PreviewAsync(It.IsAny<Stream>(), "questions.csv", request, It.IsAny<CancellationToken>())).ReturnsAsync(QuestionImportPreviewResult.Failure(ErrorCodes.ForbiddenResource, "Denied"));
        var response = Assert.IsType<ObjectResult>(await _sut.PreviewImport(file, request, default)); Assert.Equal(403, response.StatusCode);
    }

    [Fact]
    public async Task ConfirmImport_ReturnsDraftResultAndNotFoundDiagnostics()
    {
        var request = new QuestionImportConfirmRequest { PreviewToken = "token" };
        var data = new QuestionImportConfirmDataDto { ImportedCount = 2, SkippedCount = 1 };
        _importUseCaseMock.Setup(u => u.ConfirmAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync(QuestionImportConfirmResult.Success(data));
        var response = Assert.IsType<QuestionImportConfirmResponse>(Assert.IsType<OkObjectResult>(await _sut.ConfirmImport(request, default)).Value); Assert.Same(data, response.Data);
        _importUseCaseMock.Setup(u => u.ConfirmAsync(request, It.IsAny<CancellationToken>())).ReturnsAsync(QuestionImportConfirmResult.NotFound("Topic inactive"));
        var failure = Assert.IsType<ProblemDetails>(Assert.IsType<NotFoundObjectResult>(await _sut.ConfirmImport(request, default)).Value); Assert.Equal("Topic inactive", failure.Detail);
    }
}
