using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.KnowledgeGraph;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Tests.KnowledgeGraph;

/// <summary>Editing a topic's level/code from the framework editor, and deleting draft-only topics.</summary>
public sealed class KnowledgeNodeEditAndDraftCleanupTests
{
    private readonly Mock<ITenantContext> _tenant = new();
    private readonly DbContextOptions<EduTwinDbContext> _options = new DbContextOptionsBuilder<EduTwinDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
    private readonly Guid _center = Guid.NewGuid();
    private readonly Guid _subject = Guid.NewGuid();
    private readonly DateTime _now = new(2026, 11, 12, 8, 0, 0, DateTimeKind.Utc);

    public KnowledgeNodeEditAndDraftCleanupTests()
    {
        _tenant.SetupGet(x => x.IsResolved).Returns(true);
        _tenant.SetupGet(x => x.CenterId).Returns(_center);
        _tenant.SetupGet(x => x.UserId).Returns(Guid.NewGuid());
        _tenant.SetupGet(x => x.Role).Returns(nameof(UserRole.Teacher));
        using var db = Db();
        db.Centers.Add(new Center { CenterId = _center, CenterCode = "C", CenterName = "C", Timezone = "UTC", Status = CenterStatus.Active, CreatedAt = _now, UpdatedAt = _now });
        db.Subjects.Add(new Subject { SubjectId = _subject, CenterId = _center, SubjectCode = "MATH", SubjectName = "Toán", IsActive = true, CreatedAt = _now, UpdatedAt = _now });
        db.SaveChanges();
    }

    private EduTwinDbContext Db()
    {
        var accessor = new Mock<EduTwin.DAL.Persistence.Tenancy.ITenantIdAccessor>();
        accessor.Setup(x => x.CenterId).Returns(() => _tenant.Object.CenterId);
        return new EduTwinDbContext(_options, accessor.Object);
    }

    private TimeProvider Clock()
    {
        var clock = new Mock<TimeProvider>();
        clock.Setup(x => x.GetUtcNow()).Returns(new DateTimeOffset(_now));
        return clock.Object;
    }

    private void AddNode(ulong id, string code, bool deleted = false)
    {
        using var db = Db();
        db.KnowledgeNodes.Add(new KnowledgeNode
        {
            NodeId = id, CenterId = _center, SubjectId = _subject, NodeType = NodeType.Topic, NodeCode = code, NodeName = "Giải tích thực và phức",
            ExamImportance = 10, EstimatedLearningMinutes = 45, OrderIndex = 0, IsActive = true, IsDeleted = deleted, DeletedAt = deleted ? _now : null, RowVersion = 1, CreatedAt = _now, UpdatedAt = _now,
        });
        db.SaveChanges();
    }

    private Guid AddVersion(string status, params (ulong node, ulong? parent)[] mappings)
    {
        using var db = Db();
        var id = Guid.NewGuid();
        // RowVersion starts at 1 (set by SaveChanges for added rows).
        db.KnowledgeGraphVersions.Add(new KnowledgeGraphVersion { GraphVersionId = id, CenterId = _center, SubjectId = _subject, GradeLevel = 12, Status = status, Title = status });
        foreach (var (node, parent) in mappings)
            db.KnowledgeNodeGrades.Add(new KnowledgeNodeGrade { CenterId = _center, GraphVersionId = id, NodeId = node, ParentNodeId = parent });
        db.SaveChanges();
        return id;
    }

    private static UpdateKnowledgeNodeRequest Edit(string? type = null, string? code = null, string name = "Giải tích thực và phức") => new()
    {
        NodeName = name, NodeType = type, NodeCode = code, ExamImportance = 10, EstimatedLearningMinutes = 45, IsActive = true, RowVersion = "1", OrderIndex = 0,
    };

    private Task<UpdateKnowledgeNodeResult> Update(string id, UpdateKnowledgeNodeRequest request)
    {
        var db = Db();
        return new UpdateKnowledgeNodeUseCase(db, _tenant.Object, Clock(), new KnowledgeNodeHierarchyCycleDetector()).ExecuteAsync(id, request);
    }

    [Fact]
    public async Task Update_ChangesLevelAndCode_ForDraftTopic()
    {
        AddNode(1, "MATH12_REALNFAKE");
        AddVersion("Draft", (1, null));

        var result = await Update("1", Edit(type: "Chapter", code: "MATH12_C_GTTP_01"));

        Assert.True(result.IsSuccess, result.ErrorCode);
        using var db = Db();
        var node = await db.KnowledgeNodes.SingleAsync(n => n.NodeId == 1);
        Assert.Equal(NodeType.Chapter, node.NodeType);
        Assert.Equal("MATH12_C_GTTP_01", node.NodeCode);
        Assert.Equal("MATH12_C_GTTP_01", result.Data!.NodeCode);
        Assert.Equal("Chapter", result.Data.NodeType);
    }

