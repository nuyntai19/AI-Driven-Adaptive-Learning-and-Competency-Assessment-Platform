using System.Text.Json;
using EduTwin.BLL.Notifications;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Notifications;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTwin.BLL.Tests.Notifications;

public sealed class ActivityAndOperationsTests
{
    [Fact]
    public async Task Activity_SubmissionAndAnalysisUsePersistedTimesAndOnlyOwnAcademicScope()
    {
        await using var f = await NotificationManualTests.Fixture.Create();
        var student = f.User(UserRole.Student); var teacher = f.User(UserRole.Teacher);
        var submittedAt = DateTime.UtcNow.AddMinutes(-10); var analysedAt = submittedAt.AddMinutes(1);
        var assignmentId = Guid.NewGuid();
        await using var db = f.Context(student);
        db.Assignments.Add(new() { CenterId = student.CenterId, AssignmentId = assignmentId, ClassId = f.ClassId,
            Title = "Scoped assignment", CreatedByTeacherId = teacher.UserId, CreatedAt = submittedAt, UpdatedAt = submittedAt });
        db.Attempts.Add(new() { CenterId = student.CenterId, AttemptId = 101, StudentId = student.UserId, AssignmentId = assignmentId,
            FinalAnswer = "PRIVATE ANSWER", ReasoningText = "PRIVATE REASONING", ReasoningLanguage = "vi", CreatedAt = submittedAt, UpdatedAt = analysedAt });
        db.Attempts.Add(new() { CenterId = student.CenterId, AttemptId = 102, StudentId = teacher.UserId,
            FinalAnswer = "OTHER PRIVATE ANSWER", ReasoningLanguage = "vi", CreatedAt = submittedAt, UpdatedAt = analysedAt });
        db.StudentAssignmentProgresses.Add(new() { CenterId = student.CenterId, ProgressId = 1, AssignmentId = assignmentId,
            StudentId = student.UserId, CompletedAt = submittedAt, CreatedAt = submittedAt, UpdatedAt = analysedAt });
        db.ReasoningAnalyses.Add(new() { CenterId = student.CenterId, AnalysisId = 1, AttemptId = 101, SchemaVersion = "1.0",
            Feedback = "PRIVATE FEEDBACK", MissingSteps = JsonDocument.Parse("[]"), RootCauseNodeIds = JsonDocument.Parse("[]"), CreatedAt = analysedAt, UpdatedAt = analysedAt.AddMinutes(1) });
        await db.SaveChangesAsync();
        var service = new ActivityService(db, f.Tenant(student));
        var rows = (await service.ListAsync(new() { Source = "Assessment" }, default)).Data!;
        Assert.Equal(3, rows.TotalItems);
        Assert.Equal(analysedAt, rows.Items.Single(r => r.Action == "AIAnalysisCompleted").OccurredAt);
        Assert.Equal(submittedAt, rows.Items.Single(r => r.Action == "AssignmentSubmitted").OccurredAt);
        Assert.Equal(submittedAt, rows.Items.Single(r => r.Action == "AttemptSubmitted").OccurredAt);
        Assert.All(rows.Items, r => Assert.Equal("Scoped assignment", r.TargetLabel));
        Assert.DoesNotContain("PRIVATE", JsonSerializer.Serialize(rows));
        Assert.Equal(ErrorCodes.ResourceNotFound, (await service.DetailAsync("Assessment", "attempt:102", default)).ErrorCode);
        Assert.Single((await service.ListAsync(new() { Source = "Assessment", Action = "AssignmentSubmitted" }, default)).Data!.Items);
        Assert.Equal(0, (await service.ListAsync(new() { Source = "Assessment", From = analysedAt.AddSeconds(1) }, default)).Data!.TotalItems);
        await using var teacherDb = f.Context(teacher);
        Assert.Equal(3, (await new ActivityService(teacherDb, f.Tenant(teacher)).ListAsync(new() { Source = "Assessment" }, default)).Data!.TotalItems);
        var manager = f.User(UserRole.CenterManager); await using var managerDb = f.Context(manager);
        Assert.Empty((await new ActivityService(managerDb, f.Tenant(manager)).ListAsync(new() { Source = "Assessment" }, default)).Data!.Items);
    }
    [Theory]
    [InlineData("{\"TeacherFinalReviewStatus\":true,\"FinalReviewVersion\":\"old\"}")]
    [InlineData("{\"TeacherFinalReviewStatus\":99,\"FinalReviewVersion\":-1}")]
    [InlineData("{\"TeacherFinalReviewStatus\":{},\"FinalReviewVersion\":null}")]
    [InlineData("{\"TeacherFinalReviewStatus\":999999999999,\"FinalReviewVersion\":1.5}")]
    [InlineData("malformed historical json")]
    public async Task Activity_HistoricalUnknownJsonIsSafeAndNeverInventsPendingStatus(string json)
    {
        await using var f = await NotificationManualTests.Fixture.Create(); var student = f.User(UserRole.Student);
        await using var db = f.Context(student);
        db.AuthorizationAuditLogs.Add(new() { CenterId = student.CenterId, TargetUserId = student.UserId,
            ActionType = "AssignmentResultApproved", TargetType = "StudentAssignmentResult", TargetId = "own",
            BeforeData = json, AfterData = json, Reason = "Isolated historical metadata", TraceId = Guid.NewGuid().ToString(), CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var result = await new ActivityService(db, f.Tenant(student)).ListAsync(new(), default);
        Assert.True(result.IsSuccess); var row = Assert.Single(result.Data!.Items);
        Assert.Null(row.BeforeStatus); Assert.Null(row.AfterStatus); Assert.Null(row.Version);
    }
    [Fact]
    public async Task Activity_ManagerSeesMetadataNotAcademicJson_StudentOnlyOwnResult()
    {
        await using var f = await NotificationManualTests.Fixture.Create(); var teacher = f.User(UserRole.Teacher); var student = f.User(UserRole.Student); var manager = f.User(UserRole.CenterManager);
        await using var db = f.Context(teacher); var now = DateTime.UtcNow;
        db.AuthorizationAuditLogs.Add(new() { CenterId = teacher.CenterId, ActionType = "AssignmentResultApproved", TargetType = "StudentAssignmentResult", TargetId = "own",
            TargetUserId = student.UserId, BeforeData = "{\"TeacherFinalReviewStatus\":0,\"Answer\":\"SECRET\"}", AfterData = "{\"TeacherFinalReviewStatus\":1,\"FinalReviewVersion\":3}", Reason = "Valid", TraceId = "a", CreatedAt = now });
        db.AuthorizationAuditLogs.Add(new() { CenterId = teacher.CenterId, ActionType = "ClassUpdated", TargetType = "Class", TargetId = f.ClassId.ToString(),
            BeforeData = "{\"Score\":10}", AfterData = "{}", Reason = "PRIVATE REASON", TraceId = "b", CreatedAt = now }); await db.SaveChangesAsync();
        await using var md = f.Context(manager); var m = (await new ActivityService(md, f.Tenant(manager)).ListAsync(new(), default)).Data!;
        Assert.Single(m.Items); Assert.Null(m.Items[0].BeforeStatus); Assert.Null(m.Items[0].Reason); Assert.Null(m.Items[0].Link);
        Assert.Equal((await db.Classes.SingleAsync()).ClassName, m.Items[0].TargetLabel);
        await using var sd = f.Context(student); var s = (await new ActivityService(sd, f.Tenant(student)).ListAsync(new(), default)).Data!;
        Assert.Single(s.Items); Assert.Equal("Approved", s.Items[0].AfterStatus); Assert.DoesNotContain("SECRET", JsonSerializer.Serialize(s));
        Assert.Equal(ErrorCodes.ValidationFailed, (await new ActivityService(sd, f.Tenant(student)).ListAsync(new() { Page = 101, PageSize = 20 }, default)).ErrorCode);
        Assert.Equal(ErrorCodes.ResourceNotFound, (await new ActivityService(md, f.Tenant(manager)).DetailAsync("Audit", s.Items[0].SourceId, default)).ErrorCode);
    }
    [Fact]
    public async Task Activity_AdministrativeBusinessLabelCannotCopyPrivateEventTitle()
    {
        await using var f = await NotificationManualTests.Fixture.Create(); var manager = f.User(UserRole.CenterManager);
        await using var db = f.Context(manager);
        db.NotificationEvents.Add(new() { CenterId = manager.CenterId, EventId = Guid.NewGuid(), EventType = "AssignmentCreated",
            DeduplicationKey = "metadata-label", Title = "PRIVATE ACADEMIC TITLE", Body = "PRIVATE BODY", RecipientUserIds = "[]",
            ResourceContext = "{\"Kind\":\"Administrative\"}", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var result = (await new ActivityService(db, f.Tenant(manager)).ListAsync(new(), default)).Data!;
        var row = Assert.Single(result.Items); Assert.Null(row.TargetLabel);
        Assert.DoesNotContain("PRIVATE", JsonSerializer.Serialize(result));
    }
    [Fact]
    public async Task Operations_FailsClosedForStudentTeacher_AndDoesNotReturnPayload()
    {
        await using var f = await NotificationManualTests.Fixture.Create(); var manager = f.User(UserRole.CenterManager);
        await using var db = f.Context(manager); var eventId = Guid.NewGuid(); var now = DateTime.UtcNow;
        db.NotificationEvents.Add(new() { CenterId = manager.CenterId, EventId = eventId, EventType = "SystemNotice", DeduplicationKey = "ops", Title = "PRIVATE TITLE", Body = "PRIVATE BODY", RecipientUserIds = "[]", CreatedAt = now });
        db.NotificationDispatches.Add(new NotificationDispatch { CenterId = manager.CenterId, EventId = eventId, AvailableAt = now }); await db.SaveChangesAsync();
        var service = new NotificationOperationsService(db, f.Tenant(manager), f.Clock);
        var page = (await service.ListAsync(1, 20, null, default)).Data!; Assert.Single(page.Items); Assert.DoesNotContain("PRIVATE", JsonSerializer.Serialize(page));
        foreach (var role in new[] { UserRole.Student, UserRole.Teacher })
        {
            var user = f.User(role); await using var other = f.Context(user);
            Assert.Equal(ErrorCodes.ResourceNotFound, (await new NotificationOperationsService(other, f.Tenant(user), f.Clock).ListAsync(1, 20, null, default)).ErrorCode);
        }
    }
    [Fact]
    public async Task BusinessObserver_SynchronousSaveCannotSilentlyMissTransition()
    {
        await using var f = await NotificationManualTests.Fixture.Create(); var teacher = f.User(UserRole.Teacher);
        await using var db = f.Context(teacher, true); (await db.Classes.SingleAsync()).ClassName = "Updated";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }
}
