using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using EduTwin.BLL.KnowledgeGraph;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.DigitalTwin;
using EduTwin.Contracts.Recommendations;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Organization;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.DigitalTwin;
using EduTwin.DAL.Recommendations;
using EduTwin.DAL.AssessmentAndReasoning;

namespace EduTwin.BLL.Tests.KnowledgeGraph;

public class ConcurrencyEduTwinDbContext : EduTwinDbContext
{
    public ConcurrencyEduTwinDbContext(DbContextOptions<EduTwinDbContext> options, EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor accessor) : base(options, accessor) { }
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        throw new DbUpdateConcurrencyException();
    }
}

public class DbUpdateExceptionEduTwinDbContext : EduTwinDbContext
{
    public DbUpdateExceptionEduTwinDbContext(DbContextOptions<EduTwinDbContext> options, EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor accessor) : base(options, accessor) { }
    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        throw new DbUpdateException();
    }
}

public class DeleteKnowledgeNodeUseCaseTests
{
    private readonly DbContextOptions<EduTwinDbContext> _dbOptions;
    private readonly Mock<ITenantContext> _tenantContextMock;
    private readonly Mock<TimeProvider> _timeProviderMock;

    public DeleteKnowledgeNodeUseCaseTests()
    {
        _dbOptions = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _tenantContextMock = new Mock<ITenantContext>();
        _timeProviderMock = new Mock<TimeProvider>();
    }

    private EduTwinDbContext CreateDbContext()
    {
        var accessorMock = new Mock<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>();
        accessorMock.Setup(x => x.CenterId).Returns(() => _tenantContextMock.Object.CenterId);
        return new EduTwinDbContext(_dbOptions, accessorMock.Object);
    }

    private DeleteKnowledgeNodeUseCase CreateSut(EduTwinDbContext context, ITenantContext? tc = null)
    {
        return new DeleteKnowledgeNodeUseCase(context, tc ?? _tenantContextMock.Object, _timeProviderMock.Object);
    }

    private void SetupTenantContext(Guid centerId, Guid userId, string role = nameof(UserRole.CenterManager))
    {
        _tenantContextMock.Setup(x => x.IsResolved).Returns(true);
        _tenantContextMock.Setup(x => x.UserId).Returns(userId);
        _tenantContextMock.Setup(x => x.CenterId).Returns(centerId);
        _tenantContextMock.Setup(x => x.Role).Returns(role);
    }

    [Fact]
    public async Task ExecuteAsync_Success_SoftDeletesNode_UpdatesAuditFieldsAndRowVersion()
    {
        var centerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var utcNow = new DateTime(2026, 7, 23, 10, 0, 0, DateTimeKind.Utc);
        var oldDate = utcNow.AddDays(-1);
        var initialRowVersion = 1UL;
        var originalCreatedBy = Guid.NewGuid();

        SetupTenantContext(centerId, userId);
        _timeProviderMock.Setup(x => x.GetUtcNow()).Returns(new DateTimeOffset(utcNow));

        var nodeId = 100UL;

        using (var setupContext = CreateDbContext())
        {
            setupContext.Centers.Add(new Center { CenterId = centerId, CenterCode = "C", CenterName = "N", Status = CenterStatus.Active, Timezone = "T", CreatedAt = oldDate, UpdatedAt = oldDate, IsDeleted = false });
            setupContext.Subjects.Add(new Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "S", SubjectName = "S", IsActive = true, CreatedAt = oldDate, UpdatedAt = oldDate, IsDeleted = false });
            setupContext.KnowledgeNodes.Add(new KnowledgeNode
            {
                NodeId = nodeId,
                CenterId = centerId,
                SubjectId = subjectId,
                NodeName = "Original",
                NodeType = NodeType.Topic,
                NodeCode = "CODE",
                CreatedAt = oldDate,
                CreatedBy = originalCreatedBy,
                UpdatedAt = oldDate,
                IsDeleted = false,
                RowVersion = initialRowVersion
            });
            await setupContext.SaveChangesAsync();
        }

