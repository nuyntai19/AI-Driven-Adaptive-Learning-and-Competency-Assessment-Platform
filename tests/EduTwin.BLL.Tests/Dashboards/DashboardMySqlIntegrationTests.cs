using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.Data.MySqlClient;
using EduTwin.BLL.AssessmentAndReasoning.Feedback;
using EduTwin.BLL.Dashboards;
using EduTwin.BLL.DigitalTwin;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.DigitalTwin;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Assignments;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.DigitalTwin;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Xunit;

namespace EduTwin.BLL.Tests.Dashboards;

[Collection("MySqlDatabase")]
public sealed class DashboardMySqlIntegrationTests
{
    private const string AdminConnectionVariable = "EDUTWIN_TEST_MYSQL_ADMIN_CONNECTION_STRING";
    private static readonly DateTime UtcNow = new(2026, 9, 11, 10, 0, 0, DateTimeKind.Utc);

    [MySqlIntegrationFact]
    public async Task AcademicLifecycleDependencies_RealSql_TriggersAndAuditedMutations()
    {
        await using var database=await MySqlTestDatabase.CreateAsync();
        var center=Guid.NewGuid();var teacher=Guid.NewGuid();var otherTeacher=Guid.NewGuid();var student=Guid.NewGuid();
        var subject=Guid.NewGuid();var classId=Guid.NewGuid();var curriculumId=Guid.NewGuid();var manager=Guid.NewGuid();
        await SeedHierarchyAsync(database.ConnectionString,center,teacher,[student],subject,classId,[otherTeacher]);
        var tenant=new TestTenantContext {CenterId=center,UserId=teacher,Role="Teacher"};
        await using var db=CreateContext(database.ConnectionString,tenant);
        var cls=await db.Classes.SingleAsync();cls.TeacherId=otherTeacher;cls.GradeLevel=12;
        db.Users.Add(new User {CenterId=center,UserId=manager,Username="synthetic.manager",DisplayName="Synthetic Manager",PasswordHash="H",
            RoleName=UserRole.CenterManager,Status=UserStatus.Active,CreatedAt=UtcNow,UpdatedAt=UtcNow});
        await db.SaveChangesAsync();
        var createEdge=new EduTwin.BLL.KnowledgeGraph.CreateKnowledgeEdgeUseCase(db,tenant,TimeProvider.System,new EduTwin.BLL.KnowledgeGraph.KnowledgeGraphValidator());
        var created=await createEdge.ExecuteAsync(new(){SubjectId=subject,SourceNodeId="1",TargetNodeId="2",RelationType="PrerequisiteOf",Weight=1},default);
        Assert.True(created.IsSuccess,created.ErrorCode);
        var edge=await db.KnowledgeEdges.SingleAsync();
        db.Curriculums.Add(new Curriculum {CenterId=center,CurriculumId=curriculumId,TeacherId=teacher,SubjectId=subject,Title="Synthetic Shared",
            Visibility=MaterialVisibility.Shared,GradeLevel=12,ReviewStatus=ReviewStatus.Published,CreatedAt=UtcNow,UpdatedAt=UtcNow});
        db.CurriculumNodes.Add(new CurriculumNode {CenterId=center,CurriculumId=curriculumId,NodeId=1,CreatedAt=UtcNow});
        db.ClassCurriculumApplications.Add(new ClassCurriculumApplication {CenterId=center,ApplicationId=Guid.NewGuid(),ClassId=classId,CurriculumId=curriculumId,
            SubjectId=subject,ApplicationRole="Primary",AssignedBy=otherTeacher,StartedAt=UtcNow,ClassGradeAtStart=12,CurriculumGradeAtStart=12});
        await db.SaveChangesAsync();
        var usage=await new EduTwin.BLL.CurriculumAndQuestions.CurriculumApplicationUseCase(db,tenant,TimeProvider.System).UsageAsync(curriculumId,default);
        Assert.Single(usage!); // shared use by another teacher still blocks the author.
        var archive=new EduTwin.BLL.CurriculumAndQuestions.ArchiveCurriculumUseCase(db,tenant,TimeProvider.System);
        Assert.Equal(EduTwin.Contracts.Common.ErrorCodes.InvalidStateTransition,(await archive.ExecuteAsync(curriculumId,new(){RowVersion="1",Reason="Synthetic archive"})).ErrorCode);
        await Assert.ThrowsAsync<MySqlException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE curriculums SET review_status='Archived' WHERE curriculum_id={curriculumId.ToString()}"));
        await Assert.ThrowsAsync<MySqlException>(()=>db.Database.ExecuteSqlRawAsync("UPDATE knowledge_nodes SET node_name='tamper' WHERE node_id=1"));
        await Assert.ThrowsAsync<MySqlException>(()=>db.Database.ExecuteSqlRawAsync("UPDATE knowledge_nodes SET is_active=0 WHERE node_id=1"));
        await Assert.ThrowsAsync<MySqlException>(()=>db.Database.ExecuteSqlRawAsync("UPDATE knowledge_nodes SET is_deleted=1 WHERE node_id=1"));
        await Assert.ThrowsAsync<MySqlException>(()=>db.Database.ExecuteSqlRawAsync("UPDATE knowledge_edges SET weight=.5"));
        await Assert.ThrowsAsync<MySqlException>(()=>db.Database.ExecuteSqlRawAsync("UPDATE knowledge_edges SET is_deleted=1"));
        var updateNode=new EduTwin.BLL.KnowledgeGraph.UpdateKnowledgeNodeUseCase(db,tenant,TimeProvider.System,new EduTwin.BLL.KnowledgeGraph.KnowledgeNodeHierarchyCycleDetector());
        var node=await db.KnowledgeNodes.SingleAsync(n=>n.NodeId==1);
        var nodeRequest=new UpdateKnowledgeNodeRequest {NodeName=node.NodeName,Description=node.Description,ParentNodeId=null,IsActive=true,
            OrderIndex=node.OrderIndex,ExamImportance=75,EstimatedLearningMinutes=90,RowVersion=node.RowVersion.ToString()};
        Assert.True((await updateNode.ExecuteAsync("1",nodeRequest)).IsSuccess);
        nodeRequest.NodeName="Changed meaning";nodeRequest.RowVersion=node.RowVersion.ToString();
        Assert.Equal(EduTwin.Contracts.Common.ErrorCodes.InvalidStateTransition,(await updateNode.ExecuteAsync("1",nodeRequest)).ErrorCode);
        var updateEdge=new EduTwin.BLL.KnowledgeGraph.UpdateKnowledgeEdgeUseCase(db,tenant,TimeProvider.System);
        Assert.Equal(EduTwin.Contracts.Common.ErrorCodes.InvalidStateTransition,(await updateEdge.ExecuteAsync(edge.EdgeId.ToString(),new(){Weight=.5m,RowVersion=edge.RowVersion.ToString()})).ErrorCode);
        tenant.UserId=manager;tenant.Role="CenterManager";
        var reports=new EduTwin.BLL.Organization.ClassReportsUseCase(db,tenant,new EduTwin.BLL.Organization.GetClassUseCase(db,tenant,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<EduTwin.BLL.Organization.GetClassUseCase>.Instance,new OrganizationOwnershipGuard(db,tenant)),
            new EduTwin.BLL.Assignments.AssignmentResultCalculator(db),TimeProvider.System);
        Assert.NotNull(await reports.AcademicAsync(classId,default));
        var classUpdate=new EduTwin.BLL.Organization.UpdateClassUseCase(db,tenant,TimeProvider.System,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<EduTwin.BLL.Organization.UpdateClassUseCase>.Instance);
        Assert.True((await classUpdate.ExecuteAsync(classId,new(){ClassName=cls.ClassName,TeacherId=otherTeacher,Status=ClassStatus.Archived,
            GradeLevel=12,RowVersion=cls.RowVersion.ToString(),LifecycleReason="Synthetic end of class"})).IsSuccess);
        Assert.NotNull(await reports.AcademicAsync(classId,default)); // archived report remains available to Manager.
        tenant.UserId=teacher;tenant.Role="Teacher";
        Assert.Empty((await new EduTwin.BLL.CurriculumAndQuestions.CurriculumApplicationUseCase(db,tenant,TimeProvider.System).UsageAsync(curriculumId,default))!);
        Assert.True((await updateEdge.ExecuteAsync(edge.EdgeId.ToString(),new(){Weight=.5m,RowVersion=edge.RowVersion.ToString()})).IsSuccess);
        Assert.True((await archive.ExecuteAsync(curriculumId,new(){RowVersion="1",Reason="Synthetic archive after class end"})).IsSuccess);
        Assert.Equal("Synthetic archive after class end",(await db.ClassCurriculumApplications.SingleAsync()).EndReason);
        Assert.Single(await db.CurriculumNodes.ToListAsync());Assert.Equal(2,await db.KnowledgeNodes.CountAsync());
        Assert.Contains(await db.AuthorizationAuditLogs.ToListAsync(),a=>a.ActionType=="KnowledgeNodeUpdated"&&a.ActorUserId==teacher);
        Assert.Contains(await db.AuthorizationAuditLogs.ToListAsync(),a=>a.ActionType=="KnowledgeEdgeUpdated"&&a.ActorUserId==teacher);
        Assert.Contains(await db.AuthorizationAuditLogs.ToListAsync(),a=>a.ActionType=="CurriculumArchived"&&a.ActorUserId==teacher);
        await Assert.ThrowsAsync<MySqlException>(()=>db.Database.ExecuteSqlRawAsync("UPDATE knowledge_nodes SET description='overwrite history' WHERE node_id=1"));
        await db.Database.ExecuteSqlRawAsync("UPDATE knowledge_nodes SET is_active=0 WHERE node_id=2");
        Assert.Equal(EduTwin.Contracts.Common.ErrorCodes.InvalidStateTransition,(await createEdge.ExecuteAsync(new(){SubjectId=subject,SourceNodeId="2",TargetNodeId="1",RelationType="RelatedTo",Weight=1},default)).ErrorCode);
        var fresh=await new EduTwin.BLL.KnowledgeGraph.CreateKnowledgeNodeUseCase(db,tenant,TimeProvider.System).ExecuteAsync(new(){
            SubjectId=subject,NodeCode="NEW-TOPIC",NodeType="Topic",NodeName="New independent topic",IsActive=true,ExamImportance=0,EstimatedLearningMinutes=30});
        Assert.True(fresh.IsSuccess,fresh.ErrorCode);
        Assert.True((await new EduTwin.BLL.KnowledgeGraph.DeleteKnowledgeNodeUseCase(db,tenant,TimeProvider.System).ExecuteAsync(fresh.Data!.NodeId)).IsSuccess);
        Assert.Contains(await db.AuthorizationAuditLogs.ToListAsync(),a=>a.ActionType=="KnowledgeNodeCreated"&&a.ActorUserId==teacher);
        Assert.Contains(await db.AuthorizationAuditLogs.ToListAsync(),a=>a.ActionType=="KnowledgeNodeDeleted"&&a.ActorUserId==teacher);
        tenant.CenterId=Guid.NewGuid();
        Assert.Null(await reports.AcademicAsync(classId,default));
    }

