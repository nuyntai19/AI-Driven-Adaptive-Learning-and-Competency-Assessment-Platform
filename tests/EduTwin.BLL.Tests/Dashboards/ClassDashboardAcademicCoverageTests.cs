using EduTwin.BLL.Dashboards;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.DigitalTwin;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTwin.BLL.Tests.Dashboards;

public sealed class ClassDashboardAcademicCoverageTests
{
    [Theory]
    [InlineData(false, false, ReviewStatus.Published)]
    [InlineData(true, true, ReviewStatus.Published)]
    [InlineData(true, false, ReviewStatus.Archived)]
    [InlineData(true, false, ReviewStatus.Draft)]
    public async Task NoCurrentPublishedApplicationNeverUsesLegacyLinksOrWholeGraph(bool apply, bool ended, ReviewStatus state)
    {
        var (db, tenant, classId, _) = await Setup([40m], apply, ended, state);
        await using (db)
        {
            var data = (await Read(db, tenant, classId)).Data!;
            Assert.False(data.AcademicCoverage.HasAppliedCurriculum);
            Assert.Equal(0, data.AcademicCoverage.ApplicableTopicCount);
            Assert.Empty(data.GapGroups); Assert.Empty(data.WeakTopics);
        }
    }

    [Fact]
    public async Task CurrentLedgerScopesTopicsAndMissingStudentIsNotWeak()
    {
        var (db, tenant, classId, students) = await Setup([40m, null]);
        await using (db)
        {
            // Outside-curriculum evidence is real but must not enter this class's topic scope.
            db.KnowledgeTwins.Add(new() { KnowledgeTwinId=10, CenterId=tenant.CenterId!.Value, StudentId=students[1], SubjectId=db.Classes.Single().SubjectId,
                TopicNodeId=2, MasteryPercentage=1, EvidenceCount=1, CreatedAt=DateTime.UtcNow, UpdatedAt=DateTime.UtcNow });
            await db.SaveChangesAsync();
            var data = (await Read(db, tenant, classId)).Data!;
            Assert.Equal(1, data.AcademicCoverage.ApplicableTopicCount);
            Assert.Equal(1, data.AcademicCoverage.AssessedTopicCount);
            Assert.Equal(1, data.AcademicCoverage.UnassessedStudentTopicCount);
            Assert.Equal(40m, data.Overview.AverageMastery);
            Assert.Equal(new[] { students[0] }, Assert.Single(data.GapGroups).StudentIds);
            var weak = Assert.Single(data.WeakTopics);
            Assert.Equal("1", weak.TopicNodeId); Assert.Equal(1, weak.AssessedStudentCount); Assert.Equal(1, weak.UnassessedStudentCount);
        }
    }

    [Fact]
    public async Task PlaceholderTwinWithoutEvidenceIsUnassessedEvenWhenItsMasteryIsZero()
    {
        var (db, tenant, classId, _) = await Setup([0m]);
        await using (db)
        {
            db.KnowledgeTwins.Single().EvidenceCount = 0;
            await db.SaveChangesAsync();
            var data = (await Read(db, tenant, classId)).Data!;
            Assert.Equal(0, data.AcademicCoverage.AssessedTopicCount);
            Assert.Equal(1, data.AcademicCoverage.UnassessedStudentTopicCount);
            Assert.Empty(data.GapGroups); Assert.Empty(data.WeakTopics);
        }
    }

    [Fact]
    public async Task WeakIndividualIsNotHiddenByHighAssessedClassAverage()
    {
        var (db, tenant, classId, students) = await Setup([40m, 100m, 100m]);
        await using (db)
        {
            var data = (await Read(db, tenant, classId)).Data!;
            Assert.Equal(80m, data.Overview.AverageMastery);
            Assert.Empty(data.WeakTopics);
            Assert.Equal(new[] { students[0] }, Assert.Single(data.GapGroups).StudentIds);
            Assert.Equal(0, data.AcademicCoverage.UnassessedStudentTopicCount);
        }
    }

    private static Task<ClassDashboardResult> Read(EduTwinDbContext db, Tenant tenant, Guid classId) =>
        new GetClassDashboardUseCase(db, tenant, new OrganizationOwnershipGuard(db, tenant), TimeProvider.System)
            .ExecuteAsync(classId, 70, default);

