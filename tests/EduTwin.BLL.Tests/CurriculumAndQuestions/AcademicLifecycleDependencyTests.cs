using EduTwin.BLL.CurriculumAndQuestions;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.KnowledgeGraph;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Models;
using EduTwin.DAL.Persistence.Tenancy;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.CurriculumAndQuestions;

public sealed class AcademicLifecycleDependencyTests
{
    private sealed class Fixture : IDisposable
    {
        public Guid Center { get; } = Guid.NewGuid();
        public Guid Actor { get; } = Guid.NewGuid();
        public Guid Subject { get; } = Guid.NewGuid();
        public Guid CurriculumId { get; } = Guid.NewGuid();
        public EduTwinDbContext Db { get; }
        public Mock<ITenantContext> Tenant { get; } = new();
        public Fixture()
        {
            Tenant.SetupGet(t => t.IsResolved).Returns(true);
            Tenant.SetupGet(t => t.CenterId).Returns(Center);
            Tenant.SetupGet(t => t.UserId).Returns(Actor);
            Tenant.SetupGet(t => t.Role).Returns(nameof(UserRole.Teacher));
            var accessor = new Mock<ITenantIdAccessor>(); accessor.SetupGet(t => t.CenterId).Returns(Center);
            Db = new EduTwinDbContext(new DbContextOptionsBuilder<EduTwinDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, accessor.Object);
            Db.Centers.Add(new Center { CenterId=Center, CenterCode="C", CenterName="C", Timezone="UTC", Status=CenterStatus.Active, CreatedAt=DateTime.UtcNow, UpdatedAt=DateTime.UtcNow });
            Db.Subjects.Add(new Subject { CenterId=Center, SubjectId=Subject, SubjectCode="M", SubjectName="Toán", IsActive=true });
            Db.KnowledgeNodes.AddRange(new KnowledgeNode { CenterId=Center, SubjectId=Subject, NodeId=1, NodeCode="N1", NodeName="N1", NodeType=NodeType.Topic, IsActive=true, EstimatedLearningMinutes=30 },
                new KnowledgeNode { CenterId=Center, SubjectId=Subject, NodeId=2, NodeCode="N2", NodeName="N2", NodeType=NodeType.Topic, IsActive=true, EstimatedLearningMinutes=30 });
            Db.Curriculums.Add(new Curriculum { CenterId=Center, CurriculumId=CurriculumId, TeacherId=Actor, SubjectId=Subject,
                Title="Giáo trình Shared", Visibility=MaterialVisibility.Shared, ReviewStatus=ReviewStatus.Published });
            Db.CurriculumNodes.Add(new CurriculumNode { CenterId=Center, CurriculumId=CurriculumId, NodeId=1, CreatedAt=DateTime.UtcNow });
            Db.KnowledgeEdges.Add(new KnowledgeEdge { CenterId=Center, SubjectId=Subject, EdgeId=1, SourceNodeId=1, TargetNodeId=2, RelationType=RelationType.PrerequisiteOf, Weight=1 });
            SaveSeed();
        }
        public void SaveSeed()
        {
            foreach (var entry in Db.ChangeTracker.Entries<IMutableTenantAggregate>().Where(e => e.State == EntityState.Added))
            {
                entry.Entity.CreatedAt=DateTime.UtcNow;
                entry.Entity.UpdatedAt=DateTime.UtcNow;
            }
            Db.SaveChanges();
        }
        public void Application(bool archived=false, bool ended=false)
        {
            var id = Guid.NewGuid();
            Db.Classes.Add(new Class { CenterId=Center, ClassId=id, TeacherId=Guid.NewGuid(), SubjectId=Subject,
                ClassName="Lớp của giáo viên khác", AcademicYear="2026", Status=archived ? ClassStatus.Archived : ClassStatus.Active,
                LearningScope=archived ? ClassLearningScope.History : ClassLearningScope.Current });
            Db.ClassCurriculumApplications.Add(new ClassCurriculumApplication { CenterId=Center, ApplicationId=Guid.NewGuid(), ClassId=id,
                CurriculumId=CurriculumId, SubjectId=Subject, ApplicationRole="Primary", AssignedBy=Actor,
                StartedAt=DateTime.UtcNow.AddDays(-1), EndedAt=ended ? DateTime.UtcNow : null, EndedBy=ended ? Actor : null });
            SaveSeed();
        }
        public UpdateKnowledgeNodeRequest NodeRequest() => new() { NodeName="N1", ParentNodeId=null, Description=null,
            IsActive=true, EstimatedLearningMinutes=30, ExamImportance=0, OrderIndex=0, RowVersion="1" };
        public UpdateKnowledgeNodeUseCase NodeService() => new(Db,Tenant.Object,TimeProvider.System,new KnowledgeNodeHierarchyCycleDetector());
        public void Dispose() => Db.Dispose();
    }

