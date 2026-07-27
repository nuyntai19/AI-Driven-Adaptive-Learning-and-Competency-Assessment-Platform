using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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
            PasswordHash = "hash"
        };
        var teacher = new Teacher { CenterId = _centerId, TeacherId = _teacherId, User = user, CreatedAt = now, UpdatedAt = now };
        var subject = new Subject { CenterId = _centerId, SubjectId = _subjectId, SubjectCode = "S1", SubjectName = "S1", CreatedAt = now, UpdatedAt = now };

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
            LanguageCode = "vi"
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
            LanguageCode = "vi"
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
            LanguageCode = "vi"
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

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
