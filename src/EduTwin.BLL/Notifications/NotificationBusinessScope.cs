using System.Text.Json;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.Notifications;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Notifications;

internal static class NotificationBusinessScope
{
    public static async Task<Dictionary<Guid, string>> LinksAsync(EduTwinDbContext db, IEnumerable<NotificationEvent> events, ITenantContext tenant, CancellationToken ct)
    {
        var contexts = events.Where(e => e.ResourceContext != null).Select(e => (e.EventId, Context: NotificationBusinessContextSerializer.Read(e.ResourceContext)))
            .Where(x => x.Context != null).ToArray();
        var links = new Dictionary<Guid, string>();
        if (contexts.Length == 0 || tenant.UserId is not { } userId) return links;
        var permissions = (await new AuthorizationSnapshotReader(db).ReadForUserAsync(userId, ct)).Permissions.ToHashSet(StringComparer.Ordinal);
        var classIds = contexts.Where(x => x.Context!.ClassId != null).Select(x => x.Context!.ClassId!.Value).Distinct().ToArray();
        var classes = await NotificationRecipientScope.WhereIds(db.Classes.AsNoTracking(), c => c.ClassId, classIds).ToDictionaryAsync(c => c.ClassId, ct);
        var membership = tenant.Role == nameof(UserRole.Student) ? await NotificationRecipientScope.WhereIds(db.ClassStudents.AsNoTracking(), m => m.ClassId, classIds)
            .Where(m => m.StudentId == userId).ToArrayAsync(ct) : [];
        var assignmentIds = contexts.Where(x => x.Context!.AssignmentId != null).Select(x => x.Context!.AssignmentId!.Value).Distinct().ToArray();
        var targets = tenant.Role == nameof(UserRole.Student) ? await NotificationRecipientScope.WhereIds(db.AssignmentTargets.AsNoTracking(), t => t.AssignmentId, assignmentIds)
            .Where(t => t.StudentId == userId).Select(t => t.AssignmentId).ToArrayAsync(ct) : [];
        var messageIds = contexts.Where(x => x.Context!.MessageId != null).Select(x => x.Context!.MessageId!.Value).Distinct().ToArray();
        var sent = await NotificationRecipientScope.WhereIds(db.NotificationMessages.AsNoTracking(), m => m.MessageId, messageIds).Where(m => m.SenderUserId == userId).Select(m => m.MessageId).ToArrayAsync(ct);
        foreach (var (eventId, context) in contexts)
        {
            if (context!.Kind == "SentReply" && context.MessageId is { } mid && sent.Contains(mid) && permissions.Contains(tenant.Role == nameof(UserRole.PlatformAdmin) ? "platform.notifications.read_sent_own" : "notifications.messages.read_sent_own"))
            { links[eventId] = (tenant.Role == nameof(UserRole.Teacher) ? "/giao-vien/thong-bao" : tenant.Role == nameof(UserRole.CenterManager) ? "/quan-ly/thong-bao" : "/quan-tri-nen-tang/thong-bao") + $"?sent={mid:D}"; continue; }
            if (context.ClassId is not { } cid || !classes.TryGetValue(cid, out var c)) continue;
            if (tenant.Role == nameof(UserRole.CenterManager) && permissions.Contains("organization.classes.read")) links[eventId] = $"/quan-ly/lop-hoc/{cid:D}/tong-quan";
            else if (tenant.Role == nameof(UserRole.Teacher) && c.TeacherId == userId &&
                permissions.Contains(context.AssignmentId != null ? "assignments.assignments.read" : "organization.classes.read"))
                links[eventId] = context.AssignmentId is { } aid ? $"/giao-vien/bai-tap/{aid:D}/tien-do" : "/giao-vien/lop-hoc";
            else if (tenant.Role == nameof(UserRole.Student) && permissions.Contains("assignments.assignments.read") && membership.Any(m => m.ClassId == cid) && context.AssignmentId is { } aid && targets.Contains(aid))
            {
                var member = membership.Single(m => m.ClassId == cid); var history = c.Status != ClassStatus.Active || c.LearningScope != ClassLearningScope.Current || member.Status != ClassStudentStatus.Active || member.RemovedAt != null;
                links[eventId] = $"/hoc-tap/bai-tap/{aid:D}?classId={cid:D}&history={history.ToString().ToLowerInvariant()}&subjectId={c.SubjectId:D}";
            }
        }
        return links;
    }
    public static async Task<HashSet<Guid>> RevalidateAsync(EduTwinDbContext db, NotificationEvent envelope, CancellationToken ct)
    {
        var ids = JsonSerializer.Deserialize<Guid[]>(envelope.RecipientUserIds) ?? [];
        var context = NotificationBusinessContextSerializer.Read(envelope.ResourceContext);
        if (context is null || context.Kind is "Administrative" or "ClassLifecycle" or "SentReply") return ids.ToHashSet();
        if (envelope.EventType == "AssignmentAiReady" && (context.AssignmentId is not { } aiAssignment || context.StudentId is not { } aiStudent ||
            context.Version != await NotificationAiCompletion.CurrentGenerationAsync(db, aiAssignment, aiStudent, ct))) return [];
        var @class = context.ClassId is { } classId ? await db.Classes.AsNoTracking().SingleOrDefaultAsync(c => c.ClassId == classId, ct) : null;
        if (@class is null) return [];
        if (context.Kind == "TeacherWork") return await db.Users.AsNoTracking().Where(NotificationRecipientScope.Matches(ids))
            .Where(u => u.UserId == @class.TeacherId && u.RoleName == UserRole.Teacher).Select(u => u.UserId).ToHashSetAsync(ct);
        var assignment = context.AssignmentId is { } assignmentId ? await db.Assignments.AsNoTracking().SingleOrDefaultAsync(a => a.AssignmentId == assignmentId, ct) : null;
        if (assignment is null) return [];
        if (envelope.EventType is "AssignmentResultApproved" or "AssignmentResultReopened")
        {
            if (context.StudentId is not { } resultStudent || !uint.TryParse(context.Version, out var resultVersion) ||
                !await db.StudentAssignmentProgresses.AsNoTracking().AnyAsync(p => p.AssignmentId == assignment.AssignmentId &&
                    p.StudentId == resultStudent && p.FinalReviewVersion == resultVersion, ct)) return [];
        }
        var learning = context.Kind is "AssignmentPublished" or "AssignmentUpdated";
        if (learning && (assignment.Status != AssignmentStatus.Published || @class.Status != ClassStatus.Active || @class.LearningScope != ClassLearningScope.Current)) return [];
        var query = db.AssignmentTargets.AsNoTracking().Where(t => t.AssignmentId == assignment.AssignmentId &&
            db.Users.Any(u => u.UserId == t.StudentId && u.RoleName == UserRole.Student) &&
            db.ClassStudents.Any(m => m.ClassId == @class.ClassId && m.StudentId == t.StudentId && (!learning || m.Status == ClassStudentStatus.Active && m.RemovedAt == null)));
        if (context.StudentId is { } studentId) query = query.Where(t => t.StudentId == studentId);
        var targets = await query.Select(t => t.StudentId).ToArrayAsync(ct);
        return ids.Intersect(targets).ToHashSet();
    }

}