    [Fact]
    public async Task Update_WithoutTypeOrCode_KeepsThem()
    {
        AddNode(1, "LEGACY.CODE");
        var result = await Update("1", Edit(name: "Tên mới"));
        Assert.True(result.IsSuccess);
        using var db = Db();
        var node = await db.KnowledgeNodes.SingleAsync(n => n.NodeId == 1);
        Assert.Equal("LEGACY.CODE", node.NodeCode);
        Assert.Equal(NodeType.Topic, node.NodeType);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)] // a deleted node's code would be restored by a later create
    public async Task Update_CodeUsedByAnotherNode_IsDuplicate(bool otherDeleted)
    {
        AddNode(1, "MATH12_T_GTTP_01");
        AddNode(2, "MATH12_T_GTTP_02", deleted: otherDeleted);
        var result = await Update("1", Edit(code: "MATH12_T_GTTP_02"));
        Assert.Equal(ErrorCodes.DuplicateResource, result.ErrorCode);
    }

    [Theory]
    [InlineData("Published")]
    [InlineData("Archived")]
    public async Task Update_CodeOrLevelOfTopicInApprovedFramework_IsBlocked(string status)
    {
        AddNode(1, "MATH12_T_GTTP_01");
        AddVersion(status, (1, null));
        Assert.Equal(ErrorCodes.InvalidStateTransition, (await Update("1", Edit(code: "MATH12_T_GTTP_09"))).ErrorCode);
        Assert.Equal(ErrorCodes.InvalidStateTransition, (await Update("1", Edit(type: "Skill"))).ErrorCode);
        Assert.True((await Update("1", Edit(code: "MATH12_T_GTTP_01", type: "Topic"))).IsSuccess, "unchanged values are not an edit");
    }

    [Theory]
    [InlineData("topic", null)]
    [InlineData("Unknown", null)]
    [InlineData(null, "   ")]
    [InlineData(null, "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA")]
    public async Task Update_InvalidLevelOrCode_IsValidationFailed(string? type, string? code)
    {
        AddNode(1, "MATH12_T_GTTP_01");
        Assert.Equal(ErrorCodes.ValidationFailed, (await Update("1", Edit(type: type, code: code))).ErrorCode);
    }

    [Fact]
    public async Task Delete_RemovesTopicFromDraftFrameworks_AndBumpsTheirVersion()
    {
        AddNode(1, "MATH12_T_GTTP_01");
        AddNode(2, "MATH12_T_DSTT_01");
        AddNode(3, "MATH12_T_OTHER_01");
        var draft = AddVersion("Draft", (1, null), (2, 1));
        var unrelated = AddVersion("Draft", (3, null));

        using (var db = Db())
        {
            var result = await new DeleteKnowledgeNodeUseCase(db, _tenant.Object, Clock()).ExecuteAsync("1");
            Assert.True(result.IsSuccess, result.ErrorCode);
        }

        using var check = Db();
        var mappings = await check.KnowledgeNodeGrades.Where(m => m.GraphVersionId == draft).ToListAsync();
        Assert.DoesNotContain(mappings, m => m.NodeId == 1);
        Assert.Null(mappings.Single(m => m.NodeId == 2).ParentNodeId);
        Assert.Equal(2UL, (await check.KnowledgeGraphVersions.SingleAsync(v => v.GraphVersionId == draft)).RowVersion);
        Assert.Equal(1UL, (await check.KnowledgeGraphVersions.SingleAsync(v => v.GraphVersionId == unrelated)).RowVersion);
        Assert.True((await check.KnowledgeNodes.IgnoreQueryFilters().SingleAsync(n => n.NodeId == 1)).IsDeleted);
    }

    [Fact]
    public async Task Delete_TopicInPublishedFramework_IsStillBlocked()
    {
        AddNode(1, "MATH12_T_GTTP_01");
        var draft = AddVersion("Draft", (1, null));
        AddVersion("Published", (1, null));

        using (var db = Db())
            Assert.Equal(ErrorCodes.InvalidStateTransition, (await new DeleteKnowledgeNodeUseCase(db, _tenant.Object, Clock()).ExecuteAsync("1")).ErrorCode);

        using var check = Db();
        Assert.Single(await check.KnowledgeNodeGrades.Where(m => m.GraphVersionId == draft).ToListAsync());
    }
}
