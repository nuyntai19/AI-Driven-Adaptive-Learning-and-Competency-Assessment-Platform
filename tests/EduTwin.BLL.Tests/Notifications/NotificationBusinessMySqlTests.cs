using System.Text.Json;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Notifications;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.Contracts.Notifications;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.Assignments;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTwin.BLL.Tests.Notifications;

public sealed partial class NotificationMySqlTests
{
    [NotificationMySqlFact]
    public async Task FinalReview_ObsoleteApprovalSuppressed_ReopenedVersionDelivered_RealSql()
    {
        await using var f = await Fixture.Create(UserRole.Teacher, UserRole.Student); var data = await Academic(f, 1);
        await using var db = f.Context(observeBusiness: true);
        var progress = await db.StudentAssignmentProgresses.SingleAsync();
        progress.TeacherFinalReviewStatus = TeacherFinalReviewStatus.Approved; progress.FinalReviewVersion = 1; progress.FinalReviewedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        var approved = await db.NotificationEvents.SingleAsync(e => e.EventType == "AssignmentResultApproved");
        progress.TeacherFinalReviewStatus = TeacherFinalReviewStatus.Pending; progress.FinalReviewVersion = 2; progress.FinalReviewedAt = null;
        await db.SaveChangesAsync();
        var reopened = await db.NotificationEvents.SingleAsync(e => e.EventType == "AssignmentResultReopened");
        await using var worker = f.Context();
        Assert.True(await new NotificationDispatcher(worker, TimeProvider.System).DispatchAsync(approved.EventId));
        Assert.True(await new NotificationDispatcher(worker, TimeProvider.System).DispatchAsync(reopened.EventId));
        var delivery = Assert.Single(await worker.NotificationDeliveries.ToArrayAsync());
        Assert.Equal(reopened.EventId, delivery.EventId); Assert.Equal(data.StudentId, delivery.UserId);
        Assert.Equal(1, (await worker.NotificationDispatches.SingleAsync(d => d.EventId == approved.EventId)).SuppressedCount);
        Assert.Equal(2, await worker.NotificationEvents.CountAsync(e => e.EventType.StartsWith("AssignmentResult")));
    }
    [NotificationMySqlFact]
    public async Task BusinessPublishAndSubmission_RollbackWithOutbox_RealSql()
    {
        await using var f = await Fixture.Create(UserRole.Teacher, UserRole.Student);
        var data = await Academic(f, 1); await using var db = f.Context(observeBusiness: true);
        var assignment = await db.Assignments.SingleAsync();
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER deny_business_event BEFORE INSERT ON notification_events FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Isolated failure'");
        assignment.Status = AssignmentStatus.Published;
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        Assert.Equal(AssignmentStatus.Draft, (await db.Assignments.SingleAsync()).Status); Assert.Empty(await db.NotificationEvents.ToArrayAsync());
        await db.Database.ExecuteSqlRawAsync("DROP TRIGGER deny_business_event");
        assignment = await db.Assignments.SingleAsync(); assignment.Status = AssignmentStatus.Published; await db.SaveChangesAsync(); await db.SaveChangesAsync();
        var published = await db.NotificationEvents.SingleAsync(e => e.EventType == "AssignmentPublished");
        Assert.Equal(new[] { data.StudentId }, JsonSerializer.Deserialize<Guid[]>(published.RecipientUserIds));
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER deny_business_event BEFORE INSERT ON notification_events FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Isolated failure'");
        var progress = await db.StudentAssignmentProgresses.SingleAsync(); progress.Status = ProgressStatus.Completed; progress.CompletedAt = DateTime.UtcNow;
        db.Attempts.Add(AttemptFor(f, data, 1, AttemptStatus.Completed, skipped: true));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        Assert.Empty(await db.Attempts.ToArrayAsync()); Assert.Equal(ProgressStatus.NotStarted, (await db.StudentAssignmentProgresses.SingleAsync()).Status);
        Assert.Empty(await db.NotificationEvents.Where(e => e.EventType == "AssignmentSubmitted").ToArrayAsync());
        await db.Database.ExecuteSqlRawAsync("DROP TRIGGER deny_business_event");
        progress = await db.StudentAssignmentProgresses.SingleAsync(); progress.Status = ProgressStatus.Completed; progress.CompletedAt = DateTime.UtcNow;
        db.Attempts.Add(AttemptFor(f, data, 1, AttemptStatus.Completed, skipped: true)); await db.SaveChangesAsync(); await db.SaveChangesAsync();
        Assert.Single(await db.NotificationEvents.Where(e => e.EventType == "AssignmentSubmitted").ToArrayAsync());
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER deny_business_event BEFORE INSERT ON notification_events FOR EACH ROW SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT='Isolated failure'");
        progress.TeacherFinalReviewStatus = TeacherFinalReviewStatus.Approved; progress.FinalReviewVersion = 1; progress.FinalReviewedAt = DateTime.UtcNow;
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        Assert.Equal(TeacherFinalReviewStatus.Pending, (await db.StudentAssignmentProgresses.SingleAsync()).TeacherFinalReviewStatus);
        Assert.Equal(0u, (await db.StudentAssignmentProgresses.SingleAsync()).FinalReviewVersion);
        var question = await db.AssignmentQuestions.SingleAsync(); question.IsVoided = true;
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync()); db.ChangeTracker.Clear();
        Assert.False((await db.AssignmentQuestions.SingleAsync()).IsVoided);
        Assert.Empty(await db.NotificationEvents.Where(e => e.EventType == "AssignmentResultApproved" || e.EventType == "AssignmentQuestionVoided").ToArrayAsync());
        await db.Database.ExecuteSqlRawAsync("DROP TRIGGER deny_business_event");
        await using (var callerTransaction = await db.Database.BeginTransactionAsync())
        {
            (await db.Assignments.SingleAsync()).Instructions = "Caller-owned rollback";
            await db.SaveChangesAsync();
            Assert.Same(callerTransaction, db.Database.CurrentTransaction);
            await callerTransaction.RollbackAsync();
        }
        db.ChangeTracker.Clear();
        Assert.Null((await db.Assignments.SingleAsync()).Instructions);
        Assert.Empty(await db.NotificationEvents.Where(e => e.EventType == "AssignmentUpdated").ToArrayAsync());
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [NotificationMySqlFact]
    public async Task BusinessAi50_ReusedRetryJobSuppressesOldGeneration_RealSql()
    {
        await using var f = await Fixture.Create(UserRole.Teacher, UserRole.Student); var data = await Academic(f, 50);
        await using (var setup = f.Context())
        {
            var p = await setup.StudentAssignmentProgresses.SingleAsync(); p.Status = ProgressStatus.Completed; p.CompletedAt = DateTime.UtcNow;
            for (var i = 1; i <= 50; i++) { var attempt = AttemptFor(f, data, (ulong)i, AttemptStatus.NeedsTeacherReview); setup.Attempts.Add(attempt); }
            await setup.SaveChangesAsync();
            foreach (var attempt in await setup.Attempts.ToArrayAsync()) setup.AIAnalysisJobs.Add(new() { CenterId = f.CenterId, AttemptId = attempt.AttemptId,
                Status = AIJobStatus.Pending, CorrelationId = Guid.NewGuid().ToString("N"), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, AvailableAt = DateTime.UtcNow });
            await setup.SaveChangesAsync();
        }
        await using var db = f.Context(observeBusiness: true);
        foreach (var job in await db.AIAnalysisJobs.ToArrayAsync()) { job.Status = AIJobStatus.FallbackCompleted; job.CompletedAt = DateTime.UtcNow; job.UpdatedAt = DateTime.UtcNow; }
        await db.SaveChangesAsync();
        var markers = await db.NotificationEvents.Where(e => e.EventType == "AICompletionMarker").ToArrayAsync(); Assert.Equal(50, markers.Length);
        foreach (var marker in markers)
            if ((await db.NotificationDispatches.SingleAsync(d => d.EventId == marker.EventId)).State == "Pending")
                Assert.True(await new NotificationDispatcher(db, TimeProvider.System).DispatchAsync(marker.EventId));
        var old = await db.NotificationEvents.Where(e => e.EventType == "AssignmentAiReady").ToArrayAsync(); Assert.Equal(2, old.Length);
        Assert.All(old, e => Assert.Contains("50 câu fallback", e.Body)); Assert.Empty(await db.NotificationDeliveries.ToArrayAsync());
        var retry = await db.Attempts.OrderBy(a => a.AttemptId).FirstAsync(); retry.ManualRetryCount++; retry.LastManualRetryAt = DateTime.UtcNow; retry.Status = AttemptStatus.PendingAnalysis;
        var reused = await db.AIAnalysisJobs.SingleAsync(j => j.AttemptId == retry.AttemptId); reused.Status = AIJobStatus.Pending; reused.CompletedAt = null; reused.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        foreach (var e in old) Assert.True(await new NotificationDispatcher(db, TimeProvider.System).DispatchAsync(e.EventId));
        Assert.Empty(await db.NotificationDeliveries.ToArrayAsync());
        retry.Status = AttemptStatus.AnalysisFailed; reused.Status = AIJobStatus.FailedTerminal; reused.CompletedAt = DateTime.UtcNow; reused.UpdatedAt = DateTime.UtcNow; await db.SaveChangesAsync();
        var fresh = await db.NotificationEvents.SingleAsync(e => e.EventType == "AICompletionMarker" && e.DeduplicationKey.Contains($"{reused.AnalysisJobId}:1:"));
        Assert.True(await new NotificationDispatcher(db, TimeProvider.System).DispatchAsync(fresh.EventId));
        var oldIds = old.Select(x => x.EventId).ToHashSet();
        var next = (await db.NotificationEvents.Where(e => e.EventType == "AssignmentAiReady").ToArrayAsync()).Where(e => !oldIds.Contains(e.EventId)).ToArray(); Assert.Equal(2, next.Length);
        foreach (var e in next) Assert.True(await new NotificationDispatcher(db, TimeProvider.System).DispatchAsync(e.EventId));
        Assert.Equal(2, await db.NotificationDeliveries.CountAsync()); Assert.Equal(2, await db.NotificationDispatches.CountAsync(d => d.SuppressedCount == 1));
    }

