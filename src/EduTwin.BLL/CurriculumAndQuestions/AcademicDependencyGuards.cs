using System.Text.Json;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.CurriculumAndQuestions;

/// <summary>Dependency queries must cover the whole tenant, not just the author's classes.</summary>
public static class AcademicDependencyGuards
{
    public sealed record ActiveClassUsage(Guid ClassId, string ClassName);

    public static async Task<List<ActiveClassUsage>> ActiveClassesAsync(EduTwinDbContext db, Guid center,
        IQueryable<Guid> curriculumIds, CancellationToken ct)
    {
        var classes = db.Classes.AsNoTracking().Where(c => c.CenterId == center && !c.IsDeleted &&
            c.Status == ClassStatus.Active && c.LearningScope == ClassLearningScope.Current);
        var applied = await classes.Where(c => db.ClassCurriculumApplications.Any(a => a.CenterId == center &&
            a.ClassId == c.ClassId && a.EndedAt == null && curriculumIds.Contains(a.CurriculumId)))
            .Select(c => new ActiveClassUsage(c.ClassId, c.ClassName)).ToListAsync(ct);
        // Compatibility only for classes with no ledger; never resurrect an ended application.
        var legacy = await classes.Where(c => !db.ClassCurriculumApplications.Any(a => a.CenterId == center && a.ClassId == c.ClassId) &&
            db.CurriculumClasses.Any(a => a.CenterId == center && a.ClassId == c.ClassId && curriculumIds.Contains(a.CurriculumId) &&
                a.Curriculum != null && a.Curriculum.ReviewStatus == ReviewStatus.Published))
            .Select(c => new ActiveClassUsage(c.ClassId, c.ClassName)).ToListAsync(ct);
        return applied.Concat(legacy).DistinctBy(c => c.ClassId).OrderBy(c => c.ClassName).ThenBy(c => c.ClassId).ToList();
    }

    public static Task<List<ActiveClassUsage>> CurriculumUsageAsync(EduTwinDbContext db, Guid center, Guid curriculum, CancellationToken ct)
        => ActiveClassesAsync(db, center, db.Curriculums.Where(c => c.CenterId == center && c.CurriculumId == curriculum).Select(c => c.CurriculumId), ct);

    public static Task<List<ActiveClassUsage>> NodeUsageAsync(EduTwinDbContext db, Guid center, ulong first, ulong second, CancellationToken ct)
        => ActiveClassesAsync(db, center, db.CurriculumNodes.Where(n => n.CenterId == center && (n.NodeId == first || n.NodeId == second))
            .Select(n => n.CurriculumId).Distinct(), ct);

    public static string ClassBlockMessage(IEnumerable<ActiveClassUsage> usages)
        => "Đang được sử dụng bởi lớp hoạt động: " + string.Join(", ", usages.Take(10).Select(c => c.ClassName)) +
           ". Hãy thay/ngừng áp dụng giáo trình có lý do trước; thao tác này không tự ngắt lớp đang học.";

    public static async Task<string?> FrozenNodeMessageAsync(EduTwinDbContext db, Guid center, ulong node, CancellationToken ct)
    {
        var titles = await db.CurriculumNodes.Where(n => n.CenterId == center && n.NodeId == node && n.Curriculum != null &&
            (n.Curriculum.ReviewStatus == ReviewStatus.Published || n.Curriculum.ReviewStatus == ReviewStatus.Archived))
            .Select(n => n.Curriculum!.Title).Distinct().OrderBy(t => t).Take(10).ToListAsync(ct);
        return titles.Count == 0 ? null : "Không thể đổi nội dung hoặc nút cha của chủ đề thuộc giáo trình đã xuất bản/lưu trữ: " +
            string.Join(", ", titles) + ". Hãy tạo nút mới và giáo trình nháp mới; không ghi đè nội dung lịch sử.";
    }

    public static async Task<string?> DeactivationMessageAsync(EduTwinDbContext db, Guid center, ulong node, CancellationToken ct)
    {
        var classes = await NodeUsageAsync(db, center, node, node, ct);
        if (classes.Count > 0) return ClassBlockMessage(classes);
        if (await db.KnowledgeNodes.AnyAsync(n => n.CenterId == center && n.ParentNodeId == node && n.IsActive && !n.IsDeleted, ct))
            return "Không thể tắt chủ đề còn nút con đang hoạt động. Hãy xử lý các nút con trước.";
        if (await db.Questions.AnyAsync(q => q.CenterId == center && !q.IsDeleted && q.Status == QuestionStatus.Active &&
            (q.PrimaryTopicNodeId == node || db.QuestionKnowledgeNodes.Any(n => n.CenterId == center && n.QuestionId == q.QuestionId && n.NodeId == node)), ct))
            return "Không thể tắt chủ đề còn câu hỏi đã xuất bản. Hãy lưu trữ/thay chủ đề của các câu hỏi trước.";
        if (await db.LearningPathItems.AnyAsync(i => i.CenterId == center && i.TopicNodeId == node && !i.IsDeleted &&
            i.LearningPath.Status == Contracts.Recommendations.LearningPathStatus.Active && !i.LearningPath.IsDeleted &&
            (!db.ClassStudents.Any(m => m.CenterId == center && m.StudentId == i.LearningPath.StudentId && m.Class.SubjectId == i.LearningPath.SubjectId) ||
             db.ClassStudents.Any(m => m.CenterId == center && m.StudentId == i.LearningPath.StudentId && m.Status == ClassStudentStatus.Active &&
                m.Class.SubjectId == i.LearningPath.SubjectId && m.Class.Status == ClassStatus.Active && m.Class.LearningScope == ClassLearningScope.Current && !m.Class.IsDeleted)), ct))
            return "Không thể tắt chủ đề còn trong lộ trình đang học. Hãy thay/kết thúc lộ trình trước.";
        return null;
    }

    public static void Audit(EduTwinDbContext db, Guid center, Guid actor, string action, string targetType,
        string targetId, object? before, object after, DateTime now, string reason)
        => db.AuthorizationAuditLogs.Add(new AuthorizationAuditLog {
            CenterId = center, ActorUserId = actor, CreatedBy = actor, ActionType = action,
            TargetType = targetType, TargetId = targetId, BeforeData = before is null ? null : JsonSerializer.Serialize(before),
            AfterData = JsonSerializer.Serialize(after), Reason = reason, CreatedAt = now,
            TraceId = System.Diagnostics.Activity.Current?.Id ?? $"academic:{Guid.NewGuid():N}"
        });

    public static bool IsDatabaseGuard(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
            if (current.Message.Contains("EDUTWIN_ACADEMIC_DEPENDENCY:", StringComparison.Ordinal)) return true;
        return false;
    }

    public const string ConcurrentDependencyMessage = "Phụ thuộc học thuật vừa thay đổi hoặc vẫn đang sử dụng. Thao tác đã bị chặn; hãy tải lại để xem phạm vi mới nhất.";
}