    private static async Task<(EduTwinDbContext, Tenant, Guid, Guid[])> Setup(decimal?[] mastery, bool apply=true, bool ended=false, ReviewStatus state=ReviewStatus.Published)
    {
        var center=Guid.NewGuid(); var teacher=Guid.NewGuid(); var subject=Guid.NewGuid(); var classId=Guid.NewGuid(); var curriculum=Guid.NewGuid();
        var tenant = new Tenant { CenterId=center, UserId=teacher };
        var db = new EduTwinDbContext(new DbContextOptionsBuilder<EduTwinDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, tenant);
        var now=DateTime.UtcNow;
        db.Centers.Add(new() { CenterId=center, CenterCode="TEST", CenterName="Test", Timezone="Asia/Ho_Chi_Minh", CreatedAt=now, UpdatedAt=now });
        db.Users.Add(new() { UserId=teacher, CenterId=center, Username="teacher", DisplayName="Teacher", PasswordHash="test", RoleName=UserRole.Teacher, Status=UserStatus.Active, CreatedAt=now, UpdatedAt=now });
        db.Teachers.Add(new() { TeacherId=teacher, CenterId=center, Department="Math", CreatedAt=now, UpdatedAt=now });
        db.Subjects.Add(new() { SubjectId=subject, CenterId=center, SubjectCode="MATH", SubjectName="Math", IsActive=true, CreatedAt=now, UpdatedAt=now });
        db.Classes.Add(new() { ClassId=classId, CenterId=center, TeacherId=teacher, SubjectId=subject, ClassName="Class 10", AcademicYear="2026", GradeLevel=10, Status=ClassStatus.Active, LearningScope=ClassLearningScope.Current, CreatedAt=now, UpdatedAt=now });
        db.Curriculums.Add(new() { CurriculumId=curriculum, CenterId=center, TeacherId=teacher, SubjectId=subject, Title="Current curriculum", ReviewStatus=state, CreatedAt=now, UpdatedAt=now });
        for (ulong node=1;node<=2;node++) db.KnowledgeNodes.Add(new() { NodeId=node, CenterId=center, SubjectId=subject, NodeCode=$"TOPIC-{node}", NodeName=$"Topic {node}", NodeType=NodeType.Topic, ExamImportance=1, IsActive=true, CreatedAt=now, UpdatedAt=now });
        db.CurriculumNodes.Add(new() { CenterId=center, CurriculumId=curriculum, NodeId=1, OrderIndex=1, CreatedAt=now });
        // Intentionally stale planning link: it must never resurrect an ended or absent application.
        db.CurriculumClasses.Add(new() { CenterId=center, CurriculumId=curriculum, ClassId=classId, AssignedBy=teacher, AssignedAt=now });
        if (apply) db.ClassCurriculumApplications.Add(new() { ApplicationId=Guid.NewGuid(), CenterId=center, CurriculumId=curriculum, ClassId=classId,
            SubjectId=subject, AssignedBy=teacher, StartedAt=now.AddDays(-1), EndedAt=ended?now:null, EndedBy=ended?teacher:null, EndReason=ended?"Đã kết thúc áp dụng.":null });
        var students=mastery.Select(_=>Guid.NewGuid()).ToArray();
        for(var i=0;i<students.Length;i++) {
            db.Users.Add(new() { UserId=students[i], CenterId=center, Username=$"student{i}", DisplayName=$"Student {i}", PasswordHash="test", RoleName=UserRole.Student, Status=UserStatus.Active, CreatedAt=now, UpdatedAt=now });
            db.Students.Add(new() { StudentId=students[i], CenterId=center, FullName=$"Student {i}", GradeLevel=10, CreatedAt=now, UpdatedAt=now });
            db.ClassStudents.Add(new() { CenterId=center, ClassId=classId, StudentId=students[i], Status=ClassStudentStatus.Active, JoinedAt=now.AddDays(-1) });
            if(mastery[i].HasValue) db.KnowledgeTwins.Add(new() { KnowledgeTwinId=(ulong)i+1, CenterId=center, StudentId=students[i], SubjectId=subject, TopicNodeId=1, MasteryPercentage=mastery[i]!.Value, EvidenceCount=1, CreatedAt=now, UpdatedAt=now });
        }
        await db.SaveChangesAsync(); return (db, tenant, classId, students);
    }

    private sealed class Tenant : ITenantContext, ITenantIdAccessor
    {
        public Guid? CenterId { get; set; }
        public Guid? UserId { get; set; }
        public string? Role => nameof(UserRole.Teacher);
        public uint? AuthVersion => 1;
        public bool IsResolved => true;
    }
}