    [NotificationMySqlFact]
    public async Task ActivityFourActors_PrivacyOwnershipAndSourceQueries_RealSql()
    {
        await using var f = await Fixture.Create(UserRole.Teacher, UserRole.Student); var data = await Academic(f, 1); var manager = Guid.NewGuid();
        await using (var db = f.Context())
        {
            var now = DateTime.UtcNow;
            db.Users.Add(new() { CenterId = f.CenterId, UserId = manager, RoleName = UserRole.CenterManager, Status = UserStatus.Active, AuthVersion = 1,
                Username = "isolated_manager", DisplayName = "Isolated manager", PasswordHash = "not-a-password", CreatedAt = now, UpdatedAt = now });
            db.AuthorizationAuditLogs.Add(new() { CenterId = f.CenterId, ActorUserId = f.UserId, ActionType = "AssignmentResultApproved", TargetType = "StudentAssignmentResult",
                TargetId = $"{data.AssignmentId:D}:{data.StudentId:D}", TargetUserId = data.StudentId, CreatedAt = now, TraceId = Guid.NewGuid().ToString(), Reason = "Valid reason",
                BeforeData = "{\"TeacherFinalReviewStatus\":0,\"SecretAnswer\":\"DO_NOT_EXPOSE\"}", AfterData = "{\"TeacherFinalReviewStatus\":1,\"FinalReviewVersion\":2,\"SecretAnswer\":\"DO_NOT_EXPOSE\"}" });
            db.AuthorizationAuditLogs.Add(new() { CenterId = f.CenterId, ActorUserId = f.UserId, ActionType = "ClassUpdated", TargetType = "Class", TargetId = data.ClassId.ToString(),
                CreatedAt = now, TraceId = Guid.NewGuid().ToString(), Reason = "DO_NOT_EXPOSE_ADMIN_RAW_REASON", BeforeData = "{\"PrivateScore\":10}", AfterData = "{}" });
            await db.SaveChangesAsync();
        }
        var student = new TenantContext(); student.Initialize(f.CenterId, data.StudentId, "Student", 1);
        await using var sd = f.Context(student); var studentPage = (await new ActivityService(sd, student).ListAsync(new(), default)).Data!;
        Assert.Single(studentPage.Items); Assert.Equal("Approved", studentPage.Items[0].AfterStatus); Assert.Equal("2", studentPage.Items[0].Version);
        Assert.DoesNotContain("DO_NOT_EXPOSE", JsonSerializer.Serialize(studentPage));
        await using var td = f.Context(); var teacherPage = await new ActivityService(td, f.Tenant).ListAsync(new(), default);
        Assert.True(teacherPage.IsSuccess); Assert.Equal(2, teacherPage.Data!.Items.Count);
        var managerScope = new TenantContext(); managerScope.Initialize(f.CenterId, manager, "CenterManager", 1);
        await using var md = f.Context(managerScope); var mp = (await new ActivityService(md, managerScope).ListAsync(new(), default)).Data!;
        Assert.Single(mp.Items); Assert.Null(mp.Items[0].BeforeStatus); Assert.Null(mp.Items[0].AfterStatus); Assert.Null(mp.Items[0].Reason);
        var forbiddenId = studentPage.Items[0].SourceId;
        Assert.Equal(ErrorCodes.ResourceNotFound, (await new ActivityService(md, managerScope).DetailAsync("Audit", forbiddenId, default)).ErrorCode);
        var foreign = new TenantContext(); foreign.Initialize(Guid.NewGuid(), data.StudentId, "Student", 1);
        await using var fd = f.Context(foreign); Assert.Empty((await new ActivityService(fd, foreign).ListAsync(new(), default)).Data!.Items);
        var platform = new TenantContext(); platform.Initialize(f.CenterId, f.UserId, "PlatformAdmin", 1);
        await using var pd = f.Context(platform); Assert.Empty((await new ActivityService(pd, platform).ListAsync(new(), default)).Data!.Items);
        (await td.Classes.SingleAsync()).TeacherId = manager; // Query-only ownership invalidation; do not persist invalid teacher FK.
        // A different actual teacher account cannot read this class's academic result via detail.
        var otherTeacher = new TenantContext(); otherTeacher.Initialize(f.CenterId, manager, "Teacher", 1);
        await using var ot = f.Context(otherTeacher); Assert.Equal(ErrorCodes.ResourceNotFound, (await new ActivityService(ot, otherTeacher).DetailAsync("Audit", forbiddenId, default)).ErrorCode);
    }

