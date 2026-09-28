using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;
using EduTwin.BLL.AssessmentAndReasoning.PreliminaryGrading;
using EduTwin.BLL.CurriculumAndQuestions.Import;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;

namespace EduTwin.BLL.Tests.CurriculumAndQuestions;

public sealed class QuestionImportUseCaseTests : IDisposable
{
    private readonly EduTwinDbContext _dbContext;
    private readonly Mock<ITenantContext> _tenantContextMock;
    private readonly Mock<ITenantIdAccessor> _tenantIdAccessorMock;
    private readonly Mock<TimeProvider> _timeProviderMock;
    private readonly MathAnswerNormalizer _mathNormalizer = new();
    private readonly CoordinateAnswerNormalizer _coordinateNormalizer;

    private readonly Guid _centerId = Guid.NewGuid();
    private readonly Guid _teacherId = Guid.NewGuid();
    private readonly Guid _subjectId = Guid.NewGuid();
    private readonly ulong _nodeId = 201UL;
    private readonly DateTimeOffset _fixedTime = new(2026, 9, 28, 12, 0, 0, TimeSpan.Zero);

    public QuestionImportUseCaseTests()
    {
        _coordinateNormalizer = new CoordinateAnswerNormalizer(_mathNormalizer);

        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _tenantContextMock = new Mock<ITenantContext>();
        _tenantContextMock.Setup(t => t.CenterId).Returns(_centerId);
        _tenantContextMock.Setup(t => t.IsResolved).Returns(true);
        _tenantContextMock.Setup(t => t.UserId).Returns(_teacherId);
        _tenantContextMock.Setup(t => t.Role).Returns(nameof(UserRole.Teacher));

        _tenantIdAccessorMock = new Mock<ITenantIdAccessor>();
        _tenantIdAccessorMock.Setup(t => t.CenterId).Returns(_centerId);

        _timeProviderMock = new Mock<TimeProvider>();
        _timeProviderMock.Setup(t => t.GetUtcNow()).Returns(_fixedTime);

        _dbContext = new EduTwinDbContext(options, _tenantIdAccessorMock.Object);
        _dbContext.Database.EnsureCreated();

        SeedBaselineData();
    }

    private void SeedBaselineData()
    {
        var user = new User
        {
            UserId = _teacherId,
            CenterId = _centerId,
            Username = "teacher.import",
            DisplayName = "Teacher Import",
            PasswordHash = "hash",
            RoleName = UserRole.Teacher,
            Status = UserStatus.Active,
            CreatedAt = _fixedTime.UtcDateTime,
            UpdatedAt = _fixedTime.UtcDateTime
        };
        _dbContext.Users.Add(user);

        var teacher = new Teacher
        {
            TeacherId = _teacherId,
            CenterId = _centerId,
            User = user,
            CreatedAt = _fixedTime.UtcDateTime,
            UpdatedAt = _fixedTime.UtcDateTime
        };
        _dbContext.Teachers.Add(teacher);

        var subject = new Subject
        {
            SubjectId = _subjectId,
            CenterId = _centerId,
            SubjectCode = "MATH",
            SubjectName = "Mathematics",
            IsActive = true,
            CreatedAt = _fixedTime.UtcDateTime,
            UpdatedAt = _fixedTime.UtcDateTime
        };
        _dbContext.Subjects.Add(subject);

        var node = new KnowledgeNode
        {
            NodeId = _nodeId,
            CenterId = _centerId,
            SubjectId = _subjectId,
            NodeName = "Coordinate Geometry",
            NodeCode = "GEOM-01",
            IsActive = true,
            CreatedAt = _fixedTime.UtcDateTime,
            UpdatedAt = _fixedTime.UtcDateTime
        };
        _dbContext.KnowledgeNodes.Add(node);

        _dbContext.SaveChanges();
    }

