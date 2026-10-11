using System.Text.Json;
using EduTwin.BLL.Notifications;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.Assignments;
using EduTwin.DAL.AssessmentAndReasoning;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTwin.BLL.Tests.Notifications;

public sealed class NotificationBusinessTests
{
    [Fact]
    public async Task Void_OnlyAssignedSnapshotReceives_NoOtherAssignmentUsingSameQuestion()
    {
        await using var f = await NotificationManualTests.Fixture.Create(); var teacher = f.User(UserRole.Teacher); var student = f.User(UserRole.Student);
        await using var db = f.Context(teacher, true); var a = Assignment(f, teacher); var other = Assignment(f, teacher);
        db.Assignments.AddRange(a, other);
        db.AssignmentTargets.Add(new() { CenterId = teacher.CenterId, AssignmentId = a.AssignmentId, StudentId = student.UserId, CreatedAt = DateTime.UtcNow });
        var q = new AssignmentQuestion { CenterId = teacher.CenterId, AssignmentId = a.AssignmentId, QuestionId = 1, Points = 10, CreatedAt = DateTime.UtcNow };
        db.AssignmentQuestions.Add(q);
        db.AssignmentQuestions.Add(new() { CenterId = teacher.CenterId, AssignmentId = other.AssignmentId, QuestionId = 1, Points = 10, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(); q.IsVoided = true; await db.SaveChangesAsync(); await db.SaveChangesAsync();
        var notice = Assert.Single(await db.NotificationEvents.Where(e => e.EventType == "AssignmentQuestionVoided").ToArrayAsync());
        Assert.Equal(new[] { student.UserId }, JsonSerializer.Deserialize<Guid[]>(notice.RecipientUserIds));
        using var context = JsonDocument.Parse(notice.ResourceContext!);
        Assert.Equal(a.AssignmentId, context.RootElement.GetProperty("AssignmentId").GetGuid());
        Assert.False((await db.AssignmentQuestions.SingleAsync(x => x.AssignmentId == other.AssignmentId)).IsVoided);
    }
    [Fact]
    public async Task TeacherHandover_SuppressesOldPendingAcademicWork_AndNotifiesNewTeacher()
    {
        await using var f = await NotificationManualTests.Fixture.Create(); var teacher = f.User(UserRole.Teacher); var student = f.User(UserRole.Student);
        await using var db = f.Context(teacher, true); var a = Assignment(f, teacher); db.Assignments.Add(a); var p = Progress(a, student, 1); db.StudentAssignmentProgresses.Add(p);
        await db.SaveChangesAsync(); p.Status = ProgressStatus.Completed; p.CompletedAt = DateTime.UtcNow;
        db.Attempts.Add(new() { CenterId = teacher.CenterId, AttemptId = 1, AssignmentId = a.AssignmentId, StudentId = student.UserId, QuestionId = 1,
            Status = AttemptStatus.Completed, Skipped = true, FinalAnswer = "", ReasoningText = "", ReasoningLanguage = "vi", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(); var oldWork = await db.NotificationEvents.SingleAsync(e => e.EventType == "AssignmentSubmitted");
        var newTeacher = Guid.NewGuid(); db.Users.Add(new() { CenterId = teacher.CenterId, UserId = newTeacher, Username = "isolated_handover", DisplayName = "Isolated next teacher",
            RoleName = UserRole.Teacher, Status = UserStatus.Active, AuthVersion = 1, PasswordHash = "not-a-password", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(); // A real class handover selects an existing active teacher, not an unsaved account.
        (await db.Classes.SingleAsync()).TeacherId = newTeacher; await db.SaveChangesAsync();
        Assert.True(await new NotificationDispatcher(db, f.Clock).DispatchAsync(oldWork.EventId));
        Assert.Empty(await db.NotificationDeliveries.Where(d => d.EventId == oldWork.EventId).ToArrayAsync());
        var handover = await db.NotificationEvents.SingleAsync(e => e.EventType == "ClassWorkHandover");
        Assert.True(await new NotificationDispatcher(db, f.Clock).DispatchAsync(handover.EventId));
        Assert.Equal(newTeacher, (await db.NotificationDeliveries.SingleAsync(d => d.EventId == handover.EventId)).UserId);
    }
    [Fact]
    public async Task Publish_UsesMaterializedSelectedTarget_NotRoster_AndSuppressesArchivedNewWork()
    {
        await using var f = await NotificationManualTests.Fixture.Create(); var teacher = f.User(UserRole.Teacher); var student = f.User(UserRole.Student);
        await using var db = f.Context(teacher, true); var a = Assignment(f, teacher); db.Assignments.Add(a); await db.SaveChangesAsync();
        Assert.DoesNotContain(await db.NotificationEvents.ToArrayAsync(), e => e.EventType == "AssignmentPublished");
        db.AssignmentTargets.Add(new() { CenterId = teacher.CenterId, AssignmentId = a.AssignmentId, StudentId = student.UserId, CreatedBy = teacher.UserId, CreatedAt = DateTime.UtcNow });
        a.Status = AssignmentStatus.Published; a.PublishedAt = DateTime.UtcNow; await db.SaveChangesAsync(); await db.SaveChangesAsync();
        var ev = await db.NotificationEvents.SingleAsync(e => e.EventType == "AssignmentPublished");
        Assert.Equal(new[] { student.UserId }, JsonSerializer.Deserialize<Guid[]>(ev.RecipientUserIds));
        (await db.Classes.SingleAsync()).Status = ClassStatus.Archived; await db.SaveChangesAsync();
        Assert.True(await new NotificationDispatcher(db, f.Clock).DispatchAsync(ev.EventId));
        Assert.Empty(await db.NotificationDeliveries.Where(d => d.EventId == ev.EventId).ToArrayAsync());
    }

    [Fact]
    public async Task Submission_WithSkippedAttempt_IsOneEvent_ButVoidCompletionIsNotSubmission()
    {
        await using var f = await NotificationManualTests.Fixture.Create(); var teacher = f.User(UserRole.Teacher); var student = f.User(UserRole.Student);
        await using var db = f.Context(teacher, true); var a = Assignment(f, teacher); db.Assignments.Add(a);
        var progress = Progress(a, student, 1); db.StudentAssignmentProgresses.Add(progress); await db.SaveChangesAsync();
        progress.Status = ProgressStatus.Completed; progress.CompletedAt = DateTime.UtcNow; progress.CompletedQuestionCount = 0;
        db.Attempts.Add(new() { CenterId = student.CenterId, AttemptId = 1, AssignmentId = a.AssignmentId, StudentId = student.UserId, QuestionId = 1, Skipped = true, Status = AttemptStatus.Completed, FinalAnswer = "", ReasoningText = "", ReasoningLanguage = "vi", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(); await db.SaveChangesAsync();
        Assert.Single(await db.NotificationEvents.Where(e => e.EventType == "AssignmentSubmitted").ToArrayAsync());
        var other = Assignment(f, teacher); db.Assignments.Add(other); var voided = Progress(other, student, 2); db.StudentAssignmentProgresses.Add(voided); await db.SaveChangesAsync();
        voided.Status = ProgressStatus.Completed; voided.CompletedAt = DateTime.UtcNow; await db.SaveChangesAsync();
        Assert.Single(await db.NotificationEvents.Where(e => e.EventType == "AssignmentSubmitted").ToArrayAsync());
    }

    [Fact]
    public async Task FinalReview_ApprovedReopenedReapproved_KeepVersions_WithoutRawNotes()
    {
        await using var f = await NotificationManualTests.Fixture.Create(); var teacher = f.User(UserRole.Teacher); var student = f.User(UserRole.Student);
        await using var db = f.Context(teacher, true); var a = Assignment(f, teacher); db.Assignments.Add(a); var p = Progress(a, student, 1); db.StudentAssignmentProgresses.Add(p); await db.SaveChangesAsync();
        p.TeacherFinalReviewStatus = TeacherFinalReviewStatus.Approved; p.FinalReviewedAt = DateTime.UtcNow; p.FinalReviewVersion = 1; p.FinalTeacherNote = "PRIVATE RAW NOTE"; await db.SaveChangesAsync();
        p.TeacherFinalReviewStatus = TeacherFinalReviewStatus.Pending; p.FinalReviewedAt = null; p.FinalReviewVersion = 2; await db.SaveChangesAsync();
        p.TeacherFinalReviewStatus = TeacherFinalReviewStatus.Approved; p.FinalReviewedAt = DateTime.UtcNow; p.FinalReviewVersion = 3; await db.SaveChangesAsync();
        var rows = await db.NotificationEvents.Where(e => e.EventType.StartsWith("AssignmentResult")).ToArrayAsync(); Assert.Equal(3, rows.Length);
        Assert.Equal(3, rows.Select(e => e.DeduplicationKey).Distinct().Count()); Assert.All(rows, e => Assert.DoesNotContain("PRIVATE RAW NOTE", e.Body));
        db.AssignmentTargets.Add(new() { CenterId = teacher.CenterId, AssignmentId = a.AssignmentId, StudentId = student.UserId, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        foreach (var row in rows) Assert.True(await new NotificationDispatcher(db, f.Clock).DispatchAsync(row.EventId));
        var delivered = Assert.Single(await db.NotificationDeliveries.Where(d => rows.Select(r => r.EventId).Contains(d.EventId)).ToArrayAsync());
        using var deliveredContext = JsonDocument.Parse(rows.Single(r => r.EventId == delivered.EventId).ResourceContext!);
        Assert.Equal("3", deliveredContext.RootElement.GetProperty("Version").GetString());
    }

    [Theory]
    [InlineData("[DEFECTIVE_QUESTION_TYPO_LATEX] lỗi", "QuestionReported")]
    [InlineData("Xin xem lại kết quả", "ReviewRequested")]
    public async Task Review_UsesLegacyCategory_OnlyNewPendingAndRealResolution(string comment, string kind)
    {
        await using var f = await NotificationManualTests.Fixture.Create(); var teacher = f.User(UserRole.Teacher); var student = f.User(UserRole.Student);
        await using var db = f.Context(teacher, true); var a = Assignment(f, teacher); db.Assignments.Add(a);
        db.Attempts.Add(new() { CenterId = student.CenterId, AttemptId = 1, AssignmentId = a.AssignmentId, StudentId = student.UserId, QuestionId = 1, Status = AttemptStatus.Completed, FinalAnswer = "", ReasoningText = "", ReasoningLanguage = "vi", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow }); await db.SaveChangesAsync();
        var request = new StudentReviewRequest { RequestId = 1, CenterId = student.CenterId, AttemptId = 1, StudentId = student.UserId, QuestionId = 1, StudentComment = comment, Status = StudentReviewRequestStatus.Pending, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.StudentReviewRequests.Add(request); await db.SaveChangesAsync(); request.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync();
        Assert.Single(await db.NotificationEvents.Where(e => e.EventType == kind).ToArrayAsync());
        request.Status = StudentReviewRequestStatus.Resolved; await db.SaveChangesAsync();
        Assert.Single(await db.NotificationEvents.Where(e => e.EventType == "ReviewRequestResolved").ToArrayAsync());
    }

    [Theory]
    [InlineData(50)]
    [InlineData(100)]
    public async Task Ai_MarkersNeverDeliver_OnlyOneAggregatePerEffectiveGeneration(int count)
    {
        await using var f = await NotificationManualTests.Fixture.Create(); var teacher = f.User(UserRole.Teacher); var student = f.User(UserRole.Student);
        await using var db = f.Context(teacher, true); var a = Assignment(f, teacher); a.Status = AssignmentStatus.Published; db.Assignments.Add(a);
        var p = Progress(a, student, 1); p.Status = ProgressStatus.Completed; p.CompletedAt = DateTime.UtcNow; db.StudentAssignmentProgresses.Add(p);
        db.AssignmentTargets.Add(new() { CenterId = teacher.CenterId, AssignmentId = a.AssignmentId, StudentId = student.UserId, CreatedAt = DateTime.UtcNow });
        for (var i = 1; i <= count; i++)
        {
            db.AssignmentQuestions.Add(new() { CenterId = teacher.CenterId, AssignmentId = a.AssignmentId, QuestionId = (ulong)i, CreatedAt = DateTime.UtcNow });
            db.Attempts.Add(new() { CenterId = teacher.CenterId, AttemptId = (ulong)i, AssignmentId = a.AssignmentId, StudentId = student.UserId, QuestionId = (ulong)i, Status = AttemptStatus.PendingAnalysis, FinalAnswer = "", ReasoningText = "", ReasoningLanguage = "vi", CreatedAt = f.Clock.GetUtcNow().UtcDateTime, UpdatedAt = f.Clock.GetUtcNow().UtcDateTime });
            db.AIAnalysisJobs.Add(new() { CenterId = teacher.CenterId, AnalysisJobId = (ulong)i, AttemptId = (ulong)i, Status = AIJobStatus.Pending, CorrelationId = "isolated", CreatedAt = f.Clock.GetUtcNow().UtcDateTime, UpdatedAt = f.Clock.GetUtcNow().UtcDateTime, AvailableAt = f.Clock.GetUtcNow().UtcDateTime });
        }
        await db.SaveChangesAsync();
        var last = await db.AIAnalysisJobs.SingleAsync(j => j.AnalysisJobId == (ulong)count); last.Status = AIJobStatus.FailedTerminal;
        (await db.Attempts.SingleAsync(x => x.AttemptId == last.AttemptId)).Status = AttemptStatus.AnalysisFailed; await db.SaveChangesAsync();
        var firstMarker = await db.NotificationEvents.SingleAsync(e => e.EventType == "AICompletionMarker");
        Assert.False(await new NotificationDispatcher(db, f.Clock).DispatchAsync(firstMarker.EventId));
        Assert.Empty(await db.NotificationDeliveries.ToArrayAsync()); Assert.Empty(await db.NotificationEvents.Where(e => e.EventType == "AssignmentAiReady").ToArrayAsync());
        foreach (var job in await db.AIAnalysisJobs.Where(j => j.Status == AIJobStatus.Pending).ToArrayAsync()) job.Status = AIJobStatus.FallbackCompleted;
        foreach (var attempt in await db.Attempts.Where(x => x.Status == AttemptStatus.PendingAnalysis).ToArrayAsync()) attempt.Status = AttemptStatus.NeedsTeacherReview;
        await db.SaveChangesAsync(); f.Clock.Now = f.Clock.Now.AddSeconds(20);
        foreach (var marker in await db.NotificationEvents.Where(e => e.EventType == "AICompletionMarker").ToArrayAsync()) await new NotificationDispatcher(db, f.Clock).DispatchAsync(marker.EventId);
        var aggregate = await db.NotificationEvents.Where(e => e.EventType == "AssignmentAiReady").ToArrayAsync(); Assert.Equal(2, aggregate.Length);
        Assert.All(aggregate, e => { Assert.Contains($"{count - 1} câu fallback", e.Body); Assert.Contains("1 câu lỗi terminal", e.Body); Assert.Contains("chưa phải", e.Body); });
        foreach (var e in aggregate) Assert.True(await new NotificationDispatcher(db, f.Clock).DispatchAsync(e.EventId));
        Assert.Equal(2, await db.NotificationDeliveries.CountAsync());
        var retry = await db.Attempts.SingleAsync(x => x.AttemptId == 1); retry.ManualRetryCount++; retry.LastManualRetryAt = f.Clock.GetUtcNow().UtcDateTime; retry.Status = AttemptStatus.PendingAnalysis;
        var retryJob = new AIAnalysisJob { CenterId = teacher.CenterId, AnalysisJobId = (ulong)count + 1, AttemptId = 1, Status = AIJobStatus.Pending, CorrelationId = "isolated-retry", CreatedAt = f.Clock.GetUtcNow().UtcDateTime, UpdatedAt = f.Clock.GetUtcNow().UtcDateTime, AvailableAt = f.Clock.GetUtcNow().UtcDateTime }; db.AIAnalysisJobs.Add(retryJob); await db.SaveChangesAsync();
        retry.Status = AttemptStatus.AnalysisFailed; retryJob.Status = AIJobStatus.FailedTerminal; await db.SaveChangesAsync();
        var retryMarker = await db.NotificationEvents.OrderByDescending(e => e.CreatedAt).FirstAsync(e => e.EventType == "AICompletionMarker");
        Assert.True(await new NotificationDispatcher(db, f.Clock).DispatchAsync(retryMarker.EventId)); Assert.Equal(4, await db.NotificationEvents.CountAsync(e => e.EventType == "AssignmentAiReady"));
    }
    private static Assignment Assignment(NotificationManualTests.Fixture f, EduTwin.DAL.IdentityAndTenancy.User teacher) => new() {
        AssignmentId = Guid.NewGuid(), CenterId = teacher.CenterId, ClassId = f.ClassId, CreatedByTeacherId = teacher.UserId, Title = "Bài cô lập", Status = AssignmentStatus.Draft, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
    private static StudentAssignmentProgress Progress(Assignment a, EduTwin.DAL.IdentityAndTenancy.User student, ulong id) => new() {
        CenterId = a.CenterId, AssignmentId = a.AssignmentId, StudentId = student.UserId, ProgressId = id, Status = ProgressStatus.NotStarted, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
}
