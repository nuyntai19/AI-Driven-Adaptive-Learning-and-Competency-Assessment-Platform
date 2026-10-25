using System.Globalization;
using System.Text.Json;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Platform;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Notifications;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Notifications;

public sealed class ActivityService(EduTwinDbContext db, ITenantContext tenant)
{
    private static readonly string[] Sources = ["Audit", "Assessment", "TeacherReview", "ReviewRequest", "Curriculum", "Business"];
    private bool Identity => tenant.IsResolved && tenant.CenterId == db.CurrentTenantId && tenant.UserId is { } id && id != Guid.Empty;
    private bool Student => tenant.Role == nameof(UserRole.Student);
    private bool Teacher => tenant.Role == nameof(UserRole.Teacher);
    private bool Manager => tenant.Role == nameof(UserRole.CenterManager);
    private bool Platform => tenant.Role == nameof(UserRole.PlatformAdmin);
    private sealed class Row
    {
        public string Source { get; init; } = "";
        public string Id { get; init; } = "";
        public DateTime At { get; init; }
        public Guid? Actor { get; init; }
        public string Action { get; init; } = "";
        public string TargetType { get; init; } = "";
        public string TargetId { get; init; } = "";
        public string? Before { get; init; }
        public string? After { get; init; }
        public string? Version { get; init; }
        public string? Reason { get; init; }
        public decimal? PreviousScore { get; init; }
        public decimal? NewScore { get; init; }
        public string? Label { get; init; }
    }
    private IReadOnlyList<IQueryable<Row>> Queries()
    {
        var user = tenant.UserId!.Value; var academic = Student || Teacher;
        var audit = db.AuthorizationAuditLogs.AsNoTracking();
        if (Student) audit = audit.Where(a => a.TargetType == "StudentAssignmentResult" && a.TargetUserId == user);
        else if (Teacher)
        {
            // Older tenant tables use unicode_ci while GUID-to-string CAST may inherit
            // MySQL 8's 0900_ai_ci. Pin only the computed prefix, preserving stored rows,
            // the tenant/ownership predicate and the audit column's existing indexes.
            audit = db.Database.IsRelational()
                ? audit.Where(a => a.TargetType == "StudentAssignmentResult"
                    ? db.Assignments.Any(s => a.TargetId.StartsWith(EF.Functions.Collate(s.AssignmentId.ToString(), "utf8mb4_unicode_ci")) && db.Classes.Any(c => c.ClassId == s.ClassId && c.TeacherId == user))
                    : a.ActorUserId == user && (a.TargetType == "Curriculum" || a.TargetType == "KnowledgeNode" || a.TargetType == "KnowledgeEdge" || a.TargetType == "Class"))
                : audit.Where(a => a.TargetType == "StudentAssignmentResult"
                    ? db.Assignments.Any(s => a.TargetId.StartsWith(s.AssignmentId.ToString()) && db.Classes.Any(c => c.ClassId == s.ClassId && c.TeacherId == user))
                    : a.ActorUserId == user && (a.TargetType == "Curriculum" || a.TargetType == "KnowledgeNode" || a.TargetType == "KnowledgeEdge" || a.TargetType == "Class"));
        }
        else if (Manager) audit = audit.Where(a => a.TargetType == "Class" || a.TargetType == "Curriculum" || a.TargetType == "KnowledgeNode" || a.TargetType == "KnowledgeEdge" ||
            a.TargetType == "User" || a.TargetType == "Teacher" || a.TargetType == "Student" || a.TargetType == "AuthorizationRole" || a.TargetType == "UserRoleAssignment" || a.TargetType == "Center" || a.TargetType == "NotificationDispatch");
        else if (Platform) audit = audit.Where(a => db.Centers.Any(c => c.CenterId == a.CenterId && c.CenterCode == "PLATFORM") &&
            (a.TargetType == "Center" || a.TargetType == "User" || a.TargetType == "CenterManager" || a.TargetType == "AuthorizationRole" || a.TargetType == "NotificationDispatch"));
        else return [];
        var result = new List<IQueryable<Row>> { audit.Select(a => new Row { Source = "Audit", Id = a.AuthorizationAuditId.ToString(), At = a.CreatedAt,
            Actor = a.ActorUserId, Action = a.ActionType, TargetType = a.TargetType, TargetId = a.TargetId,
            Before = academic && a.TargetType == "StudentAssignmentResult" || a.TargetType == "NotificationDispatch" ? a.BeforeData : null,
            After = academic && a.TargetType == "StudentAssignmentResult" || a.TargetType == "NotificationDispatch" ? a.AfterData : null,
            Reason = academic || a.TargetType == "NotificationDispatch" ? a.Reason : null }) };
        // Newly staged administrative events are sources in their own right, not a duplicate audit table.
        var business = db.NotificationEvents.AsNoTracking().Where(e => e.MessageId == null && e.ResourceContext != null && e.EventType != "AICompletionMarker");
        if (Student || Teacher) business = business.Where(e => db.NotificationDeliveries.Any(d => d.EventId == e.EventId && d.UserId == user));
        else business = business.Where(e => e.EventType == "AssignmentCreated" || e.EventType == "CurriculumCreated" ||
            e.EventType == "AdministrativeActivity" || e.EventType == "AccountAccessChanged" || e.EventType == "ClassArchived" || e.EventType == "ClassReopened");
        if (Platform) business = business.Where(e => db.Centers.Any(c => c.CenterId == e.CenterId && c.CenterCode == "PLATFORM"));
        result.Add(business.Select(e => new Row { Source = "Business", Id = e.EventId.ToString(), At = e.CreatedAt, Actor = e.CreatedBy,
            Action = e.EventType, TargetType = "AdministrativeMetadata", TargetId = e.EventId.ToString(), Label = academic ? e.Title : null }));
        if (!Platform)
        {
            var ledger = db.ClassCurriculumApplications.AsNoTracking();
            if (Student) ledger = ledger.Where(l => db.ClassStudents.Any(m => m.ClassId == l.ClassId && m.StudentId == user));
            else if (Teacher) ledger = ledger.Where(l => db.Classes.Any(c => c.ClassId == l.ClassId && c.TeacherId == user));
            result.Add(ledger.Select(l => new Row { Source = "Curriculum", Id = l.ApplicationId.ToString() + ":start", At = l.StartedAt,
                Actor = l.AssignedBy, Action = "CurriculumApplied", TargetType = "Class", TargetId = l.ClassId.ToString(), After = "Applied", Reason = academic ? l.ChangeReason : null }));
            result.Add(ledger.Where(l => l.EndedAt != null).Select(l => new Row { Source = "Curriculum", Id = l.ApplicationId.ToString() + ":end", At = l.EndedAt!.Value,
                Actor = l.EndedBy, Action = "CurriculumApplicationEnded", TargetType = "Class", TargetId = l.ClassId.ToString(), Before = "Applied", After = "Ended", Reason = academic ? l.EndReason : null }));
        }
        if (academic)
        {
            var attempts = db.Attempts.AsNoTracking();
            if (Student) attempts = attempts.Where(a => a.StudentId == user);
            else attempts = attempts.Where(a => a.AssignmentId != null && db.Assignments.Any(s => s.AssignmentId == a.AssignmentId && db.Classes.Any(c => c.ClassId == s.ClassId && c.TeacherId == user)));
            // Submission and analysis timestamps are persisted evidence, independent of
            // notification delivery and teacher review. Never infer them from UpdatedAt.
            result.Add(attempts.Select(a => new Row { Source = "Assessment", Id = "attempt:" + a.AttemptId.ToString(), At = a.CreatedAt,
                Actor = a.StudentId, Action = "AttemptSubmitted", TargetType = "Attempt", TargetId = a.AttemptId.ToString() }));
            var submissions = db.StudentAssignmentProgresses.AsNoTracking().Where(p => p.CompletedAt != null);
            submissions = Student ? submissions.Where(p => p.StudentId == user)
                : submissions.Where(p => db.Assignments.Any(s => s.AssignmentId == p.AssignmentId && db.Classes.Any(c => c.ClassId == s.ClassId && c.TeacherId == user)));
            result.Add(submissions.Select(p => new Row { Source = "Assessment", Id = "submission:" + p.ProgressId.ToString(), At = p.CompletedAt!.Value,
                Actor = p.StudentId, Action = "AssignmentSubmitted", TargetType = "StudentAssignmentResult", TargetId = p.AssignmentId.ToString() + ":" + p.StudentId.ToString() }));
            var analyses = db.ReasoningAnalyses.AsNoTracking().Where(r => attempts.Any(a => a.AttemptId == r.AttemptId));
            result.Add(analyses.Select(r => new Row { Source = "Assessment", Id = "analysis:" + r.AnalysisId.ToString(), At = r.CreatedAt,
                Action = "AIAnalysisCompleted", TargetType = "Attempt", TargetId = r.AttemptId.ToString() }));
            var histories = db.TeacherReviewHistories.AsNoTracking().Where(h => attempts.Any(a => a.AttemptId == h.AttemptId));
            result.Add(histories.Select(h => new Row { Source = "TeacherReview", Id = h.HistoryId.ToString(), At = h.CreatedAt, Actor = h.TeacherId,
                Action = h.Decision, TargetType = "Attempt", TargetId = h.AttemptId.ToString(), Version = h.OverrideVersion.ToString(),
                Reason = h.Note, PreviousScore = h.PreviousScore, NewScore = h.NewScore }));
            var requests = db.StudentReviewRequests.AsNoTracking().Where(r => attempts.Any(a => a.AttemptId == r.AttemptId));
            result.Add(requests.Select(r => new Row { Source = "ReviewRequest", Id = r.RequestId.ToString() + ":created", At = r.CreatedAt,
                Actor = r.CreatedBy, Action = "ReviewRequestCreated", TargetType = "Attempt", TargetId = r.AttemptId.ToString(), Reason = r.StudentComment }));
            result.Add(requests.Where(r => r.ResolvedAt != null).Select(r => new Row { Source = "ReviewRequest", Id = r.RequestId.ToString() + ":resolved", At = r.ResolvedAt!.Value,
                Actor = r.ResolvedByTeacherId, Action = "ReviewRequestResolved", TargetType = "Attempt", TargetId = r.AttemptId.ToString(),
                After = r.Status == StudentReviewRequestStatus.Resolved ? "Resolved" : "Dismissed", Reason = r.TeacherNote }));
        }
        return result;
    }
    public async Task<NotificationResult<ActivityPage>> ListAsync(ActivityQuery query, CancellationToken ct)
    {
        if (!Identity) return new(null, ErrorCodes.ResourceNotFound);
        if (query.Page < 1 || query.PageSize is < 1 or > 100 || (long)query.Page * query.PageSize > 2_000 ||
            query.Source is { } source && !Sources.Contains(source) || query.Action?.Length > 64 || query.From > query.Until ||
            query.From is { Kind: not DateTimeKind.Utc } || query.Until is { Kind: not DateTimeKind.Utc }) return new(null, ErrorCodes.ValidationFailed);
        var rows = new List<Row>(); long total = 0; var limit = query.Page * query.PageSize;
        foreach (var original in Queries())
        {
            var q = original;
            if (query.Source != null) q = q.Where(r => r.Source == query.Source);
            if (!string.IsNullOrWhiteSpace(query.Action)) q = q.Where(r => r.Action == query.Action);
            if (query.From is { } from) q = q.Where(r => r.At >= from);
            if (query.Until is { } until) q = q.Where(r => r.At <= until);
            total += await q.LongCountAsync(ct);
            rows.AddRange(await q.OrderByDescending(r => r.At).ThenBy(r => r.Source).ThenByDescending(r => r.Id).Take(limit).ToArrayAsync(ct));
        }
        var page = rows.OrderByDescending(r => r.At).ThenBy(r => r.Source, StringComparer.Ordinal).ThenByDescending(r => r.Id, StringComparer.Ordinal)
            .Skip((query.Page - 1) * query.PageSize).Take(query.PageSize).ToArray();
        return new(new(await MapAsync(page, ct), total));
    }
    public async Task<NotificationResult<ActivityDto>> DetailAsync(string source, string id, CancellationToken ct)
    {
        if (!Identity || !Sources.Contains(source) || id.Length > 100) return new(null, ErrorCodes.ResourceNotFound);
        foreach (var query in Queries())
        {
            var row = await query.SingleOrDefaultAsync(r => r.Source == source && r.Id == id, ct);
            if (row is not null) return new((await MapAsync([row], ct))[0]);
        }
        return new(null, ErrorCodes.ResourceNotFound);
    }
    private async Task<ActivityDto[]> MapAsync(Row[] rows, CancellationToken ct)
    {
        var actors = rows.Where(r => r.Actor != null).Select(r => r.Actor!.Value).Distinct().ToArray();
        var names = await db.Users.AsNoTracking().Where(NotificationRecipientScope.Matches(actors)).ToDictionaryAsync(u => u.UserId, u => u.DisplayName, ct);
        var permissions = (await new AuthorizationSnapshotReader(db).ReadForUserAsync(tenant.UserId!.Value, ct)).Permissions.ToHashSet(StringComparer.Ordinal);
        var labels = await TargetLabelsAsync(rows, ct);
        return rows.Select(r => {
            var before = r.Source == "Audit" ? Status(r.Before) : r.Before;
            var after = r.Source == "Audit" ? Status(r.After) : r.After;
            var version = r.Source == "Audit" ? Version(r.After) : r.Version;
            if (r.Source == "TeacherReview") { before = Score(r.PreviousScore); after = Score(r.NewScore); }
            var reason = r.Reason == null ? null : PlatformAuditSanitizer.SanitizeReason(r.Reason);
            if (reason?.Length > 500) reason = reason[..500] + "…";
            // Existing destination guards remain authoritative; no arbitrary sender-provided URL.
            var link = r.TargetType == "Class" && Guid.TryParse(r.TargetId, out var classId)
                ? Manager && permissions.Contains("organization.classes.read") ? $"/quan-ly/lop-hoc/{classId:D}/tong-quan" : Teacher && permissions.Contains("organization.classes.read") ? "/giao-vien/lop-hoc" : Student && permissions.Contains("assignments.assignments.read") ? "/hoc-tap/bai-tap" : null
                : r.TargetType is "Attempt" or "StudentAssignmentResult" ? Teacher && (permissions.Contains("twin.reasoning.review") || permissions.Contains("twin.teacher_reviews.read")) ? "/giao-vien/cham-bai" : Student && permissions.Contains("assignments.assignments.read") ? "/hoc-tap/bai-tap" : null : null;
            return new ActivityDto(r.Source, r.Id, r.At, r.Actor is { } actor ? names.GetValueOrDefault(actor) ?? "Tài khoản không còn hiển thị" : "Hệ thống / Migration",
                r.Action, r.TargetType, r.TargetId, before, after, version, reason, link,
                SafeLabel(r.Label ?? labels.GetValueOrDefault(r.TargetType + ":" + r.TargetId)));
        }).ToArray();
    }
    // Enrich only the already-scoped page (at most 100 rows), never a tenant roster.
    // Administrative actors do not query assignments, attempts, answers or grades.
    private async Task<Dictionary<string, string>> TargetLabelsAsync(Row[] rows, CancellationToken ct)
    {
        var labels = new Dictionary<string, string>(StringComparer.Ordinal);
        var classIds = rows.Where(r => r.TargetType == "Class").Select(r => Guid.TryParse(r.TargetId, out var id) ? id : Guid.Empty).Where(id => id != Guid.Empty).Distinct().ToArray();
        if (!Platform && classIds.Length > 0)
        {
            var classes = NotificationRecipientScope.WhereIds(db.Classes.AsNoTracking(), c => c.ClassId, classIds);
            if (Teacher) classes = classes.Where(c => c.TeacherId == tenant.UserId);
            else if (Student) classes = classes.Where(c => db.ClassStudents.Any(m => m.ClassId == c.ClassId && m.StudentId == tenant.UserId));
            foreach (var item in await classes.Select(c => new { c.ClassId, c.ClassName }).ToArrayAsync(ct)) labels["Class:" + item.ClassId] = item.ClassName;
        }
        if (!Student && !Teacher) return labels;
        var assignmentIds = rows.Where(r => r.TargetType == "StudentAssignmentResult").Select(r => Guid.TryParse(r.TargetId.Split(':')[0], out var id) ? id : Guid.Empty).Where(id => id != Guid.Empty).Distinct().ToArray();
        if (assignmentIds.Length > 0)
        {
            var assignments = NotificationRecipientScope.WhereIds(db.Assignments.AsNoTracking(), a => a.AssignmentId, assignmentIds);
            assignments = Teacher ? assignments.Where(a => db.Classes.Any(c => c.ClassId == a.ClassId && c.TeacherId == tenant.UserId))
                : assignments.Where(a => db.StudentAssignmentProgresses.Any(p => p.AssignmentId == a.AssignmentId && p.StudentId == tenant.UserId));
            var titles = await assignments.ToDictionaryAsync(a => a.AssignmentId, a => a.Title, ct);
            foreach (var row in rows.Where(r => r.TargetType == "StudentAssignmentResult"))
                if (Guid.TryParse(row.TargetId.Split(':')[0], out var id) && titles.TryGetValue(id, out var title)) labels[row.TargetType + ":" + row.TargetId] = title;
        }
        var attemptIds = rows.Where(r => r.TargetType == "Attempt").Select(r => ulong.TryParse(r.TargetId, out var id) ? id : 0UL).Where(id => id > 0).Distinct().ToArray();
        if (attemptIds.Length > 0)
        {
            var attempts = db.Attempts.AsNoTracking().Where(a => attemptIds.Contains(a.AttemptId));
            attempts = Student ? attempts.Where(a => a.StudentId == tenant.UserId)
                : attempts.Where(a => a.AssignmentId != null && db.Assignments.Any(s => s.AssignmentId == a.AssignmentId && db.Classes.Any(c => c.ClassId == s.ClassId && c.TeacherId == tenant.UserId)));
            var items = await attempts.Select(a => new { a.AttemptId, Title = db.Assignments.Where(s => s.AssignmentId == a.AssignmentId).Select(s => s.Title).FirstOrDefault() }).ToArrayAsync(ct);
            foreach (var item in items) if (item.Title is { } title) labels["Attempt:" + item.AttemptId] = title;
        }
        return labels;
    }
    private static string? SafeLabel(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var label = PlatformAuditSanitizer.SanitizeReason(value);
        return label.Length > 160 ? label[..160] + "…" : label;
    }
    private static string? Score(decimal? value) => value is { } v ? v.ToString("0.##", CultureInfo.InvariantCulture) + " / 10 (điểm câu)" : null;
    private static string? Status(string? json)
    {
        try {
            using var document = JsonDocument.Parse(json ?? "null"); if (document.RootElement.ValueKind != JsonValueKind.Object) return null;
            if (document.RootElement.TryGetProperty("State", out var state) && state.ValueKind == JsonValueKind.String)
                return state.GetString() is "DeadLetter" or "Pending" ? state.GetString() : null;
            if (!document.RootElement.TryGetProperty("TeacherFinalReviewStatus", out var value)) return null;
            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
                return number == 1 ? "Approved" : number == 0 ? "Pending" : null;
            return value.ValueKind == JsonValueKind.String && value.GetString() is "Approved" or "Pending" ? value.GetString() : null;
        }
        catch (JsonException) { return null; }
    }
    private static string? Version(string? json)
    {
        try { using var document = JsonDocument.Parse(json ?? "null"); return document.RootElement.ValueKind == JsonValueKind.Object &&
            document.RootElement.TryGetProperty("FinalReviewVersion", out var value) && value.ValueKind == JsonValueKind.Number &&
            value.TryGetUInt32(out var version) ? version.ToString(CultureInfo.InvariantCulture) : null; }
        catch (JsonException) { return null; }
    }
}
