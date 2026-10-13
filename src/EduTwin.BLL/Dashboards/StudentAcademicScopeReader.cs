using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.Dashboards;
using EduTwin.Contracts.Organization;
using EduTwin.BLL.Organization;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Dashboards;

public sealed record StudentAcademicScope(StudentAcademicContextDto Context, HashSet<ulong> TopicIds);

public sealed class StudentAcademicScopeReader(EduTwinDbContext db)
{
    public async Task<StudentAcademicScope?> ReadAsync(Guid center, Guid student, Guid? subject, Guid? classId, bool history, CancellationToken ct)
    {
        if (classId == Guid.Empty || (classId.HasValue && (!subject.HasValue || subject == Guid.Empty))) return null;
        if (!await db.Students.AnyAsync(s => s.CenterId == center && s.StudentId == student && !s.IsDeleted, ct)) return null;
        var memberships = await StudentClassScope.Memberships(db, center, student).AsNoTracking()
            .Where(m => !subject.HasValue || subject == Guid.Empty || m.SubjectId == subject).ToListAsync(ct);
        var context = new StudentAcademicContextDto { IsHistory = history,
            Classes = memberships.Select(c => new StudentAcademicClassDto { ClassId = c.ClassId, SubjectId = c.SubjectId,
                ClassName = c.ClassName, GradeLevel = c.GradeLevel, IsHistorical = c.IsHistorical })
                .OrderBy(c => c.IsHistorical).ThenBy(c => c.ClassName).ToList() };
        var eligible = memberships.Where(c => c.IsHistorical == history).ToList();
        if (classId.HasValue && !eligible.Any(c => c.ClassId == classId)) return null;
        if (subject.HasValue && subject != Guid.Empty)
        {
            var selected = classId ?? eligible.OrderByDescending(c => c.GradeLevel.HasValue)
                .ThenBy(c => c.ClassName).Select(c => (Guid?)c.ClassId).FirstOrDefault();
            context.SelectedClassId = selected;
            eligible = eligible.Where(c => c.ClassId == selected).ToList();
        }
        var topicIds = new HashSet<ulong>();
        foreach (var membership in eligible)
        {
            var applications = await db.ClassCurriculumApplications.AsNoTracking()
                .Where(a => a.CenterId == center && a.ClassId == membership.ClassId && !a.Curriculum.IsDeleted &&
                    (history ? a.Curriculum.ReviewStatus != ReviewStatus.Draft &&
                        (!membership.RemovedAt.HasValue || a.StartedAt <= membership.RemovedAt) &&
                        (!a.EndedAt.HasValue || a.EndedAt >= membership.JoinedAt)
                    : a.EndedAt == null && a.Curriculum.ReviewStatus == ReviewStatus.Published))
                .Select(a => new { a.CurriculumId, a.ClassId, a.Curriculum.Title, a.ApplicationRole }).Distinct().ToListAsync(ct);
            foreach (var application in applications)
            {
                context.Curriculums.Add(new() { CurriculumId = application.CurriculumId, ClassId = application.ClassId,
                    Title = application.Title, ApplicationRole = application.ApplicationRole });
                var ids = await db.CurriculumNodes.AsNoTracking().Where(n => n.CenterId == center && n.CurriculumId == application.CurriculumId &&
                    n.Node != null && n.Node.SubjectId == membership.SubjectId && n.Node.IsActive && !n.Node.IsDeleted)
                    .Select(n => n.NodeId).ToListAsync(ct);
                topicIds.UnionWith(ids);
            }
        }
        if (eligible.Count == 0) context.Message = history ? "Chưa có lịch sử lớp học trong phạm vi này. Chỉ lớp đã kết thúc hoặc lớp bạn đã rời khỏi mới xuất hiện ở đây." : "Chưa có lớp đang học trong phạm vi này. Bạn có thể chuyển sang Xem lịch sử.";
        else if (context.Curriculums.Count == 0) context.Message = "Lớp chưa có giáo trình đã xuất bản được áp dụng. Giáo viên phụ trách cần gán giáo trình; hệ thống không lấy toàn bộ đồ thị môn thay thế.";
        return new(context, topicIds);
    }
}
