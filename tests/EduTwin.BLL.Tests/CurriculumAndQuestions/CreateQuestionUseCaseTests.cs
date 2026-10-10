using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;
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

public class CreateQuestionUseCaseTests : IDisposable
{
    private readonly EduTwinDbContext _dbContext;
    private readonly Mock<ITenantContext> _tenantContextMock;
    private readonly Mock<ITenantIdAccessor> _tenantIdAccessorMock;
    private readonly Mock<TimeProvider> _timeProviderMock;
    private readonly CreateQuestionUseCase _sut;

    private readonly Guid _centerId = Guid.NewGuid();
    private readonly Guid _teacherId = Guid.NewGuid();
    private readonly Guid _subjectId = Guid.NewGuid();
    private readonly DateTimeOffset _fixedTime = new DateTimeOffset(2026, 7, 24, 14, 0, 0, TimeSpan.Zero);

    public CreateQuestionUseCaseTests()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;

        _tenantContextMock = new Mock<ITenantContext>();
        _tenantContextMock.Setup(t => t.CenterId).Returns(_centerId);

        _tenantIdAccessorMock = new Mock<ITenantIdAccessor>();
        _tenantIdAccessorMock.Setup(t => t.CenterId).Returns(_centerId);

        _dbContext = new EduTwinDbContext(options, _tenantIdAccessorMock.Object);
        _dbContext.Database.EnsureCreated();

        _tenantContextMock.Setup(t => t.IsResolved).Returns(true);
        _tenantContextMock.Setup(t => t.UserId).Returns(_teacherId);
        _tenantContextMock.Setup(t => t.Role).Returns(nameof(UserRole.Teacher));

        _timeProviderMock = new Mock<TimeProvider>();
        _timeProviderMock.Setup(t => t.GetUtcNow()).Returns(_fixedTime);

