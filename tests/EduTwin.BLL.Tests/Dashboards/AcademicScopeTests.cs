using EduTwin.BLL.Dashboards;
using EduTwin.BLL.CurriculumAndQuestions;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.Organization;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.Dashboards;

public class AcademicScopeTests
{
    private static readonly Guid CenterId = Guid.NewGuid(), StudentId = Guid.NewGuid(), TeacherId = Guid.NewGuid(), SubjectId = Guid.NewGuid();
    private static readonly DateTime Now = new(2026,10,8,12,0,0,DateTimeKind.Utc);
    private static EduTwinDbContext NewDb()
    {
        var tenant = new Mock<ITenantIdAccessor>(); tenant.Setup(t=>t.CenterId).Returns(CenterId);
        return new(new DbContextOptionsBuilder<EduTwinDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w=>w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning)).Options,tenant.Object);
    }
    private static async Task<(Guid Current,Guid History,Guid Curriculum)> Seed(EduTwinDbContext db, bool apply=true)
    {
        db.Students.Add(new Student {CenterId=CenterId,StudentId=StudentId,FullName="Student",GradeLevel=10,CreatedAt=Now,UpdatedAt=Now});
        db.Subjects.Add(new Subject {CenterId=CenterId,SubjectId=SubjectId,SubjectName="Math",SubjectCode="M",IsActive=true,CreatedAt=Now,UpdatedAt=Now});
        var current=Guid.NewGuid(); var history=Guid.NewGuid(); var curriculum=Guid.NewGuid();
        foreach(var (id,scope) in new[]{(current,ClassLearningScope.Current),(history,ClassLearningScope.History)})
        {
            db.Classes.Add(new Class {CenterId=CenterId,ClassId=id,TeacherId=TeacherId,SubjectId=SubjectId,ClassName=scope.ToString(),
                AcademicYear="2026-2027",GradeLevel=10,Status=scope==ClassLearningScope.History?ClassStatus.Archived:ClassStatus.Active,LearningScope=scope,CreatedAt=Now,UpdatedAt=Now});
            db.ClassStudents.Add(new ClassStudent {CenterId=CenterId,ClassId=id,StudentId=StudentId,Status=ClassStudentStatus.Active,JoinedAt=Now.AddDays(-10)});
        }
        db.Curriculums.Add(new Curriculum {CenterId=CenterId,CurriculumId=curriculum,TeacherId=TeacherId,SubjectId=SubjectId,GradeLevel=10,Title="Applied",ReviewStatus=ReviewStatus.Published,CreatedAt=Now,UpdatedAt=Now});
        db.KnowledgeNodes.Add(new KnowledgeNode {CenterId=CenterId,SubjectId=SubjectId,NodeId=1,NodeCode="One",NodeName="Topic one",NodeType=Contracts.KnowledgeGraph.NodeType.Topic,IsActive=true,CreatedAt=Now,UpdatedAt=Now});
        db.CurriculumNodes.Add(new CurriculumNode {CenterId=CenterId,CurriculumId=curriculum,NodeId=1,OrderIndex=1,CreatedAt=Now});
        if(apply) db.ClassCurriculumApplications.Add(new() {ApplicationId=Guid.NewGuid(),CenterId=CenterId,ClassId=current,CurriculumId=curriculum,SubjectId=SubjectId,AssignedBy=TeacherId,StartedAt=Now});
        await db.SaveChangesAsync(); return(current,history,curriculum);
    }
    [Fact]
    public async Task CurrentScope_ExcludesArchivedClass_AndUsesOnlyAppliedCurriculum()
    {
        await using var db=NewDb(); var ids=await Seed(db);
        var scope=await new StudentAcademicScopeReader(db).ReadAsync(CenterId,StudentId,SubjectId,null,false,default);
        Assert.NotNull(scope); Assert.Equal(ids.Current,scope.Context.SelectedClassId); Assert.Equal(new ulong[]{1},scope.TopicIds);
        Assert.True(scope.Context.Classes.Single(c=>c.ClassId==ids.History).IsHistorical);
        Assert.Null(await new StudentAcademicScopeReader(db).ReadAsync(CenterId,StudentId,SubjectId,ids.History,false,default));
        Assert.Null(await new StudentAcademicScopeReader(db).ReadAsync(CenterId,StudentId,SubjectId,Guid.NewGuid(),false,default));
    }
    [Fact]
    public async Task HistoryScope_WithOnlyCurrentClasses_IsEmpty_AndRejectsCurrentClassSelection()
    {
        await using var db=NewDb(); var ids=await Seed(db);
        db.ClassStudents.Remove(await db.ClassStudents.SingleAsync(c=>c.ClassId==ids.History));
        db.Classes.Remove(await db.Classes.SingleAsync(c=>c.ClassId==ids.History));
        await db.SaveChangesAsync();
        var reader=new StudentAcademicScopeReader(db);
        var history=await reader.ReadAsync(CenterId,StudentId,SubjectId,null,true,default);
        Assert.NotNull(history);Assert.Null(history.Context.SelectedClassId);Assert.Empty(history.TopicIds);
        Assert.Empty(history.Context.Curriculums);Assert.Contains("Chưa có lịch sử lớp học",history.Context.Message);
        Assert.False(history.Context.Classes.Single().IsHistorical);
        Assert.Null(await reader.ReadAsync(CenterId,StudentId,SubjectId,ids.Current,true,default));
        var allSubjects=await reader.ReadAsync(CenterId,StudentId,null,null,true,default);
        Assert.NotNull(allSubjects);Assert.Empty(allSubjects.TopicIds);Assert.Empty(allSubjects.Context.Curriculums);
        Assert.Equal(ClassStatus.Active,(await db.Classes.SingleAsync()).Status);
        Assert.Single(await db.ClassCurriculumApplications.ToListAsync());
    }
    [Fact]
    public async Task HistoryScope_SelectsArchivedClassOnly_AndDoesNotIncludeCurrentApplications()
    {
        await using var db=NewDb();var ids=await Seed(db);
        db.ClassCurriculumApplications.Add(new(){ApplicationId=Guid.NewGuid(),CenterId=CenterId,ClassId=ids.History,
            CurriculumId=ids.Curriculum,SubjectId=SubjectId,AssignedBy=TeacherId,StartedAt=Now.AddDays(-5),EndedAt=Now,
            EndedBy=TeacherId,EndReason="Kết thúc lớp"});
        await db.SaveChangesAsync();
        var reader=new StudentAcademicScopeReader(db);
        var history=await reader.ReadAsync(CenterId,StudentId,SubjectId,null,true,default);
        Assert.NotNull(history);Assert.Equal(ids.History,history.Context.SelectedClassId);
        Assert.Equal(ids.History,Assert.Single(history.Context.Curriculums).ClassId);
        Assert.Equal(new ulong[]{1},history.TopicIds);
        var allSubjects=await reader.ReadAsync(CenterId,StudentId,null,null,true,default);
        Assert.Equal(ids.History,Assert.Single(allSubjects!.Context.Curriculums).ClassId);
        Assert.Null(await reader.ReadAsync(CenterId,StudentId,SubjectId,ids.Current,true,default));
        Assert.Null(await reader.ReadAsync(CenterId,Guid.NewGuid(),SubjectId,ids.History,true,default));
        Assert.Equal(2,await db.ClassCurriculumApplications.CountAsync());
    }
    [Fact]
    public async Task RemovedStudent_SeesFormerClassInHistory_EvenWhenClassIsStillActive()
    {
        await using var db=NewDb();var ids=await Seed(db);
        var membership=await db.ClassStudents.SingleAsync(c=>c.ClassId==ids.Current);
        membership.Status=ClassStudentStatus.Removed;membership.RemovedAt=Now.AddDays(1);await db.SaveChangesAsync();
        var reader=new StudentAcademicScopeReader(db);
        var history=await reader.ReadAsync(CenterId,StudentId,SubjectId,ids.Current,true,default);
        Assert.NotNull(history);Assert.Equal(ids.Current,history.Context.SelectedClassId);
        Assert.True(history.Context.Classes.Single(c=>c.ClassId==ids.Current).IsHistorical);
        Assert.Single(history.Context.Curriculums);Assert.Equal(new ulong[]{1},history.TopicIds);
        Assert.Null(await reader.ReadAsync(CenterId,StudentId,SubjectId,ids.Current,false,default));
        Assert.Equal(ClassStatus.Active,(await db.Classes.SingleAsync(c=>c.ClassId==ids.Current)).Status);
        Assert.Equal(ClassLearningScope.Current,(await db.Classes.SingleAsync(c=>c.ClassId==ids.Current)).LearningScope);
    }
    [Fact]
    public async Task MissingApplication_DoesNotFallBackToWholeSubject_AndDraftIsNeverVisible()
    {
        await using var db=NewDb(); await Seed(db,false);
        var scope=await new StudentAcademicScopeReader(db).ReadAsync(CenterId,StudentId,SubjectId,null,false,default);
        Assert.NotNull(scope); Assert.Empty(scope.TopicIds); Assert.NotNull(scope.Context.Message);
        Assert.Empty(scope.Context.Curriculums);
    }
    [Fact]
    public async Task ApplyingCurriculum_RequiresTeacherOwnership_AndKeepsPriorApplicationWhenReplacing()
    {
        await using var db=NewDb(); var ids=await Seed(db);
        var tenant = new Mock<BLL.IdentityAndTenancy.ITenantContext>(); tenant.Setup(t=>t.IsResolved).Returns(true);
        tenant.Setup(t=>t.CenterId).Returns(CenterId);tenant.Setup(t=>t.UserId).Returns(TeacherId);tenant.Setup(t=>t.Role).Returns("Teacher");
        var replacement=Guid.NewGuid();db.Curriculums.Add(new Curriculum {CenterId=CenterId,CurriculumId=replacement,TeacherId=TeacherId,SubjectId=SubjectId,GradeLevel=10,Title="Replacement",ReviewStatus=ReviewStatus.Published,CreatedAt=Now,UpdatedAt=Now});await db.SaveChangesAsync();
        var usecase=new CurriculumApplicationUseCase(db,tenant.Object,TimeProvider.System);
        var blocked=await usecase.ApplyAsync(replacement,new(){ClassIds=[ids.Current],RowVersion="1"},default);Assert.False(blocked.IsSuccess);
        var applied=await usecase.ApplyAsync(replacement,new(){ClassIds=[ids.Current],RowVersion="1",ChangeReason="Chuyển giáo trình mới"},default);Assert.True(applied.IsSuccess,applied.Message);
        Assert.Equal(2,await db.ClassCurriculumApplications.CountAsync());Assert.Single(await db.ClassCurriculumApplications.Where(a=>a.EndedAt==null).ToListAsync());
        Assert.Equal("Chuyển giáo trình mới",(await db.ClassCurriculumApplications.SingleAsync(a=>a.CurriculumId==ids.Curriculum)).EndReason);
        tenant.Setup(t=>t.UserId).Returns(Guid.NewGuid());
        Assert.False((await new CurriculumApplicationUseCase(db,tenant.Object,TimeProvider.System).ApplyAsync(replacement,new(){ClassIds=[ids.Current],RowVersion=applied.RowVersion!},default)).IsSuccess);
    }
}