    [MySqlIntegrationFact]
    public async Task AcademicLifecycleDependencies_RealSql_ConcurrentEditsCannotCreateCycles()
    {
        await using var database=await MySqlTestDatabase.CreateAsync();
        var center=Guid.NewGuid();var teacher=Guid.NewGuid();var subject=Guid.NewGuid();
        await SeedHierarchyAsync(database.ConnectionString,center,teacher,[],subject,Guid.NewGuid());
        var tenant=new TestTenantContext {CenterId=center,UserId=teacher,Role="Teacher"};
        await using var a=CreateContext(database.ConnectionString,tenant);
        await using var b=CreateContext(database.ConnectionString,tenant);
        async Task<EduTwin.BLL.KnowledgeGraph.CreateKnowledgeEdgeResult> Edge(EduTwinDbContext context,string source,string target)=>
            await new EduTwin.BLL.KnowledgeGraph.CreateKnowledgeEdgeUseCase(context,tenant,TimeProvider.System,new EduTwin.BLL.KnowledgeGraph.KnowledgeGraphValidator())
                .ExecuteAsync(new(){SubjectId=subject,SourceNodeId=source,TargetNodeId=target,RelationType="PrerequisiteOf",Weight=1},default);
        var edges=await Task.WhenAll(Edge(a,"1","2"),Edge(b,"2","1"));
        Assert.Single(edges,x=>x.IsSuccess);
        Assert.Single(edges,x=>x.ErrorCode==EduTwin.Contracts.Common.ErrorCodes.DagCycleDetected);
        async Task<EduTwin.BLL.KnowledgeGraph.UpdateKnowledgeNodeResult> Parent(EduTwinDbContext context,string node,string parent)=>
            await new EduTwin.BLL.KnowledgeGraph.UpdateKnowledgeNodeUseCase(context,tenant,TimeProvider.System,new EduTwin.BLL.KnowledgeGraph.KnowledgeNodeHierarchyCycleDetector())
                .ExecuteAsync(node,new(){NodeName=node=="1"?"Topic One":"Topic Two",ParentNodeId=parent,RowVersion="1",IsActive=true,ExamImportance=50,EstimatedLearningMinutes=60});
        var nodes=await Task.WhenAll(Parent(a,"1","2"),Parent(b,"2","1"));
        Assert.Single(nodes,x=>x.IsSuccess);
        Assert.Single(nodes,x=>x.ErrorCode==EduTwin.Contracts.Common.ErrorCodes.DagCycleDetected);
        await using var proof=CreateContext(database.ConnectionString,tenant);
        Assert.Equal(1,await proof.KnowledgeEdges.CountAsync());
        Assert.Equal(1,await proof.KnowledgeNodes.CountAsync(n=>n.ParentNodeId!=null));
        Assert.Equal(2,await proof.AuthorizationAuditLogs.CountAsync());
    }

    [MySqlIntegrationFact]
    public async Task ClassLifecycle_RealSql_MigrationAudit_ActorTransition_AndReportScore()
    {
        await using var database = await MySqlTestDatabase.CreateAsync("20261008162602_AddAcademicClassScopeAndCurriculumApplications");
        var center=Guid.NewGuid();var teacher=Guid.NewGuid();var student=Guid.NewGuid();var subject=Guid.NewGuid();var classId=Guid.NewGuid();
        await SeedHierarchyAsync(database.ConnectionString,center,teacher,[student],subject,classId);
        var tenant=new TestTenantContext {CenterId=center,UserId=teacher,Role="Teacher"};
        await using var db=CreateContext(database.ConnectionString,tenant);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE classes SET learning_scope='History' WHERE class_id={classId.ToString()}");
        await db.GetService<IMigrator>().MigrateAsync();
        var cls=await db.Classes.SingleAsync(c=>c.ClassId==classId);
        Assert.Equal(ClassStatus.Active,cls.Status);Assert.Equal(ClassLearningScope.Current,cls.LearningScope);
        var correction=await db.AuthorizationAuditLogs.SingleAsync(a=>a.ActionType=="ClassScopeCorrected");
        Assert.Null(correction.ActorUserId);Assert.Null(correction.CreatedBy);Assert.Equal(classId.ToString(),correction.TargetId);
        Assert.Contains("System/Migration",correction.AfterData!);
        await Assert.ThrowsAsync<MySqlException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE classes SET learning_scope='History' WHERE class_id={classId.ToString()}"));
        var manager=Guid.NewGuid();
        db.Users.Add(new User {CenterId=center,UserId=manager,Username="synthetic.manager",DisplayName="Synthetic Manager",PasswordHash="H",RoleName=UserRole.CenterManager,Status=UserStatus.Active,CreatedAt=UtcNow,UpdatedAt=UtcNow});
        var assignment=Guid.NewGuid();
        db.Assignments.Add(new Assignment {CenterId=center,AssignmentId=assignment,ClassId=classId,CreatedByTeacherId=teacher,Title="Synthetic zero-score",Status=AssignmentStatus.Published,CreatedAt=UtcNow,UpdatedAt=UtcNow});
        db.Questions.Add(new Question {CenterId=center,QuestionId=100,SubjectId=subject,PrimaryTopicNodeId=1,CreatedByTeacherId=teacher,QuestionType=QuestionType.MultipleChoice,
            QuestionText="Synthetic question",CorrectAnswer="A",Solution="Synthetic reference",LanguageCode="vi",Difficulty=2,MaxScore=2,EstimatedTimeSeconds=60,Status=QuestionStatus.Active,CreatedAt=UtcNow,UpdatedAt=UtcNow});
        db.AssignmentQuestions.Add(new AssignmentQuestion {CenterId=center,AssignmentId=assignment,QuestionId=100,OrderIndex=1,Points=2,CreatedAt=UtcNow});
        db.AssignmentTargets.Add(new AssignmentTarget {CenterId=center,AssignmentId=assignment,StudentId=student,CreatedAt=UtcNow,CreatedBy=teacher});
        db.Attempts.Add(new Attempt {CenterId=center,AttemptId=100,StudentId=student,AssignmentId=assignment,QuestionId=100,FinalAnswer="B",ReasoningLanguage="vi",AwardedScore=0,IsCorrect=false,Status=AttemptStatus.Completed,CreatedAt=UtcNow,UpdatedAt=UtcNow});
        db.StudentAssignmentProgresses.Add(new StudentAssignmentProgress {CenterId=center,StudentId=student,AssignmentId=assignment,Status=ProgressStatus.Completed,CompletedQuestionCount=1,TotalQuestionCount=1,
            TeacherFinalReviewStatus=TeacherFinalReviewStatus.Approved,CreatedAt=UtcNow,UpdatedAt=UtcNow});
        await db.SaveChangesAsync();
        tenant.UserId=student;tenant.Role="Student";
        var studentList=new EduTwin.BLL.Assignments.ListStudentAssignmentsUseCase(db,tenant,TimeProvider.System,new EduTwin.BLL.Assignments.AssignmentResultCalculator(db));
        Assert.Empty((await studentList.ExecuteAsync(new(){SubjectId=subject,History=true},default)).Data!.Data);
        Assert.Equal(0,(await new GetStudentWorkspaceSummaryUseCase(db,tenant,TimeProvider.System).ExecuteAsync(subject,null,true))!.AssignmentCount);
        var emptyHistory=await new StudentAcademicScopeReader(db).ReadAsync(center,student,subject,null,true,default);
        Assert.Null(emptyHistory!.Context.SelectedClassId);Assert.Empty(emptyHistory.TopicIds);
        tenant.UserId=teacher;tenant.Role="Teacher";
        var logger=Microsoft.Extensions.Logging.Abstractions.NullLogger<EduTwin.BLL.Organization.UpdateClassUseCase>.Instance;
        var update=new EduTwin.BLL.Organization.UpdateClassUseCase(db,tenant,TimeProvider.System,logger);
        UpdateClassRequest Request(ClassStatus status,string version,string reason)=>new(){ClassName=cls.ClassName,TeacherId=teacher,Status=status,GradeLevel=cls.GradeLevel,RowVersion=version,LifecycleReason=reason};
        Assert.False((await update.ExecuteAsync(classId,Request(ClassStatus.Archived,cls.RowVersion.ToString(),"Teacher may not archive"))).IsSuccess);
        tenant.UserId=manager;tenant.Role="CenterManager";
        var archived=await update.ExecuteAsync(classId,Request(ClassStatus.Archived,cls.RowVersion.ToString(),"Kết thúc lớp thử nghiệm"));
        Assert.True(archived.IsSuccess,archived.ErrorCode);
        Assert.Equal("History",archived.Data!.LearningScope);
        Assert.True(await EduTwin.BLL.Assignments.StudentAssignmentScope.SuspendedAsync(db,center,classId,default));
        Assert.Equal(1,await db.Attempts.CountAsync());
        tenant.UserId=teacher;tenant.Role="Teacher";
        var access=new EduTwin.BLL.Organization.GetClassUseCase(db,tenant,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<EduTwin.BLL.Organization.GetClassUseCase>.Instance,new OrganizationOwnershipGuard(db,tenant));
        var reports=new EduTwin.BLL.Organization.ClassReportsUseCase(db,tenant,access,new EduTwin.BLL.Assignments.AssignmentResultCalculator(db),TimeProvider.System);
        var report=await reports.AcademicAsync(classId,default);
        Assert.NotNull(report);Assert.Equal(100,report.Students.Single().Summary.CompletionRate);
        Assert.Equal(0,report.Students.Single().Summary.AverageScore); // all questions answered is NOT full score.
        Assert.Equal("HighRisk",report.Students.Single().Summary.AssessmentStatus);
        var history=await reports.HistoryAsync(classId,1,default);
        Assert.Contains(history!.Data,a=>a.ActionType=="ClassArchived"&&a.ActorUserId==manager);
        tenant.UserId=student;tenant.Role="Student";
        var current=await new GetStudentWorkspaceSummaryUseCase(db,tenant,TimeProvider.System).ExecuteAsync(subject,classId,false);
        var historical=await new GetStudentWorkspaceSummaryUseCase(db,tenant,TimeProvider.System).ExecuteAsync(subject,classId,true);
        Assert.Null(current);Assert.Equal(1,historical!.AssignmentCount);
        Assert.Single((await studentList.ExecuteAsync(new(){SubjectId=subject,ClassId=classId,History=true},default)).Data!.Data);
        Assert.Empty((await studentList.ExecuteAsync(new(){SubjectId=subject,History=false},default)).Data!.Data);
        Assert.Equal(classId,(await new StudentAcademicScopeReader(db).ReadAsync(center,student,subject,null,true,default))!.Context.SelectedClassId);
        var savedSubmission=await new EduTwin.BLL.Assignments.GetStudentAssignmentUseCase(db,tenant,TimeProvider.System,new EduTwin.BLL.Assignments.AssignmentResultCalculator(db)).ExecuteAsync(assignment,default);
        Assert.True(savedSubmission.IsSuccess);Assert.True(savedSubmission.Data!.Data.IsReadOnly);
        Assert.Equal(classId.ToString(),savedSubmission.Data.Data.ClassId);Assert.Equal(1,await db.Attempts.CountAsync());
        tenant.UserId=manager;tenant.Role="CenterManager";
        var reopened=await update.ExecuteAsync(classId,Request(ClassStatus.Active,archived.Data.RowVersion,"Học tiếp"));
        Assert.True(reopened.IsSuccess,reopened.ErrorCode);Assert.Equal("Current",reopened.Data!.LearningScope);
        tenant.UserId=student;tenant.Role="Student";
        Assert.Empty((await studentList.ExecuteAsync(new(){SubjectId=subject,History=true},default)).Data!.Data);
        Assert.Equal(0,(await new GetStudentWorkspaceSummaryUseCase(db,tenant,TimeProvider.System).ExecuteAsync(subject,null,true))!.AssignmentCount);
        Assert.Single((await studentList.ExecuteAsync(new(){SubjectId=subject,History=false},default)).Data!.Data);
        var member=await db.ClassStudents.SingleAsync(m=>m.ClassId==classId&&m.StudentId==student);
        member.Status=ClassStudentStatus.Removed;member.RemovedAt=DateTime.UtcNow;await db.SaveChangesAsync();
        Assert.Single((await studentList.ExecuteAsync(new(){SubjectId=subject,ClassId=classId,History=true},default)).Data!.Data);
        Assert.Equal(1,(await new GetStudentWorkspaceSummaryUseCase(db,tenant,TimeProvider.System).ExecuteAsync(subject,classId,true))!.AssignmentCount);
        Assert.Equal(classId,(await new StudentAcademicScopeReader(db).ReadAsync(center,student,subject,null,true,default))!.Context.SelectedClassId);
        Assert.Equal(ClassStatus.Active,(await db.Classes.SingleAsync(c=>c.ClassId==classId)).Status);
        Assert.Equal(1,await db.Attempts.CountAsync());
    }