        _sut = new CreateQuestionUseCase(_dbContext, _tenantContextMock.Object, _timeProviderMock.Object);
    }

    private async Task SeedDataAsync(bool includeValidTopic = true)
    {
        var now = _fixedTime.UtcDateTime;
        var user = new User 
        { 
            CenterId = _centerId, 
            UserId = _teacherId, 
            Status = UserStatus.Active, 
            CreatedAt = now, 
            UpdatedAt = now,
            Username = "test_teacher",
            DisplayName = "Test Teacher",
            PasswordHash = "hash",
            RoleName = UserRole.Teacher
        };
        var teacher = new Teacher { CenterId = _centerId, TeacherId = _teacherId, User = user, CreatedAt = now, UpdatedAt = now };
        var subject = new Subject { CenterId = _centerId, SubjectId = _subjectId, SubjectCode = "S1", SubjectName = "S1", IsActive = true, CreatedAt = now, UpdatedAt = now };

        _dbContext.Users.Add(user);
        _dbContext.Teachers.Add(teacher);
        _dbContext.Subjects.Add(subject);

        if (includeValidTopic)
        {
            var node = new KnowledgeNode
            {
                CenterId = _centerId,
                NodeId = 1,
                SubjectId = _subjectId,
                NodeCode = "N1",
                NodeName = "N1",
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now
            };
            _dbContext.KnowledgeNodes.Add(node);
        }

        await _dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task Create_ValidShortAnswer_ReturnsSuccess()
    {
        await SeedDataAsync();

        var request = new CreateQuestionRequest
        {
            SubjectId = _subjectId,
            PrimaryTopicNodeId = "1",
            QuestionType = "ShortAnswer",
            Difficulty = 3,
            QuestionText = "Text",
            CorrectAnswer = "Ans",
            Solution = "Sol",
            MaxScore = 1,
            EstimatedTimeSeconds = 60,
            LanguageCode = "vi",
            GradeLevel = 10
        };

        var result = await _sut.ExecuteAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal("ShortAnswer", result.Data.QuestionType);
        Assert.Equal("1", result.Data.PrimaryTopicNodeId);
        Assert.Empty(result.Data.Options);
    }

    [Fact]
    public async Task Create_MultipleChoice_WithoutOptions_ReturnsValidationFailed()
    {
        await SeedDataAsync();

        var request = new CreateQuestionRequest
        {
            SubjectId = _subjectId,
            PrimaryTopicNodeId = "1",
            QuestionType = "MultipleChoice",
            Difficulty = 3,
            QuestionText = "Text",
            CorrectAnswer = "Ans",
            Solution = "Sol",
            MaxScore = 1,
            EstimatedTimeSeconds = 60,
            LanguageCode = "vi",
            GradeLevel = 10,
            Options = new List<QuestionOptionInput>() // Empty options
        };

        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task Create_MultipleChoice_WithTwoCorrectOptions_ReturnsValidationFailed()
    {
        await SeedDataAsync();

        var request = new CreateQuestionRequest
        {
            SubjectId = _subjectId,
            PrimaryTopicNodeId = "1",
            QuestionType = "MultipleChoice",
            Difficulty = 3,
            QuestionText = "Text",
            CorrectAnswer = "Ans",
            Solution = "Sol",
            MaxScore = 1,
            EstimatedTimeSeconds = 60,
            LanguageCode = "vi",
            GradeLevel = 10,
            Options = new List<QuestionOptionInput>
            {
                new QuestionOptionInput { OptionLabel = "A", OptionText = "Opt A", IsCorrect = true, OrderIndex = 1 },
                new QuestionOptionInput { OptionLabel = "B", OptionText = "Opt B", IsCorrect = true, OrderIndex = 2 }
            }
        };

        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task Create_MismatchTopicSubject_ReturnsResourceNotFound()
    {
        await SeedDataAsync(includeValidTopic: false);

        // Create a topic belonging to a DIFFERENT subject
        var diffSubjectId = Guid.NewGuid();
        var node = new KnowledgeNode
        {
            CenterId = _centerId,
            NodeId = 1,
            SubjectId = diffSubjectId,
            NodeCode = "N1",
            NodeName = "N1",
            IsActive = true,
            CreatedAt = _fixedTime.UtcDateTime,
            UpdatedAt = _fixedTime.UtcDateTime
        };
        _dbContext.KnowledgeNodes.Add(node);
        await _dbContext.SaveChangesAsync();

        var request = new CreateQuestionRequest
        {
            SubjectId = _subjectId,
            PrimaryTopicNodeId = "1", // This node belongs to diffSubjectId
            QuestionType = "ShortAnswer",
            Difficulty = 3,
            QuestionText = "Text",
            CorrectAnswer = "Ans",
            Solution = "Sol",
            MaxScore = 1,
            EstimatedTimeSeconds = 60,
            LanguageCode = "vi",
            GradeLevel = 10
        };

        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Create_InvalidQuestionType_ReturnsValidationFailed()
    {
        await SeedDataAsync();

        var request = new CreateQuestionRequest
        {
            SubjectId = _subjectId,
            PrimaryTopicNodeId = "1",
            QuestionType = "InvalidType",
            Difficulty = 3,
            QuestionText = "Text",
            CorrectAnswer = "Ans",
            Solution = "Sol",
            MaxScore = 1,
            EstimatedTimeSeconds = 60,
            LanguageCode = "vi",
            GradeLevel = 10
        };

        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }
    
    [Fact]
    public void StudentQuestionDto_DoesNotExposeAnswer()
    {
        // Act
        var dto = new StudentQuestionDto
        {
            QuestionId = "1",
            SubjectId = Guid.NewGuid().ToString(),
            PrimaryTopicNodeId = "1",
            QuestionType = "MultipleChoice",
            Difficulty = 3,
            QuestionText = "Find x",
            MaxScore = 1,
            EstimatedTimeSeconds = 60,
            ReasoningRequired = true,
            LanguageCode = "vi",
            Options = new List<StudentQuestionOptionDto>
            {
                new StudentQuestionOptionDto { OptionId = "1", Label = "A", Text = "1", OrderIndex = 1 },
                new StudentQuestionOptionDto { OptionId = "2", Label = "B", Text = "2", OrderIndex = 2 }
            }
        };

        var type = typeof(StudentQuestionDto);
        var properties = type.GetProperties();

        // Assert
        Assert.DoesNotContain(properties, p => p.Name == "CorrectAnswer");
        Assert.DoesNotContain(properties, p => p.Name == "Solution");
        Assert.DoesNotContain(properties, p => p.Name == "ExpectedReasoning");
        Assert.DoesNotContain(properties, p => p.Name == "GradingCriteria");

        var optionType = typeof(StudentQuestionOptionDto);
        var optionProperties = optionType.GetProperties();
        Assert.DoesNotContain(optionProperties, p => p.Name == "IsCorrect");
    }

    [Fact]
    public async Task Create_WithGradingCriteria_PersistsProperly()
    {
        await SeedDataAsync();

        var criteria = new GradingCriteria
        {
            SchemaVersion = "1.0",
            RequiredIdeas = new List<string> { "Idea 1" },
            CommonErrors = new List<string> { "Error 1" },
            ScoringNotes = "Notes"
        };

        var request = new CreateQuestionRequest
        {
            SubjectId = _subjectId,
            PrimaryTopicNodeId = "1",
            QuestionType = "Essay",
            Difficulty = 3,
            QuestionText = "Text",
            CorrectAnswer = "Ans",
            Solution = "Sol",
            MaxScore = 1,
            EstimatedTimeSeconds = 60,
            LanguageCode = "vi",
            GradeLevel = 10,
            GradingCriteria = criteria
        };

        var result = await _sut.ExecuteAsync(request);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.NotNull(result.Data.GradingCriteria);
        Assert.Equal("1.0", result.Data.GradingCriteria.SchemaVersion);
        Assert.Single(result.Data.GradingCriteria.RequiredIdeas);
        Assert.Equal("Idea 1", result.Data.GradingCriteria.RequiredIdeas[0]);
    }

    [Fact]
    public async Task Create_CenterManager_ReturnsForbiddenResource()
    {
        await SeedDataAsync();
        _tenantContextMock.Setup(t => t.UserId).Returns(Guid.NewGuid());
        _tenantContextMock.Setup(t => t.Role).Returns(nameof(UserRole.CenterManager));

        var result = await _sut.ExecuteAsync(ValidShortAnswerRequest());

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ForbiddenResource, result.ErrorCode);
    }

    [Fact]
    public async Task Create_TeacherWithExplicitTeacherId_ReturnsValidationFailed()
    {
        await SeedDataAsync();
        var request = ValidShortAnswerRequest();
        request.TeacherId = _teacherId.ToString();

        var result = await _sut.ExecuteAsync(request);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    private CreateQuestionRequest ValidShortAnswerRequest() => new()
    {
        SubjectId = _subjectId,
        PrimaryTopicNodeId = "1",
        QuestionType = "ShortAnswer",
        Difficulty = 3,
        QuestionText = "Text",
        CorrectAnswer = "Ans",
        Solution = "Sol",
        MaxScore = 1,
        EstimatedTimeSeconds = 60,
        LanguageCode = "vi",
            GradeLevel = 10
    };

    [Fact]
    public async Task Create_ImageOnly_SavesPrivateImageAndSafePlaceholder()
    {
        await SeedDataAsync();
        var request = ValidShortAnswerRequest(); request.QuestionText = ""; request.ImageDataUrl = QuestionImageFixture.DataUrl;
        var result = await _sut.ExecuteAsync(request);
        Assert.True(result.IsSuccess); Assert.True(result.Data!.HasImage);
        Assert.Equal(QuestionImageContent.ImageOnlyText, result.Data.QuestionText);
        var image = await _dbContext.QuestionImages.SingleAsync();
        Assert.Equal(_centerId, image.CenterId); Assert.Equal(_teacherId, image.CreatedBy);
        Assert.Equal(QuestionImageFixture.Bytes, image.Data);
        Assert.Equal(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(image.Data)), image.Sha256);
        Assert.DoesNotContain("DataUrl", System.Text.Json.JsonSerializer.Serialize(result.Data));
    }

    [Fact]
    public async Task Create_BadImage_DoesNotPersistQuestion()
    {
        await SeedDataAsync();
        var request = ValidShortAnswerRequest(); request.ImageDataUrl = "data:image/svg+xml;base64,PHN2Zy8+";
        Assert.False((await _sut.ExecuteAsync(request)).IsSuccess);
        Assert.Empty(await _dbContext.Questions.ToArrayAsync()); Assert.Empty(await _dbContext.QuestionImages.ToArrayAsync());
    }

    [Fact]
    public async Task Create_CopyOwnImage_DoesNotLoseImage()
    {
        await SeedDataAsync();
        var source = ValidShortAnswerRequest(); source.ImageDataUrl = QuestionImageFixture.DataUrl;
        var created = await _sut.ExecuteAsync(source); Assert.True(created.IsSuccess);
        var copy = ValidShortAnswerRequest(); copy.QuestionText = ""; copy.CopyImageFromQuestionId = created.Data!.QuestionId;
        var result = await _sut.ExecuteAsync(copy);
        Assert.True(result.IsSuccess); Assert.True(result.Data!.HasImage);
        Assert.Equal(2, await _dbContext.QuestionImages.CountAsync());
        var question = await _dbContext.Questions.SingleAsync(q => q.QuestionId == ulong.Parse(created.Data.QuestionId));
        question.CreatedByTeacherId = Guid.NewGuid(); await _dbContext.SaveChangesAsync();
        Assert.False((await _sut.ExecuteAsync(copy)).IsSuccess); // another teacher's private image is not copyable
        Assert.Equal(2, await _dbContext.QuestionImages.CountAsync());
    }

    [Fact]
    public async Task Update_RemoveImage_RequiresRealText_AndCannotChangeUsedImage()
    {
        await SeedDataAsync();
        var create = ValidShortAnswerRequest(); create.QuestionText = ""; create.ImageDataUrl = QuestionImageFixture.DataUrl;
        var result = await _sut.ExecuteAsync(create); Assert.True(result.IsSuccess);
        var q = await _dbContext.Questions.SingleAsync();
        var update = new UpdateQuestionRequest { PrimaryTopicNodeId = "1", QuestionType = "ShortAnswer", Difficulty = 3,
            GradeLevel = 10, QuestionText = QuestionImageContent.ImageOnlyText, CorrectAnswer = "Ans", Solution = "Sol",
            MaxScore = 1, EstimatedTimeSeconds = 60, LanguageCode = "vi", RowVersion = q.RowVersion.ToString(), RemoveImage = true };
        var sut = new UpdateQuestionUseCase(_dbContext, _tenantContextMock.Object, _timeProviderMock.Object);
        Assert.False((await sut.ExecuteAsync(q.QuestionId.ToString(), update)).IsSuccess);
        Assert.Single(await _dbContext.QuestionImages.ToArrayAsync());
        update.QuestionText = "Real typed question";
        _dbContext.Attempts.Add(new EduTwin.DAL.AssessmentAndReasoning.Attempt { CenterId = _centerId, StudentId = Guid.NewGuid(),
            QuestionId = q.QuestionId, FinalAnswer = "Ans", ReasoningLanguage = "vi", ClientSubmissionId = Guid.NewGuid(),
            CreatedAt = _fixedTime.UtcDateTime, UpdatedAt = _fixedTime.UtcDateTime });
        await _dbContext.SaveChangesAsync();
        var blocked = await sut.ExecuteAsync(q.QuestionId.ToString(), update);
        Assert.False(blocked.IsSuccess); Assert.Equal(ErrorCodes.InvalidStateTransition, blocked.ErrorCode);
        Assert.Single(await _dbContext.QuestionImages.ToArrayAsync());
    }

    [Fact]
    public async Task ImageRead_IsPrivateAndCenterBound()
    {
        await SeedDataAsync();
        var request = ValidShortAnswerRequest(); request.ImageDataUrl = QuestionImageFixture.DataUrl;
        Assert.True((await _sut.ExecuteAsync(request)).IsSuccess);
        var q = await _dbContext.Questions.SingleAsync();
        var sut = new GetQuestionImageUseCase(_dbContext, _tenantContextMock.Object);
        Assert.Equal(QuestionImageFixture.Bytes, await sut.ExecuteAsync(q.QuestionId, CancellationToken.None));
        _tenantContextMock.Setup(t => t.UserId).Returns(Guid.NewGuid());
        Assert.Null(await sut.ExecuteAsync(q.QuestionId, CancellationToken.None));
        _tenantContextMock.Setup(t => t.Role).Returns(nameof(UserRole.Student));
        Assert.Null(await sut.ExecuteAsync(q.QuestionId, CancellationToken.None));
        _tenantContextMock.Setup(t => t.UserId).Returns(_teacherId);
        _tenantContextMock.Setup(t => t.Role).Returns(nameof(UserRole.Teacher));
        _tenantContextMock.Setup(t => t.CenterId).Returns(Guid.NewGuid());
        Assert.Null(await sut.ExecuteAsync(q.QuestionId, CancellationToken.None));
    }

    [Fact]
    public async Task CreateQuestion_WithoutGradeLevel_ReturnsValidationFailed()
    {
        await SeedDataAsync();
        var request = ValidShortAnswerRequest();
        request.GradeLevel = null;
        var result = await _sut.ExecuteAsync(request);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Theory]
    [InlineData(9)]
    [InlineData(13)]
    public async Task CreateQuestion_WithInvalidGradeLevel_ReturnsValidationFailed(byte grade)
    {
        await SeedDataAsync();
        var request = ValidShortAnswerRequest();
        request.GradeLevel = grade;
        var result = await _sut.ExecuteAsync(request);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