    [Fact]
    public async Task Archive_BlocksOtherTeachersSharedClass_UsagePreviewCoversAllOwners()
    {
        using var f = new Fixture(); f.Application();
        var result = await new ArchiveCurriculumUseCase(f.Db,f.Tenant.Object,TimeProvider.System).ExecuteAsync(f.CurriculumId,new() { RowVersion="1", Reason="Thay phiên bản" });
        Assert.Equal(ErrorCodes.InvalidStateTransition,result.ErrorCode);
        Assert.Contains("Lớp của giáo viên khác",result.Message);
        Assert.Equal(ReviewStatus.Published,(await f.Db.Curriculums.SingleAsync()).ReviewStatus);
        Assert.Null((await f.Db.ClassCurriculumApplications.SingleAsync()).EndedAt);
        Assert.Empty(await f.Db.AuthorizationAuditLogs.ToListAsync());
        Assert.Single((await new CurriculumApplicationUseCase(f.Db,f.Tenant.Object,TimeProvider.System).UsageAsync(f.CurriculumId,default))!);
    }

    [Fact]
    public async Task Archive_ArchivedClassAllowed_PreservesCompositionAndReason()
    {
        using var f = new Fixture(); f.Application(archived:true);
        var result = await new ArchiveCurriculumUseCase(f.Db,f.Tenant.Object,TimeProvider.System).ExecuteAsync(f.CurriculumId,new() { RowVersion="1", Reason="Kết thúc giáo trình cũ" });
        Assert.True(result.IsSuccess,result.ErrorCode);
        Assert.Equal("Kết thúc giáo trình cũ",(await f.Db.ClassCurriculumApplications.SingleAsync()).EndReason);
        Assert.Single(await f.Db.CurriculumNodes.ToListAsync());
        Assert.Equal(2,await f.Db.KnowledgeNodes.CountAsync());
        Assert.Equal("CurriculumArchived",(await f.Db.AuthorizationAuditLogs.SingleAsync()).ActionType);
    }

    [Fact]
    public async Task Archive_RequiresReason_EndedLedgerDoesNotResurrectPlanningLink()
    {
        using var f = new Fixture(); f.Application(ended:true);
        f.Db.CurriculumClasses.Add(new CurriculumClass { CenterId=f.Center, CurriculumId=f.CurriculumId, ClassId=(await f.Db.Classes.SingleAsync()).ClassId, AssignedAt=DateTime.UtcNow, AssignedBy=f.Actor }); await f.Db.SaveChangesAsync();
        Assert.Empty(await AcademicDependencyGuards.CurriculumUsageAsync(f.Db,f.Center,f.CurriculumId,default));
        Assert.Equal(ErrorCodes.ValidationFailed,(await new ArchiveCurriculumUseCase(f.Db,f.Tenant.Object,TimeProvider.System)
            .ExecuteAsync(f.CurriculumId,new() { RowVersion="1" })).ErrorCode);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("description")]
    [InlineData("parent")]
    public async Task FrozenNode_ContentAndParentCannotBeOverwritten(string field)
    {
        using var f = new Fixture(); var request=f.NodeRequest();
        if (field=="name") request.NodeName="Nội dung mới";
        if (field=="description") request.Description="Đổi khái niệm";
        if (field=="parent") request.ParentNodeId="2";
        var result = await f.NodeService().ExecuteAsync("1",request);
        Assert.Equal(ErrorCodes.InvalidStateTransition,result.ErrorCode); Assert.Contains("Giáo trình Shared",result.Message);
        Assert.Equal("N1",(await f.Db.KnowledgeNodes.SingleAsync(n=>n.NodeId==1)).NodeName);
        Assert.Empty(await f.Db.AuthorizationAuditLogs.ToListAsync());
    }