    [NotificationMySqlFact]
    public async Task ActivityTeacher_LegacyColumnCollationAndGuidCastRemainScoped_RealSql()
    {
        await using var f = await Fixture.Create(UserRole.Teacher, UserRole.Student); var data = await Academic(f, 1);
        await using var db = f.Context();
        // Only the disposable UUID schema is altered, never the user's database.
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE authorization_audit_logs MODIFY target_id varchar(128) CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci NOT NULL");
        var now = DateTime.UtcNow;
        db.AuthorizationAuditLogs.Add(new() { CenterId = f.CenterId, ActorUserId = f.UserId, TargetUserId = data.StudentId,
            ActionType = "AssignmentResultApproved", TargetType = "StudentAssignmentResult", TargetId = $"{data.AssignmentId:D}:{data.StudentId:D}",
            CreatedAt = now, TraceId = Guid.NewGuid().ToString(), Reason = "Synthetic collation regression", AfterData = "{\"TeacherFinalReviewStatus\":1,\"FinalReviewVersion\":1}" });
        await db.SaveChangesAsync();
        var legacy = await Assert.ThrowsAsync<MySql.Data.MySqlClient.MySqlException>(() => db.AuthorizationAuditLogs
            .Where(a => db.Assignments.Any(s => a.TargetId.StartsWith(s.AssignmentId.ToString()))).CountAsync());
        Assert.Contains("collation", legacy.Message, StringComparison.OrdinalIgnoreCase);
        var service = new ActivityService(db, f.Tenant);
        var page = await service.ListAsync(new() { Source = "Audit" }, default);
        Assert.True(page.IsSuccess); var item = Assert.Single(page.Data!.Items); Assert.Equal("Approved", item.AfterStatus);
        Assert.True((await service.DetailAsync("Audit", item.SourceId, default)).IsSuccess);
        var outside = new TenantContext(); outside.Initialize(f.CenterId, data.StudentId, "Teacher", 1);
        await using var other = f.Context(outside);
        Assert.Empty((await new ActivityService(other, outside).ListAsync(new() { Source = "Audit" }, default)).Data!.Items);
        Assert.Equal(ErrorCodes.ResourceNotFound, (await new ActivityService(other, outside).DetailAsync("Audit", item.SourceId, default)).ErrorCode);
    }

