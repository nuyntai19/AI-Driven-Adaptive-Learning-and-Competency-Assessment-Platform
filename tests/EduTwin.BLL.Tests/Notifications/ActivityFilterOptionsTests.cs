using EduTwin.API.Controllers;
using EduTwin.API.Security;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Notifications;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Notifications;
using EduTwin.DAL.IdentityAndTenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTwin.BLL.Tests.Notifications;

public sealed class ActivityFilterOptionsTests
{
    [Fact]
    public void FilterOptions_RequiresTheSameReadPolicyAsTheJournal()
    {
        var method = typeof(ActivityController).GetMethod(nameof(ActivityController.FilterOptions))!;
        Assert.Equal(CompositePermissionPolicies.ActivityRead, method.GetCustomAttributes(typeof(AuthorizeAttribute), false).Cast<AuthorizeAttribute>().Single().Policy);
        Assert.Equal("filter-options", method.GetCustomAttributes(typeof(HttpGetAttribute), false).Cast<HttpGetAttribute>().Single().Template);
    }

    [Fact]
    public async Task FilterOptions_UsesRoleClassTenantAndDeliveryScope_NotJustTheFirstPage()
    {
        await using var f = await NotificationManualTests.Fixture.Create();
        var teacher = f.User(UserRole.Teacher); var manager = f.User(UserRole.CenterManager); var student = f.User(UserRole.Student);
        var now = DateTime.UtcNow; var assignment = Guid.NewGuid(); var otherAssignment = Guid.NewGuid(); var otherClass = Guid.NewGuid();
        await using (var db = f.Context(teacher))
        {
            db.Classes.Add(new() { CenterId = teacher.CenterId, ClassId = otherClass, TeacherId = Guid.NewGuid(), SubjectId = Guid.NewGuid(),
                ClassName = "Another teacher's class", AcademicYear = "2026", CreatedAt = now, UpdatedAt = now });
            foreach (var (id, classId) in new[] { (assignment, f.ClassId), (otherAssignment, otherClass) })
                db.Assignments.Add(new() { CenterId = teacher.CenterId, AssignmentId = id, ClassId = classId,
                    Title = "PRIVATE assignment title", CreatedByTeacherId = teacher.UserId, CreatedAt = now, UpdatedAt = now });
            for (var i = 0; i < 25; i++)
                db.AuthorizationAuditLogs.Add(Audit("ClassUpdated", "Class", f.ClassId.ToString(), manager, now.AddMinutes(i)));
            db.AuthorizationAuditLogs.Add(Audit("TeacherPasswordReset", "Teacher", teacher.UserId.ToString(), manager, now.AddDays(-1)));
            for (var i = 0; i < 2; i++)
                db.AuthorizationAuditLogs.Add(Audit("AssignmentResultApproved", "StudentAssignmentResult", $"{assignment}:{student.UserId}", teacher, now));
            db.AuthorizationAuditLogs.Add(Audit("ExamResultsPublished", "Assignment", otherAssignment.ToString(), teacher, now));
            db.Attempts.Add(new() { CenterId = teacher.CenterId, AttemptId = 201, StudentId = student.UserId, AssignmentId = assignment,
                FinalAnswer = "PRIVATE answer", ReasoningLanguage = "vi", CreatedAt = now, UpdatedAt = now });
            var created = Guid.NewGuid(); var published = Guid.NewGuid();
            foreach (var (id, action) in new[] { (created, "AssignmentCreated"), (published, "AssignmentPublished") })
                db.NotificationEvents.Add(new() { CenterId = teacher.CenterId, EventId = id, EventType = action,
                    DeduplicationKey = id.ToString(), Title = "PRIVATE title", Body = "PRIVATE body", RecipientUserIds = "[]",
                    ResourceContext = "{\"Kind\":\"Administrative\"}", CreatedAt = now });
            db.NotificationDeliveries.Add(new() { CenterId = teacher.CenterId, EventId = published, UserId = teacher.UserId,
                DeliveredAt = now, DeliveryId = 1 });
            await db.SaveChangesAsync();
        }
        await using (var foreignDb = f.Context(f.ForeignTeacher))
        {
            foreignDb.AuthorizationAuditLogs.Add(Audit("CenterCreated", "Center", f.ForeignTeacher.CenterId.ToString(), f.ForeignTeacher, now));
            await foreignDb.SaveChangesAsync();
        }
        await using var td = f.Context(teacher);
        var teacherOptions = (await new ActivityService(td, f.Tenant(teacher)).FilterOptionsAsync(default)).Data!;
        Assert.Equal(new HashSet<ActivityFilterOption> {
            new("Audit", "AssignmentResultApproved"), new("Assessment", "AttemptSubmitted"), new("Business", "AssignmentPublished") }, teacherOptions.ToHashSet());
        Assert.DoesNotContain("PRIVATE", System.Text.Json.JsonSerializer.Serialize(teacherOptions));
        await using var md = f.Context(manager);
        var service = new ActivityService(md, f.Tenant(manager));
        var firstPage = (await service.ListAsync(new(), default)).Data!;
        Assert.DoesNotContain(firstPage.Items, item => item.Action == "TeacherPasswordReset");
        var managerOptions = (await service.FilterOptionsAsync(default)).Data!;
        Assert.Equal(new HashSet<ActivityFilterOption> {
            new("Audit", "ClassUpdated"), new("Audit", "TeacherPasswordReset"), new("Business", "AssignmentCreated") }, managerOptions.ToHashSet());
        foreach (var option in teacherOptions)
            Assert.NotEmpty((await new ActivityService(td, f.Tenant(teacher)).ListAsync(new() { Source = option.Source, Action = option.Action }, default)).Data!.Items);
        foreach (var option in managerOptions)
            Assert.NotEmpty((await service.ListAsync(new() { Source = option.Source, Action = option.Action }, default)).Data!.Items);
        Assert.Null((await md.NotificationDeliveries.SingleAsync()).ReadAt);
    }

    [Fact]
    public async Task FilterOptions_EmptyHistoryAndUnresolvedOrMismatchedIdentityFailClosed()
    {
        await using var f = await NotificationManualTests.Fixture.Create(); var manager = f.User(UserRole.CenterManager);
        await using var db = f.Context(manager);
        Assert.Empty((await new ActivityService(db, f.Tenant(manager)).FilterOptionsAsync(default)).Data!);
        Assert.Equal(ErrorCodes.ResourceNotFound, (await new ActivityService(db, new TenantContext()).FilterOptionsAsync(default)).ErrorCode);
        Assert.Equal(ErrorCodes.ResourceNotFound, (await new ActivityService(db, f.Tenant(f.ForeignTeacher)).FilterOptionsAsync(default)).ErrorCode);
    }

    private static AuthorizationAuditLog Audit(string action, string type, string id, User actor, DateTime at) => new() {
        CenterId = actor.CenterId, ActorUserId = actor.UserId, TargetType = type, TargetId = id, ActionType = action,
        CreatedAt = at, TraceId = Guid.NewGuid().ToString(), Reason = "PRIVATE reason", BeforeData = "{\"PRIVATE\":true}" };
}
