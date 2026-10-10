using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Organization;

public sealed class LearningScopeDeniedException(string message) : InvalidOperationException(message);

public sealed record StudentLearningScopeResult(bool Allowed, HashSet<ulong>? TopicIds, string? Reason)
{
    public bool Includes(ulong topicId) => Allowed && (TopicIds is null || TopicIds.Contains(topicId));
}

public static class StudentLearningScope
{
    public const string ReadOnlyReason = "Lớp đã lưu trữ hoặc bạn đã rời lớp. Chỉ được xem lại bài làm; hãy chuyển sang lớp đang học để tiếp tục.";

    public static async Task<StudentLearningScopeResult> ResolveAsync(EduTwinDbContext db, Guid center, Guid student,
        Guid subject, Guid? classId, bool history, CancellationToken ct)
    {
        if (history || classId == Guid.Empty) return new(false, null, ReadOnlyReason);
        var memberships = await StudentClassScope.Memberships(db, center, student).AsNoTracking()
            .Where(m => m.SubjectId == subject).ToListAsync(ct);
        var active = memberships.Where(m => !m.IsHistorical && (!classId.HasValue || m.ClassId == classId)).ToList();
        if (active.Count == 0)
        {
            if (classId.HasValue || memberships.Count > 0) return new(false, null, ReadOnlyReason);
            // Preserve independent practice for students with no class history in this subject.
            // An archived/removed membership must never silently turn into independent practice.
            return new(true, null, null);
        }
        // Keep GUID membership sets as SQL subqueries. The MySQL EF provider cannot
        // map parameterized List<Guid>.Contains reliably for char(36) identifiers.
        var classIds = StudentClassScope.Memberships(db, center, student).Where(m => m.SubjectId == subject &&
            !m.IsHistorical && (!classId.HasValue || m.ClassId == classId)).Select(m => m.ClassId);
        var ledger = db.ClassCurriculumApplications.AsNoTracking().Where(a => a.CenterId == center && classIds.Contains(a.ClassId));
        var hasLedger = await ledger.AnyAsync(ct);
        var curriculumIds = ledger.Where(a => a.EndedAt == null && !a.Curriculum.IsDeleted &&
            a.Curriculum.SubjectId == subject && a.Curriculum.ReviewStatus == ReviewStatus.Published)
            .Select(a => a.CurriculumId).Distinct();
        if (!hasLedger) curriculumIds = db.CurriculumClasses.AsNoTracking()
            .Where(a => a.CenterId == center && classIds.Contains(a.ClassId) && a.Curriculum != null &&
                !a.Curriculum.IsDeleted && a.Curriculum.SubjectId == subject && a.Curriculum.ReviewStatus == ReviewStatus.Published)
            .Select(a => a.CurriculumId).Distinct();
        var topicIds = (await db.CurriculumNodes.AsNoTracking().Where(n => n.CenterId == center &&
            curriculumIds.Contains(n.CurriculumId) && n.Node != null && n.Node.SubjectId == subject && n.Node.IsActive && !n.Node.IsDeleted)
            .Select(n => n.NodeId).Distinct().ToListAsync(ct)).ToHashSet();
        return topicIds.Count == 0
            ? new(false, topicIds, "Lớp đang học chưa có giáo trình đã xuất bản được áp dụng. Không chuyển sang luyện toàn môn thay thế.")
            : new(true, topicIds, null);
    }
}
