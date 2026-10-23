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
        db.Students.Add(new Student{CenterId=center,StudentId=student,FullName="Synthetic",GradeLevel=10,CreatedAt=now,UpdatedAt=now});
        db.Subjects.Add(new Subject{CenterId=center,SubjectId=subject,SubjectCode="M",SubjectName="Math",IsActive=true,CreatedAt=now,UpdatedAt=now});
        var version = GraphScopeFixture.Approve(db, center, subject, 10, 1, 2);
        foreach(var (id,archived,node) in new[]{(oldClass,true,1ul),(currentClass,false,2ul)})
        {
            db.KnowledgeNodes.Add(new KnowledgeNode{CenterId=center,SubjectId=subject,NodeId=node,NodeCode=node.ToString(),NodeName="Topic",NodeType=NodeType.Topic,IsActive=true,CreatedAt=now,UpdatedAt=now});
            if(!withCurrent&&!archived)continue;
            db.Classes.Add(new Class{CenterId=center,ClassId=id,SubjectId=subject,TeacherId=teacher,ClassName=id.ToString(),AcademicYear="2026",GradeLevel=10,Status=archived?ClassStatus.Archived:ClassStatus.Active,
                LearningScope=archived?ClassLearningScope.History:ClassLearningScope.Current,CreatedAt=now,UpdatedAt=now});
            db.ClassStudents.Add(new ClassStudent{CenterId=center,ClassId=id,StudentId=student,Status=ClassStudentStatus.Active,JoinedAt=now.AddDays(-10)});
            var curriculum=Guid.NewGuid();
            db.Curriculums.Add(new Curriculum{CenterId=center,CurriculumId=curriculum,TeacherId=teacher,SubjectId=subject,GradeLevel=10,GraphVersionId=version,Title="Synthetic",ReviewStatus=ReviewStatus.Published,CreatedAt=now,UpdatedAt=now});
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
        engine.Setup(e=>e.GetNextForClassAsync(center,student,subject,currentClass,It.IsAny<DateTime>(),It.IsAny<CancellationToken>()))
            .ReturnsAsync(new NextQuestionDto{Topic=new(1,"Old",0)});
        var next=new GetNextQuestionUseCase(db,tenant,engine.Object,TimeProvider.System);
        Assert.True((await next.ExecuteAsync(subject,currentClass,false,default)).NotFound);
        engine.Setup(e=>e.GetNextForClassAsync(center,student,subject,currentClass,It.IsAny<DateTime>(),It.IsAny<CancellationToken>()))
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
        Assert.True(scope.Allowed);Assert.Equal(new HashSet<ulong>{1,2}, scope.TopicIds);
        Assert.False((await StudentLearningScope.ResolveAsync(db,center,student,subject,oldClass,false,default)).Allowed);
    }

    [Fact]
    public async Task AdaptiveSubmission_RejectsArchivedAndWrongCurriculumQuestions_WithoutWritingAttempts()
    {
        await Seed();
        db.Users.Add(new User{CenterId=center,UserId=student,Username="synthetic",DisplayName="Synthetic",PasswordHash="H",RoleName=UserRole.Student,Status=UserStatus.Active,CreatedAt=now,UpdatedAt=now});
        foreach(var node in new[]{1ul,2ul})
            db.Questions.Add(new Question{CenterId=center,QuestionId=100+node,SubjectId=subject,PrimaryTopicNodeId=node,GradeLevel=10,CreatedByTeacherId=teacher,
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
    [Theory]
    [InlineData("GoalType")]
    [InlineData("Pace")]
    [InlineData("PreferredMode")]
    [InlineData("SelfAssessedLevel")]
    [InlineData("Note")]
    public async Task Audit_InvalidPreferences_FailBeforeEngineOrMutation(string field)
    {
        await Seed();
        var request = new GenerateLearningPathRequest { SubjectId = subject, ClassId = currentClass };
        if (field == "Note") request.Note = new string('a', 1001);
        else typeof(GenerateLearningPathRequest).GetProperty(field)!.SetValue(request, "INVALID");
        var generator = new GenerateLearningPathUseCase(db, tenant, new Mock<IRecommendationEngine>(MockBehavior.Strict).Object);
        await Assert.ThrowsAsync<ArgumentException>(() => generator.ExecuteAsync(request, default));
        Assert.Empty(await db.StudentLearningPathPreferences.ToListAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Audit_CompletionNeedsCurrentPracticeEvidence_AndUpdatesCheckpoint(bool teacherConfirmed)
    {
        await Seed();
        var pathId = Guid.NewGuid(); var sid = $"{pathId:D}-w01-s01";
        db.Questions.Add(new Question { CenterId=center, QuestionId=900, SubjectId=subject, PrimaryTopicNodeId=2,GradeLevel=10, CreatedByTeacherId=teacher,
            QuestionText="Synthetic", CorrectAnswer="2", Solution="Explanation", LanguageCode="vi", Status=QuestionStatus.Active,
            QuestionType=QuestionType.ShortAnswer, MaxScore=10, Difficulty=1, EstimatedTimeSeconds=30, CreatedAt=now, UpdatedAt=now });
        var path = new LearningPath { CenterId=center, LearningPathId=pathId, StudentId=student, SubjectId=subject, Status=LearningPathStatus.Active,
            CreatedAt=now, UpdatedAt=now, GeneratedAt=now, RowVersion=1,
            PlanJson=JsonSerializer.SerializeToDocument(new[]{new LearningPathPhaseDto{Weeks=[new(){Sessions=[new(){SessionId=sid,TopicNodeId="2",ClassId=currentClass,
                RecommendedQuestionIds=["900"],RequiredCorrectRate=0.75m}]}]}},new JsonSerializerOptions(JsonSerializerDefaults.Web)) };
        db.LearningPaths.Add(path); await db.SaveChangesAsync();
        var updater = new UpdateLearningPathSessionUseCase(db, tenant);
        Assert.False(await updater.ExecuteAsync(subject,sid,new(){ClassId=currentClass,Status="Completed"},default));
        Assert.True(await updater.ExecuteAsync(subject,sid,new(){ClassId=currentClass,Status="InProgress",ExpectedRowVersion="1"},default));
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => updater.ExecuteAsync(subject,sid,new(){ClassId=currentClass,Status="Skipped",ExpectedRowVersion="1"},default));
        Assert.False(await updater.ExecuteAsync(subject,sid,new(){ClassId=currentClass,Status="Completed"},default));
        var engine = new Mock<IRecommendationEngine>(MockBehavior.Strict);
        var reader = new GetNextQuestionUseCase(db,tenant,engine.Object,TimeProvider.System);
        var next = await reader.ExecuteSessionAsync(subject,sid,currentClass,false,default);
        Assert.Equal(900ul,next.Data!.Question!.QuestionId);
        Assert.True((await reader.ExecuteSessionAsync(subject,sid,oldClass,false,default)).NotFound);
        db.Attempts.Add(new EduTwin.DAL.AssessmentAndReasoning.Attempt { CenterId=center, AttemptId=909, StudentId=student, QuestionId=900,
            FinalAnswer="2", IsCorrect=teacherConfirmed ? null : true, Confidence=80, ReasoningLanguage="vi", CreatedAt=DateTime.UtcNow.AddSeconds(1), UpdatedAt=now });
        if (teacherConfirmed) db.ReasoningAnalyses.Add(new EduTwin.DAL.AssessmentAndReasoning.ReasoningAnalysis {
            CenterId=center, AnalysisId=910, AttemptId=909, SchemaVersion="1", Feedback="Teacher confirmed",
            MissingSteps=JsonDocument.Parse("[]"), RootCauseNodeIds=JsonDocument.Parse("[]"),
            OverrideIsCorrect=true, CreatedAt=now, UpdatedAt=now });
        await db.SaveChangesAsync();
        Assert.True(await updater.ExecuteAsync(subject,sid,new(){ClassId=currentClass,Status="Evaluate"},default));
        var phases = JsonSerializer.Deserialize<List<LearningPathPhaseDto>>(path.PlanJson!.RootElement.GetRawText(),new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal("Completed",phases[0].Weeks[0].Sessions[0].Status);
        Assert.Equal(1,phases[0].Weeks[0].Checkpoint.CompletedTasks);
        Assert.Equal(100,phases[0].Weeks[0].ProgressPercentage);
        Assert.Equal(LearningPathStatus.Completed,path.Status);
        Assert.True((await reader.ExecuteSessionAsync(subject,sid,currentClass,false,default)).NotFound);
        // Automatic recommendation replacement must not hide the durable plan
        // when the student returns from its completed session.
        path.Status = LearningPathStatus.Superseded;
        var replacementId = Guid.NewGuid();
        db.LearningPaths.Add(new LearningPath { CenterId=center,LearningPathId=replacementId,StudentId=student,SubjectId=subject,
            Status=LearningPathStatus.Active,Version=path.Version+1,CreatedAt=now,UpdatedAt=now,GeneratedAt=now,
            PlanJson=JsonSerializer.SerializeToDocument(new[]{new LearningPathPhaseDto{Weeks=[new(){Sessions=[new(){SessionId=$"{replacementId:D}-w01-s01",TopicNodeId="2",ClassId=currentClass}]}]}},new JsonSerializerOptions(JsonSerializerDefaults.Web)) });
        db.StudentLearningPathPreferences.Add(new(){CenterId=center,StudentId=student,SubjectId=subject,TargetWeeks=1,DaysPerWeek=3,MinutesPerDay=20,TargetMastery=80,
            WeakTopicNodeIds=JsonDocument.Parse("[]"),FocusTopicNodeIds=JsonDocument.Parse("[]"),CreatedAt=now,UpdatedAt=now});
        await db.SaveChangesAsync();
        var details = new GenerateLearningPathUseCase(db,tenant,engine.Object);
        var original = await details.ExecuteAsync(subject,currentClass,pathId,default);
        Assert.Equal(pathId.ToString(),original!.LearningPathId);
        Assert.Equal(100,original.ProgressPercentage);
        Assert.Equal(1,original.Phases[0].Weeks[0].Checkpoint.CompletedTasks);
        Assert.Equal(replacementId.ToString(),(await details.ExecuteAsync(subject,currentClass,default))!.LearningPathId);
        Assert.Null(await details.ExecuteAsync(subject,currentClass,Guid.NewGuid(),default));
        Assert.Null(await details.ExecuteAsync(subject,null,pathId,default));
    }

    [Theory]
    [InlineData(QuestionStatus.Archived, false)]
    [InlineData(QuestionStatus.Draft, false)]
    [InlineData(QuestionStatus.Active, true)]
    public async Task SessionQuestionEligibility_ExcludesRetiredQuestionsFromReadingAndCompletion(QuestionStatus retiredStatus, bool deleted)
    {
        await Seed();
        var pathId = Guid.NewGuid(); var sid = $"{pathId:D}-w01-s01";
        foreach (var id in new[] { 900ul, 901ul })
            db.Questions.Add(new Question { CenterId=center,QuestionId=id,SubjectId=subject,PrimaryTopicNodeId=2,GradeLevel=10,CreatedByTeacherId=teacher,
                QuestionText="Synthetic",CorrectAnswer="2",Solution="Explanation",LanguageCode="vi",Status=QuestionStatus.Active,
                QuestionType=QuestionType.ShortAnswer,AnswerEvaluationMode=QuestionAnswerEvaluationMode.NumericRational,MaxScore=10,Difficulty=1,
                EstimatedTimeSeconds=30,CreatedAt=now,UpdatedAt=now });
        var path = new LearningPath { CenterId=center,LearningPathId=pathId,StudentId=student,SubjectId=subject,Status=LearningPathStatus.Active,
            CreatedAt=now,UpdatedAt=now,GeneratedAt=now,RowVersion=1,
            PlanJson=JsonSerializer.SerializeToDocument(new[]{new LearningPathPhaseDto{Weeks=[new(){Sessions=[new(){SessionId=sid,TopicNodeId="2",ClassId=currentClass,
                Status="InProgress",StartedAtUtc=now.AddMinutes(-1),RecommendedQuestionIds=["900","901"],RequiredCorrectRate=0.75m}]}]}},new JsonSerializerOptions(JsonSerializerDefaults.Web)) };
        db.LearningPaths.Add(path); await db.SaveChangesAsync();
        var retired = await db.Questions.SingleAsync(q => q.QuestionId==901);
        retired.Status=retiredStatus; retired.IsDeleted=deleted; retired.UpdatedAt=DateTime.UtcNow;
        await db.SaveChangesAsync();
        var updater = new UpdateLearningPathSessionUseCase(db,tenant);
        var version = path.RowVersion; var originalJson = path.PlanJson.RootElement.GetRawText();
        Assert.Equal(new[]{"900"},(await updater.GetAsync(subject,sid,currentClass,false,default))!.RecommendedQuestionIds);
        Assert.Equal(version,path.RowVersion); Assert.Equal(originalJson,path.PlanJson.RootElement.GetRawText());
        var reader = new GetNextQuestionUseCase(db,tenant,new Mock<IRecommendationEngine>(MockBehavior.Strict).Object,TimeProvider.System);
        Assert.Equal(900ul,(await reader.ExecuteSessionAsync(subject,sid,currentClass,false,default)).Data!.Question!.QuestionId);
        db.Attempts.Add(new EduTwin.DAL.AssessmentAndReasoning.Attempt { CenterId=center,AttemptId=909,StudentId=student,QuestionId=900,
            FinalAnswer="2",IsCorrect=true,Confidence=80,ReasoningLanguage="vi",CreatedAt=DateTime.UtcNow,UpdatedAt=now });
        await db.SaveChangesAsync();
        Assert.True(await updater.ExecuteAsync(subject,sid,new(){ClassId=currentClass,Status="Evaluate",ExpectedRowVersion=version.ToString()},default));
        var phases=JsonSerializer.Deserialize<List<LearningPathPhaseDto>>(path.PlanJson.RootElement.GetRawText(),new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        Assert.Equal("Completed",phases[0].Weeks[0].Sessions[0].Status);
        Assert.Equal(new[]{"900"},phases[0].Weeks[0].Sessions[0].RecommendedQuestionIds);
        Assert.Equal(1,phases[0].Weeks[0].Checkpoint.CompletedTasks);
        Assert.Equal(100m,phases[0].Weeks[0].ProgressPercentage);
        Assert.Equal(1,await db.Attempts.CountAsync());
    }

    [Fact]
    public async Task SessionWithNoEligibleQuestions_RequiresExplicitActivityAcknowledgement()
    {
        await Seed(); var pathId=Guid.NewGuid(); var sid=$"{pathId:D}-w01-s01";
        db.Questions.Add(new Question{CenterId=center,QuestionId=901,SubjectId=subject,PrimaryTopicNodeId=2,GradeLevel=10,CreatedByTeacherId=teacher,
            QuestionText="Synthetic",CorrectAnswer="2",Solution="Explanation",LanguageCode="vi",Status=QuestionStatus.Archived,
            QuestionType=QuestionType.ShortAnswer,MaxScore=10,CreatedAt=now,UpdatedAt=now});
        var path=new LearningPath{CenterId=center,LearningPathId=pathId,StudentId=student,SubjectId=subject,Status=LearningPathStatus.Active,
            CreatedAt=now,UpdatedAt=now,GeneratedAt=now,RowVersion=1,
            PlanJson=JsonSerializer.SerializeToDocument(new[]{new LearningPathPhaseDto{Weeks=[new(){Sessions=[new(){SessionId=sid,TopicNodeId="2",ClassId=currentClass,
                Status="InProgress",StartedAtUtc=now.AddMinutes(-1),RecommendedQuestionIds=["901"]}]}]}},new JsonSerializerOptions(JsonSerializerDefaults.Web))};
        db.LearningPaths.Add(path);await db.SaveChangesAsync();
        var updater=new UpdateLearningPathSessionUseCase(db,tenant);
        Assert.True(await updater.ExecuteAsync(subject,sid,new(){ClassId=currentClass,Status="Evaluate"},default));
        Assert.Equal("InProgress",(await updater.GetAsync(subject,sid,currentClass,false,default))!.Status);
        Assert.False(await updater.ExecuteAsync(subject,sid,new(){ClassId=currentClass,Status="Completed"},default));
        Assert.True(await updater.ExecuteAsync(subject,sid,new(){ClassId=currentClass,Status="Completed",AcknowledgedActivities=true},default));
        Assert.Equal("Completed",(await updater.GetAsync(subject,sid,currentClass,false,default))!.Status);
        Assert.Empty(await db.Attempts.ToListAsync());
    }

    [Fact]
    public async Task Audit_ScheduleFiltersDraft_AndPreferencesChangeActivitiesAndCapacity()
    {
        await Seed();
        foreach(var (id,status) in new[]{(901ul,QuestionStatus.Draft),(902ul,QuestionStatus.Active),(903ul,QuestionStatus.Archived),(904ul,QuestionStatus.Active)})
            db.Questions.Add(new Question{CenterId=center,QuestionId=id,SubjectId=subject,PrimaryTopicNodeId=2,GradeLevel=10,CreatedByTeacherId=teacher,QuestionText="Synthetic",CorrectAnswer="2",
                Solution="Explanation",LanguageCode="vi",Status=status,QuestionType=id==904 ? QuestionType.Essay : QuestionType.ShortAnswer,
                AnswerEvaluationMode=id==904 ? QuestionAnswerEvaluationMode.Manual : QuestionAnswerEvaluationMode.TextExact,
                MaxScore=10,Difficulty=1,EstimatedTimeSeconds=30,CreatedAt=now,UpdatedAt=now});
        await db.SaveChangesAsync();
        var generator=new GenerateLearningPathUseCase(db,tenant,new Mock<IRecommendationEngine>().Object);
        var method=typeof(GenerateLearningPathUseCase).GetMethod("BuildScheduleAsync",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance)!;
        var items=new List<LearningPathItemDetailDto>{new(){TopicNodeId="2",TopicName="Topic",CurrentMastery=60,TargetMastery=80}};
        var pref=new StudentLearningPathPreference{TargetWeeks=1,DaysPerWeek=3,MinutesPerDay=30,PreferredMode="TheoryHeavy"};
        async Task<List<LearningPathPhaseDto>> Build() => await (Task<List<LearningPathPhaseDto>>)method.Invoke(generator,[center,subject,Guid.NewGuid(),items,pref,CancellationToken.None])!;
        var theory=await Build();
        Assert.Equal("LearnTheory",theory[0].Weeks[0].Sessions[0].Type);
        Assert.All(theory.SelectMany(p=>p.Weeks).SelectMany(w=>w.Sessions),s=>Assert.Equal(new[]{"902"},s.RecommendedQuestionIds));
        pref.PreferredMode="PracticeHeavy";pref.GoalType="ExamPrep";
        var practice=await Build();
        Assert.Equal("GuidedPractice",practice[0].Weeks[0].Sessions[0].Type);
        Assert.Equal("MockTest",practice[0].Weeks[0].Sessions[2].Type);
    }

}
