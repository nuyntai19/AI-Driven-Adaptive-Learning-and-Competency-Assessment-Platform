using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Notifications;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Notifications;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.Organization;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTwin.BLL.Tests.Notifications;

public sealed partial class NotificationMySqlTests
{
    [NotificationMySqlFact]
    public async Task ActivityFilterOptions_FourActorsAndTenantScope_RealSql()
    {
        await using var f = await Fixture.Create(UserRole.Teacher, UserRole.Student);
        await using var db = f.Context(); var now = DateTime.UtcNow;
        var student = await db.Users.SingleAsync(u => u.RoleName == UserRole.Student);
        var manager = Guid.NewGuid(); var subject = Guid.NewGuid(); var classId = Guid.NewGuid(); var assignment = Guid.NewGuid();
        db.Users.Add(new() { CenterId = f.CenterId, UserId = manager, RoleName = UserRole.CenterManager, Status = UserStatus.Active,
            Username = "isolated_manager", DisplayName = "Isolated manager", PasswordHash = "not-a-password", AuthVersion = 1, CreatedAt = now, UpdatedAt = now });
        db.Teachers.Add(new Teacher { CenterId = f.CenterId, TeacherId = f.UserId, CreatedAt = now, UpdatedAt = now });
        db.Subjects.Add(new Subject { CenterId = f.CenterId, SubjectId = subject, SubjectCode = "FILTERS", SubjectName = "Isolated", IsActive = true, CreatedAt = now, UpdatedAt = now });
        db.Classes.Add(new Class { CenterId = f.CenterId, ClassId = classId, SubjectId = subject, TeacherId = f.UserId,
            ClassName = "Isolated", AcademicYear = "2026", GradeLevel = 10, Status = ClassStatus.Active, LearningScope = ClassLearningScope.Current, CreatedAt = now, UpdatedAt = now });
        db.Assignments.Add(new() { CenterId = f.CenterId, AssignmentId = assignment, ClassId = classId, CreatedByTeacherId = f.UserId,
            Title = "PRIVATE assignment", CreatedAt = now, UpdatedAt = now });
        for (var i = 0; i < 2; i++) db.AuthorizationAuditLogs.Add(new() { CenterId = f.CenterId, ActorUserId = f.UserId, TargetUserId = student.UserId,
            ActionType = "AssignmentResultApproved", TargetType = "StudentAssignmentResult", TargetId = $"{assignment}:{student.UserId}",
            CreatedAt = now, TraceId = Guid.NewGuid().ToString(), Reason = "PRIVATE reason", BeforeData = "{\"PRIVATE\":true}" });
        db.AuthorizationAuditLogs.Add(new() { CenterId = f.CenterId, ActorUserId = manager, ActionType = "ClassUpdated", TargetType = "Class",
            TargetId = classId.ToString(), CreatedAt = now, TraceId = Guid.NewGuid().ToString(), Reason = "Isolated class metadata" });
        await db.SaveChangesAsync();
        Assert.Equal(new ActivityFilterOption("Audit", "AssignmentResultApproved"), Assert.Single((await new ActivityService(db, f.Tenant).FilterOptionsAsync(default)).Data!));
        foreach (var (user, role, expected) in new[] { (manager, "CenterManager", "ClassUpdated"), (student.UserId, "Student", "AssignmentResultApproved") })
        {
            var tenant = new TenantContext(); tenant.Initialize(f.CenterId, user, role, 1);
            await using var scoped = f.Context(tenant);
            var service = new ActivityService(scoped, tenant);
            var option = Assert.Single((await service.FilterOptionsAsync(default)).Data!);
            Assert.Equal(new ActivityFilterOption("Audit", expected), option);
            Assert.NotEmpty((await service.ListAsync(new() { Source = option.Source, Action = option.Action }, default)).Data!.Items);
        }
        foreach (var (center, role) in new[] { (Guid.NewGuid(), "Teacher"), (f.CenterId, "PlatformAdmin") })
        {
            var tenant = new TenantContext(); tenant.Initialize(center, f.UserId, role, 1);
            await using var scoped = f.Context(tenant);
            Assert.Empty((await new ActivityService(scoped, tenant).FilterOptionsAsync(default)).Data!);
        }
    }
}
