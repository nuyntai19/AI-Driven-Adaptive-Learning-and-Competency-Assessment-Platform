using EduTwin.BLL.Dashboards;
using EduTwin.BLL.Assignments;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Assignments;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTwin.BLL.Tests.Dashboards;

public sealed class StudentWorkspaceSummaryTests
{
    private static readonly DateTime SeedTime = new(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
    private sealed class Tenant : ITenantContext, ITenantIdAccessor
    {
        public Guid? CenterId { get; set; } = Guid.NewGuid();
        public Guid? UserId { get; set; } = Guid.NewGuid();
        public string? Role { get; set; } = nameof(UserRole.Student);
        public uint? AuthVersion => 1;
        public bool IsResolved => CenterId.HasValue;
    }
    private sealed class Clock(DateTimeOffset now) : TimeProvider { public override DateTimeOffset GetUtcNow() => now; }
    private static async Task<(EduTwinDbContext Db, Tenant Tenant)> Setup(string zone = "Asia/Ho_Chi_Minh")
    {
        var tenant = new Tenant();
        var db = new EduTwinDbContext(new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, tenant);
        db.Centers.Add(new Center { CenterId = tenant.CenterId!.Value, CenterName = "Test", CenterCode = "TEST", Timezone = zone, Status = CenterStatus.Active, CreatedAt = SeedTime, UpdatedAt = SeedTime });
        db.Users.Add(new User { UserId = tenant.UserId!.Value, CenterId = tenant.CenterId.Value, Username = "student", DisplayName = "Student", PasswordHash = "hash", RoleName = UserRole.Student, CreatedAt = SeedTime, UpdatedAt = SeedTime });
        db.Students.Add(new Student { StudentId = tenant.UserId.Value, CenterId = tenant.CenterId.Value, FullName = "Student", CreatedAt = SeedTime, UpdatedAt = SeedTime });
        await db.SaveChangesAsync();
        return (db, tenant);
    }
    private static void Attempt(EduTwinDbContext db, Tenant tenant, string utc, bool skipped = false, Guid? student = null)
    {
        var date = DateTime.Parse(utc, null, System.Globalization.DateTimeStyles.AdjustToUniversal | System.Globalization.DateTimeStyles.AssumeUniversal);
        db.Attempts.Add(new Attempt { CenterId = tenant.CenterId!.Value, StudentId = student ?? tenant.UserId!.Value,
            FinalAnswer = "answer", ReasoningLanguage = "vi", CreatedAt = date, UpdatedAt = date, Skipped = skipped,
            ClientSubmissionId = Guid.NewGuid() });
    }
    private static GetStudentWorkspaceSummaryUseCase Sut(EduTwinDbContext db, Tenant tenant, string now = "2026-10-08T01:00:00Z") =>
        new(db, tenant, new Clock(DateTimeOffset.Parse(now)));

    [Fact]
    public async Task EmptyHistory_ReturnsActualZero_NotDemoValues()
    {
        var (db, tenant) = await Setup(); await using var scope = db;
        var data = await Sut(db, tenant).ExecuteAsync(null);
        Assert.NotNull(data); Assert.Equal(0, data.AssignmentCount); Assert.Equal(0, data.DailyStreak); Assert.False(data.StudiedToday);
    }
    [Fact]
    public async Task Midnight_Duplicates_Skips_Future_AndOtherStudent_AreHandled()
    {
        var (db, tenant) = await Setup(); await using var scope = db;
        Attempt(db, tenant, "2026-10-07T17:00:00Z"); // today 00:00
        Attempt(db, tenant, "2026-10-07T18:00:00Z");
        Attempt(db, tenant, "2026-10-07T16:59:59Z"); // yesterday
        Attempt(db, tenant, "2026-10-06T16:00:00Z", skipped: true);
        Attempt(db, tenant, "2026-10-06T16:00:00Z", student: Guid.NewGuid());
        Attempt(db, tenant, "2026-10-08T17:00:00Z"); // future
        await db.SaveChangesAsync();
        var data = await Sut(db, tenant).ExecuteAsync(null);
        Assert.Equal(2, data!.DailyStreak); Assert.True(data.StudiedToday); Assert.Equal("2026-10-08", data.LocalDate);
    }
    [Fact]
    public async Task YesterdayStreak_IsRetainedUntilTodayEnds_ThenExpires()
    {
        var (db, tenant) = await Setup(); await using var scope = db;
        Attempt(db, tenant, "2026-10-07T00:00:00Z"); Attempt(db, tenant, "2026-10-06T00:00:00Z");
        await db.SaveChangesAsync();
        var today = await Sut(db, tenant).ExecuteAsync(null);
        Assert.Equal(2, today!.DailyStreak); Assert.False(today.StudiedToday);
        Assert.Equal(0, (await Sut(db, tenant, "2026-10-08T17:00:00Z").ExecuteAsync(null))!.DailyStreak);
    }
    [Fact]
    public async Task LongStreak_IsNotTruncatedAtOneWindow()
    {
        var (db, tenant) = await Setup(); await using var scope = db;
        var todayUtc = DateTime.Parse("2026-10-08T00:00:00Z", null, System.Globalization.DateTimeStyles.AdjustToUniversal);
        for (var i = 0; i < 70; i++) Attempt(db, tenant, todayUtc.AddDays(-i).ToString("O"));
        await db.SaveChangesAsync();
        Assert.Equal(70, (await Sut(db, tenant).ExecuteAsync(null))!.DailyStreak);
    }
    [Fact]
    public async Task DaylightSaving_UsesHistoricalLocalDayBounds()
    {
        var (db, tenant) = await Setup("America/New_York"); await using var scope = db;
        Attempt(db, tenant, "2026-03-09T04:20:00Z"); // March 9 00:20 EDT
        Attempt(db, tenant, "2026-03-08T06:50:00Z"); // March 8 01:50 EST
        Attempt(db, tenant, "2026-03-08T04:30:00Z"); // March 7 23:30 EST, not March 8
        await db.SaveChangesAsync();
        Assert.Equal(3, (await Sut(db, tenant, "2026-03-09T04:30:00Z").ExecuteAsync(null))!.DailyStreak);
    }
    [Fact]
    public async Task AssignmentCount_MatchesVisibleList_AndSubject_NotPageLength()
    {
        var (db, tenant) = await Setup(); await using var scope = db;
        var subjectA = Guid.NewGuid(); var subjectB = Guid.NewGuid();
        ulong progressId = 1;
        foreach (var subject in new[] { subjectA, subjectB })
        {
            var cls = new Class { ClassId = Guid.NewGuid(), CenterId = tenant.CenterId!.Value, SubjectId = subject, ClassName = "Test", AcademicYear = "2026-2027", CreatedAt = SeedTime, UpdatedAt = SeedTime };
            db.Classes.Add(cls);
            foreach (var status in new[] { AssignmentStatus.Draft, AssignmentStatus.Published, AssignmentStatus.Closed })
            {
                var assignment = new Assignment { AssignmentId = Guid.NewGuid(), CenterId = tenant.CenterId.Value, ClassId = cls.ClassId, Title = "Test", Status = status, CreatedAt = SeedTime, UpdatedAt = SeedTime };
                db.Assignments.Add(assignment);
                db.StudentAssignmentProgresses.Add(new StudentAssignmentProgress { ProgressId = progressId++, CenterId = tenant.CenterId.Value, StudentId = tenant.UserId!.Value,
                    AssignmentId = assignment.AssignmentId, Assignment = assignment, Status = ProgressStatus.Completed, CreatedAt = SeedTime, UpdatedAt = SeedTime });
            }
        }
        await db.SaveChangesAsync();
        Assert.Equal(4, (await Sut(db, tenant).ExecuteAsync(null))!.AssignmentCount);
        Assert.Equal(2, (await Sut(db, tenant).ExecuteAsync(subjectA))!.AssignmentCount);
        Assert.Equal(0, (await Sut(db, tenant).ExecuteAsync(Guid.NewGuid()))!.AssignmentCount);
    }
    [Fact]
    public async Task ClassHistory_ListAndCounter_ExcludeCurrentClasses_AndPreserveFormerTargets()
    {
        var (db, tenant)=await Setup();await using var scope=db;
        var center=tenant.CenterId!.Value;var student=tenant.UserId!.Value;var subject=Guid.NewGuid();
        ulong progressId=1;var currentClass=Guid.NewGuid();var archivedClass=Guid.NewGuid();var leftClass=Guid.NewGuid();
        foreach(var (id,status,member) in new[]{(currentClass,ClassStatus.Active,ClassStudentStatus.Active),
            (archivedClass,ClassStatus.Archived,ClassStudentStatus.Active),(leftClass,ClassStatus.Active,ClassStudentStatus.Removed)})
        {
            db.Classes.Add(new Class{CenterId=center,ClassId=id,SubjectId=subject,ClassName=id.ToString(),AcademicYear="2026-2027",
                Status=status,LearningScope=status==ClassStatus.Active?ClassLearningScope.Current:ClassLearningScope.History,CreatedAt=SeedTime,UpdatedAt=SeedTime});
            db.ClassStudents.Add(new ClassStudent{CenterId=center,ClassId=id,StudentId=student,Status=member,
                JoinedAt=SeedTime,RemovedAt=member==ClassStudentStatus.Removed?SeedTime.AddDays(1):null});
            var assignment=new Assignment{CenterId=center,AssignmentId=Guid.NewGuid(),ClassId=id,Title="Test",Status=AssignmentStatus.Published,CreatedAt=SeedTime,UpdatedAt=SeedTime};
            db.Assignments.Add(assignment);
            db.StudentAssignmentProgresses.Add(new StudentAssignmentProgress{ProgressId=progressId++,CenterId=center,StudentId=student,
                AssignmentId=assignment.AssignmentId,Assignment=assignment,Status=ProgressStatus.Completed,CreatedAt=SeedTime,UpdatedAt=SeedTime});
        }
        await db.SaveChangesAsync();
        var list=new ListStudentAssignmentsUseCase(db,tenant,TimeProvider.System,new AssignmentResultCalculator(db));
        foreach(var (history,count) in new[]{(false,1),(true,2)})
        {
            var response=await list.ExecuteAsync(new(){SubjectId=subject,History=history},default);
            Assert.True(response.IsSuccess);Assert.Equal(count,response.Data!.Meta.TotalItems);Assert.Equal(count,response.Data.Data.Count);
            Assert.Equal(count,(await Sut(db,tenant).ExecuteAsync(subject,null,history))!.AssignmentCount);
        }
        Assert.False((await list.ExecuteAsync(new(){SubjectId=subject,ClassId=currentClass,History=true},default)).IsSuccess);
        Assert.Null(await Sut(db,tenant).ExecuteAsync(subject,currentClass,true));
        Assert.False((await list.ExecuteAsync(new(){SubjectId=subject,ClassId=leftClass,History=false},default)).IsSuccess);
        Assert.Equal(1,(await Sut(db,tenant).ExecuteAsync(subject,leftClass,true))!.AssignmentCount);
        Assert.Equal(ClassStatus.Active,(await db.Classes.SingleAsync(c=>c.ClassId==leftClass)).Status);
        db.ClassStudents.RemoveRange(await db.ClassStudents.Where(m=>m.ClassId!=currentClass).ToListAsync());
        await db.SaveChangesAsync();
        var empty=await list.ExecuteAsync(new(){SubjectId=subject,History=true},default);
        Assert.True(empty.IsSuccess);Assert.Empty(empty.Data!.Data);
        Assert.Equal(0,(await Sut(db,tenant).ExecuteAsync(subject,null,true))!.AssignmentCount);
        Assert.Equal(3,await db.StudentAssignmentProgresses.CountAsync()); // Viewing history does not remove old submissions/targets.
        Assert.Equal(3,(await Sut(db,tenant).ExecuteAsync(subject))!.AssignmentCount); // Legacy omitted-mode contract unchanged.
    }
    [Fact]
    public async Task WrongActorOrCenter_FailsClosed()
    {
        var (db, tenant) = await Setup(); await using var scope = db;
        tenant.Role = nameof(UserRole.Teacher);
        Assert.Null(await Sut(db, tenant).ExecuteAsync(null));
        tenant.Role = nameof(UserRole.Student); tenant.CenterId = Guid.NewGuid();
        Assert.Null(await Sut(db, tenant).ExecuteAsync(null));
    }
}