    private sealed class TestTenantContext : ITenantContext, ITenantIdAccessor
    {
        public Guid? CenterId { get; set; }
        public Guid? UserId { get; set; }
        public string? Role { get; set; }
        public uint? AuthVersion { get; set; } = 1;
        public bool IsResolved => CenterId.HasValue && CenterId.Value != Guid.Empty;
    }

    [MySqlIntegrationFact]
    public async Task WorkspaceSummaryAndNewAccountRoles_RealSql_NoProviderCalls()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var centerId = Guid.NewGuid(); var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid(); var subjectId = Guid.NewGuid(); var classId = Guid.NewGuid();
        await SeedHierarchyAsync(database.ConnectionString, centerId, teacherId, [studentId], subjectId, classId);
        var tenant = new TestTenantContext { CenterId = centerId, UserId = studentId, Role = nameof(UserRole.Student) };
        await using var db = CreateContext(database.ConnectionString, tenant);
        var center = await db.Centers.SingleAsync(c => c.CenterId == centerId);
        center.Timezone = "Asia/Ho_Chi_Minh";
        db.Questions.Add(new Question { QuestionId = 100, CenterId = centerId, SubjectId = subjectId,
            PrimaryTopicNodeId = 1, CreatedByTeacherId = teacherId, QuestionType = QuestionType.ShortAnswer,
            Difficulty = 1, QuestionText = "Synthetic question", CorrectAnswer = "1", Solution = "Synthetic solution",
            MaxScore = 10, EstimatedTimeSeconds = 60, LanguageCode = "vi", Status = QuestionStatus.Active,
            CreatedAt = UtcNow, UpdatedAt = UtcNow });
        foreach (var (date, skipped) in new[]
        {
            (UtcNow.Date.AddDays(-1).AddHours(17), false), // today at local midnight
            (UtcNow.Date.AddDays(-1).AddHours(16), false), // yesterday
            (UtcNow.Date.AddDays(-2).AddHours(16), true)   // skipped does not extend streak
        })
            db.Attempts.Add(new Attempt { CenterId = centerId, StudentId = studentId, QuestionId = 100,
                FinalAnswer = skipped ? "SKIPPED" : "1", ReasoningLanguage = "vi", Skipped = skipped,
                Status = AttemptStatus.NeedsTeacherReview, ClientSubmissionId = Guid.NewGuid(), CreatedAt = date, UpdatedAt = date });
        foreach (var status in new[] { AssignmentStatus.Draft, AssignmentStatus.Published, AssignmentStatus.Closed })
        {
            var assignment = new Assignment { AssignmentId = Guid.NewGuid(), CenterId = centerId, ClassId = classId,
                CreatedByTeacherId = teacherId, Title = "Synthetic assignment", Status = status, CreatedAt = UtcNow, UpdatedAt = UtcNow };
            db.Assignments.Add(assignment);
            db.StudentAssignmentProgresses.Add(new StudentAssignmentProgress { ProgressId = (ulong)status + 1, CenterId = centerId, StudentId = studentId,
                AssignmentId = assignment.AssignmentId, Status = ProgressStatus.Completed, TotalQuestionCount = 1,
                CompletedQuestionCount = 1, CreatedAt = UtcNow, UpdatedAt = UtcNow });
        }
        await db.SaveChangesAsync();
        var summary = await new GetStudentWorkspaceSummaryUseCase(db, tenant, new WorkspaceClock()).ExecuteAsync(subjectId);
        Assert.NotNull(summary); Assert.Equal(2, summary.AssignmentCount); Assert.Equal(2, summary.DailyStreak); Assert.True(summary.StudiedToday);

        var managerId = Guid.NewGuid();
        db.Users.Add(new User { UserId = managerId, CenterId = centerId, Username = "manager", PasswordHash = "synthetic",
            DisplayName = "Manager", RoleName = UserRole.CenterManager, Status = UserStatus.Active, AuthVersion = 1,
            CreatedAt = UtcNow, UpdatedAt = UtcNow });
        await db.SaveChangesAsync();
        await new EduTwin.BLL.Seeding.AuthorizationBootstrapper(db, TimeProvider.System).EnsureCenterAsync(centerId);
        tenant.UserId = managerId; tenant.Role = nameof(UserRole.CenterManager);
        var createTeacher = new EduTwin.BLL.Organization.CreateTeacherUseCase(db, tenant, TimeProvider.System,
            new Microsoft.AspNetCore.Identity.PasswordHasher<User>());
        var request = new CreateTeacherRequest { Username = "new.teacher", DisplayName = "New Teacher", TemporaryPassword = "SyntheticPassword123!" };
        var result = await createTeacher.ExecuteAsync(request);
        Assert.True(result.IsSuccess);
        var createdId = Guid.Parse(result.Data!.TeacherId);
        var authorization = await new AuthorizationSnapshotReader(db).ReadForUserAsync(createdId);
        Assert.Single(authorization.Roles);
        Assert.Contains("dashboards.teacher.read_scoped", authorization.Permissions);
        Assert.All(EduTwin.DAL.Seeding.AuthorizationPermissionCatalog.SensitiveSharedAcademicCodes,
            code => Assert.Contains(code, authorization.Permissions));
        Assert.DoesNotContain("organization.teachers.create", authorization.Permissions);
        Assert.Single(await db.AuthorizationAuditLogs.Where(a => a.TargetUserId == createdId && a.ActionType == "UserSystemRoleAssigned").ToArrayAsync());
        Assert.False((await createTeacher.ExecuteAsync(request)).IsSuccess);
        Assert.Single(await db.UserRoleAssignments.Where(a => a.UserId == createdId).ToArrayAsync());

        var createStudent = new EduTwin.BLL.Organization.CreateStudentUseCase(db, tenant,
            new Microsoft.AspNetCore.Identity.PasswordHasher<User>(), TimeProvider.System,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<EduTwin.BLL.Organization.CreateStudentUseCase>.Instance);
        var studentResult = await createStudent.ExecuteAsync(new CreateStudentRequest { Username = "new.student", FullName = "New Student",
            TemporaryPassword = "SyntheticPassword123!", GradeLevel = 12, ClassIds = [] });
        Assert.True(studentResult.IsSuccess);
        Assert.Single((await new AuthorizationSnapshotReader(db).ReadForUserAsync(studentResult.Data!.StudentId)).Roles);

