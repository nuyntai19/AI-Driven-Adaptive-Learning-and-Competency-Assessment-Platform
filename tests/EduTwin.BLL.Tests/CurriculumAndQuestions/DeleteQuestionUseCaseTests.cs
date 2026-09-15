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
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Assignments;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;

namespace EduTwin.BLL.Tests.CurriculumAndQuestions;

public class DeleteQuestionUseCaseTests
{
    private readonly DbContextOptions<EduTwinDbContext> _dbOptions;
    private readonly Mock<ITenantContext> _tenantMock;
    private readonly Mock<ITenantIdAccessor> _tenantAccessorMock;
    private readonly Mock<TimeProvider> _timeProviderMock;

    public DeleteQuestionUseCaseTests()
    {
        _dbOptions = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        _tenantMock = new Mock<ITenantContext>();
        _tenantAccessorMock = new Mock<ITenantIdAccessor>();
        _tenantAccessorMock.Setup(x => x.CenterId).Returns(() => _tenantMock.Object.CenterId);
        _timeProviderMock = new Mock<TimeProvider>();
        _timeProviderMock.Setup(x => x.GetUtcNow()).Returns(new DateTimeOffset(2026, 7, 24, 12, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task ExecuteAsync_TenantNotResolved_ReturnsNotFound()
    {
        using var dbContext = new EduTwinDbContext(_dbOptions, _tenantAccessorMock.Object);
        _tenantMock.Setup(t => t.IsResolved).Returns(false);

        var sut = new DeleteQuestionUseCase(dbContext, _tenantMock.Object, _timeProviderMock.Object);

        var result = await sut.ExecuteAsync("100");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_ActorNotCenterManager_ReturnsNotFound()
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        _tenantMock.Setup(t => t.IsResolved).Returns(true);
        _tenantMock.Setup(t => t.CenterId).Returns(centerId);
        _tenantMock.Setup(t => t.UserId).Returns(teacherId);
        _tenantMock.Setup(t => t.Role).Returns(nameof(UserRole.Teacher)); // Teacher is not allowed to delete question

        using var dbContext = new EduTwinDbContext(_dbOptions, _tenantAccessorMock.Object);
        var sut = new DeleteQuestionUseCase(dbContext, _tenantMock.Object, _timeProviderMock.Object);

        var result = await sut.ExecuteAsync("100");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not-a-number")]
    [InlineData("0")]
    [InlineData("-5")]
    public async Task ExecuteAsync_InvalidQuestionIdFormat_ReturnsValidationFailed(string? questionId)
    {
        var centerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        _tenantMock.Setup(t => t.IsResolved).Returns(true);
        _tenantMock.Setup(t => t.CenterId).Returns(centerId);
        _tenantMock.Setup(t => t.UserId).Returns(managerId);
        _tenantMock.Setup(t => t.Role).Returns(nameof(UserRole.CenterManager));

        using var dbContext = new EduTwinDbContext(_dbOptions, _tenantAccessorMock.Object);
        var sut = new DeleteQuestionUseCase(dbContext, _tenantMock.Object, _timeProviderMock.Object);

        var result = await sut.ExecuteAsync(questionId!);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_QuestionNotFound_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        _tenantMock.Setup(t => t.IsResolved).Returns(true);
        _tenantMock.Setup(t => t.CenterId).Returns(centerId);
        _tenantMock.Setup(t => t.UserId).Returns(managerId);
        _tenantMock.Setup(t => t.Role).Returns(nameof(UserRole.CenterManager));

        using var dbContext = new EduTwinDbContext(_dbOptions, _tenantAccessorMock.Object);
        var sut = new DeleteQuestionUseCase(dbContext, _tenantMock.Object, _timeProviderMock.Object);

        var result = await sut.ExecuteAsync("9999");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_QuestionNotDraft_ReturnsInvalidStateTransition()
    {
        var centerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        _tenantMock.Setup(t => t.IsResolved).Returns(true);
        _tenantMock.Setup(t => t.CenterId).Returns(centerId);
        _tenantMock.Setup(t => t.UserId).Returns(managerId);
        _tenantMock.Setup(t => t.Role).Returns(nameof(UserRole.CenterManager));

        const ulong qId = 200;
        using (var dbContext = new EduTwinDbContext(_dbOptions, _tenantAccessorMock.Object))
        {
            dbContext.Questions.Add(new Question
            {
                QuestionId = qId,
                CenterId = centerId,
                SubjectId = Guid.NewGuid(),
                PrimaryTopicNodeId = 1,
                CreatedByTeacherId = managerId,
                QuestionText = "Active Question",
                CorrectAnswer = "A",
                Solution = "Solution",
                LanguageCode = "vi",
                QuestionType = QuestionType.MultipleChoice,
                Status = QuestionStatus.Active, // Not Draft
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = managerId
            });
            await dbContext.SaveChangesAsync();
        }

        using (var dbContext = new EduTwinDbContext(_dbOptions, _tenantAccessorMock.Object))
        {
            var sut = new DeleteQuestionUseCase(dbContext, _tenantMock.Object, _timeProviderMock.Object);

            var result = await sut.ExecuteAsync(qId.ToString());

            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorCodes.InvalidStateTransition, result.ErrorCode);
        }
    }

    [Fact]
    public async Task ExecuteAsync_QuestionHasAttempts_ReturnsInvalidStateTransition()
    {
        var centerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        _tenantMock.Setup(t => t.IsResolved).Returns(true);
        _tenantMock.Setup(t => t.CenterId).Returns(centerId);
        _tenantMock.Setup(t => t.UserId).Returns(managerId);
        _tenantMock.Setup(t => t.Role).Returns(nameof(UserRole.CenterManager));

        const ulong qId = 201;
        using (var dbContext = new EduTwinDbContext(_dbOptions, _tenantAccessorMock.Object))
        {
            dbContext.Questions.Add(new Question
            {
                QuestionId = qId,
                CenterId = centerId,
                SubjectId = Guid.NewGuid(),
                PrimaryTopicNodeId = 1,
                CreatedByTeacherId = managerId,
                QuestionText = "Draft with attempts",
                CorrectAnswer = "A",
                Solution = "Solution",
                LanguageCode = "vi",
                QuestionType = QuestionType.MultipleChoice,
                Status = QuestionStatus.Draft,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = managerId
            });
            dbContext.Attempts.Add(new Attempt
            {
                AttemptId = 5001,
                CenterId = centerId,
                StudentId = Guid.NewGuid(),
                QuestionId = qId,
                FinalAnswer = "A",
                ReasoningLanguage = "vi",
                IsCorrect = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        using (var dbContext = new EduTwinDbContext(_dbOptions, _tenantAccessorMock.Object))
        {
            var sut = new DeleteQuestionUseCase(dbContext, _tenantMock.Object, _timeProviderMock.Object);

            var result = await sut.ExecuteAsync(qId.ToString());

            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorCodes.InvalidStateTransition, result.ErrorCode);
        }
    }

    [Fact]
    public async Task ExecuteAsync_QuestionHasAssignments_ReturnsInvalidStateTransition()
    {
        var centerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        _tenantMock.Setup(t => t.IsResolved).Returns(true);
        _tenantMock.Setup(t => t.CenterId).Returns(centerId);
        _tenantMock.Setup(t => t.UserId).Returns(managerId);
        _tenantMock.Setup(t => t.Role).Returns(nameof(UserRole.CenterManager));

        const ulong qId = 202;
        using (var dbContext = new EduTwinDbContext(_dbOptions, _tenantAccessorMock.Object))
        {
            dbContext.Questions.Add(new Question
            {
                QuestionId = qId,
                CenterId = centerId,
                SubjectId = Guid.NewGuid(),
                PrimaryTopicNodeId = 1,
                CreatedByTeacherId = managerId,
                QuestionText = "Draft in assignment",
                CorrectAnswer = "A",
                Solution = "Solution",
                LanguageCode = "vi",
                QuestionType = QuestionType.MultipleChoice,
                Status = QuestionStatus.Draft,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = managerId
            });
            dbContext.AssignmentQuestions.Add(new AssignmentQuestion
            {
                AssignmentId = Guid.NewGuid(),
                QuestionId = qId,
                CenterId = centerId,
                OrderIndex = 1,
                Points = 10,
                CreatedAt = DateTime.UtcNow
            });
            await dbContext.SaveChangesAsync();
        }

        using (var dbContext = new EduTwinDbContext(_dbOptions, _tenantAccessorMock.Object))
        {
            var sut = new DeleteQuestionUseCase(dbContext, _tenantMock.Object, _timeProviderMock.Object);

            var result = await sut.ExecuteAsync(qId.ToString());

            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorCodes.InvalidStateTransition, result.ErrorCode);
        }
    }

    [Fact]
    public async Task ExecuteAsync_ValidDraftWithoutDependencies_SoftDeletesQuestionAndOptions()
    {
        var centerId = Guid.NewGuid();
        var managerId = Guid.NewGuid();
        _tenantMock.Setup(t => t.IsResolved).Returns(true);
        _tenantMock.Setup(t => t.CenterId).Returns(centerId);
        _tenantMock.Setup(t => t.UserId).Returns(managerId);
        _tenantMock.Setup(t => t.Role).Returns(nameof(UserRole.CenterManager));

        const ulong qId = 203;
        const ulong optId = 901;

        using (var dbContext = new EduTwinDbContext(_dbOptions, _tenantAccessorMock.Object))
        {
            dbContext.Questions.Add(new Question
            {
                QuestionId = qId,
                CenterId = centerId,
                SubjectId = Guid.NewGuid(),
                PrimaryTopicNodeId = 1,
                CreatedByTeacherId = managerId,
                QuestionText = "Valid draft question",
                CorrectAnswer = "A",
                Solution = "Solution",
                LanguageCode = "vi",
                QuestionType = QuestionType.MultipleChoice,
                Status = QuestionStatus.Draft,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                CreatedBy = managerId,
                IsDeleted = false
            });
            dbContext.QuestionOptions.Add(new QuestionOption
            {
                OptionId = optId,
                QuestionId = qId,
                CenterId = centerId,
                OptionLabel = "A",
                OptionText = "Option A",
                IsCorrect = true,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow,
                IsDeleted = false
            });
            await dbContext.SaveChangesAsync();
        }

        using (var dbContext = new EduTwinDbContext(_dbOptions, _tenantAccessorMock.Object))
        {
            var sut = new DeleteQuestionUseCase(dbContext, _tenantMock.Object, _timeProviderMock.Object);

            var result = await sut.ExecuteAsync(qId.ToString());

            Assert.True(result.IsSuccess);

            var qInDb = await dbContext.Questions.IgnoreQueryFilters().SingleAsync(q => q.QuestionId == qId);
            Assert.True(qInDb.IsDeleted);
            Assert.NotNull(qInDb.DeletedAt);
            Assert.Equal(managerId, qInDb.DeletedBy);

            var optInDb = await dbContext.QuestionOptions.IgnoreQueryFilters().SingleAsync(o => o.OptionId == optId);
            Assert.True(optInDb.IsDeleted);
            Assert.NotNull(optInDb.DeletedAt);
            Assert.Equal(managerId, optInDb.DeletedBy);
        }
    }
}