    [NotificationMySqlFact]
    public async Task Operations_RedriveIsFencedIdempotentAndAudited_RealSql()
    {
        await using var f = await Fixture.Create(UserRole.CenterManager); var id = await f.Stage("ops-only"); await using var db = f.Context();
        var row = await db.NotificationDispatches.SingleAsync(); row.State = "DeadLetter"; row.AttemptCount = 5; await db.SaveChangesAsync();
        var service = new NotificationOperationsService(db, f.Tenant, TimeProvider.System);
        var list = (await service.ListAsync(1, 20, "DeadLetter", default)).Data!; Assert.Single(list.Items); Assert.Equal(1, list.Summary.DeadLetter);
        var request = new NotificationRedriveRequest { ClientActionId = Guid.NewGuid(), ExpectedRowVersion = row.RowVersion.ToString(), Reason = "Cô lập: kiểm tra phục hồi" };
        Assert.Null(await service.RedriveAsync(id, request, default)); Assert.Null(await service.RedriveAsync(id, request, default));
        Assert.Equal("Pending", row.State); Assert.Equal(0, row.AttemptCount); Assert.Single(await db.AuthorizationAuditLogs.Where(a => a.ActionType == "NotificationRedriven").ToArrayAsync());
        request.ClientActionId = Guid.NewGuid(); Assert.Equal(ErrorCodes.ConcurrencyConflict, await service.RedriveAsync(id, request, default));
        Assert.True(await new NotificationDispatcher(db, TimeProvider.System).DispatchAsync(id)); Assert.Single(await db.NotificationDeliveries.ToArrayAsync());
        Assert.False(db.Database.HasPendingModelChanges());
    }

