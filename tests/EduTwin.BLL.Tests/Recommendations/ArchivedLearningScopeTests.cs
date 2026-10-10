using System.Text.Json;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.AssessmentAndReasoning;
using EduTwin.BLL.AssessmentAndReasoning.PreliminaryGrading;
using EduTwin.BLL.Organization;
using EduTwin.BLL.Recommendations;
using EduTwin.BLL.Recommendations.UseCases;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.Contracts.Organization;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Recommendations;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.Recommendations;

public sealed class ArchivedLearningScopeTests : IDisposable
{
    private readonly Guid center=Guid.NewGuid(),student=Guid.NewGuid(),subject=Guid.NewGuid(),teacher=Guid.NewGuid(),oldClass=Guid.NewGuid(),currentClass=Guid.NewGuid();
    private readonly DateTime now=DateTime.UtcNow;
    private readonly EduTwinDbContext db;
    private readonly TenantContext tenant;
    public ArchivedLearningScopeTests()
    {
        tenant=new();tenant.Initialize(center,student,"Student",1);
        db=new(new DbContextOptionsBuilder<EduTwinDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,tenant);
    }
    public void Dispose()=>db.Dispose();
    private async Task Seed(bool withCurrent=true)
    {
        db.Students.Add(new Student{CenterId=center,StudentId=student,FullName="Synthetic",CreatedAt=now,UpdatedAt=now});
        db.Subjects.Add(new Subject{CenterId=center,SubjectId=subject,SubjectCode="M",SubjectName="Math",IsActive=true,CreatedAt=now,UpdatedAt=now});
        foreach(var (id,archived,node) in new[]{(oldClass,true,1ul),(currentClass,false,2ul)})
        {
            db.KnowledgeNodes.Add(new KnowledgeNode{CenterId=center,SubjectId=subject,NodeId=node,NodeCode=node.ToString(),NodeName="Topic",NodeType=NodeType.Topic,IsActive=true,CreatedAt=now,UpdatedAt=now});
            if(!withCurrent&&!archived)continue;
            db.Classes.Add(new Class{CenterId=center,ClassId=id,SubjectId=subject,TeacherId=teacher,ClassName=id.ToString(),AcademicYear="2026",Status=archived?ClassStatus.Archived:ClassStatus.Active,
                LearningScope=archived?ClassLearningScope.History:ClassLearningScope.Current,CreatedAt=now,UpdatedAt=now});
            db.ClassStudents.Add(new ClassStudent{CenterId=center,ClassId=id,StudentId=student,Status=ClassStudentStatus.Active,JoinedAt=now.AddDays(-10)});
            var curriculum=Guid.NewGuid();
            db.Curriculums.Add(new Curriculum{CenterId=center,CurriculumId=curriculum,TeacherId=teacher,SubjectId=subject,Title="Synthetic",ReviewStatus=ReviewStatus.Published,CreatedAt=now,UpdatedAt=now});
            db.CurriculumNodes.Add(new CurriculumNode{CenterId=center,CurriculumId=curriculum,NodeId=node,OrderIndex=1,CreatedAt=now});
            db.ClassCurriculumApplications.Add(new(){CenterId=center,ApplicationId=Guid.NewGuid(),ClassId=id,CurriculumId=curriculum,SubjectId=subject,AssignedBy=teacher,StartedAt=now});
        }
        await db.SaveChangesAsync();
    }
    [Fact]
    public async Task QuestionnaireTopics_AreCurrentClassOnly_NoArchivedOrOtherClassFallback()
    {
        await Seed();
        var reader = new GetLearningPathTopicsUseCase(db, tenant);
        var topics = await reader.ExecuteScopedAsync(subject, currentClass, false, default);
        Assert.Equal(new[] { "2" }, topics.Select(t => t.TopicNodeId));
        Assert.Empty(await reader.ExecuteScopedAsync(subject, oldClass, false, default));
        Assert.Empty(await reader.ExecuteScopedAsync(subject, currentClass, true, default));
        Assert.Empty(await reader.ExecuteScopedAsync(subject, Guid.NewGuid(), false, default));
        Assert.Equal(new[] { "2" }, (await reader.ExecuteAsync(subject, default)).Select(t => t.TopicNodeId));
        Assert.Equal(2, await db.ClassCurriculumApplications.CountAsync());
    }
    [Fact]
    public async Task ArchivedOnly_CannotBypassByOmittingClassOrSettingHistoryFalse_AndNoWholeSubjectFallback()
    {
        await Seed(false);
        Assert.False((await StudentLearningScope.ResolveAsync(db,center,student,subject,null,false,default)).Allowed);
        Assert.False((await StudentLearningScope.ResolveAsync(db,center,student,subject,oldClass,false,default)).Allowed);
        var candidates=await new OpportunityCandidateBuilder(db).BuildCandidatesAsync(center,student,subject,default);
        Assert.Equal("NO_ACTIVE_CLASS",candidates.BlockedReason);Assert.Empty(candidates.AllActiveTopicNodes);
        Assert.Single(await db.ClassCurriculumApplications.ToListAsync());
    }
    [Fact]
    public async Task CurrentOtherClass_RemainsWritable_ButHistoricalClassAndHistoryFlagAreNot()
    {
        await Seed();
        var scope=await StudentLearningScope.ResolveAsync(db,center,student,subject,currentClass,false,default);
        Assert.True(scope.Includes(2));Assert.False(scope.Includes(1));
        Assert.False((await StudentLearningScope.ResolveAsync(db,center,student,subject,oldClass,false,default)).Allowed);
        Assert.False((await StudentLearningScope.ResolveAsync(db,center,student,subject,currentClass,true,default)).Allowed);
        Assert.False((await StudentLearningScope.ResolveAsync(db,center,Guid.NewGuid(),subject,currentClass,false,default)).Allowed);
        var candidates=await new OpportunityCandidateBuilder(db).BuildForClassAsync(center,student,subject,currentClass,default);
        Assert.Equal(new ulong[]{2},candidates.AllActiveTopicNodes.Select(n=>n.NodeId));
    }
    [Fact]
    public async Task ReadOnlyGenerateAndNext_AreRejectedBeforeEngineOrProvider_AndDoNotSavePreferences()
    {
        await Seed();var engine=new Mock<IRecommendationEngine>(MockBehavior.Strict);
        var generator=new GenerateLearningPathUseCase(db,tenant,engine.Object);
        await Assert.ThrowsAsync<LearningScopeDeniedException>(()=>generator.ExecuteAsync(new GenerateLearningPathRequest{SubjectId=subject,ClassId=oldClass},default));
        await Assert.ThrowsAsync<LearningScopeDeniedException>(()=>generator.ExecuteAsync(new GenerateLearningPathRequest{SubjectId=subject,ClassId=currentClass,History=true},default));
        var next=new GetNextQuestionUseCase(db,tenant,engine.Object,TimeProvider.System);
        Assert.True((await next.ExecuteAsync(subject,oldClass,false,default)).Forbidden);
        Assert.True((await next.ExecuteAsync(subject,currentClass,true,default)).Forbidden);
        Assert.Empty(engine.Invocations);Assert.Empty(await db.StudentLearningPathPreferences.ToListAsync());Assert.Empty(await db.LearningPaths.ToListAsync());
    }
    [Fact]
    public async Task CachedQuestionMustStillBelongToCurrentCurriculum()
    {
        await Seed();var engine=new Mock<IRecommendationEngine>();
        engine.Setup(e=>e.GetNextQuestionAsync(center,student,subject,It.IsAny<DateTime>(),It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NextQuestionDto{Topic=new(1,"Old",0)});
        var next=new GetNextQuestionUseCase(db,tenant,engine.Object,TimeProvider.System);
        Assert.True((await next.ExecuteAsync(subject,currentClass,false,default)).NotFound);
        engine.Setup(e=>e.GetNextQuestionAsync(center,student,subject,It.IsAny<DateTime>(),It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NextQuestionDto{Topic=new(2,"Current",0)});
        Assert.True((await next.ExecuteAsync(subject,currentClass,false,default)).IsSuccess);
    }
    [Fact]
    public async Task SessionProgress_CannotChangeFromHistoryOrArchivedClass_ButCanInCurrentClass()
    {
        await Seed();var path=new LearningPath{CenterId=center,LearningPathId=Guid.NewGuid(),StudentId=student,SubjectId=subject,Status=LearningPathStatus.Active,
            CreatedAt=now,UpdatedAt=now,GeneratedAt=now,PlanJson=JsonSerializer.SerializeToDocument(new[]{new LearningPathPhaseDto{Weeks=[new(){Sessions=[new(){SessionId="s",TopicNodeId="2"}]}]}},new JsonSerializerOptions(JsonSerializerDefaults.Web))};
        db.LearningPaths.Add(path);await db.SaveChangesAsync();var original=path.PlanJson!.RootElement.GetRawText();
        var usecase=new UpdateLearningPathSessionUseCase(db,tenant);
        Assert.False(await usecase.ExecuteAsync(subject,"s",new(){ClassId=oldClass},default));
        Assert.False(await usecase.ExecuteAsync(subject,"s",new(){ClassId=currentClass,History=true},default));
        Assert.Equal(original,path.PlanJson.RootElement.GetRawText());
        Assert.True(await usecase.ExecuteAsync(subject,"s",new(){ClassId=currentClass},default));
        Assert.Contains("InProgress",path.PlanJson.RootElement.GetRawText());
    }
    [Fact]
    public async Task RemovedMemberAndEndedCurriculum_DoNotBecomeUnscopedPractice()
    {
        await Seed();var member=await db.ClassStudents.SingleAsync(m=>m.ClassId==currentClass);
        member.Status=ClassStudentStatus.Removed;member.RemovedAt=now;await db.SaveChangesAsync();
        Assert.False((await StudentLearningScope.ResolveAsync(db,center,student,subject,null,false,default)).Allowed);
        member.Status=ClassStudentStatus.Active;member.RemovedAt=null;
        var application=await db.ClassCurriculumApplications.SingleAsync(a=>a.ClassId==currentClass);
        application.EndedAt=now;application.EndedBy=teacher;application.EndReason="Synthetic";await db.SaveChangesAsync();
        Assert.False((await StudentLearningScope.ResolveAsync(db,center,student,subject,currentClass,false,default)).Allowed);
        Assert.Equal(2,await db.ClassCurriculumApplications.CountAsync());
    }
    [Fact]
    public async Task StandalonePracticeWithoutAnyClassHistory_IsNotGloballyDisabled()
    {
        await Seed();db.ClassStudents.RemoveRange(await db.ClassStudents.ToListAsync());await db.SaveChangesAsync();
        var scope=await StudentLearningScope.ResolveAsync(db,center,student,subject,null,false,default);
        Assert.True(scope.Allowed);Assert.Null(scope.TopicIds);
        Assert.False((await StudentLearningScope.ResolveAsync(db,center,student,subject,oldClass,false,default)).Allowed);
    }

    [Fact]
    public async Task AdaptiveSubmission_RejectsArchivedAndWrongCurriculumQuestions_WithoutWritingAttempts()
    {
        await Seed();
        db.Users.Add(new User{CenterId=center,UserId=student,Username="synthetic",DisplayName="Synthetic",PasswordHash="H",RoleName=UserRole.Student,Status=UserStatus.Active,CreatedAt=now,UpdatedAt=now});
        foreach(var node in new[]{1ul,2ul})
            db.Questions.Add(new Question{CenterId=center,QuestionId=100+node,SubjectId=subject,PrimaryTopicNodeId=node,CreatedByTeacherId=teacher,
                QuestionType=QuestionType.ShortAnswer,QuestionText="Synthetic",CorrectAnswer="x",Solution="Synthetic",LanguageCode="vi",Status=QuestionStatus.Active,MaxScore=10,CreatedAt=now,UpdatedAt=now});
        await db.SaveChangesAsync();
        var validator=new AttemptSubmissionValidator(db,tenant,new PreliminaryGraderFactory(new MultipleChoiceGrader(),new ShortAnswerGrader(),new EssayGrader()));
        SubmitAttemptRequest Request(ulong question,Guid? cls,bool history=false)=>new(){ClientSubmissionId=Guid.NewGuid(),QuestionId=question.ToString(),FinalAnswer="x",ClassId=cls,History=history};
        Assert.Equal(ErrorCodes.AssignmentNotAvailable,(await validator.ValidateAsync(Request(101,oldClass))).ErrorCode);
        Assert.Equal(ErrorCodes.AssignmentNotAvailable,(await validator.ValidateAsync(Request(101,currentClass))).ErrorCode);
        Assert.Equal(ErrorCodes.AssignmentNotAvailable,(await validator.ValidateAsync(Request(102,currentClass,true))).ErrorCode);
        Assert.True((await validator.ValidateAsync(Request(102,currentClass))).IsSuccess);
        var member=await db.ClassStudents.SingleAsync(m=>m.ClassId==currentClass);member.Status=ClassStudentStatus.Removed;member.RemovedAt=now;await db.SaveChangesAsync();
        Assert.Equal(ErrorCodes.AssignmentNotAvailable,(await validator.ValidateAsync(Request(102,null))).ErrorCode);
        Assert.Empty(await db.Attempts.ToListAsync());Assert.Empty(await db.AIAnalysisJobs.ToListAsync());
    }

    [Fact]
    public async Task DetailedPathRead_RejectsOutOfScopeSavedSessions_AndNeverBackfillsOnGet()
    {
        await Seed();
        db.StudentLearningPathPreferences.Add(new(){CenterId=center,StudentId=student,SubjectId=subject,WeakTopicNodeIds=JsonSerializer.SerializeToDocument(Array.Empty<ulong>()),
            FocusTopicNodeIds=JsonSerializer.SerializeToDocument(Array.Empty<ulong>()),CreatedAt=now,UpdatedAt=now});
        var path=new LearningPath{CenterId=center,LearningPathId=Guid.NewGuid(),StudentId=student,SubjectId=subject,Status=LearningPathStatus.Active,CreatedAt=now,UpdatedAt=now,GeneratedAt=now};
        path.Items.Add(new(){CenterId=center,TopicNodeId=2,RankOrder=1,Reason="Synthetic",CreatedAt=now,UpdatedAt=now});
        db.LearningPaths.Add(path);await db.SaveChangesAsync();
        var reader=new GenerateLearningPathUseCase(db,tenant,new Mock<IRecommendationEngine>(MockBehavior.Strict).Object);
        Assert.NotNull(await reader.ExecuteAsync(subject,currentClass,default));
        Assert.Null(path.PlanJson);Assert.Equal(now,path.UpdatedAt);
        path.PlanJson=JsonSerializer.SerializeToDocument(new[]{new LearningPathPhaseDto{Weeks=[new(){Sessions=[new(){SessionId="old",TopicNodeId="1"}]}]}},new JsonSerializerOptions(JsonSerializerDefaults.Web));
        await db.SaveChangesAsync();var saved=path.PlanJson.RootElement.GetRawText();
        Assert.Null(await reader.ExecuteAsync(subject,currentClass,default));
        Assert.Null(await reader.ExecuteAsync(subject,oldClass,default));
        Assert.NotNull(await reader.ExecuteAsync(subject,default)); // personal history remains readable without claiming a class snapshot
        Assert.Equal(saved,path.PlanJson.RootElement.GetRawText());Assert.Equal(now,path.UpdatedAt);
    }
}
