using EduTwin.BLL.Assignments;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Organization;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.Organization;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.Assignments;
using EduTwin.DAL.Organization;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.Organization;

public class ClassReportsTests
{
    [Theory]
    [InlineData(null,"Processing","NotStarted","Unknown")]
    [InlineData(10,"Provisional","Completed","PendingGrading")]
    [InlineData(0,"Final","Completed","HighRisk")]
    [InlineData(7,"Final","Completed","Developing")]
    [InlineData(8,"Final","Completed","Good")]
    public void Summary_OnlyFinalGradesContribute_NotCompletion(int? score, string result, string progress, string expected)
    {
        var summary = new StudentAcademicSummaryDto { Records = new() {
            new() { Score=score, ResultStatus=result, Status=progress } } };
        ClassReportsUseCase.Summarize(summary);
        Assert.Equal(expected, summary.AssessmentStatus);
        Assert.Equal(result=="Final" ? score : null, summary.AverageScore);
        if (progress=="Completed") Assert.Equal(100, summary.CompletionRate);
    }

    [Fact]
    public void NoAssignments_IsUnknown_NotHighRisk()
    {
        var summary = new StudentAcademicSummaryDto();
        ClassReportsUseCase.Summarize(summary);
        Assert.Equal("Unknown",summary.AssessmentStatus); Assert.Null(summary.AverageScore);
    }

    [Fact]
    public async Task Snapshot_IsCompleteBeyondOldCaps_RespectsTargets_AndPreservesErrors()
    {
        var center = Guid.NewGuid(); var actor = Guid.NewGuid(); var cls = Guid.NewGuid(); var subject = Guid.NewGuid();
        var tenant = new Mock<ITenantContext>(); tenant.SetupGet(t=>t.CenterId).Returns(center);
        var accessor = new Mock<ITenantIdAccessor>(); accessor.SetupGet(t=>t.CenterId).Returns(center);
        await using var db = new EduTwinDbContext(new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, accessor.Object);
        var ids = Enumerable.Range(1,101).Select(_=>Guid.NewGuid()).ToArray();
        foreach(var (id,index) in ids.Select((id,index)=>(id,index))) {
            db.Users.Add(new User {UserId=id,CenterId=center,Username=$"u{index}",DisplayName=$"S{index}",PasswordHash="H",RoleName=UserRole.Student});
            db.Students.Add(new Student {StudentId=id,CenterId=center,FullName=$"S{index}",GradeLevel=10});
            db.ClassStudents.Add(new ClassStudent {CenterId=center,ClassId=cls,StudentId=id,Status=ClassStudentStatus.Active});
        }
        for(var index=0;index<56;index++) {
            var id=Guid.NewGuid();
            db.Assignments.Add(new Assignment {CenterId=center,ClassId=cls,AssignmentId=id,Title=$"A{index}",
                Status=index==55 ? AssignmentStatus.Draft : AssignmentStatus.Published});
            db.AssignmentTargets.Add(new AssignmentTarget {CenterId=center,AssignmentId=id,StudentId=ids[100]});
        }
        foreach (var entry in db.ChangeTracker.Entries())
            foreach (var property in entry.Properties.Where(p=>p.Metadata.ClrType==typeof(DateTime)))
                property.CurrentValue=DateTime.UtcNow;
        await db.SaveChangesAsync();
        var access = new Mock<IGetClassUseCase>();
        access.Setup(x=>x.ExecuteAsync(cls,It.IsAny<CancellationToken>())).ReturnsAsync(GetClassResult.Success(new ClassDto {
            ClassId=cls.ToString(),ClassName="Class",AcademicYear="2026",Status="Active",RowVersion="1",
            Subject=new(){SubjectId=subject.ToString(),SubjectName="Math"},Teacher=new(){TeacherId=actor.ToString(),DisplayName="Teacher"} }));
        var service = new ClassReportsUseCase(db,tenant.Object,access.Object,new AssignmentResultCalculator(db),TimeProvider.System);
        var report = await service.AcademicAsync(cls,default);
        Assert.NotNull(report); Assert.Equal(101,report.Students.Count); Assert.Equal(55,report.TotalAssignments);
        Assert.Equal(55,report.Students.Single(r=>r.Student.StudentId==ids[100]).Summary.TotalAssigned);
        Assert.Equal(0,report.Students.Single(r=>r.Student.StudentId==ids[0]).Summary.TotalAssigned);
        Assert.All(report.Students,r=>Assert.Equal("Unknown",r.Summary.AssessmentStatus));
        access.Setup(x=>x.ExecuteAsync(cls,It.IsAny<CancellationToken>())).ReturnsAsync(GetClassResult.Failure(ErrorCodes.ForbiddenResource));
        Assert.Null(await service.AcademicAsync(cls,default));
        Assert.Null(await service.HistoryAsync(cls,1,default));
    }
}