    private sealed record AcademicData(Guid ClassId, Guid AssignmentId, Guid StudentId);
    private static async Task<AcademicData> Academic(Fixture f, int count)
    {
        await using var db = f.Context(); var now = DateTime.UtcNow; var student = await db.Users.Where(u => u.RoleName == UserRole.Student).Select(u => u.UserId).SingleAsync();
        var subject = Guid.NewGuid(); var classId = Guid.NewGuid(); var assignment = Guid.NewGuid();
        db.Teachers.Add(new Teacher { CenterId = f.CenterId, TeacherId = f.UserId, CreatedAt = now, UpdatedAt = now });
        db.Students.Add(new Student { CenterId = f.CenterId, StudentId = student, FullName = "Isolated student", GradeLevel = 10, CreatedAt = now, UpdatedAt = now });
        db.Subjects.Add(new Subject { CenterId = f.CenterId, SubjectId = subject, SubjectCode = "ISOLATED", SubjectName = "Isolated", IsActive = true, CreatedAt = now, UpdatedAt = now });
        db.Classes.Add(new Class { CenterId = f.CenterId, ClassId = classId, SubjectId = subject, TeacherId = f.UserId, ClassName = "Isolated", AcademicYear = "2026", GradeLevel = 10,
            Status = ClassStatus.Active, LearningScope = ClassLearningScope.Current, CreatedAt = now, UpdatedAt = now });
        db.ClassStudents.Add(new ClassStudent { CenterId = f.CenterId, ClassId = classId, StudentId = student, Status = ClassStudentStatus.Active, JoinedAt = now, GradeLevelAtEnrollment = 10 });
        db.KnowledgeNodes.Add(new KnowledgeNode { CenterId = f.CenterId, NodeId = 1, SubjectId = subject, NodeCode = "ISOLATED", NodeName = "Isolated", NodeType = NodeType.Topic,
            IsActive = true, EstimatedLearningMinutes = 60, CreatedAt = now, UpdatedAt = now });
        for (var i = 1; i <= count; i++) db.Questions.Add(new Question { CenterId = f.CenterId, QuestionId = (ulong)i, SubjectId = subject, PrimaryTopicNodeId = 1, CreatedByTeacherId = f.UserId,
            QuestionType = QuestionType.ShortAnswer, Difficulty = 1, GradeLevel = 10, QuestionText = "Isolated", CorrectAnswer = "1", Solution = "Isolated", MaxScore = 10,
            EstimatedTimeSeconds = 60, LanguageCode = "vi", Status = QuestionStatus.Active, CreatedAt = now, UpdatedAt = now });
        db.Assignments.Add(new Assignment { CenterId = f.CenterId, AssignmentId = assignment, ClassId = classId, CreatedByTeacherId = f.UserId, Title = "Isolated", Status = AssignmentStatus.Draft, CreatedAt = now, UpdatedAt = now });
        for (var i = 1; i <= count; i++) db.AssignmentQuestions.Add(new AssignmentQuestion { CenterId = f.CenterId, AssignmentId = assignment, QuestionId = (ulong)i, OrderIndex = (uint)i, Points = 10, CreatedAt = now });
        db.AssignmentTargets.Add(new AssignmentTarget { CenterId = f.CenterId, AssignmentId = assignment, StudentId = student, CreatedAt = now });
        db.StudentAssignmentProgresses.Add(new StudentAssignmentProgress { CenterId = f.CenterId, AssignmentId = assignment, StudentId = student, Status = ProgressStatus.NotStarted, CreatedAt = now, UpdatedAt = now });
        await db.SaveChangesAsync(); return new(classId, assignment, student);
    }
    private static Attempt AttemptFor(Fixture f, AcademicData data, ulong question, AttemptStatus status, bool skipped = false) => new() { CenterId = f.CenterId,
        StudentId = data.StudentId, AssignmentId = data.AssignmentId, QuestionId = question, FinalAnswer = "", ReasoningText = "", ReasoningLanguage = "vi",
        ClientSubmissionId = Guid.NewGuid(), Skipped = skipped, Status = status, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
}
