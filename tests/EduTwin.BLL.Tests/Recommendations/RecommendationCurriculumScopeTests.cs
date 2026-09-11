using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Recommendations;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.DigitalTwin;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace EduTwin.BLL.Tests.Recommendations;

public sealed class RecommendationCurriculumScopeTests : IDisposable
{
    private readonly Guid _centerId = Guid.NewGuid();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Guid _subjectId = Guid.NewGuid();
    private readonly Guid _classId = Guid.NewGuid();
    private readonly Guid _teacherId = Guid.NewGuid();
    private readonly DateTime _utcNow = new(2026, 9, 11, 10, 0, 0, DateTimeKind.Utc);
    private readonly EduTwinDbContext _dbContext;

    public RecommendationCurriculumScopeTests()
    {
        var tenant = new TenantContext();
        tenant.Initialize(_centerId, _studentId, "Student", 1);
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase($"RecommendationCurriculumScope_{Guid.NewGuid():N}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        _dbContext = new EduTwinDbContext(options, tenant);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task DraftAndArchivedAssignments_AreIgnored_WhenPublishedCurriculumExists()
    {
        await SeedClassAndTopicsAsync();
        var publishedId = await AddCurriculumAsync(ReviewStatus.Published, "Published");
        var draftId = await AddCurriculumAsync(ReviewStatus.Draft, "Draft");
        var archivedId = await AddCurriculumAsync(ReviewStatus.Archived, "Archived");

        _dbContext.CurriculumNodes.AddRange(
            CurriculumNode(publishedId, nodeId: 2, orderIndex: 1),
            CurriculumNode(draftId, nodeId: 1, orderIndex: 1),
            CurriculumNode(archivedId, nodeId: 1, orderIndex: 1));
        await _dbContext.SaveChangesAsync();

        var result = await new OpportunityCandidateBuilder(_dbContext)
            .BuildCandidatesAsync(_centerId, _studentId, _subjectId, CancellationToken.None);

        Assert.Null(result.BlockedReason);
        Assert.Collection(result.AllActiveTopicNodes, node => Assert.Equal(2ul, node.NodeId));
    }

    [Fact]
    public async Task PublishedCurriculum_UsesCurriculumNodeOrder_NotGlobalKnowledgeNodeOrder()
    {
        await SeedClassAndTopicsAsync();
        var curriculumId = await AddCurriculumAsync(ReviewStatus.Published, "Published");
        _dbContext.CurriculumNodes.AddRange(
            CurriculumNode(curriculumId, nodeId: 1, orderIndex: 2),
            CurriculumNode(curriculumId, nodeId: 2, orderIndex: 1));
        await _dbContext.SaveChangesAsync();

        var result = await new OpportunityCandidateBuilder(_dbContext)
            .BuildCandidatesAsync(_centerId, _studentId, _subjectId, CancellationToken.None);

        Assert.Equal(new ulong[] { 2, 1 }, result.AllActiveTopicNodes.Select(n => n.NodeId));
        Assert.Equal(1u, result.EligibleCandidates.Single(c => c.TopicNodeId == 2).OrderIndex);
        Assert.Equal(2u, result.EligibleCandidates.Single(c => c.TopicNodeId == 1).OrderIndex);
    }

    [Fact]
    public async Task PrerequisiteOutsideCurriculum_UsesSubjectMastery_AndUnlocksCandidate()
    {
        await SeedClassAndTopicsAsync();
        var curriculumId = await AddCurriculumAsync(ReviewStatus.Published, "Published");
        _dbContext.CurriculumNodes.Add(CurriculumNode(curriculumId, nodeId: 2, orderIndex: 1));
        _dbContext.KnowledgeEdges.Add(new KnowledgeEdge
        {
            CenterId = _centerId,
            EdgeId = 100,
            SubjectId = _subjectId,
            SourceNodeId = 1,
            TargetNodeId = 2,
            RelationType = RelationType.PrerequisiteOf,
            Weight = 1m,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        });
        _dbContext.KnowledgeTwins.AddRange(
            Twin(nodeId: 1, mastery: 90m, id: 201),
            Twin(nodeId: 2, mastery: 20m, id: 202));
        await _dbContext.SaveChangesAsync();

        var result = await new OpportunityCandidateBuilder(_dbContext)
            .BuildCandidatesAsync(_centerId, _studentId, _subjectId, CancellationToken.None);

        var candidate = Assert.Single(result.EligibleCandidates);
        Assert.Equal(2ul, candidate.TopicNodeId);
        Assert.Equal(new[] { 90m }, candidate.PrerequisiteMasteries);
    }

    private async Task SeedClassAndTopicsAsync()
    {
        _dbContext.Classes.Add(new Class
        {
            CenterId = _centerId,
            ClassId = _classId,
            TeacherId = _teacherId,
            SubjectId = _subjectId,
            ClassName = "R07",
            AcademicYear = "2026",
            Status = ClassStatus.Active,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        });
        _dbContext.ClassStudents.Add(new ClassStudent
        {
            CenterId = _centerId,
            ClassId = _classId,
            StudentId = _studentId,
            JoinedAt = _utcNow,
            Status = ClassStudentStatus.Active
        });
        _dbContext.KnowledgeNodes.AddRange(
            Topic(nodeId: 1, globalOrder: 1),
            Topic(nodeId: 2, globalOrder: 2));
        await _dbContext.SaveChangesAsync();
    }

    private async Task<Guid> AddCurriculumAsync(ReviewStatus status, string title)
    {
        var curriculumId = Guid.NewGuid();
        _dbContext.Curriculums.Add(new Curriculum
        {
            CenterId = _centerId,
            CurriculumId = curriculumId,
            TeacherId = _teacherId,
            SubjectId = _subjectId,
            Title = title,
            ReviewStatus = status,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        });
        _dbContext.CurriculumClasses.Add(new CurriculumClass
        {
            CenterId = _centerId,
            CurriculumId = curriculumId,
            ClassId = _classId,
            AssignedAt = _utcNow,
            AssignedBy = _teacherId
        });
        await _dbContext.SaveChangesAsync();
        return curriculumId;
    }

    private CurriculumNode CurriculumNode(Guid curriculumId, ulong nodeId, uint orderIndex) =>
        new()
        {
            CenterId = _centerId,
            CurriculumId = curriculumId,
            NodeId = nodeId,
            OrderIndex = orderIndex,
            CreatedAt = _utcNow
        };

    private KnowledgeNode Topic(ulong nodeId, uint globalOrder) =>
        new()
        {
            CenterId = _centerId,
            SubjectId = _subjectId,
            NodeId = nodeId,
            NodeCode = $"T-{nodeId}",
            NodeName = $"Topic {nodeId}",
            NodeType = NodeType.Topic,
            OrderIndex = globalOrder,
            ExamImportance = 50m,
            EstimatedLearningMinutes = 60,
            IsActive = true,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        };

    private KnowledgeTwin Twin(ulong nodeId, decimal mastery, ulong id) =>
        new()
        {
            CenterId = _centerId,
            KnowledgeTwinId = id,
            StudentId = _studentId,
            SubjectId = _subjectId,
            TopicNodeId = nodeId,
            MasteryPercentage = mastery,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        };
}