        using (var context = CreateDbContext())
        {
            var sut = CreateSut(context);
            var result = await sut.ExecuteAsync(nodeId.ToString(CultureInfo.InvariantCulture));
            Assert.True(result.IsSuccess);
        }

        using (var assertContext = CreateDbContext())
        {
            var node = await assertContext.KnowledgeNodes.IgnoreQueryFilters().FirstAsync(x => x.NodeId == nodeId);
            Assert.True(node.IsDeleted);
            Assert.Equal(utcNow, node.DeletedAt);
            Assert.Equal(utcNow, node.UpdatedAt);
            Assert.Equal(userId, node.DeletedBy);
            Assert.Equal(userId, node.UpdatedBy);
            Assert.Equal(initialRowVersion + 1, node.RowVersion);

            Assert.Equal(nodeId, node.NodeId);
            Assert.Equal(centerId, node.CenterId);
            Assert.Equal(subjectId, node.SubjectId);
            Assert.Null(node.ParentNodeId);
            Assert.Equal(NodeType.Topic, node.NodeType);
            Assert.Equal("CODE", node.NodeCode);
            Assert.Equal("Original", node.NodeName);
            Assert.Equal(oldDate, node.CreatedAt);
            Assert.Equal(originalCreatedBy, node.CreatedBy);
        }
    }

    [Theory]
    [InlineData(null, "center", "role")]
    [InlineData("00000000-0000-0000-0000-000000000000", "center", "role")]
    [InlineData("user", null, "role")]
    [InlineData("user", "00000000-0000-0000-0000-000000000000", "role")]
    [InlineData("user", "center", null)]
    [InlineData("user", "center", "")]
    public async Task ExecuteAsync_InvalidTenantContext_ReturnsResourceNotFound_NoMutation(string? userIdStr, string? centerIdStr, string? role)
    {
        Guid userId = userIdStr == "user" ? Guid.NewGuid() : (userIdStr == null ? Guid.Empty : Guid.Parse(userIdStr));
        Guid centerId = centerIdStr == "center" ? Guid.NewGuid() : (centerIdStr == null ? Guid.Empty : Guid.Parse(centerIdStr));

        var isNull = (userIdStr == null && centerIdStr == null && role == null);

        if (isNull)
        {
            _tenantContextMock.Setup(x => x.IsResolved).Returns(false);
        }
        else
        {
            _tenantContextMock.Setup(x => x.IsResolved).Returns(true);
            _tenantContextMock.Setup(x => x.UserId).Returns(userId);
            _tenantContextMock.Setup(x => x.CenterId).Returns(centerId);
            _tenantContextMock.Setup(x => x.Role).Returns(role);
        }

        using var context = CreateDbContext();
        var sut = CreateSut(context, isNull ? null : _tenantContextMock.Object);
        var result = await sut.ExecuteAsync("1");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData("Teacher")]
    [InlineData("Student")]
    [InlineData("Admin")]
    [InlineData("centerManager")]
    [InlineData("CenterManager ")]
    [InlineData("2")]
    public async Task ExecuteAsync_InvalidRole_ReturnsResourceNotFound_NoMutation(string role)
    {
        SetupTenantContext(Guid.NewGuid(), Guid.NewGuid(), role);

        using var context = CreateDbContext();
        var sut = CreateSut(context);
        var result = await sut.ExecuteAsync("1");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(" 1")]
    [InlineData("1 ")]
    [InlineData("-1")]
    [InlineData("1.5")]
    [InlineData("0")]
    [InlineData("18446744073709551616")]
    [InlineData("abc")]
    [InlineData("+1")]
    public async Task ExecuteAsync_InvalidNodeId_ReturnsValidationFailed(string rawNodeId)
    {
        SetupTenantContext(Guid.NewGuid(), Guid.NewGuid());

        using var context = CreateDbContext();
        var sut = CreateSut(context);
        var result = await sut.ExecuteAsync(rawNodeId);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Theory]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public async Task ExecuteAsync_CenterInvalid_ReturnsResourceNotFound(bool present, bool isActive, bool isDeleted)
    {
        var centerId = Guid.NewGuid();
        SetupTenantContext(centerId, Guid.NewGuid());

        var oldDate = DateTime.UtcNow;
        using (var setupContext = CreateDbContext())
        {
            if (present)
            {
                setupContext.Centers.Add(new Center
                {
                    CreatedAt = oldDate,
                    UpdatedAt = oldDate,
                    CenterCode = "C",
                    CenterName = "N",
                    Timezone = "T",
                    CenterId = centerId,
                    Status = isActive ? CenterStatus.Active : CenterStatus.Suspended,
                    IsDeleted = isDeleted
                });
                await setupContext.SaveChangesAsync();
            }
        }

        using var context = CreateDbContext();
        var sut = CreateSut(context);
        var result = await sut.ExecuteAsync("1");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_NodeMissingOrDeletedOrCrossTenant_ReturnsResourceNotFound()
    {
        var centerId = Guid.NewGuid();
        var otherCenterId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        SetupTenantContext(centerId, Guid.NewGuid());

        var utcNow = DateTime.UtcNow;
        using (var setupContext = CreateDbContext())
        {
            setupContext.Centers.Add(new Center { CreatedAt = utcNow, UpdatedAt = utcNow, CenterCode = "C", CenterName = "N", Timezone = "T", CenterId = centerId, Status = CenterStatus.Active, IsDeleted = false });
            setupContext.Subjects.Add(new Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "S", SubjectName = "S", IsActive = true, CreatedAt = utcNow, UpdatedAt = utcNow, IsDeleted = false });

            setupContext.KnowledgeNodes.Add(new KnowledgeNode { CreatedAt = utcNow, UpdatedAt = utcNow, NodeCode = "C1", NodeId = 1, CenterId = centerId, IsDeleted = true, SubjectId = subjectId, NodeName = "1", NodeType = NodeType.Topic });
            setupContext.KnowledgeNodes.Add(new KnowledgeNode { CreatedAt = utcNow, UpdatedAt = utcNow, NodeCode = "C2", NodeId = 2, CenterId = otherCenterId, IsDeleted = false, SubjectId = subjectId, NodeName = "2", NodeType = NodeType.Topic });

            await setupContext.SaveChangesAsync();
        }

        using var context = CreateDbContext();
        var sut = CreateSut(context);

        var resultDeleted = await sut.ExecuteAsync("1");
        Assert.Equal(ErrorCodes.ResourceNotFound, resultDeleted.ErrorCode);

        var resultCrossTenant = await sut.ExecuteAsync("2");
        Assert.Equal(ErrorCodes.ResourceNotFound, resultCrossTenant.ErrorCode);

        var resultMissing = await sut.ExecuteAsync("3");
        Assert.Equal(ErrorCodes.ResourceNotFound, resultMissing.ErrorCode);
    }

    [Theory]
    [InlineData("ChildNode")]
    [InlineData("EdgeSource")]
    [InlineData("EdgeTarget")]
    [InlineData("CurriculumNode")]
    [InlineData("Question")]
    [InlineData("QuestionNode")]
    [InlineData("KnowledgeTwin")]
    [InlineData("TwinHistory")]
    [InlineData("LearningPathItem")]
    [InlineData("Recommendation")]
    [InlineData("ReasoningAnalysisNumber")]
    [InlineData("ReasoningAnalysisString")]
    public async Task ExecuteAsync_HasEvidence_ReturnsInvalidStateTransition(string evidenceType)
    {
        var centerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SetupTenantContext(centerId, userId);

        var nodeId = 100UL;
        var subjectId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;

        using (var setupContext = CreateDbContext())
        {
            setupContext.Centers.Add(new Center { CreatedAt = utcNow, UpdatedAt = utcNow, CenterCode = "C", CenterName = "N", Timezone = "T", CenterId = centerId, Status = CenterStatus.Active });
            setupContext.Users.Add(new EduTwin.DAL.IdentityAndTenancy.User { UserId = userId, CenterId = centerId, Username = "U", PasswordHash = "H", RoleName = EduTwin.Contracts.IdentityAndTenancy.UserRole.Teacher, DisplayName = "D", Status = EduTwin.Contracts.IdentityAndTenancy.UserStatus.Active, CreatedAt = utcNow, UpdatedAt = utcNow });
            setupContext.Teachers.Add(new EduTwin.DAL.Organization.Teacher { TeacherId = userId, CenterId = centerId, CreatedAt = utcNow, UpdatedAt = utcNow });
            setupContext.Students.Add(new EduTwin.DAL.Organization.Student { StudentId = userId, CenterId = centerId, FullName = "F", GradeLevel = 10, CreatedAt = utcNow, UpdatedAt = utcNow });
            setupContext.Subjects.Add(new Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "S", SubjectName = "S", IsActive = true, CreatedAt = utcNow, UpdatedAt = utcNow });
            setupContext.KnowledgeNodes.Add(new KnowledgeNode { CreatedAt = utcNow, UpdatedAt = utcNow, NodeCode = "C", NodeId = nodeId, CenterId = centerId, SubjectId = subjectId, NodeName = "N", NodeType = NodeType.Topic });

            switch (evidenceType)
            {
                case "ChildNode":
                    setupContext.KnowledgeNodes.Add(new KnowledgeNode { CreatedAt = utcNow, UpdatedAt = utcNow, NodeCode = "C2", NodeId = 200, CenterId = centerId, ParentNodeId = nodeId, SubjectId = subjectId, NodeName = "C", NodeType = NodeType.Topic });
                    break;
                case "EdgeSource":
                    setupContext.KnowledgeNodes.Add(new KnowledgeNode { CreatedAt = utcNow, UpdatedAt = utcNow, NodeCode = "C3", NodeId = 200, CenterId = centerId, SubjectId = subjectId, NodeName = "C", NodeType = NodeType.Topic });
                    setupContext.KnowledgeEdges.Add(new KnowledgeEdge { CreatedAt = utcNow, UpdatedAt = utcNow, EdgeId = 1, CenterId = centerId, SourceNodeId = nodeId, TargetNodeId = 200, SubjectId = subjectId, RelationType = RelationType.PrerequisiteOf });
                    break;
                case "EdgeTarget":
                    setupContext.KnowledgeNodes.Add(new KnowledgeNode { CreatedAt = utcNow, UpdatedAt = utcNow, NodeCode = "C4", NodeId = 200, CenterId = centerId, SubjectId = subjectId, NodeName = "C", NodeType = NodeType.Topic });
                    setupContext.KnowledgeEdges.Add(new KnowledgeEdge { CreatedAt = utcNow, UpdatedAt = utcNow, EdgeId = 1, CenterId = centerId, SourceNodeId = 200, TargetNodeId = nodeId, SubjectId = subjectId, RelationType = RelationType.PrerequisiteOf });
                    break;
                case "CurriculumNode":
                    var currId = Guid.NewGuid();
                    setupContext.Curriculums.Add(new Curriculum { CurriculumId = currId, CenterId = centerId, TeacherId = userId, SubjectId = subjectId, Title = "T", ReviewStatus = ReviewStatus.Draft, CreatedAt = utcNow, UpdatedAt = utcNow });
                    setupContext.CurriculumNodes.Add(new CurriculumNode { CenterId = centerId, NodeId = nodeId, CurriculumId = currId, CreatedAt = utcNow });
                    break;
                case "Question":
                    setupContext.Questions.Add(new Question { CreatedAt = utcNow, UpdatedAt = utcNow, QuestionId = 1, CenterId = centerId, PrimaryTopicNodeId = nodeId, Difficulty = 1, SubjectId = subjectId, CreatedByTeacherId = userId, QuestionType = QuestionType.MultipleChoice, QuestionText = "Q", CorrectAnswer = "A", Solution = "S", GradingCriteria = new GradingCriteria(), MaxScore = 1, EstimatedTimeSeconds = 60, LanguageCode = "vi", Status = EduTwin.Contracts.CurriculumAndQuestions.QuestionStatus.Active });
                    break;
                case "QuestionNode":
                    setupContext.Questions.Add(new Question { CreatedAt = utcNow, UpdatedAt = utcNow, QuestionId = 1, CenterId = centerId, PrimaryTopicNodeId = 999, Difficulty = 1, SubjectId = subjectId, CreatedByTeacherId = userId, QuestionType = QuestionType.MultipleChoice, QuestionText = "Q", CorrectAnswer = "A", Solution = "S", GradingCriteria = new GradingCriteria(), MaxScore = 1, EstimatedTimeSeconds = 60, LanguageCode = "vi", Status = EduTwin.Contracts.CurriculumAndQuestions.QuestionStatus.Active });
                    setupContext.QuestionKnowledgeNodes.Add(new QuestionKnowledgeNode { QuestionId = 1, CenterId = centerId, NodeId = nodeId, MappingRole = MappingRole.Secondary, CreatedAt = utcNow });
                    break;
                case "KnowledgeTwin":
                    setupContext.KnowledgeTwins.Add(new KnowledgeTwin { KnowledgeTwinId = 1, CenterId = centerId, TopicNodeId = nodeId, StudentId = userId, SubjectId = subjectId, CreatedAt = utcNow, UpdatedAt = utcNow });
                    break;
                case "TwinHistory":
                    setupContext.TwinUpdateHistories.Add(new TwinUpdateHistory { HistoryId = 1, CenterId = centerId, TopicNodeId = nodeId, StudentId = userId, SubjectId = subjectId, CreatedAt = utcNow, EventSource = TwinEventSource.AIAnalysis, PreviousMastery = 0, NewMastery = 0, MasteryDelta = 0, CalculationVersion = "v1", CalculationBreakdown = JsonDocument.Parse("{}"), Explanation = "E" });
                    break;
                case "LearningPathItem":
                    var lpId = Guid.NewGuid();
                    setupContext.LearningPaths.Add(new LearningPath { LearningPathId = lpId, CenterId = centerId, StudentId = userId, SubjectId = subjectId, Strategy = LearningPathStrategy.LinearFallback, Status = LearningPathStatus.Active, GeneratedAt = utcNow, CreatedAt = utcNow, UpdatedAt = utcNow });
                    setupContext.LearningPathItems.Add(new LearningPathItem { LearningPathItemId = 1, CenterId = centerId, TopicNodeId = nodeId, LearningPathId = lpId, RankOrder = 1, Reason = "R", Status = LearningPathItemStatus.Pending, CreatedAt = utcNow, UpdatedAt = utcNow });
                    break;
                case "Recommendation":
                    setupContext.Recommendations.Add(new Recommendation { CreatedAt = utcNow, UpdatedAt = utcNow, RecommendationId = 1, CenterId = centerId, TopicNodeId = nodeId, StudentId = userId, SubjectId = subjectId, RecommendationType = RecommendationType.TopicAndQuestion, CalculationVersion = "v1", CalculationBreakdown = JsonDocument.Parse("{}"), Explanation = "E", Status = RecommendationStatus.Active, GeneratedAt = utcNow });
                    break;
                case "ReasoningAnalysisNumber":
                    setupContext.Attempts.Add(new Attempt { AttemptId = 1, CenterId = centerId, StudentId = userId, QuestionId = 1, FinalAnswer = "A", TimeSpentSeconds = 60, Confidence = 100, ReasoningLanguage = "vi", Status = EduTwin.Contracts.AssessmentAndReasoning.AttemptStatus.Completed, ClientSubmissionId = Guid.NewGuid(), CreatedAt = utcNow, UpdatedAt = utcNow });
                    setupContext.ReasoningAnalyses.Add(new ReasoningAnalysis { CreatedAt = utcNow, UpdatedAt = utcNow, AnalysisId = 1, CenterId = centerId, AttemptId = 1, SchemaVersion = "v1", ErrorType = ErrorType.None, MissingSteps = JsonDocument.Parse("[]"), RootCauseNodeIds = JsonDocument.Parse("[100]"), Feedback = "F", Provider = AnalysisProvider.Gemini });
                    break;
                case "ReasoningAnalysisString":
                    setupContext.Attempts.Add(new Attempt { AttemptId = 1, CenterId = centerId, StudentId = userId, QuestionId = 1, FinalAnswer = "A", TimeSpentSeconds = 60, Confidence = 100, ReasoningLanguage = "vi", Status = EduTwin.Contracts.AssessmentAndReasoning.AttemptStatus.Completed, ClientSubmissionId = Guid.NewGuid(), CreatedAt = utcNow, UpdatedAt = utcNow });
                    setupContext.ReasoningAnalyses.Add(new ReasoningAnalysis { CreatedAt = utcNow, UpdatedAt = utcNow, AnalysisId = 2, CenterId = centerId, AttemptId = 1, SchemaVersion = "v1", ErrorType = ErrorType.None, MissingSteps = JsonDocument.Parse("[]"), RootCauseNodeIds = JsonDocument.Parse("[\"100\"]"), Feedback = "F", Provider = AnalysisProvider.Gemini });
                    break;
            }
            await setupContext.SaveChangesAsync();
        }

        using var context = CreateDbContext();
        var sut = CreateSut(context);
        var result = await sut.ExecuteAsync("100");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.InvalidStateTransition, result.ErrorCode);
    }

    [Theory]
    [InlineData("{}", true)]
    [InlineData("[100.5]", true)]
    [InlineData("[-1]", true)]
    [InlineData("[\" 100 \"]", true)]
    [InlineData("[\"+100\"]", true)]
    [InlineData("[\"100.0\"]", true)]
    [InlineData("[0]", true)]
    [InlineData("[\"0\"]", true)]
    [InlineData("[\"18446744073709551616\"]", true)]
    [InlineData("[\"abc\"]", true)]
    [InlineData("[100]", true)]
    [InlineData("[\"100\"]", true)]
    [InlineData("[]", false)]
    [InlineData("[200]", false)]
    [InlineData("[\"200\"]", false)]
    public async Task ExecuteAsync_ReasoningAnalysis_FailClosedMatrix(string jsonString, bool shouldFail)
    {
        var centerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        SetupTenantContext(centerId, userId);

        var nodeId = 100UL;
        var subjectId = Guid.NewGuid();
        var utcNow = DateTime.UtcNow;

        using (var setupContext = CreateDbContext())
        {
            setupContext.Centers.Add(new Center { CreatedAt = utcNow, UpdatedAt = utcNow, CenterCode = "C", CenterName = "N", Timezone = "T", CenterId = centerId, Status = CenterStatus.Active });
            setupContext.Subjects.Add(new Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "S", SubjectName = "S", IsActive = true, CreatedAt = utcNow, UpdatedAt = utcNow });
            setupContext.KnowledgeNodes.Add(new KnowledgeNode { CreatedAt = utcNow, UpdatedAt = utcNow, NodeCode = "C", NodeId = nodeId, CenterId = centerId, SubjectId = subjectId, NodeName = "N", NodeType = NodeType.Topic });

            setupContext.Attempts.Add(new Attempt { AttemptId = 1, CenterId = centerId, StudentId = userId, QuestionId = 1, FinalAnswer = "A", TimeSpentSeconds = 60, Confidence = 100, ReasoningLanguage = "vi", Status = EduTwin.Contracts.AssessmentAndReasoning.AttemptStatus.Completed, ClientSubmissionId = Guid.NewGuid(), CreatedAt = utcNow, UpdatedAt = utcNow });

            var rootCauses = jsonString == "null_doc" ? null : JsonDocument.Parse(jsonString);
            setupContext.ReasoningAnalyses.Add(new ReasoningAnalysis { CreatedAt = utcNow, UpdatedAt = utcNow, AnalysisId = 1, CenterId = centerId, AttemptId = 1, SchemaVersion = "v1", ErrorType = ErrorType.None, MissingSteps = JsonDocument.Parse("[]"), RootCauseNodeIds = rootCauses!, Feedback = "F", Provider = AnalysisProvider.Gemini });

            await setupContext.SaveChangesAsync();
        }

        using var context = CreateDbContext();
        var sut = CreateSut(context);
        var result = await sut.ExecuteAsync("100");

        if (shouldFail)
        {
            Assert.False(result.IsSuccess);
            Assert.Equal(ErrorCodes.InvalidStateTransition, result.ErrorCode);
        }
        else
        {
            Assert.True(result.IsSuccess);
        }
    }

    [Fact]
    public async Task ExecuteAsync_DeletedOrCrossTenantEvidence_DoesNotBlockDelete()
    {
        var centerId = Guid.NewGuid();
        var otherCenterId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        SetupTenantContext(centerId, Guid.NewGuid());

        var nodeId = 100UL;
        var utcNow = DateTime.UtcNow;

        using (var setupContext = CreateDbContext())
        {
            setupContext.Centers.Add(new Center { CreatedAt = utcNow, UpdatedAt = utcNow, CenterCode = "C", CenterName = "N", Timezone = "T", CenterId = centerId, Status = CenterStatus.Active });
            setupContext.Subjects.Add(new Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "S", SubjectName = "S", IsActive = true, CreatedAt = utcNow, UpdatedAt = utcNow });
            setupContext.KnowledgeNodes.Add(new KnowledgeNode { CreatedAt = utcNow, UpdatedAt = utcNow, NodeCode = "C", NodeId = nodeId, CenterId = centerId, SubjectId = subjectId, NodeName = "N", NodeType = NodeType.Topic });

            // Deleted same-tenant child node
            setupContext.KnowledgeNodes.Add(new KnowledgeNode { CreatedAt = utcNow, UpdatedAt = utcNow, NodeCode = "C1", NodeId = 201, CenterId = centerId, ParentNodeId = nodeId, IsDeleted = true, SubjectId = subjectId, NodeName = "N1", NodeType = NodeType.Topic });

            // Cross-tenant child node (though logically invalid, just checking isolation)
            setupContext.KnowledgeNodes.Add(new KnowledgeNode { CreatedAt = utcNow, UpdatedAt = utcNow, NodeCode = "C2", NodeId = 202, CenterId = otherCenterId, ParentNodeId = nodeId, IsDeleted = false, SubjectId = subjectId, NodeName = "N2", NodeType = NodeType.Topic });

            // Deleted same-tenant edge
            setupContext.KnowledgeNodes.Add(new KnowledgeNode { CreatedAt = utcNow, UpdatedAt = utcNow, NodeCode = "C3", NodeId = 200, CenterId = centerId, SubjectId = subjectId, NodeName = "N3", NodeType = NodeType.Topic });
            setupContext.KnowledgeEdges.Add(new KnowledgeEdge { CreatedAt = utcNow, UpdatedAt = utcNow, EdgeId = 2, CenterId = centerId, SourceNodeId = nodeId, TargetNodeId = 200, IsDeleted = true, SubjectId = subjectId, RelationType = RelationType.PrerequisiteOf });

            await setupContext.SaveChangesAsync();
        }

        using var context = CreateDbContext();
        var sut = CreateSut(context);
        var result = await sut.ExecuteAsync("100");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ExecuteAsync_ConcurrencyConflict_ReturnsConcurrencyConflict()
    {
        var centerId = Guid.NewGuid();
        SetupTenantContext(centerId, Guid.NewGuid());

        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var utcNow = DateTime.UtcNow;
        var accessorMock = new Mock<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>();
        accessorMock.Setup(x => x.CenterId).Returns(centerId);

        using (var setupContext = new EduTwinDbContext(options, accessorMock.Object))
        {
            var subjectId = Guid.NewGuid();
            setupContext.Centers.Add(new Center { CreatedAt = utcNow, UpdatedAt = utcNow, CenterCode = "C", CenterName = "N", Timezone = "T", CenterId = centerId, Status = CenterStatus.Active });
            setupContext.Subjects.Add(new Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "S", SubjectName = "S", IsActive = true, CreatedAt = utcNow, UpdatedAt = utcNow });
            setupContext.KnowledgeNodes.Add(new KnowledgeNode { CreatedAt = utcNow, UpdatedAt = utcNow, NodeCode = "C", NodeId = 1, CenterId = centerId, SubjectId = subjectId, NodeName = "1", NodeType = NodeType.Topic });
            await setupContext.SaveChangesAsync();
        }

        using var context = new ConcurrencyEduTwinDbContext(options, accessorMock.Object);
        var sut = CreateSut(context);

        var result = await sut.ExecuteAsync("1");

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, result.ErrorCode);
        Assert.Empty(context.ChangeTracker.Entries());
        var trackedSoftDeleted = context.ChangeTracker.Entries<KnowledgeNode>().Any(x => x.Entity.IsDeleted);
        Assert.False(trackedSoftDeleted);
    }

    [Fact]
    public async Task ExecuteAsync_DbUpdateException_Throws()
    {
        var centerId = Guid.NewGuid();
        SetupTenantContext(centerId, Guid.NewGuid());

        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        var utcNow = DateTime.UtcNow;
        var accessorMock = new Mock<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>();
        accessorMock.Setup(x => x.CenterId).Returns(centerId);

        using (var setupContext = new EduTwinDbContext(options, accessorMock.Object))
        {
            var subjectId = Guid.NewGuid();
            setupContext.Centers.Add(new Center { CreatedAt = utcNow, UpdatedAt = utcNow, CenterCode = "C", CenterName = "N", Timezone = "T", CenterId = centerId, Status = CenterStatus.Active });
            setupContext.Subjects.Add(new Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "S", SubjectName = "S", IsActive = true, CreatedAt = utcNow, UpdatedAt = utcNow });
            setupContext.KnowledgeNodes.Add(new KnowledgeNode { CreatedAt = utcNow, UpdatedAt = utcNow, NodeCode = "C", NodeId = 1, CenterId = centerId, SubjectId = subjectId, NodeName = "1", NodeType = NodeType.Topic });
            await setupContext.SaveChangesAsync();
        }

        using var context = new DbUpdateExceptionEduTwinDbContext(options, accessorMock.Object);
        var sut = CreateSut(context);

        await Assert.ThrowsAsync<DbUpdateException>(() => sut.ExecuteAsync("1"));
    }

    [Fact]
    public async Task ExecuteAsync_PassesCancellationToken_ThrowsOperationCanceledException()
    {
        var centerId = Guid.NewGuid();
        SetupTenantContext(centerId, Guid.NewGuid());

        var utcNow = DateTime.UtcNow;
        using (var setupContext = CreateDbContext())
        {
            var subjectId = Guid.NewGuid();
            setupContext.Centers.Add(new Center { CreatedAt = utcNow, UpdatedAt = utcNow, CenterCode = "C", CenterName = "N", Timezone = "T", CenterId = centerId, Status = CenterStatus.Active });
            setupContext.Subjects.Add(new Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "S", SubjectName = "S", IsActive = true, CreatedAt = utcNow, UpdatedAt = utcNow });
            setupContext.KnowledgeNodes.Add(new KnowledgeNode { CreatedAt = utcNow, UpdatedAt = utcNow, NodeCode = "C", NodeId = 1, CenterId = centerId, SubjectId = subjectId, NodeName = "1", NodeType = NodeType.Topic });
            await setupContext.SaveChangesAsync();
        }

        using var context = CreateDbContext();
        var sut = CreateSut(context);

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var ex = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => sut.ExecuteAsync("1", cts.Token));
        Assert.Equal(cts.Token, ex.CancellationToken);
    }
}