    [Fact]
    public async Task PreviewAsync_Csv_ParsesAnswerEvaluationModeCorrectly()
    {
        var sut = new QuestionImportUseCase(
            _dbContext,
            _tenantContextMock.Object,
            _timeProviderMock.Object,
            _mathNormalizer,
            _coordinateNormalizer);

        var csvContent = new StringBuilder()
            .AppendLine("QuestionText,QuestionType,AnswerEvaluationMode,CorrectAnswer,Solution,Difficulty,OptionA,OptionB")
            .AppendLine("What is half?,ShortAnswer,NumericRational,1/2,Solution text,3,,")
            .AppendLine("What is point A?,ShortAnswer,Coordinate2D,\"(1; 2)\",Solution text,3,,")
            .AppendLine("Capital of Vietnam?,ShortAnswer,TextExact,Hanoi,Solution text,3,,")
            .AppendLine("Write essay on AI?,Essay,Manual,Good points,Solution text,3,,")
            .AppendLine("Pick correct one,MultipleChoice,TextExact,A,Solution text,3,Correct,Incorrect")
            .ToString();

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
        var result = await sut.PreviewAsync(stream, "test.csv", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(5, result.Data.ValidQuestions.Count);

        Assert.Equal(QuestionAnswerEvaluationMode.NumericRational, result.Data.ValidQuestions[0].AnswerEvaluationMode);
        Assert.Equal(QuestionType.ShortAnswer, result.Data.ValidQuestions[0].QuestionType);

        Assert.Equal(QuestionAnswerEvaluationMode.Coordinate2D, result.Data.ValidQuestions[1].AnswerEvaluationMode);
        Assert.Equal(QuestionType.ShortAnswer, result.Data.ValidQuestions[1].QuestionType);

        Assert.Equal(QuestionAnswerEvaluationMode.TextExact, result.Data.ValidQuestions[2].AnswerEvaluationMode);
        Assert.Equal(QuestionType.ShortAnswer, result.Data.ValidQuestions[2].QuestionType);

        Assert.Equal(QuestionAnswerEvaluationMode.Manual, result.Data.ValidQuestions[3].AnswerEvaluationMode);
        Assert.Equal(QuestionType.Essay, result.Data.ValidQuestions[3].QuestionType);

        Assert.Equal(QuestionAnswerEvaluationMode.TextExact, result.Data.ValidQuestions[4].AnswerEvaluationMode);
        Assert.Equal(QuestionType.MultipleChoice, result.Data.ValidQuestions[4].QuestionType);
    }

    [Theory]
    [InlineData("MultipleChoice", "NumericRational")]
    [InlineData("Essay", "TextExact")]
    public async Task PreviewAsync_InvalidEvaluationModeForQuestionType_ReturnsValidationError(
        string questionType,
        string evaluationMode)
    {
        var sut = new QuestionImportUseCase(
            _dbContext,
            _tenantContextMock.Object,
            _timeProviderMock.Object,
            _mathNormalizer,
            _coordinateNormalizer);

        var csvContent = new StringBuilder()
            .AppendLine("QuestionText,QuestionType,AnswerEvaluationMode,CorrectAnswer,Solution,Difficulty,OptionA,OptionB")
            .AppendLine($"Test question,{questionType},{evaluationMode},A,Solution text,3,Opt 1,Opt 2")
            .ToString();

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
        var result = await sut.PreviewAsync(stream, "invalid_mode.csv", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Contains(result.Data.Errors, e => e.Field == "AnswerEvaluationMode");
    }

    [Fact]
    public async Task PreviewAsync_InvalidMathAnswerFormat_ReturnsValidationError()
    {
        var sut = new QuestionImportUseCase(
            _dbContext,
            _tenantContextMock.Object,
            _timeProviderMock.Object,
            _mathNormalizer,
            _coordinateNormalizer);

        var csvContent = new StringBuilder()
            .AppendLine("QuestionText,QuestionType,AnswerEvaluationMode,CorrectAnswer,Solution,Difficulty")
            .AppendLine("Calculate fraction,ShortAnswer,NumericRational,not_a_number,Solution text,3")
            .AppendLine("Find point,ShortAnswer,Coordinate2D,invalid_coord,Solution text,3")
            .ToString();

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
        var result = await sut.PreviewAsync(stream, "invalid_math.csv", CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.Errors.Count);
        Assert.Contains(result.Data.Errors, e => e.RowIndex == 2 && e.ErrorMessage.Contains("NumericRational"));
        Assert.Contains(result.Data.Errors, e => e.RowIndex == 3 && e.ErrorMessage.Contains("Coordinate2D"));
    }

    [Fact]
    public async Task ConfirmAsync_ImportsActiveQuestions_WithPreservedEvaluationModes()
    {
        var sut = new QuestionImportUseCase(
            _dbContext,
            _tenantContextMock.Object,
            _timeProviderMock.Object,
            _mathNormalizer,
            _coordinateNormalizer);

        var csvContent = new StringBuilder()
            .AppendLine("QuestionText,QuestionType,AnswerEvaluationMode,CorrectAnswer,Solution,Difficulty")
            .AppendLine("Half fraction,ShortAnswer,NumericRational,1/2,Solution,3")
            .AppendLine("Origin point,ShortAnswer,Coordinate2D,\"(0; 0)\",Solution,3")
            .ToString();

        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
        var previewResult = await sut.PreviewAsync(stream, "math_import.csv", CancellationToken.None);
        Assert.True(previewResult.IsSuccess);
        Assert.NotNull(previewResult.Data);

        var confirmRequest = new QuestionImportConfirmRequest
        {
            PreviewToken = previewResult.Data.PreviewToken,
            SubjectId = _subjectId,
            PrimaryTopicNodeId = _nodeId
        };

        var confirmResult = await sut.ConfirmAsync(confirmRequest, CancellationToken.None);

        Assert.True(confirmResult.IsSuccess);
        Assert.Equal(2, confirmResult.Data?.ImportedCount);

        var questions = await _dbContext.Questions
            .Where(q => q.CenterId == _centerId && q.SubjectId == _subjectId)
            .OrderBy(q => q.QuestionText)
            .ToListAsync();

        Assert.Equal(2, questions.Count);

        var qHalf = questions.First(q => q.QuestionText == "Half fraction");
        Assert.Equal(QuestionType.ShortAnswer, qHalf.QuestionType);
        Assert.Equal(QuestionAnswerEvaluationMode.NumericRational, qHalf.AnswerEvaluationMode);
        Assert.Equal(QuestionStatus.Active, qHalf.Status);

        var qOrigin = questions.First(q => q.QuestionText == "Origin point");
        Assert.Equal(QuestionType.ShortAnswer, qOrigin.QuestionType);
        Assert.Equal(QuestionAnswerEvaluationMode.Coordinate2D, qOrigin.AnswerEvaluationMode);
        Assert.Equal(QuestionStatus.Active, qOrigin.Status);
    }

    [Fact]
    public async Task ConfirmAsync_ItemFailingActivationGuard_FailsGracefullyWithoutCommitting()
    {
        var sut = new QuestionImportUseCase(
            _dbContext,
            _tenantContextMock.Object,
            _timeProviderMock.Object,
            _mathNormalizer,
            _coordinateNormalizer);

        var confirmRequest = new QuestionImportConfirmRequest
        {
            SubjectId = _subjectId,
            PrimaryTopicNodeId = _nodeId,
            Questions = new List<QuestionImportItemDto>
            {
                new()
                {
                    RowIndex = 2,
                    QuestionText = "Broken MC Question",
                    QuestionType = QuestionType.MultipleChoice,
                    AnswerEvaluationMode = QuestionAnswerEvaluationMode.Manual, // Invalid evaluation mode for MC
                    CorrectAnswer = "A",
                    Solution = "Solution",
                    Options = new List<QuestionOptionInput>()
                }
            }
        };

        var confirmResult = await sut.ConfirmAsync(confirmRequest, CancellationToken.None);

        Assert.False(confirmResult.IsSuccess);
        Assert.Equal("IMPORT_FAILED", confirmResult.ErrorCode);

        var count = await _dbContext.Questions.CountAsync(q => q.CenterId == _centerId);
        Assert.Equal(0, count);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }
}
