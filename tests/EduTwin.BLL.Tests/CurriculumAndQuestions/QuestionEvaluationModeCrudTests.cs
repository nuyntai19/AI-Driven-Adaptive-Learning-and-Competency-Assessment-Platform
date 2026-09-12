using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using EduTwin.BLL.AssessmentAndReasoning.PreliminaryGrading;
using EduTwin.BLL.CurriculumAndQuestions;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;

namespace EduTwin.BLL.Tests.CurriculumAndQuestions;

public class QuestionEvaluationModeCrudTests : IDisposable
{
    private readonly EduTwinDbContext _dbContext;
    private readonly Mock<ITenantContext> _tenantContextMock;
    private readonly Mock<ITenantIdAccessor> _tenantIdAccessorMock;
    private readonly Mock<TimeProvider> _timeProviderMock;
    private readonly MathAnswerNormalizer _normalizer = new();

    private readonly Guid _centerId = Guid.NewGuid();
    private readonly Guid _teacherId = Guid.NewGuid();
    private readonly Guid _subjectId = Guid.NewGuid();
    private readonly ulong _nodeId = 101UL;
    private readonly DateTimeOffset _fixedTime = new(2026, 9, 13, 0, 0, 0, TimeSpan.Zero);

    public QuestionEvaluationModeCrudTests()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
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
            Username = "teacher.math",
            DisplayName = "Teacher Math",
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
            CreatedAt = _fixedTime.UtcDateTime,
            UpdatedAt = _fixedTime.UtcDateTime
        };
        _dbContext.Subjects.Add(subject);

        var node = new KnowledgeNode
        {
            NodeId = _nodeId,
            CenterId = _centerId,
            SubjectId = _subjectId,
            NodeName = "Fractions",
            NodeCode = "FRAC-01",
            IsActive = true,
            CreatedAt = _fixedTime.UtcDateTime,
            UpdatedAt = _fixedTime.UtcDateTime
        };
        _dbContext.KnowledgeNodes.Add(node);

        _dbContext.SaveChanges();
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
        _dbContext.Dispose();
    }

    [Fact]
    public async Task CreateQuestion_MultipleChoice_WithNumericRational_ReturnsValidationFailed()
    {
        var sut = new CreateQuestionUseCase(_dbContext, _tenantContextMock.Object, _timeProviderMock.Object);
        var request = new CreateQuestionRequest
        {
            SubjectId = _subjectId,
            PrimaryTopicNodeId = _nodeId.ToString(),
            QuestionType = nameof(QuestionType.MultipleChoice),
            AnswerEvaluationMode = nameof(QuestionAnswerEvaluationMode.NumericRational),
            Difficulty = 2,
            QuestionText = "What is 1/2?",
            CorrectAnswer = "A",
            Solution = "It is A.",
            MaxScore = 1m,
            EstimatedTimeSeconds = 60,
            LanguageCode = "vi",
            Options = new List<QuestionOptionInput>
            {
                new() { OptionLabel = "A", OptionText = "0.5", IsCorrect = true, OrderIndex = 1 },
                new() { OptionLabel = "B", OptionText = "0.25", IsCorrect = false, OrderIndex = 2 }
            }
        };

        var result = await sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task CreateQuestion_Essay_WithTextExact_ReturnsValidationFailed()
    {
        var sut = new CreateQuestionUseCase(_dbContext, _tenantContextMock.Object, _timeProviderMock.Object);
        var request = new CreateQuestionRequest
        {
            SubjectId = _subjectId,
            PrimaryTopicNodeId = _nodeId.ToString(),
            QuestionType = nameof(QuestionType.Essay),
            AnswerEvaluationMode = nameof(QuestionAnswerEvaluationMode.TextExact),
            Difficulty = 3,
            QuestionText = "Prove Fermat's Last Theorem.",
            CorrectAnswer = "Proof",
            Solution = "Long proof",
            MaxScore = 10m,
            EstimatedTimeSeconds = 600,
            LanguageCode = "vi"
        };

        var result = await sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task CreateQuestion_ShortAnswer_WithNumericRational_Succeeds()
    {
        var sut = new CreateQuestionUseCase(_dbContext, _tenantContextMock.Object, _timeProviderMock.Object);
        var request = new CreateQuestionRequest
        {
            SubjectId = _subjectId,
            PrimaryTopicNodeId = _nodeId.ToString(),
            QuestionType = nameof(QuestionType.ShortAnswer),
            AnswerEvaluationMode = nameof(QuestionAnswerEvaluationMode.NumericRational),
            Difficulty = 2,
            QuestionText = "Calculate 1/4 + 1/4",
            CorrectAnswer = "1/2",
            Solution = "2/4 = 1/2",
            MaxScore = 1m,
            EstimatedTimeSeconds = 60,
            LanguageCode = "vi"
        };

        var result = await sut.ExecuteAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(nameof(QuestionAnswerEvaluationMode.NumericRational), result.Data.AnswerEvaluationMode);
    }

    [Fact]
    public async Task CreateQuestion_WithoutEvaluationMode_DefaultsByMatrix()
    {
        var sut = new CreateQuestionUseCase(_dbContext, _tenantContextMock.Object, _timeProviderMock.Object);

        // 1. MCQ defaults to TextExact
        var mcqRequest = new CreateQuestionRequest
        {
            SubjectId = _subjectId,
            PrimaryTopicNodeId = _nodeId.ToString(),
            QuestionType = nameof(QuestionType.MultipleChoice),
            Difficulty = 1,
            QuestionText = "Sample MCQ",
            CorrectAnswer = "A",
            Solution = "Solution",
            MaxScore = 1m,
            EstimatedTimeSeconds = 60,
            LanguageCode = "vi",
            Options = new List<QuestionOptionInput>
            {
                new() { OptionLabel = "A", OptionText = "Opt A", IsCorrect = true, OrderIndex = 1 },
                new() { OptionLabel = "B", OptionText = "Opt B", IsCorrect = false, OrderIndex = 2 }
            }
        };
        var mcqResult = await sut.ExecuteAsync(mcqRequest);
        Assert.True(mcqResult.IsSuccess);
        Assert.Equal(nameof(QuestionAnswerEvaluationMode.TextExact), mcqResult.Data!.AnswerEvaluationMode);

        // 2. Essay defaults to Manual
        var essayRequest = new CreateQuestionRequest
        {
            SubjectId = _subjectId,
            PrimaryTopicNodeId = _nodeId.ToString(),
            QuestionType = nameof(QuestionType.Essay),
            Difficulty = 3,
            QuestionText = "Sample Essay",
            CorrectAnswer = "Answer",
            Solution = "Solution",
            MaxScore = 5m,
            EstimatedTimeSeconds = 300,
            LanguageCode = "vi"
        };
        var essayResult = await sut.ExecuteAsync(essayRequest);
        Assert.True(essayResult.IsSuccess);
        Assert.Equal(nameof(QuestionAnswerEvaluationMode.Manual), essayResult.Data!.AnswerEvaluationMode);

        // 3. ShortAnswer defaults to TextExact
        var saRequest = new CreateQuestionRequest
        {
            SubjectId = _subjectId,
            PrimaryTopicNodeId = _nodeId.ToString(),
            QuestionType = nameof(QuestionType.ShortAnswer),
            Difficulty = 2,
            QuestionText = "Sample SA",
            CorrectAnswer = "Answer",
            Solution = "Solution",
            MaxScore = 2m,
            EstimatedTimeSeconds = 90,
            LanguageCode = "vi"
        };
        var saResult = await sut.ExecuteAsync(saRequest);
        Assert.True(saResult.IsSuccess);
        Assert.Equal(nameof(QuestionAnswerEvaluationMode.TextExact), saResult.Data!.AnswerEvaluationMode);
    }

    [Fact]
    public async Task UpdateQuestion_MultipleChoice_ToNumericRational_ReturnsValidationFailed()
    {
        var question = new Question
        {
            QuestionId = 1UL,
            CenterId = _centerId,
            SubjectId = _subjectId,
            PrimaryTopicNodeId = _nodeId,
            CreatedByTeacherId = _teacherId,
            QuestionType = QuestionType.MultipleChoice,
            AnswerEvaluationMode = QuestionAnswerEvaluationMode.TextExact,
            Difficulty = 1,
            QuestionText = "MCQ question",
            CorrectAnswer = "A",
            Solution = "Solution",
            MaxScore = 1m,
            EstimatedTimeSeconds = 60,
            LanguageCode = "vi",
            Status = QuestionStatus.Draft,
            RowVersion = 1UL,
            CreatedAt = _fixedTime.UtcDateTime,
            UpdatedAt = _fixedTime.UtcDateTime
        };
        _dbContext.Questions.Add(question);
        await _dbContext.SaveChangesAsync();

        var sut = new UpdateQuestionUseCase(_dbContext, _tenantContextMock.Object, _timeProviderMock.Object);
        var request = new UpdateQuestionRequest
        {
            PrimaryTopicNodeId = _nodeId.ToString(),
            QuestionType = nameof(QuestionType.MultipleChoice),
            AnswerEvaluationMode = nameof(QuestionAnswerEvaluationMode.NumericRational),
            Difficulty = 1,
            QuestionText = "MCQ question updated",
            CorrectAnswer = "A",
            Solution = "Solution",
            MaxScore = 1m,
            EstimatedTimeSeconds = 60,
            LanguageCode = "vi",
            RowVersion = "1"
        };

        var result = await sut.ExecuteAsync("1", request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task ActivateQuestion_Essay_WithNonManual_ReturnsValidationFailed()
    {
        var question = new Question
        {
            QuestionId = 2UL,
            CenterId = _centerId,
            SubjectId = _subjectId,
            PrimaryTopicNodeId = _nodeId,
            CreatedByTeacherId = _teacherId,
            QuestionType = QuestionType.Essay,
            AnswerEvaluationMode = QuestionAnswerEvaluationMode.TextExact, // Invalid state
            Difficulty = 3,
            QuestionText = "Essay question",
            CorrectAnswer = "Correct",
            Solution = "Solution",
            MaxScore = 5m,
            EstimatedTimeSeconds = 300,
            LanguageCode = "vi",
            Status = QuestionStatus.Draft,
            RowVersion = 1UL,
            CreatedAt = _fixedTime.UtcDateTime,
            UpdatedAt = _fixedTime.UtcDateTime
        };
        _dbContext.Questions.Add(question);
        await _dbContext.SaveChangesAsync();

        var sut = new ActivateQuestionUseCase(_dbContext, _tenantContextMock.Object, _timeProviderMock.Object, _normalizer);
        var result = await sut.ExecuteAsync("2", new ActivateQuestionRequest { RowVersion = "1" });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task ActivateQuestion_ShortAnswer_NumericRational_NonNormalizableAnswer_ReturnsValidationFailed()
    {
        var question = new Question
        {
            QuestionId = 3UL,
            CenterId = _centerId,
            SubjectId = _subjectId,
            PrimaryTopicNodeId = _nodeId,
            CreatedByTeacherId = _teacherId,
            QuestionType = QuestionType.ShortAnswer,
            AnswerEvaluationMode = QuestionAnswerEvaluationMode.NumericRational,
            Difficulty = 2,
            QuestionText = "Calculate something",
            CorrectAnswer = "invalid-number-string",
            Solution = "Solution",
            MaxScore = 2m,
            EstimatedTimeSeconds = 60,
            LanguageCode = "vi",
            Status = QuestionStatus.Draft,
            RowVersion = 1UL,
            CreatedAt = _fixedTime.UtcDateTime,
            UpdatedAt = _fixedTime.UtcDateTime
        };
        _dbContext.Questions.Add(question);
        await _dbContext.SaveChangesAsync();

        var sut = new ActivateQuestionUseCase(_dbContext, _tenantContextMock.Object, _timeProviderMock.Object, _normalizer);
        var result = await sut.ExecuteAsync("3", new ActivateQuestionRequest { RowVersion = "1" });

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task ActivateQuestion_ShortAnswer_NumericRational_ValidAnswer_Succeeds()
    {
        var question = new Question
        {
            QuestionId = 4UL,
            CenterId = _centerId,
            SubjectId = _subjectId,
            PrimaryTopicNodeId = _nodeId,
            CreatedByTeacherId = _teacherId,
            QuestionType = QuestionType.ShortAnswer,
            AnswerEvaluationMode = QuestionAnswerEvaluationMode.NumericRational,
            Difficulty = 2,
            QuestionText = "Calculate 1/2",
            CorrectAnswer = "1/2",
            Solution = "Solution",
            MaxScore = 2m,
            EstimatedTimeSeconds = 60,
            LanguageCode = "vi",
            Status = QuestionStatus.Draft,
            RowVersion = 1UL,
            CreatedAt = _fixedTime.UtcDateTime,
            UpdatedAt = _fixedTime.UtcDateTime
        };
        _dbContext.Questions.Add(question);
        await _dbContext.SaveChangesAsync();

        var sut = new ActivateQuestionUseCase(_dbContext, _tenantContextMock.Object, _timeProviderMock.Object, _normalizer);
        var result = await sut.ExecuteAsync("4", new ActivateQuestionRequest { RowVersion = "1" });

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(QuestionStatus.Active.ToString(), result.Data.Status);
    }
}
