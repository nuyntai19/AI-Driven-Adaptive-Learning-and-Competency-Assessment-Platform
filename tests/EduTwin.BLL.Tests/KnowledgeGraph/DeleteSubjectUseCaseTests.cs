using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.KnowledgeGraph;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.DigitalTwin;
using EduTwin.DAL.Recommendations;

namespace EduTwin.BLL.Tests.KnowledgeGraph;

public class MockEduTwinDbContextDelete : EduTwinDbContext
{
    public Func<CancellationToken, Task<int>>? SaveChangesAsyncCallback { get; set; }

    public MockEduTwinDbContextDelete(DbContextOptions<EduTwinDbContext> options, ITenantIdAccessor tenantIdAccessor)
        : base(options, tenantIdAccessor)
    {
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        if (SaveChangesAsyncCallback != null)
        {
            return SaveChangesAsyncCallback(cancellationToken);
        }

        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }
}

public class DeleteSubjectUseCaseTests
{
    private readonly MockEduTwinDbContextDelete _dbContext;
    private readonly Mock<ITenantContext> _tenantMock;
    private readonly Mock<TimeProvider> _timeProviderMock;
    private readonly DeleteSubjectUseCase _sut;

    public DeleteSubjectUseCaseTests()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var tenantIdAccessorMock = new Mock<ITenantIdAccessor>();
        _tenantMock = new Mock<ITenantContext>();

        tenantIdAccessorMock.SetupGet(x => x.CenterId).Returns(() => _tenantMock.Object.CenterId);

        _dbContext = new MockEduTwinDbContextDelete(options, tenantIdAccessorMock.Object);

        _timeProviderMock = new Mock<TimeProvider>();
        var utcNow = new DateTimeOffset(2026, 7, 22, 10, 0, 0, TimeSpan.Zero);
        _timeProviderMock.Setup(x => x.GetUtcNow()).Returns(utcNow);

