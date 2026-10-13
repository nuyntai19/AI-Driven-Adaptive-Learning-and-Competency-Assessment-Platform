using EduTwin.BLL.CurriculumAndQuestions;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.Organization;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.Organization;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.CurriculumAndQuestions;

public class CurriculumApplicationProjectionTests
{
    [Fact]
    public async Task DetailAndList_UseTheSameLiveLedger_PlansAndHistoryAreNotLive_OnlyOwnClassesVisible()
    {
        var center=Guid.NewGuid();var teacher=Guid.NewGuid();var other=Guid.NewGuid();var subject=Guid.NewGuid();var now=DateTime.UtcNow;
        var tenant=new Mock<ITenantContext>();tenant.SetupGet(t=>t.IsResolved).Returns(true);tenant.SetupGet(t=>t.CenterId).Returns(center);
        tenant.SetupGet(t=>t.UserId).Returns(teacher);tenant.SetupGet(t=>t.Role).Returns("Teacher");
        var accessor=new Mock<ITenantIdAccessor>();accessor.SetupGet(t=>t.CenterId).Returns(center);
        await using var db=new EduTwinDbContext(new DbContextOptionsBuilder<EduTwinDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options,accessor.Object);
        db.Centers.Add(new Center{CenterId=center,CenterCode="A",CenterName="A",Timezone="Asia/Bangkok",Status=CenterStatus.Active,CreatedAt=now,UpdatedAt=now});
        foreach(var id in new[]{teacher,other}) {
            db.Users.Add(new User{CenterId=center,UserId=id,Username=id.ToString(),PasswordHash="H",DisplayName="Teacher",RoleName=UserRole.Teacher,Status=UserStatus.Active,CreatedAt=now,UpdatedAt=now});
            db.Teachers.Add(new Teacher{CenterId=center,TeacherId=id,CreatedAt=now,UpdatedAt=now});
        }
        db.Subjects.Add(new Subject{CenterId=center,SubjectId=subject,SubjectCode="MATH",SubjectName="Math",IsActive=true,CreatedAt=now,UpdatedAt=now});
        var live=Guid.NewGuid();var ended=Guid.NewGuid();var archivedClass=Guid.NewGuid();var otherClass=Guid.NewGuid();
        foreach(var id in new[]{live,ended,archivedClass,otherClass}) db.Classes.Add(new Class{CenterId=center,ClassId=id,TeacherId=id==otherClass?other:teacher,
            SubjectId=subject,ClassName=id.ToString(),AcademicYear="2026",GradeLevel=12,Status=id==archivedClass?ClassStatus.Archived:ClassStatus.Active,
            LearningScope=id==archivedClass?ClassLearningScope.History:ClassLearningScope.Current,CreatedAt=now,UpdatedAt=now});
        var published=Guid.NewGuid();var draft=Guid.NewGuid();var archived=Guid.NewGuid();var shared=Guid.NewGuid();
        foreach(var (id,status,owner) in new[]{(published,ReviewStatus.Published,teacher),(draft,ReviewStatus.Draft,teacher),(archived,ReviewStatus.Archived,teacher),(shared,ReviewStatus.Published,other)})
            db.Curriculums.Add(new Curriculum{CenterId=center,CurriculumId=id,TeacherId=owner,SubjectId=subject,GradeLevel=12,Visibility=MaterialVisibility.Shared,
                Title=id.ToString(),ReviewStatus=status,CreatedAt=now,UpdatedAt=now});
        foreach(var (cur,cls) in new[]{(published,ended),(draft,live),(archived,live)})
            db.CurriculumClasses.Add(new CurriculumClass{CenterId=center,CurriculumId=cur,ClassId=cls,AssignedAt=now,AssignedBy=teacher});
        foreach(var (cur,cls,isEnded,role) in new[]{(published,live,false,"Primary"),(published,ended,true,"Primary"),(published,archivedClass,false,"Primary"),(published,otherClass,false,"Primary"),
            (shared,live,false,"Supplemental"),(shared,otherClass,false,"Supplemental")})
            db.ClassCurriculumApplications.Add(new ClassCurriculumApplication{ApplicationId=Guid.NewGuid(),CenterId=center,CurriculumId=cur,ClassId=cls,SubjectId=subject,
                ApplicationRole=role,AssignedBy=cls==otherClass?other:teacher,StartedAt=now,ClassGradeAtStart=12,CurriculumGradeAtStart=12,
                EndedAt=isEnded?now.AddMinutes(1):null,EndedBy=isEnded?teacher:null,EndReason=isEnded?"Ended":null});
        await db.SaveChangesAsync();
        var get=new GetCurriculumUseCase(db,tenant.Object);var list=new ListCurriculumsUseCase(db,tenant.Object);
        var all=await list.ExecuteAsync(new());Assert.True(all.IsSuccess);
        foreach(var id in new[]{published,draft,archived,shared}) {
            var detail=await get.ExecuteAsync(new(){CurriculumId=id});Assert.True(detail.IsSuccess);
            var row=all.Data!.Single(c=>c.CurriculumId==id.ToString());
            Assert.Equal(detail.Data!.ClassIds,row.ClassIds);
            if(id==archived) Assert.Empty(row.ClassIds);else Assert.Equal(new[]{live.ToString()},row.ClassIds);
            Assert.DoesNotContain(otherClass.ToString(),row.ClassIds);
        }
        Assert.Equal(6,await db.ClassCurriculumApplications.CountAsync());
        Assert.Equal(3,await db.CurriculumClasses.CountAsync()); // Read projection never rewrites historical/legacy data.
    }
}