        // Simulate the existing production system role from before this release.
        // The additive startup path must work without calling the demo bootstrapper.
        var teacherRole = await db.AuthorizationRoles.SingleAsync(r => r.AccountType == UserRole.Teacher && r.IsSystemRole);
        var graphIds = EduTwin.DAL.Seeding.AuthorizationPermissionCatalog.SensitiveSharedAcademicCodes
            .Select(EduTwin.DAL.Seeding.AuthorizationPermissionCatalog.CreateDeterministicId).ToArray();
        var currentTeacherGrants = await db.RolePermissions.Where(p => p.RoleId == teacherRole.RoleId).ToArrayAsync();
        db.RolePermissions.RemoveRange(currentTeacherGrants.Where(p => graphIds.Contains(p.PermissionId)));
        await db.SaveChangesAsync();
        var backfill = new EduTwin.BLL.Seeding.DefaultSystemRolePermissionBackfill(db, TimeProvider.System);
        await backfill.EnsureAsync(); await backfill.EnsureAsync();
        Assert.Equal(32, await db.RolePermissions.CountAsync(p => p.RoleId == teacherRole.RoleId));
        Assert.Equal(2u, (await db.Users.SingleAsync(u => u.UserId == createdId)).AuthVersion);
        Assert.Equal(1u, (await db.Users.SingleAsync(u => u.UserId == managerId)).AuthVersion);
        Assert.Single(await db.AuthorizationAuditLogs.Where(a => a.ActionType == "SystemRoleDefaultPermissionsGranted").ToArrayAsync());
    }

    private sealed class WorkspaceClock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(UtcNow); }

    [MySqlIntegrationFact]
    public async Task AcademicScopeAndApplicationHistory_RealSql_GuardsAndPreservesLedger_NoProviderCalls()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var center = Guid.NewGuid(); var teacher = Guid.NewGuid(); var otherTeacher = Guid.NewGuid();
        var student = Guid.NewGuid(); var subject = Guid.NewGuid(); var classId = Guid.NewGuid();
        await SeedHierarchyAsync(database.ConnectionString, center, teacher, [student], subject, classId, [otherTeacher]);
        var tenant = new TestTenantContext { CenterId=center,UserId=teacher,Role="Teacher" };
        await using var db = CreateContext(database.ConnectionString, tenant);
        (await db.Classes.SingleAsync(c=>c.ClassId==classId)).GradeLevel=12;
        var a=Guid.NewGuid(); var b=Guid.NewGuid(); var supplemental=Guid.NewGuid();
        foreach(var (id,grade) in new[]{(a,(byte)12),(b,(byte)12),(supplemental,(byte)11)})
            db.Curriculums.Add(new Curriculum {CenterId=center,CurriculumId=id,TeacherId=teacher,SubjectId=subject,GradeLevel=grade,
                Title=$"Synthetic {id}",ReviewStatus=ReviewStatus.Published,Visibility=MaterialVisibility.Shared,CreatedAt=UtcNow,UpdatedAt=UtcNow});
        db.CurriculumNodes.AddRange(new CurriculumNode {CenterId=center,CurriculumId=a,NodeId=1,OrderIndex=1,CreatedAt=UtcNow},
            new CurriculumNode {CenterId=center,CurriculumId=b,NodeId=2,OrderIndex=1,CreatedAt=UtcNow},
            new CurriculumNode {CenterId=center,CurriculumId=supplemental,NodeId=1,OrderIndex=1,CreatedAt=UtcNow});
        await db.SaveChangesAsync();
        var usecase = new EduTwin.BLL.CurriculumAndQuestions.CurriculumApplicationUseCase(db,tenant,TimeProvider.System);
        var first=await usecase.ApplyAsync(a,new(){ClassIds=[classId],RowVersion="1"},default);
        Assert.True(first.IsSuccess,first.Message);
        var noop=await usecase.ApplyAsync(a,new(){ClassIds=[classId],RowVersion=first.RowVersion!},default);
        Assert.True(noop.IsSuccess);Assert.Equal(first.RowVersion,noop.RowVersion);
        tenant.UserId=otherTeacher;
        var denied=await usecase.ApplyAsync(b,new(){ClassIds=[classId],RowVersion="1",ChangeReason="Not my class"},default);
        Assert.False(denied.IsSuccess);
        tenant.UserId=teacher;
        Assert.False((await usecase.ApplyAsync(b,new(){ClassIds=[classId],RowVersion="1"},default)).IsSuccess);
        var replacement=await usecase.ApplyAsync(b,new(){ClassIds=[classId],RowVersion="1",ChangeReason="Synthetic replacement"},default);
        Assert.True(replacement.IsSuccess,replacement.Message);
        var previous=await db.ClassCurriculumApplications.SingleAsync(x=>x.CurriculumId==a);
        Assert.NotNull(previous.EndedAt);Assert.Equal("Synthetic replacement",previous.EndReason);
        Assert.Null(previous.ChangeReason); // original application reason was not overwritten
        Assert.False((await usecase.ApplyAsync(supplemental,new(){ClassIds=[classId],RowVersion="1",ApplicationRole="Supplemental"},default)).IsSuccess);
        var extra=await usecase.ApplyAsync(supplemental,new(){ClassIds=[classId],RowVersion="1",ApplicationRole="Supplemental",GradeMismatchReason="Ôn kiến thức tiên quyết"},default);
        Assert.True(extra.IsSuccess,extra.Message);
        var exception=await db.ClassCurriculumApplications.SingleAsync(x=>x.CurriculumId==supplemental);
        Assert.True(exception.IsGradeException);Assert.Equal(teacher,exception.ExceptionApprovedBy);Assert.NotNull(exception.ExceptionApprovedAt);
        Assert.Equal(1,await db.ClassCurriculumApplications.CountAsync(x=>x.EndedAt==null&&x.ApplicationRole=="Primary"));
        Assert.Equal(3,await db.ClassCurriculumApplications.CountAsync());

        // Database uniqueness, independent of the usecase/UI.
        var detailProjection = new EduTwin.BLL.CurriculumAndQuestions.GetCurriculumUseCase(db,tenant);
        var listProjection = new EduTwin.BLL.CurriculumAndQuestions.ListCurriculumsUseCase(db,tenant);
        Assert.Equal(new[]{classId.ToString()}, (await detailProjection.ExecuteAsync(new(){CurriculumId=b})).Data!.ClassIds);
        Assert.Empty((await detailProjection.ExecuteAsync(new(){CurriculumId=a})).Data!.ClassIds);
        var projected = await listProjection.ExecuteAsync(new());Assert.True(projected.IsSuccess,projected.ErrorCode);
        Assert.Equal(new[]{classId.ToString()},projected.Data!.Single(c=>c.CurriculumId==b.ToString()).ClassIds);
        Assert.Empty(projected.Data!.Single(c=>c.CurriculumId==a.ToString()).ClassIds);
        db.ClassCurriculumApplications.Add(new ClassCurriculumApplication {ApplicationId=Guid.NewGuid(),CenterId=center,ClassId=classId,
            CurriculumId=a,SubjectId=subject,ApplicationRole="Primary",AssignedBy=teacher,StartedAt=DateTime.UtcNow,
            ClassGradeAtStart=12,CurriculumGradeAtStart=12});
        await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<MySqlException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE class_curriculum_applications SET change_reason='tamper' WHERE application_id={previous.ApplicationId.ToString()}"));
        await Assert.ThrowsAsync<MySqlException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"DELETE FROM class_curriculum_applications WHERE application_id={previous.ApplicationId.ToString()}"));
        await Assert.ThrowsAsync<MySqlException>(()=>db.Database.ExecuteSqlInterpolatedAsync($"UPDATE class_students SET grade_mismatch_reason='incomplete' WHERE center_id={center.ToString()} AND class_id={classId.ToString()} AND student_id={student.ToString()}"));
        Assert.Equal(3,await db.ClassCurriculumApplications.CountAsync());

        var otherSubject=Guid.NewGuid();
        db.Subjects.Add(new Subject {CenterId=center,SubjectId=otherSubject,SubjectCode="OTHER",SubjectName="Other subject",IsActive=true,CreatedAt=UtcNow,UpdatedAt=UtcNow});
        db.KnowledgeNodes.Add(new KnowledgeNode {CenterId=center,SubjectId=otherSubject,NodeId=99,NodeCode="OTHER_TOPIC",NodeName="Other topic",NodeType=NodeType.Topic,
            IsActive=true,EstimatedLearningMinutes=1,ExamImportance=1,OrderIndex=1,CreatedAt=UtcNow,UpdatedAt=UtcNow});await db.SaveChangesAsync();
        db.CurriculumNodes.Add(new CurriculumNode {CenterId=center,CurriculumId=b,NodeId=99,OrderIndex=2,CreatedAt=UtcNow});
        await Assert.ThrowsAsync<DbUpdateException>(()=>db.SaveChangesAsync());db.ChangeTracker.Clear();
        tenant.UserId=student;tenant.Role="Student";
        db.KnowledgeNodes.Add(new KnowledgeNode {CenterId=center,SubjectId=subject,NodeId=3,NodeCode="OUTSIDE_CURRICULUM",NodeName="Outside selected curriculum",NodeType=NodeType.Topic,
            IsActive=true,EstimatedLearningMinutes=1,ExamImportance=1,OrderIndex=3,CreatedAt=UtcNow,UpdatedAt=UtcNow});
        await db.SaveChangesAsync();
        var questionnaireTopics=new EduTwin.BLL.Recommendations.UseCases.GetLearningPathTopicsUseCase(db,tenant);
        Assert.Equal(new[]{"1","2"},(await questionnaireTopics.ExecuteScopedAsync(subject,classId,false,default)).Select(x=>x.TopicNodeId).Order());
        Assert.Empty(await questionnaireTopics.ExecuteScopedAsync(subject,Guid.NewGuid(),false,default));
        Assert.Empty(await questionnaireTopics.ExecuteScopedAsync(subject,classId,true,default));
        var scope=await new StudentAcademicScopeReader(db).ReadAsync(center,student,subject,classId,false,default);
        Assert.NotNull(scope);Assert.Equal(new ulong[]{1,2},scope.TopicIds.Order());
        Assert.Equal(2,scope.Context.Curriculums.Count);
        Assert.Null(await new StudentAcademicScopeReader(db).ReadAsync(center,student,subject,Guid.NewGuid(),false,default));
        var dashboard=await new GetStudentDashboardUseCase(db,tenant,TimeProvider.System).ExecuteAsync(subject,classId,false,default);
        Assert.True(dashboard.IsSuccess);Assert.Equal(2,dashboard.Data!.MasteryRadar.Count);
        var liveLearning=await EduTwin.BLL.Organization.StudentLearningScope.ResolveAsync(db,center,student,subject,classId,false,default);
        Assert.True(liveLearning.Allowed);Assert.Equal(new ulong[]{1,2},liveLearning.TopicIds!.Order());
        db.Questions.Add(new Question{CenterId=center,QuestionId=101,SubjectId=subject,PrimaryTopicNodeId=2,CreatedByTeacherId=teacher,
            QuestionType=QuestionType.ShortAnswer,QuestionText="Synthetic scope check",CorrectAnswer="x",Solution="Synthetic",LanguageCode="vi",Status=QuestionStatus.Active,
            MaxScore=10,Difficulty=2,EstimatedTimeSeconds=60,CreatedAt=UtcNow,UpdatedAt=UtcNow});
        db.StudentLearningPathPreferences.Add(new(){CenterId=center,StudentId=student,SubjectId=subject,WeakTopicNodeIds=JsonSerializer.SerializeToDocument(Array.Empty<ulong>()),
            FocusTopicNodeIds=JsonSerializer.SerializeToDocument(Array.Empty<ulong>()),CreatedAt=UtcNow,UpdatedAt=UtcNow});
        var savedPath=new EduTwin.DAL.Recommendations.LearningPath{CenterId=center,LearningPathId=Guid.NewGuid(),StudentId=student,SubjectId=subject,
            Status=EduTwin.Contracts.Recommendations.LearningPathStatus.Active,Version=1,GeneratedAt=UtcNow,CreatedAt=UtcNow,UpdatedAt=UtcNow};
        savedPath.Items.Add(new(){CenterId=center,TopicNodeId=2,RankOrder=1,Reason="Synthetic scope",CreatedAt=UtcNow,UpdatedAt=UtcNow});
        db.LearningPaths.Add(savedPath);await db.SaveChangesAsync();
        var strictEngine=new Moq.Mock<EduTwin.BLL.Recommendations.IRecommendationEngine>(Moq.MockBehavior.Strict);
        var pathReader=new EduTwin.BLL.Recommendations.UseCases.GenerateLearningPathUseCase(db,tenant,strictEngine.Object);
        Assert.NotNull(await pathReader.ExecuteAsync(subject,classId,default));Assert.Null(savedPath.PlanJson);
        var validator=new EduTwin.BLL.AssessmentAndReasoning.AttemptSubmissionValidator(db,tenant,
            new EduTwin.BLL.AssessmentAndReasoning.PreliminaryGrading.PreliminaryGraderFactory(new(),new(),new()));
        SubmitAttemptRequest Adaptive(Guid? selected)=>new(){ClientSubmissionId=Guid.NewGuid(),QuestionId="101",FinalAnswer="x",ClassId=selected};
        Assert.True((await validator.ValidateAsync(Adaptive(classId))).IsSuccess);
        Assert.Empty(await db.Attempts.ToListAsync()); // no AI request or submission mutation
        // Manager class lifecycle must not rewrite teacher-owned application history.
        var manager=Guid.NewGuid();
        db.Users.Add(new User {CenterId=center,UserId=manager,Username="synthetic.lifecycle.manager",DisplayName="Manager",PasswordHash="H",
            RoleName=UserRole.CenterManager,Status=UserStatus.Active,CreatedAt=UtcNow,UpdatedAt=UtcNow});await db.SaveChangesAsync();
        tenant.UserId=manager;tenant.Role="CenterManager";
        var cls=await db.Classes.SingleAsync(c=>c.ClassId==classId);
        var update=new EduTwin.BLL.Organization.UpdateClassUseCase(db,tenant,TimeProvider.System,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<EduTwin.BLL.Organization.UpdateClassUseCase>.Instance);
        UpdateClassRequest Change(ClassStatus status,string version)=>new(){ClassName=cls.ClassName,TeacherId=teacher,Status=status,GradeLevel=cls.GradeLevel,RowVersion=version,LifecycleReason="Synthetic class lifecycle"};
        var archived=await update.ExecuteAsync(classId,Change(ClassStatus.Archived,cls.RowVersion.ToString()));Assert.True(archived.IsSuccess,archived.ErrorCode);
        Assert.False((await EduTwin.BLL.Organization.StudentLearningScope.ResolveAsync(db,center,student,subject,classId,false,default)).Allowed);
        Assert.False((await EduTwin.BLL.Organization.StudentLearningScope.ResolveAsync(db,center,student,subject,null,false,default)).Allowed);
        var archivedCandidates=await new EduTwin.BLL.Recommendations.OpportunityCandidateBuilder(db).BuildCandidatesAsync(center,student,subject,default);
        Assert.Equal("NO_ACTIVE_CLASS",archivedCandidates.BlockedReason);
        tenant.UserId=student;tenant.Role="Student";
        Assert.Empty(await questionnaireTopics.ExecuteScopedAsync(subject,classId,false,default));
        Assert.Empty(await questionnaireTopics.ExecuteScopedAsync(subject,classId,true,default));
        Assert.Equal(EduTwin.Contracts.Common.ErrorCodes.AssignmentNotAvailable,(await validator.ValidateAsync(Adaptive(classId))).ErrorCode);
        Assert.Equal(EduTwin.Contracts.Common.ErrorCodes.AssignmentNotAvailable,(await validator.ValidateAsync(Adaptive(null))).ErrorCode);
        Assert.Null(await pathReader.ExecuteAsync(subject,classId,default));
        await Assert.ThrowsAsync<EduTwin.BLL.Organization.LearningScopeDeniedException>(()=>pathReader.ExecuteAsync(new EduTwin.Contracts.Recommendations.GenerateLearningPathRequest{SubjectId=subject,ClassId=classId},default));
        Assert.Null(savedPath.PlanJson);Assert.Empty(strictEngine.Invocations);Assert.Empty(await db.Attempts.ToListAsync());
        tenant.UserId=teacher;tenant.Role="Teacher";
        var paused=await usecase.ReadAsync(b,default);Assert.All(paused.Data!,row=>Assert.True(row.PausedByClass));
        Assert.Empty((await detailProjection.ExecuteAsync(new(){CurriculumId=b})).Data!.ClassIds);
        Assert.Equal(3,await db.ClassCurriculumApplications.CountAsync());
        Assert.Equal(2,await db.ClassCurriculumApplications.CountAsync(x=>x.EndedAt==null));
        tenant.UserId=manager;tenant.Role="CenterManager";
        Assert.True((await update.ExecuteAsync(classId,Change(ClassStatus.Active,archived.Data!.RowVersion))).IsSuccess);
        Assert.True((await EduTwin.BLL.Organization.StudentLearningScope.ResolveAsync(db,center,student,subject,classId,false,default)).Allowed);
        tenant.UserId=teacher;tenant.Role="Teacher";
        Assert.Equal(new[]{classId.ToString()}, (await detailProjection.ExecuteAsync(new(){CurriculumId=b})).Data!.ClassIds);
        Assert.Equal(3,await db.ClassCurriculumApplications.CountAsync());
        Assert.Equal(2,await db.ClassCurriculumApplications.CountAsync(x=>x.EndedAt==null));
        Assert.NotNull((await db.ClassCurriculumApplications.SingleAsync(x=>x.CurriculumId==a)).EndedAt);
    }

    [MySqlIntegrationFact]
    public async Task StudentGradeGuards_RealSql_PreserveEnrollmentSnapshot_NoProviderCalls()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var centerId = Guid.NewGuid(); var teacherId = Guid.NewGuid(); var managerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid(); var classId = Guid.NewGuid();
        await SeedHierarchyAsync(database.ConnectionString, centerId, teacherId, [], subjectId, classId);
        var tenant = new TestTenantContext { CenterId = centerId, UserId = managerId, Role = nameof(UserRole.CenterManager) };
        await using var db = CreateContext(database.ConnectionString, tenant);
        var seededClass = await db.Classes.SingleAsync();
        seededClass.GradeLevel = 10;
        var secondClassId = Guid.NewGuid();
        db.Classes.Add(new Class { CenterId = centerId, ClassId = secondClassId, TeacherId = teacherId,
            SubjectId = subjectId, ClassName = "Second Grade 10 Class", AcademicYear = seededClass.AcademicYear,
            GradeLevel = 10, Status = ClassStatus.Active, CreatedAt = UtcNow, UpdatedAt = UtcNow });
        db.Users.Add(new User { UserId = managerId, CenterId = centerId, Username = "grade.manager", PasswordHash = "synthetic",
            DisplayName = "Grade Manager", RoleName = UserRole.CenterManager, Status = UserStatus.Active,
            AuthVersion = 1, CreatedAt = UtcNow, UpdatedAt = UtcNow });
        await db.SaveChangesAsync();
        await new EduTwin.BLL.Seeding.AuthorizationBootstrapper(db, TimeProvider.System).EnsureCenterAsync(centerId);
        var create = new EduTwin.BLL.Organization.CreateStudentUseCase(db, tenant,
            new Microsoft.AspNetCore.Identity.PasswordHasher<User>(), TimeProvider.System,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<EduTwin.BLL.Organization.CreateStudentUseCase>.Instance);
        var request = new CreateStudentRequest { Username = "grade.student", FullName = "Grade Student",
            TemporaryPassword = "SyntheticPassword123!", GradeLevel = 11, ClassIds = [classId, secondClassId] };
        var rejected = await create.ExecuteAsync(request);
        Assert.False(rejected.IsSuccess);
        Assert.Equal(EduTwin.Contracts.Common.ErrorCodes.ValidationFailed, rejected.ErrorCode);
        Assert.False(await db.Users.AnyAsync(u => u.Username == request.Username));
        request.GradeLevel = 10;
        var created = await create.ExecuteAsync(request);
        Assert.True(created.IsSuccess, created.ErrorMessage);
        var studentId = created.Data!.StudentId;
        var memberships = await db.ClassStudents.Where(cs => cs.StudentId == studentId).ToListAsync();
        Assert.Equal(2, memberships.Count);
        Assert.All(memberships, membership => Assert.Equal((byte)10, membership.GradeLevelAtEnrollment));
        var update = new EduTwin.BLL.Organization.UpdateStudentUseCase(db, tenant,
            new OrganizationOwnershipGuard(db, tenant), TimeProvider.System,
            Microsoft.Extensions.Logging.Abstractions.NullLogger<EduTwin.BLL.Organization.UpdateStudentUseCase>.Instance);
        var updateRequest = new UpdateStudentRequest { FullName = "Grade Student", GradeLevel = 11,
            Status = UserStatus.Active, RowVersion = created.Data.RowVersion };
        var blocked = await update.ExecuteAsync(studentId, updateRequest);
        Assert.False(blocked.IsSuccess);
        Assert.Equal(EduTwin.Contracts.Common.ErrorCodes.ValidationFailed, blocked.ErrorCode);
        Assert.Equal((byte)10, (await db.Students.SingleAsync(s => s.StudentId == studentId)).GradeLevel);
        foreach (var membership in memberships) membership.Status = ClassStudentStatus.Removed;
        await db.SaveChangesAsync();
        var changed = await update.ExecuteAsync(studentId, updateRequest);
        Assert.True(changed.IsSuccess, changed.ErrorMessage);
        Assert.Equal(11, changed.Data!.GradeLevel);
        Assert.All(memberships, membership => Assert.Equal((byte)10, membership.GradeLevelAtEnrollment)); // historical enrollment is never rewritten
    }

    [MySqlIntegrationFact]
    public async Task QuestionImages_RealSql_CreateReadAndScope_NoProviderCalls()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var centerId = Guid.NewGuid(); var teacherId = Guid.NewGuid(); var studentId = Guid.NewGuid();
        var otherStudentId = Guid.NewGuid(); var subjectId = Guid.NewGuid(); var classId = Guid.NewGuid();
        await SeedHierarchyAsync(database.ConnectionString, centerId, teacherId, [studentId, otherStudentId], subjectId, classId);
        var tenant = new TestTenantContext { CenterId = centerId, UserId = teacherId, Role = nameof(UserRole.Teacher) };
        await using var db = CreateContext(database.ConnectionString, tenant);
        var create = new EduTwin.BLL.CurriculumAndQuestions.CreateQuestionUseCase(db, tenant, new WorkspaceClock());
        var result = await create.ExecuteAsync(new CreateQuestionRequest { SubjectId = subjectId, PrimaryTopicNodeId = "1",
            QuestionType = "ShortAnswer", AnswerEvaluationMode = "TextExact", GradeLevel = 12, Difficulty = 2,
            QuestionText = "", ImageDataUrl = CurriculumAndQuestions.QuestionImageFixture.DataUrl, CorrectAnswer = "5",
            Solution = "Synthetic reference solution", MaxScore = 10, EstimatedTimeSeconds = 60, LanguageCode = "vi" });
        Assert.True(result.IsSuccess); Assert.True(result.Data!.HasImage);
        var qid = ulong.Parse(result.Data.QuestionId);
        var reader = new EduTwin.BLL.CurriculumAndQuestions.GetQuestionImageUseCase(db, tenant);
        Assert.Equal(CurriculumAndQuestions.QuestionImageFixture.Bytes, await reader.ExecuteAsync(qid, CancellationToken.None));
        var assignment = new Assignment { AssignmentId = Guid.NewGuid(), CenterId = centerId, ClassId = classId,
            CreatedByTeacherId = teacherId, Title = "Synthetic image scope test", Status = AssignmentStatus.Draft,
            CreatedAt = UtcNow, UpdatedAt = UtcNow };
        db.Assignments.Add(assignment);
        db.AssignmentQuestions.Add(new AssignmentQuestion { CenterId = centerId, AssignmentId = assignment.AssignmentId,
            QuestionId = qid, OrderIndex = 1, Points = 10, CreatedAt = UtcNow });
        db.StudentAssignmentProgresses.Add(new StudentAssignmentProgress { CenterId = centerId, StudentId = studentId,
            AssignmentId = assignment.AssignmentId, TotalQuestionCount = 1, CreatedAt = UtcNow, UpdatedAt = UtcNow });
        await db.SaveChangesAsync();
        tenant.UserId = studentId; tenant.Role = nameof(UserRole.Student);
        Assert.Null(await reader.ExecuteAsync(qid, CancellationToken.None)); // draft is not an assignment yet
        assignment.Status = AssignmentStatus.Published; await db.SaveChangesAsync();
        Assert.Equal(CurriculumAndQuestions.QuestionImageFixture.Bytes, await reader.ExecuteAsync(qid, CancellationToken.None));
        tenant.UserId = otherStudentId;
        Assert.Null(await reader.ExecuteAsync(qid, CancellationToken.None)); // same center but not assigned
        tenant.CenterId = Guid.NewGuid(); tenant.UserId = teacherId; tenant.Role = nameof(UserRole.Teacher);
        Assert.Null(await reader.ExecuteAsync(qid, CancellationToken.None));
    }

    [MySqlIntegrationFact]
    public async Task AssessedClassMastery_And_AssignmentCompletion_CalculatedCorrectly()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var student1Id = Guid.NewGuid();
        var student2Id = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var classId = Guid.NewGuid();

        await SeedHierarchyAsync(database.ConnectionString, centerId, teacherId, [student1Id, student2Id], subjectId, classId);

        var tenant = new TestTenantContext
        {
            CenterId = centerId,
            UserId = teacherId,
            Role = nameof(UserRole.Teacher)
        };
        await using var context = CreateContext(database.ConnectionString, tenant);

        await ApplyPublishedCurriculumFixtureAsync(context, centerId, teacherId, subjectId, classId);
        // Topic 1: exam importance 60, Topic 2: exam importance 40
        // Student 1 has KnowledgeTwin on Topic 1 (mastery 80), missing KnowledgeTwin on Topic 2 (zero-fill => 0)
        // Student 1 weighted mastery = (80 * 60 + 0 * 40) / 100 = 48%
        // Student 2 has missing KnowledgeTwins on both topics (zero-fill => 0 on both)
        // Student 2 weighted mastery = 0%
        // Class dashboard separates unknown evidence: only S1/topic1 is assessed, so its mean is 80%.
        // The Center dashboard's legacy aggregate policy is a separate existing contract below.
        context.KnowledgeTwins.Add(new KnowledgeTwin
        {
            KnowledgeTwinId = 1UL,
            CenterId = centerId,
            StudentId = student1Id,
            SubjectId = subjectId,
            TopicNodeId = 1,
            MasteryPercentage = 80m,
            EvidenceCount = 3,
            CreatedAt = UtcNow,
            UpdatedAt = UtcNow
        });

        // Add 1 published assignment with 2 student targets: student 1 Completed, student 2 InProgress
        // Completion rate = 1 / 2 = 50.0%
        var assignmentId = Guid.NewGuid();
        context.Assignments.Add(new Assignment
        {
            AssignmentId = assignmentId,
            CenterId = centerId,
            ClassId = classId,
            CreatedByTeacherId = teacherId,
            Title = "Math Assignment 1",
            Status = AssignmentStatus.Published,
            CreatedAt = UtcNow,
            UpdatedAt = UtcNow,
            RowVersion = 1
        });

        context.AssignmentTargets.AddRange(
            new AssignmentTarget
            {
                CenterId = centerId,
                AssignmentId = assignmentId,
                StudentId = student1Id,
                TargetSource = TargetSource.SelectedStudents,
                CreatedAt = UtcNow
            },
            new AssignmentTarget
            {
                CenterId = centerId,
                AssignmentId = assignmentId,
                StudentId = student2Id,
                TargetSource = TargetSource.SelectedStudents,
                CreatedAt = UtcNow
            }
        );

        context.StudentAssignmentProgresses.Add(new StudentAssignmentProgress
        {
            ProgressId = 1UL,
            CenterId = centerId,
            AssignmentId = assignmentId,
            StudentId = student1Id,
            Status = ProgressStatus.Completed,
            CompletedQuestionCount = 5,
            TotalQuestionCount = 5,
            CompletedAt = UtcNow,
            CreatedAt = UtcNow,
            UpdatedAt = UtcNow,
            RowVersion = 1
        });

        // Add high-risk goal for student 2: Target 8.5, Predicted 1.0, Risk 75.0 (>= 70 threshold)
        context.StudentSubjectGoals.Add(new StudentSubjectGoal
        {
            GoalId = 1UL,
            CenterId = centerId,
            StudentId = student2Id,
            SubjectId = subjectId,
            TargetScore = 8.5m,
            RemainingDays = 30U,
            CurrentPredictedScore = 1.0m,
            RiskScore = 75.0m,
            CreatedAt = UtcNow,
            UpdatedAt = UtcNow
        });

        await context.SaveChangesAsync();

        // 1. Verify Class Dashboard Use Case
        var ownershipGuard = new OrganizationOwnershipGuard(context, tenant);
        var classDashboardUseCase = new GetClassDashboardUseCase(context, tenant, ownershipGuard, TimeProvider.System);
        var classResult = await classDashboardUseCase.ExecuteAsync(classId, 70m, CancellationToken.None);

        Assert.True(classResult.IsSuccess);
        Assert.NotNull(classResult.Data);
        Assert.Equal(2, classResult.Data.Overview.StudentCount);
        Assert.Equal(80.0m, classResult.Data.Overview.AverageMastery);
        Assert.Equal(50.0m, classResult.Data.Overview.AssignmentCompletionRate);
        Assert.Single(classResult.Data.HighRiskStudents);
        Assert.Equal(student2Id, classResult.Data.HighRiskStudents[0].StudentId);
        Assert.Equal(75.0m, classResult.Data.HighRiskStudents[0].RiskScore);

        Assert.Empty(classResult.Data.WeakTopics);
        Assert.Empty(classResult.Data.GapGroups);
        Assert.Equal(2, classResult.Data.AcademicCoverage.ApplicableTopicCount);
        Assert.Equal(1, classResult.Data.AcademicCoverage.AssessedTopicCount);
        Assert.Equal(3, classResult.Data.AcademicCoverage.UnassessedStudentTopicCount);

        // 2. Verify Center Dashboard Use Case
        var centerTenant = new TestTenantContext
        {
            CenterId = centerId,
            UserId = Guid.NewGuid(),
            Role = nameof(UserRole.CenterManager)
        };
        await using var centerContext = CreateContext(database.ConnectionString, centerTenant);
        var centerDashboardUseCase = new GetCenterDashboardUseCase(centerContext, centerTenant, TimeProvider.System);
        var centerResult = await centerDashboardUseCase.ExecuteAsync(subjectId, 70m, CancellationToken.None);

        Assert.True(centerResult.IsSuccess);
        Assert.NotNull(centerResult.Data);
        Assert.Equal(2, centerResult.Data.Summary.StudentCount);
        Assert.Equal(1, centerResult.Data.Summary.ClassCount);
        Assert.Equal(1, centerResult.Data.Summary.TeacherCount);
        Assert.Single(centerResult.Data.MasteryBySubject);
        Assert.Equal(24.0m, centerResult.Data.MasteryBySubject[0].AverageMastery);
        Assert.Single(centerResult.Data.ClassRanking);
        Assert.Equal(24.0m, centerResult.Data.ClassRanking[0].AverageMastery);
    }

    [MySqlIntegrationFact]
    public async Task CrossTenantIsolation_ReturnsNotFound_And_Forbidden()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var centerAId = Guid.NewGuid();
        var centerBId = Guid.NewGuid();
        var teacherAId = Guid.NewGuid();
        var teacherBId = Guid.NewGuid();
        var studentAId = Guid.NewGuid();
        var studentBId = Guid.NewGuid();
        var subjectAId = Guid.NewGuid();
        var subjectBId = Guid.NewGuid();
        var classAId = Guid.NewGuid();
        var classBId = Guid.NewGuid();

        await SeedHierarchyAsync(database.ConnectionString, centerAId, teacherAId, [studentAId], subjectAId, classAId);
        await SeedHierarchyAsync(database.ConnectionString, centerBId, teacherBId, [studentBId], subjectBId, classBId, null, baseNodeId: 10UL);

        // Set tenant context to Center A
        var tenantA = new TestTenantContext
        {
            CenterId = centerAId,
            UserId = teacherAId,
            Role = nameof(UserRole.Teacher)
        };
        await using var contextA = CreateContext(database.ConnectionString, tenantA);

        var ownershipGuardA = new OrganizationOwnershipGuard(contextA, tenantA);
        var classDashboardUseCase = new GetClassDashboardUseCase(contextA, tenantA, ownershipGuardA, TimeProvider.System);

        // Query class from Center B while logged into Center A
        var crossTenantClassResult = await classDashboardUseCase.ExecuteAsync(classBId, 70m, CancellationToken.None);
        Assert.False(crossTenantClassResult.IsSuccess);
        Assert.Equal("RESOURCE_NOT_FOUND", crossTenantClassResult.ErrorCode);

        // Query student twin from Center B while logged into Center A
        var teacherTwinUseCase = new GetTeacherStudentTwinUseCase(contextA, tenantA, ownershipGuardA);
        var crossTenantStudentResult = await teacherTwinUseCase.ExecuteAsync(studentBId, subjectAId, CancellationToken.None);
        Assert.False(crossTenantStudentResult.IsSuccess);
        Assert.Equal("RESOURCE_NOT_FOUND", crossTenantStudentResult.ErrorCode);
    }

    [MySqlIntegrationFact]
    public async Task AuthorizationGuards_TeacherOwnership_EnforcesForbiddenProperly()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var centerId = Guid.NewGuid();
        var teacherAssignedId = Guid.NewGuid();
        var teacherUnassignedId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var classId = Guid.NewGuid();

        await SeedHierarchyAsync(database.ConnectionString, centerId, teacherAssignedId, [studentId], subjectId, classId, [teacherUnassignedId]);

        // Test with unassigned teacher context
        var unassignedTenant = new TestTenantContext
        {
            CenterId = centerId,
            UserId = teacherUnassignedId,
            Role = nameof(UserRole.Teacher)
        };
        await using var unassignedContext = CreateContext(database.ConnectionString, unassignedTenant);

        var unassignedGuard = new OrganizationOwnershipGuard(unassignedContext, unassignedTenant);
        var unassignedClassDashboardUseCase = new GetClassDashboardUseCase(unassignedContext, unassignedTenant, unassignedGuard, TimeProvider.System);

        var forbiddenClassResult = await unassignedClassDashboardUseCase.ExecuteAsync(classId, 70m, CancellationToken.None);
        Assert.False(forbiddenClassResult.IsSuccess);
        Assert.Equal("FORBIDDEN_RESOURCE", forbiddenClassResult.ErrorCode);

        var unassignedTwinUseCase = new GetTeacherStudentTwinUseCase(unassignedContext, unassignedTenant, unassignedGuard);
        var forbiddenTwinResult = await unassignedTwinUseCase.ExecuteAsync(studentId, subjectId, CancellationToken.None);
        Assert.False(forbiddenTwinResult.IsSuccess);
        Assert.Equal("FORBIDDEN_RESOURCE", forbiddenTwinResult.ErrorCode);

        // Now test with assigned teacher context
        var assignedTenant = new TestTenantContext
        {
            CenterId = centerId,
            UserId = teacherAssignedId,
            Role = nameof(UserRole.Teacher)
        };
        await using var assignedContext = CreateContext(database.ConnectionString, assignedTenant);

        var assignedGuard = new OrganizationOwnershipGuard(assignedContext, assignedTenant);
        var assignedClassDashboardUseCase = new GetClassDashboardUseCase(assignedContext, assignedTenant, assignedGuard, TimeProvider.System);
        var allowedClassResult = await assignedClassDashboardUseCase.ExecuteAsync(classId, 70m, CancellationToken.None);
        Assert.True(allowedClassResult.IsSuccess);

        var assignedTwinUseCase = new GetTeacherStudentTwinUseCase(assignedContext, assignedTenant, assignedGuard);
        var allowedTwinResult = await assignedTwinUseCase.ExecuteAsync(studentId, subjectId, CancellationToken.None);
        Assert.True(allowedTwinResult.IsSuccess);
    }

    [MySqlIntegrationFact]
    public async Task StudentDashboard_ProgressLineReconstruction_And_GoalRead()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var classId = Guid.NewGuid();

        await SeedHierarchyAsync(database.ConnectionString, centerId, teacherId, [studentId], subjectId, classId);

        var tenant = new TestTenantContext
        {
            CenterId = centerId,
            UserId = studentId,
            Role = nameof(UserRole.Student)
        };
        await using var context = CreateContext(database.ConnectionString, tenant);

        // Persist governed Goal state (R06 single source of truth)
        await ApplyPublishedCurriculumFixtureAsync(context, centerId, teacherId, subjectId, classId);
        context.StudentSubjectGoals.Add(new StudentSubjectGoal
        {
            GoalId = 1UL,
            CenterId = centerId,
            StudentId = studentId,
            SubjectId = subjectId,
            TargetScore = 9.0m,
            RemainingDays = 60U,
            CurrentPredictedScore = 6.5m,
            RiskScore = 18.2m,
            CreatedAt = UtcNow,
            UpdatedAt = UtcNow
        });

        using var jsonDoc = JsonDocument.Parse("{}");

        // Add topic history events:
        // Event 1: Topic 1 (importance 60) goes to 50%
        // Event 2: Topic 2 (importance 40) goes to 40%
        // Weighted subject mastery at event 1: (50 * 60 + 0 * 40) / 100 = 30.0%
        // Weighted subject mastery at event 2: (50 * 60 + 40 * 40) / 100 = 46.0%
        context.TwinUpdateHistories.AddRange(
            new TwinUpdateHistory
            {
                HistoryId = 1UL,
                CenterId = centerId,
                StudentId = studentId,
                SubjectId = subjectId,
                TopicNodeId = 1UL,
                EventSource = TwinEventSource.AIAnalysis,
                PreviousMastery = 0m,
                NewMastery = 50m,
                MasteryDelta = 50m,
                CalculationVersion = "1.0",
                CalculationBreakdown = jsonDoc,
                Explanation = "Test 1",
                CreatedAt = UtcNow.AddMinutes(1)
            },
            new TwinUpdateHistory
            {
                HistoryId = 2UL,
                CenterId = centerId,
                StudentId = studentId,
                SubjectId = subjectId,
                TopicNodeId = 2UL,
                EventSource = TwinEventSource.AIAnalysis,
                PreviousMastery = 0m,
                NewMastery = 40m,
                MasteryDelta = 40m,
                CalculationVersion = "1.0",
                CalculationBreakdown = jsonDoc,
                Explanation = "Test 2",
                CreatedAt = UtcNow.AddMinutes(2)
            }
        );

        // Also add KnowledgeTwin for radar
        context.KnowledgeTwins.AddRange(
            new KnowledgeTwin
            {
                KnowledgeTwinId = 1UL,
                CenterId = centerId,
                StudentId = studentId,
                SubjectId = subjectId,
                TopicNodeId = 1UL,
                MasteryPercentage = 50m,
                EvidenceCount = 1,
                CreatedAt = UtcNow,
                UpdatedAt = UtcNow
            },
            new KnowledgeTwin
            {
                KnowledgeTwinId = 2UL,
                CenterId = centerId,
                StudentId = studentId,
                SubjectId = subjectId,
                TopicNodeId = 2UL,
                MasteryPercentage = 40m,
                EvidenceCount = 1,
                CreatedAt = UtcNow,
                UpdatedAt = UtcNow
            }
        );

        await context.SaveChangesAsync();

        var studentDashboardUseCase = new GetStudentDashboardUseCase(context, tenant, TimeProvider.System);
        var result = await studentDashboardUseCase.ExecuteAsync(subjectId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);

        // Governed goal read directly, not re-calculated in R08
        Assert.Equal(9.0m, result.Data.Goal.TargetScore);
        Assert.Equal(6.5m, result.Data.Goal.CurrentPredictedScore);
        Assert.Equal(18.2m, result.Data.Goal.RiskScore);
        Assert.Equal(60U, result.Data.Goal.RemainingDays);

        // Progress line reconstruction: exactly 2 points reflecting cumulative weighted mastery
        Assert.Equal(2, result.Data.ProgressLine.Count);
        Assert.Equal(30.0m, result.Data.ProgressLine[0].OverallSubjectMastery);
        Assert.Equal(46.0m, result.Data.ProgressLine[1].OverallSubjectMastery);

        // Mastery radar: 2 topics
        Assert.Equal(2, result.Data.MasteryRadar.Count);
    }

    [MySqlIntegrationFact]
    public async Task AttemptFeedback_ExactContract54_And_OwnershipAccess()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var student1Id = Guid.NewGuid();
        var student2Id = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var classId = Guid.NewGuid();

        await SeedHierarchyAsync(database.ConnectionString, centerId, teacherId, [student1Id, student2Id], subjectId, classId);

        var adminTenant = new TestTenantContext { CenterId = centerId, UserId = teacherId, Role = nameof(UserRole.Teacher) };
        await using var setupContext = CreateContext(database.ConnectionString, adminTenant);

        // Seed question
        var question = new Question
        {
            QuestionId = 100UL,
            CenterId = centerId,
            SubjectId = subjectId,
            PrimaryTopicNodeId = 1UL,
            CreatedByTeacherId = teacherId,
            QuestionType = QuestionType.MultipleChoice,
            Difficulty = 3,
            QuestionText = "Calculate derivative of f(x) = x^2",
            CorrectAnswer = "2x",
            Solution = "Using power rule: 2x",
            MaxScore = 10.0m,
            EstimatedTimeSeconds = 120,
            ReasoningRequired = true,
            LanguageCode = "vi",
            Status = QuestionStatus.Active,
            CreatedAt = UtcNow,
            UpdatedAt = UtcNow,
            RowVersion = 1
        };
        setupContext.Questions.Add(question);

        // Seed attempt for student1
        var attempt = new Attempt
        {
            AttemptId = 200UL,
            CenterId = centerId,
            StudentId = student1Id,
            QuestionId = 100UL,
            FinalAnswer = "2x",
            ReasoningText = "f'(x) = 2*x^(2-1) = 2x",
            IsCorrect = true,
            AwardedScore = 10.0m,
            TimeSpentSeconds = 65,
            Confidence = 0.95m,
            AnswerChanges = 0,
            Skipped = false,
            ReasoningLanguage = "vi",
            Status = AttemptStatus.Completed,
            ClientSubmissionId = Guid.NewGuid(),
            CreatedAt = UtcNow,
            UpdatedAt = UtcNow,
            RowVersion = 1
        };
        setupContext.Attempts.Add(attempt);

        using var emptyArrayDoc = JsonDocument.Parse("[]");
        var reasoningAnalysis = new ReasoningAnalysis
        {
            AnalysisId = 300UL,
            CenterId = centerId,
            AttemptId = 200UL,
            SchemaVersion = "1.0",
            MethodDetected = "Power Rule",
            ReasoningQuality = 95m,
            ErrorType = ErrorType.None,
            Misconception = null,
            MissingSteps = emptyArrayDoc,
            RootCauseNodeIds = emptyArrayDoc,
            AnalysisConfidence = 0.98m,
            Feedback = "Rất tốt, lập luận chặt chẽ và chính xác.",
            IsFallback = false,
            NeedsTeacherReview = false,
            Provider = AnalysisProvider.RuleBased,
            CreatedAt = UtcNow,
            UpdatedAt = UtcNow,
            RowVersion = 1
        };
        setupContext.ReasoningAnalyses.Add(reasoningAnalysis);

        await setupContext.SaveChangesAsync();

        // 1. Student 1 (Owner) can read feedback
        var student1Tenant = new TestTenantContext
        {
            CenterId = centerId,
            UserId = student1Id,
            Role = nameof(UserRole.Student)
        };
        await using var s1Context = CreateContext(database.ConnectionString, student1Tenant);
        var s1Guard = new OrganizationOwnershipGuard(s1Context, student1Tenant);
        var feedbackUseCase = new GetAttemptFeedbackUseCase(s1Context, student1Tenant, s1Guard);

        var s1Result = await feedbackUseCase.ExecuteAsync(200UL, CancellationToken.None);
        Assert.True(s1Result.IsSuccess);
        Assert.NotNull(s1Result.Data);
        Assert.True(s1Result.Data.Grading.IsCorrect);
        Assert.Equal(10.0m, s1Result.Data.Grading.AwardedScore);
        Assert.Equal(10.0m, s1Result.Data.Grading.MaxScore);
        Assert.NotNull(s1Result.Data.Analysis);
        Assert.Equal("300", s1Result.Data.Analysis.AnalysisId);
        Assert.Equal("Power Rule", s1Result.Data.Analysis.MethodDetected);
        Assert.Equal(95, s1Result.Data.Analysis.ReasoningQuality);
        Assert.Equal("Good", s1Result.Data.Analysis.QualityBand);
        Assert.False(s1Result.Data.Analysis.NeedsTeacherReview);

        // 2. Student 2 cannot read student 1's feedback -> Forbidden
        var student2Tenant = new TestTenantContext
        {
            CenterId = centerId,
            UserId = student2Id,
            Role = nameof(UserRole.Student)
        };
        await using var s2Context = CreateContext(database.ConnectionString, student2Tenant);
        var s2Guard = new OrganizationOwnershipGuard(s2Context, student2Tenant);
        var s2FeedbackUseCase = new GetAttemptFeedbackUseCase(s2Context, student2Tenant, s2Guard);

        var s2Result = await s2FeedbackUseCase.ExecuteAsync(200UL, CancellationToken.None);
        Assert.False(s2Result.IsSuccess);
        Assert.Equal("FORBIDDEN_RESOURCE", s2Result.ErrorCode);

        // 3. Teacher assigned to student 1's class can read feedback
        var teacherTenant = new TestTenantContext
        {
            CenterId = centerId,
            UserId = teacherId,
            Role = nameof(UserRole.Teacher)
        };
        await using var tContext = CreateContext(database.ConnectionString, teacherTenant);
        var tGuard = new OrganizationOwnershipGuard(tContext, teacherTenant);
        var tFeedbackUseCase = new GetAttemptFeedbackUseCase(tContext, teacherTenant, tGuard);

        var tResult = await tFeedbackUseCase.ExecuteAsync(200UL, CancellationToken.None);
        Assert.True(tResult.IsSuccess);
        Assert.NotNull(tResult.Data);

        // 4. CenterManager can read feedback
        var mgrTenant = new TestTenantContext
        {
            CenterId = centerId,
            UserId = Guid.NewGuid(),
            Role = nameof(UserRole.CenterManager)
        };
        await using var mgrContext = CreateContext(database.ConnectionString, mgrTenant);
        var mgrGuard = new OrganizationOwnershipGuard(mgrContext, mgrTenant);
        var mgrFeedbackUseCase = new GetAttemptFeedbackUseCase(mgrContext, mgrTenant, mgrGuard);

        var mgrResult = await mgrFeedbackUseCase.ExecuteAsync(200UL, CancellationToken.None);
        Assert.True(mgrResult.IsSuccess);
        Assert.NotNull(mgrResult.Data);
    }

    private static async Task SeedHierarchyAsync(
        string connectionString,
        Guid centerId,
        Guid teacherId,
        IReadOnlyList<Guid> studentIds,
        Guid subjectId,
        Guid classId,
        IReadOnlyList<Guid>? extraTeacherIds = null,
        ulong baseNodeId = 1)
    {
        var tenant = new TestTenantContext { CenterId = centerId };
        await using var context = CreateContext(connectionString, tenant);
        await context.Database.OpenConnectionAsync();
        try
        {
            await context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 0;");

            context.Centers.Add(new Center
            {
                CenterId = centerId,
                CenterCode = $"C-{centerId:N}"[..10],
                CenterName = "Dashboard Test Center",
                Status = CenterStatus.Active,
                Timezone = "UTC",
                CreatedAt = UtcNow,
                UpdatedAt = UtcNow
            });

            context.Users.Add(new User
            {
                UserId = teacherId,
                CenterId = centerId,
                Username = $"teacher-{teacherId:N}"[..20],
                PasswordHash = "hash",
                RoleName = UserRole.Teacher,
                DisplayName = "Teacher",
                Status = UserStatus.Active,
                AuthVersion = 1,
                CreatedAt = UtcNow,
                UpdatedAt = UtcNow,
                RowVersion = 1
            });

            context.Teachers.Add(new Teacher
            {
                TeacherId = teacherId,
                CenterId = centerId,
                Department = "Mathematics",
                CreatedAt = UtcNow,
                UpdatedAt = UtcNow
            });

            if (extraTeacherIds != null)
            {
                foreach (var extraTeacherId in extraTeacherIds)
                {
                    context.Users.Add(new User
                    {
                        UserId = extraTeacherId,
                        CenterId = centerId,
                        Username = $"teacher-{extraTeacherId:N}"[..20],
                        PasswordHash = "hash",
                        RoleName = UserRole.Teacher,
                        DisplayName = "Teacher Extra",
                        Status = UserStatus.Active,
                        AuthVersion = 1,
                        CreatedAt = UtcNow,
                        UpdatedAt = UtcNow,
                        RowVersion = 1
                    });

                    context.Teachers.Add(new Teacher
                    {
                        TeacherId = extraTeacherId,
                        CenterId = centerId,
                        Department = "Mathematics",
                        CreatedAt = UtcNow,
                        UpdatedAt = UtcNow
                    });
                }
            }

            context.Subjects.Add(new Subject
            {
                SubjectId = subjectId,
                CenterId = centerId,
                SubjectCode = $"SUB-{subjectId:N}"[..10],
                SubjectName = "Mathematics",
                IsActive = true,
                CreatedAt = UtcNow,
                UpdatedAt = UtcNow
            });

            context.Classes.Add(new Class
            {
                ClassId = classId,
                CenterId = centerId,
                SubjectId = subjectId,
                TeacherId = teacherId,
                ClassName = "Class 12A",
                AcademicYear = "2026",
                Status = ClassStatus.Active,
                CreatedAt = UtcNow,
                UpdatedAt = UtcNow
            });

            foreach (var studentId in studentIds)
            {
                context.Users.Add(new User
                {
                    UserId = studentId,
                    CenterId = centerId,
                    Username = $"student-{studentId:N}"[..20],
                    PasswordHash = "hash",
                    RoleName = UserRole.Student,
                    DisplayName = $"Student {studentId:N}"[..15],
                    Status = UserStatus.Active,
                    AuthVersion = 1,
                    CreatedAt = UtcNow,
                    UpdatedAt = UtcNow,
                    RowVersion = 1
                });

                context.Students.Add(new Student
                {
                    StudentId = studentId,
                    CenterId = centerId,
                    FullName = $"Student {studentId:N}"[..15],
                    GradeLevel = 12,
                    CreatedAt = UtcNow,
                    UpdatedAt = UtcNow
                });

                context.ClassStudents.Add(new ClassStudent
                {
                    CenterId = centerId,
                    ClassId = classId,
                    StudentId = studentId,
                    Status = ClassStudentStatus.Active,
                    JoinedAt = UtcNow
                });
            }

            // Topic 1: importance 60, Topic 2: importance 40
            context.KnowledgeNodes.AddRange(
                new KnowledgeNode
                {
                    CenterId = centerId,
                    SubjectId = subjectId,
                    NodeId = baseNodeId,
                    NodeCode = $"TOPIC-{baseNodeId}",
                    NodeName = "Topic One",
                    NodeType = NodeType.Topic,
                    OrderIndex = 1,
                    ExamImportance = 60m,
                    EstimatedLearningMinutes = 60,
                    IsActive = true,
                    CreatedAt = UtcNow,
                    UpdatedAt = UtcNow
                },
                new KnowledgeNode
                {
                    CenterId = centerId,
                    SubjectId = subjectId,
                    NodeId = baseNodeId + 1,
                    NodeCode = $"TOPIC-{baseNodeId + 1}",
                    NodeName = "Topic Two",
                    NodeType = NodeType.Topic,
                    OrderIndex = 2,
                    ExamImportance = 40m,
                    EstimatedLearningMinutes = 60,
                    IsActive = true,
                    CreatedAt = UtcNow,
                    UpdatedAt = UtcNow
                }
            );

            await context.SaveChangesAsync();
        }
        finally
        {
            await context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 1;");
        }
    }

    private static async Task ApplyPublishedCurriculumFixtureAsync(EduTwinDbContext db, Guid center, Guid teacher, Guid subject, Guid classId)
    {
        // The SQL trigger requires actual matching class/curriculum grades and snapshots.
        // Seed only this isolated test class; do not relax production guards or use legacy links.
        var cls = await db.Classes.SingleAsync(c => c.CenterId == center && c.ClassId == classId);
        cls.GradeLevel = 12;
        await db.SaveChangesAsync();
        var curriculum = new Curriculum { CurriculumId=Guid.NewGuid(), CenterId=center, SubjectId=subject, TeacherId=teacher,
            Title="Applied test curriculum", GradeLevel=12, ReviewStatus=ReviewStatus.Published, CreatedAt=UtcNow, UpdatedAt=UtcNow };
        db.Curriculums.Add(curriculum);
        var ids = await db.KnowledgeNodes.Where(n => n.CenterId == center && n.SubjectId == subject).OrderBy(n => n.NodeId).Select(n => n.NodeId).ToListAsync();
        for(var i=0;i<ids.Count;i++) db.CurriculumNodes.Add(new() { CenterId=center, CurriculumId=curriculum.CurriculumId,
            NodeId=ids[i], OrderIndex=(uint)i+1, CreatedAt=UtcNow });
        db.ClassCurriculumApplications.Add(new() { ApplicationId=Guid.NewGuid(), CenterId=center, ClassId=classId,
            CurriculumId=curriculum.CurriculumId, SubjectId=subject, AssignedBy=teacher, StartedAt=UtcNow,
            ClassGradeAtStart=12, CurriculumGradeAtStart=12 });
        await db.SaveChangesAsync();
    }

    private static EduTwinDbContext CreateContext(string connectionString, ITenantIdAccessor tenant)
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseMySQL(connectionString);
        return new EduTwinDbContext(options.Options, tenant);
    }

    private sealed class MySqlIntegrationFactAttribute : FactAttribute
    {
        public MySqlIntegrationFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(AdminConnectionVariable)))
            {
                Skip = $"Set {AdminConnectionVariable} to run MySQL integration tests.";
            }
        }
    }

    private sealed class MySqlTestDatabase : IAsyncDisposable
    {
        private readonly string _adminConnectionString;
        private readonly string _databaseName;

        private MySqlTestDatabase(string adminConnectionString, string databaseName, string connectionString)
        {
            _adminConnectionString = adminConnectionString;
            _databaseName = databaseName;
            ConnectionString = connectionString;
        }

        public string ConnectionString { get; }

        public static async Task<MySqlTestDatabase> CreateAsync(string? targetMigration = null)
        {
            var configuredConnection = Environment.GetEnvironmentVariable(AdminConnectionVariable)
                ?? throw new InvalidOperationException($"{AdminConnectionVariable} is required.");
            var adminBuilder = new MySqlConnectionStringBuilder(configuredConnection)
            {
                Database = string.Empty,
                Pooling = false
            };
            var databaseName = $"edutwin_dash_{Guid.NewGuid():N}";
            await using (var connection = new MySqlConnection(adminBuilder.ConnectionString))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = $"CREATE DATABASE `{databaseName}` CHARACTER SET utf8mb4;";
                await command.ExecuteNonQueryAsync();
            }

            var databaseBuilder = new MySqlConnectionStringBuilder(adminBuilder.ConnectionString)
            {
                Database = databaseName,
                Pooling = false
            };
            var database = new MySqlTestDatabase(
                adminBuilder.ConnectionString,
                databaseName,
                databaseBuilder.ConnectionString);
            try
            {
                var tenant = new TestTenantContext();
                await using var context = CreateContext(database.ConnectionString, tenant);
                await context.GetService<IMigrator>().MigrateAsync(targetMigration);
                return database;
            }
            catch
            {
                await database.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var connection = new MySqlConnection(_adminConnectionString);
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = $"DROP DATABASE IF EXISTS `{_databaseName}`;";
                await command.ExecuteNonQueryAsync();
            }
            catch
            {
                // Best-effort cleanup
            }
        }
    }
}