        _sut = new DeleteSubjectUseCase(_dbContext, _tenantMock.Object, _timeProviderMock.Object);
    }

    private async Task SetupActiveCenter(Guid centerId)
    {
        _dbContext.Centers.Add(new Center
        {
            CenterId = centerId,
            CenterCode = "C" + centerId.ToString()[..4],
            CenterName = "Test Center",
            Timezone = "UTC",
            Status = EduTwin.Contracts.Organization.CenterStatus.Active,
            IsDeleted = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
    }

    [Fact]
    public async Task Delete_ValidCenterManager_ReturnsSuccess()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        await SetupActiveCenter(centerId);

        var originalCreatedAt = DateTime.UtcNow.AddDays(-1);
        var originalCreatedBy = Guid.NewGuid();
        _dbContext.Subjects.Add(new Subject
        {
            SubjectId = subjectId,
            CenterId = centerId,
            SubjectCode = "S1",
            SubjectName = "Test",
            IsActive = true,
            CreatedAt = originalCreatedAt,
            CreatedBy = originalCreatedBy,
            UpdatedAt = originalCreatedAt,
            UpdatedBy = originalCreatedBy,
            IsDeleted = false
        });
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(userId);
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.CenterManager));

        var result = await _sut.ExecuteAsync(subjectId, CancellationToken.None);

        Assert.True(result.IsSuccess);

        var updatedSubject = await _dbContext.Subjects.IgnoreQueryFilters().FirstOrDefaultAsync(x => x.SubjectId == subjectId);
        Assert.NotNull(updatedSubject);
        Assert.True(updatedSubject.IsDeleted);
        Assert.Equal(userId, updatedSubject.DeletedBy);
        Assert.Equal(userId, updatedSubject.UpdatedBy);
        Assert.Equal(new DateTimeOffset(2026, 7, 22, 10, 0, 0, TimeSpan.Zero).UtcDateTime, updatedSubject.DeletedAt);
        Assert.Equal(new DateTimeOffset(2026, 7, 22, 10, 0, 0, TimeSpan.Zero).UtcDateTime, updatedSubject.UpdatedAt);
        Assert.Equal(originalCreatedAt, updatedSubject.CreatedAt);
        Assert.Equal(originalCreatedBy, updatedSubject.CreatedBy);
    }

    [Theory]
    [InlineData(nameof(UserRole.Teacher))]
    [InlineData(nameof(UserRole.Student))]
    [InlineData("Admin")]
    [InlineData("centermanager")]
    [InlineData("   ")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("")]
    [InlineData(null)]
    public async Task Delete_InvalidRole_ReturnsResourceNotFound(string? role)
    {
        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(role);

        var result = await _sut.ExecuteAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Delete_TenantUnresolved_ReturnsResourceNotFound()
    {
        _tenantMock.SetupGet(x => x.IsResolved).Returns(false);

        var result = await _sut.ExecuteAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Delete_CenterIdNull_ReturnsResourceNotFound()
    {
        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns((Guid?)null);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.CenterManager));

        var result = await _sut.ExecuteAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Delete_CenterIdEmpty_ReturnsResourceNotFound()
    {
        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(Guid.Empty);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.CenterManager));

        var result = await _sut.ExecuteAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Delete_UserIdNull_ReturnsResourceNotFound()
    {
        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.UserId).Returns((Guid?)null);
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.CenterManager));

        var result = await _sut.ExecuteAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Delete_UserIdEmpty_ReturnsResourceNotFound()
    {
        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.Empty);
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.CenterManager));

        var result = await _sut.ExecuteAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Delete_SubjectIdEmpty_ReturnsResourceNotFound()
    {
        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.CenterManager));

        var result = await _sut.ExecuteAsync(Guid.Empty, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Delete_CenterMissing_ReturnsResourceNotFound()
    {
        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.CenterManager));

        var result = await _sut.ExecuteAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Delete_CenterDeleted_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        _dbContext.Centers.Add(new Center
        {
            CenterId = centerId,
            CenterCode = "C1",
            CenterName = "Test",
            Timezone = "UTC",
            IsDeleted = true,
            DeletedAt = DateTime.UtcNow,
            Status = EduTwin.Contracts.Organization.CenterStatus.Active,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await _dbContext.SaveChangesAsync();

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.CenterManager));

        var result = await _sut.ExecuteAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Delete_SubjectMissing_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        await SetupActiveCenter(centerId);

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.CenterManager));

        var result = await _sut.ExecuteAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Delete_SubjectDeleted_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        await SetupActiveCenter(centerId);
        _dbContext.Subjects.Add(new Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "S1", SubjectName = "Test", IsDeleted = true, DeletedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.CenterManager));

        var result = await _sut.ExecuteAsync(subjectId, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task Delete_SubjectCrossTenant_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        var otherCenterId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        await SetupActiveCenter(centerId);
        _dbContext.Subjects.Add(new Subject { SubjectId = subjectId, CenterId = otherCenterId, SubjectCode = "S1", SubjectName = "Test", IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.CenterManager));

        var result = await _sut.ExecuteAsync(subjectId, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    private async Task SetupSubjectForDependencyTests(Guid centerId, Guid subjectId)
    {
        await SetupActiveCenter(centerId);
        _dbContext.Subjects.Add(new Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "S1", SubjectName = "Test", IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        _tenantMock.SetupGet(x => x.IsResolved).Returns(true);
        _tenantMock.SetupGet(x => x.CenterId).Returns(centerId);
        _tenantMock.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenantMock.SetupGet(x => x.Role).Returns(nameof(UserRole.CenterManager));
    }

    [Fact]
    public async Task Delete_HasClass_ReturnsInvalidStateTransition()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        await SetupSubjectForDependencyTests(centerId, subjectId);
        _dbContext.Classes.Add(new Class { ClassId = Guid.NewGuid(), CenterId = centerId, SubjectId = subjectId, ClassName = "TestClass", AcademicYear = "2026", IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ExecuteAsync(subjectId, CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidStateTransition, result.ErrorCode);
    }

    [Fact]
    public async Task Delete_HasCurriculum_ReturnsInvalidStateTransition()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        await SetupSubjectForDependencyTests(centerId, subjectId);
        _dbContext.Curriculums.Add(new Curriculum { CurriculumId = Guid.NewGuid(), CenterId = centerId, SubjectId = subjectId, IsDeleted = false, Title = "", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ExecuteAsync(subjectId, CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidStateTransition, result.ErrorCode);
    }

    [Fact]
    public async Task Delete_HasQuestion_ReturnsInvalidStateTransition()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        await SetupSubjectForDependencyTests(centerId, subjectId);
        _dbContext.Questions.Add(new Question { QuestionId = 1, CenterId = centerId, SubjectId = subjectId, QuestionText = "Q", CorrectAnswer = "A", Solution = "S", LanguageCode = "en", IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ExecuteAsync(subjectId, CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidStateTransition, result.ErrorCode);
    }

    [Fact]
    public async Task Delete_HasKnowledgeNode_ReturnsInvalidStateTransition()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        await SetupSubjectForDependencyTests(centerId, subjectId);
        _dbContext.KnowledgeNodes.Add(new KnowledgeNode { NodeId = 1, CenterId = centerId, SubjectId = subjectId, NodeCode = "N1", NodeName = "Node1", IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ExecuteAsync(subjectId, CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidStateTransition, result.ErrorCode);
    }

    [Fact]
    public async Task Delete_HasKnowledgeEdge_ReturnsInvalidStateTransition()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        await SetupSubjectForDependencyTests(centerId, subjectId);
        _dbContext.KnowledgeEdges.Add(new KnowledgeEdge { EdgeId = 1, CenterId = centerId, SubjectId = subjectId, IsDeleted = false, SourceNodeId = 1, TargetNodeId = 2, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ExecuteAsync(subjectId, CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidStateTransition, result.ErrorCode);
    }

    [Fact]
    public async Task Delete_HasStudentSubjectGoal_ReturnsInvalidStateTransition()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        await SetupSubjectForDependencyTests(centerId, subjectId);
        _dbContext.StudentSubjectGoals.Add(new StudentSubjectGoal { GoalId = 1, CenterId = centerId, SubjectId = subjectId, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ExecuteAsync(subjectId, CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidStateTransition, result.ErrorCode);
    }

    [Fact]
    public async Task Delete_HasKnowledgeTwin_ReturnsInvalidStateTransition()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        await SetupSubjectForDependencyTests(centerId, subjectId);
        _dbContext.KnowledgeTwins.Add(new KnowledgeTwin { KnowledgeTwinId = 1, CenterId = centerId, SubjectId = subjectId, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ExecuteAsync(subjectId, CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidStateTransition, result.ErrorCode);
    }

    [Fact]
    public async Task Delete_HasBehaviorTwin_ReturnsInvalidStateTransition()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        await SetupSubjectForDependencyTests(centerId, subjectId);
        _dbContext.BehaviorTwins.Add(new BehaviorTwin { BehaviorTwinId = 1, CenterId = centerId, SubjectId = subjectId, IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ExecuteAsync(subjectId, CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidStateTransition, result.ErrorCode);
    }

    [Fact]
    public async Task Delete_HasTwinUpdateHistory_ReturnsInvalidStateTransition()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        await SetupSubjectForDependencyTests(centerId, subjectId);
        _dbContext.TwinUpdateHistories.Add(new TwinUpdateHistory { HistoryId = 1, CenterId = centerId, SubjectId = subjectId, CalculationVersion = "1", CalculationBreakdown = System.Text.Json.JsonDocument.Parse("{}"), Explanation = "Test", CreatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ExecuteAsync(subjectId, CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidStateTransition, result.ErrorCode);
    }

    [Fact]
    public async Task Delete_HasLearningPath_ReturnsInvalidStateTransition()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        await SetupSubjectForDependencyTests(centerId, subjectId);
        _dbContext.LearningPaths.Add(new LearningPath { LearningPathId = Guid.NewGuid(), CenterId = centerId, SubjectId = subjectId, IsDeleted = false, GeneratedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ExecuteAsync(subjectId, CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidStateTransition, result.ErrorCode);
    }

    [Fact]
    public async Task Delete_HasRecommendation_ReturnsInvalidStateTransition()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        await SetupSubjectForDependencyTests(centerId, subjectId);
        _dbContext.Recommendations.Add(new Recommendation { RecommendationId = 1, CenterId = centerId, SubjectId = subjectId, CalculationVersion = "1", CalculationBreakdown = System.Text.Json.JsonDocument.Parse("{}"), Explanation = "Test", IsDeleted = false, GeneratedAt = DateTime.UtcNow, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ExecuteAsync(subjectId, CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidStateTransition, result.ErrorCode);
    }

    [Fact]
    public async Task Delete_DependencyCrossTenant_DoesNotBlock()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var otherCenterId = Guid.NewGuid();
        await SetupSubjectForDependencyTests(centerId, subjectId);
        _dbContext.Classes.Add(new Class { ClassId = Guid.NewGuid(), CenterId = otherCenterId, SubjectId = subjectId, ClassName = "TestClass", AcademicYear = "2026", IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ExecuteAsync(subjectId, CancellationToken.None);
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task Delete_DbUpdateConcurrencyException_ReturnsConcurrencyConflictAndEmptyTracker()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        await SetupSubjectForDependencyTests(centerId, subjectId);

        _dbContext.SaveChangesAsyncCallback = _ => throw new DbUpdateConcurrencyException();

        var result = await _sut.ExecuteAsync(subjectId, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, result.ErrorCode);
        Assert.Empty(_dbContext.ChangeTracker.Entries());
    }

    [Fact]
    public async Task Delete_UnrelatedDbUpdateException_Rethrows()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        await SetupSubjectForDependencyTests(centerId, subjectId);

        _dbContext.SaveChangesAsyncCallback = _ => throw new DbUpdateException();

        await Assert.ThrowsAsync<DbUpdateException>(() => _sut.ExecuteAsync(subjectId, CancellationToken.None));
    }

    [Fact]
    public async Task Delete_ExactCancellationToken_IsPassedToSaveChangesAsync()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        await SetupSubjectForDependencyTests(centerId, subjectId);

        CancellationToken? tokenPassed = null;
        _dbContext.SaveChangesAsyncCallback = token => { tokenPassed = token; return Task.FromResult(1); };

        using var cts = new CancellationTokenSource();
        var exactToken = cts.Token;

        var result = await _sut.ExecuteAsync(subjectId, exactToken);

        Assert.True(result.IsSuccess);
        Assert.Equal(exactToken, tokenPassed);
    }

    [Fact]
    public async Task Delete_NoPersistenceWhenRejected()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        await SetupSubjectForDependencyTests(centerId, subjectId);
        _dbContext.Classes.Add(new Class { ClassId = Guid.NewGuid(), CenterId = centerId, SubjectId = subjectId, ClassName = "TestClass", AcademicYear = "2026", IsDeleted = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var result = await _sut.ExecuteAsync(subjectId, CancellationToken.None);
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidStateTransition, result.ErrorCode);

        var subject = await _dbContext.Subjects.AsNoTracking().FirstOrDefaultAsync(x => x.SubjectId == subjectId);
        Assert.False(subject!.IsDeleted);
    }
}