    [Fact]
    public async Task MetadataEditable_WithAtomicBeforeAfterAudit()
    {
        using var f=new Fixture(); f.Application(); var request=f.NodeRequest(); request.ExamImportance=75; request.EstimatedLearningMinutes=45;
        Assert.True((await f.NodeService().ExecuteAsync("1",request)).IsSuccess);
        var audit=await f.Db.AuthorizationAuditLogs.SingleAsync();
        Assert.Equal(f.Actor,audit.ActorUserId); Assert.Contains("75",audit.AfterData); Assert.Contains("30",audit.BeforeData);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Deactivation_LiveClassBlocked_HistoricalClassAllowed(bool archived)
    {
        using var f=new Fixture(); f.Application(archived:archived); var request=f.NodeRequest(); request.IsActive=false;
        var result=await f.NodeService().ExecuteAsync("1",request);
        Assert.Equal(archived,result.IsSuccess);
        Assert.Equal(archived ? null : ErrorCodes.InvalidStateTransition,result.ErrorCode);
        Assert.Equal(!archived,(await f.Db.KnowledgeNodes.SingleAsync(n=>n.NodeId==1)).IsActive);
    }

    [Fact]
    public async Task ActiveQuestionBlocksRetirementWithoutLiveClass()
    {
        using var f=new Fixture();
        f.Db.Questions.Add(new Question { CenterId=f.Center, SubjectId=f.Subject, QuestionId=5, PrimaryTopicNodeId=1,
            CreatedByTeacherId=f.Actor, Status=QuestionStatus.Active, QuestionText="Q", CorrectAnswer="A", Solution="S", LanguageCode="vi" });
        f.SaveSeed(); var request=f.NodeRequest(); request.IsActive=false;
        Assert.Equal(ErrorCodes.InvalidStateTransition,(await f.NodeService().ExecuteAsync("1",request)).ErrorCode);
    }

    [Theory]
    [InlineData("create")]
    [InlineData("update")]
    [InlineData("delete")]
    public async Task LiveEdgeUsageBlocksChanges_NoStoredGraphOrAuditMutation(string operation)
    {
        using var f=new Fixture(); f.Application(); string? code;
        if(operation=="create") code=(await new CreateKnowledgeEdgeUseCase(f.Db,f.Tenant.Object,TimeProvider.System,new KnowledgeGraphValidator())
            .ExecuteAsync(new() { SubjectId=f.Subject, SourceNodeId="2",TargetNodeId="1",RelationType="RelatedTo",Weight=1 },default)).ErrorCode;
        else if(operation=="update") code=(await new UpdateKnowledgeEdgeUseCase(f.Db,f.Tenant.Object,TimeProvider.System).ExecuteAsync("1",new() { Weight=0.5m,RowVersion="1" })).ErrorCode;
        else code=(await new DeleteKnowledgeEdgeUseCase(f.Db,f.Tenant.Object,TimeProvider.System).ExecuteAsync("1")).ErrorCode;
        Assert.Equal(ErrorCodes.InvalidStateTransition,code);
        Assert.False((await f.Db.KnowledgeEdges.SingleAsync()).IsDeleted); Assert.Equal(1,(await f.Db.KnowledgeEdges.SingleAsync()).Weight);
        Assert.Empty(await f.Db.AuthorizationAuditLogs.ToListAsync());
    }

    [Fact]
    public async Task InactiveEndpointRejected_HistoricalClassAllowsAuditedWeightChange()
    {
        using var f=new Fixture(); f.Application(archived:true);
        var node=await f.Db.KnowledgeNodes.SingleAsync(n=>n.NodeId==2); node.IsActive=false; await f.Db.SaveChangesAsync();
        Assert.Equal(ErrorCodes.InvalidStateTransition,(await new CreateKnowledgeEdgeUseCase(f.Db,f.Tenant.Object,TimeProvider.System,new KnowledgeGraphValidator())
            .ExecuteAsync(new() { SubjectId=f.Subject,SourceNodeId="2",TargetNodeId="1",RelationType="RelatedTo",Weight=1 },default)).ErrorCode);
        node.IsActive=true; await f.Db.SaveChangesAsync();
        Assert.True((await new UpdateKnowledgeEdgeUseCase(f.Db,f.Tenant.Object,TimeProvider.System).ExecuteAsync("1",new() { Weight=.5m,RowVersion="1" })).IsSuccess);
        Assert.Equal("KnowledgeEdgeUpdated",(await f.Db.AuthorizationAuditLogs.SingleAsync()).ActionType);
    }

    [Fact]
    public async Task ForeignActorAndTenantCannotReadUsageOrArchive()
    {
        using var f=new Fixture(); f.Application(); f.Tenant.SetupGet(t=>t.UserId).Returns(Guid.NewGuid());
        Assert.Null(await new CurriculumApplicationUseCase(f.Db,f.Tenant.Object,TimeProvider.System).UsageAsync(f.CurriculumId,default));
        var archive=new ArchiveCurriculumUseCase(f.Db,f.Tenant.Object,TimeProvider.System);
        Assert.Equal(ErrorCodes.ResourceNotFound,(await archive.ExecuteAsync(f.CurriculumId,new() { RowVersion="1",Reason="Test" })).ErrorCode);
        f.Tenant.SetupGet(t=>t.CenterId).Returns(Guid.NewGuid());
        Assert.Equal(ErrorCodes.ResourceNotFound,(await archive.ExecuteAsync(f.CurriculumId,new() { RowVersion="1",Reason="Test" })).ErrorCode);
    }

    [Fact]
    public async Task RestoringDeletedCodeCannotOverwritePublishedHistory()
    {
        using var f=new Fixture();var node=await f.Db.KnowledgeNodes.SingleAsync(n=>n.NodeId==1);
        node.IsDeleted=true;await f.Db.SaveChangesAsync();
        var result=await new CreateKnowledgeNodeUseCase(f.Db,f.Tenant.Object,TimeProvider.System).ExecuteAsync(new() {
            SubjectId=f.Subject,NodeCode="N1",NodeType="Topic",NodeName="Different concept",IsActive=true,EstimatedLearningMinutes=30,ExamImportance=0 });
        Assert.Equal(ErrorCodes.InvalidStateTransition,result.ErrorCode);
        Assert.True((await f.Db.KnowledgeNodes.IgnoreQueryFilters().SingleAsync(n=>n.NodeId==1)).IsDeleted);
        Assert.Empty(await f.Db.AuthorizationAuditLogs.ToListAsync());
    }
}
