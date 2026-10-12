using System.Data;
using EduTwin.BLL.Assignments;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.Organization;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Organization;

/// <summary>Authorized, deterministic snapshot; no provider calls and no per-student SQL loop.</summary>
public sealed class ClassReportsUseCase(EduTwinDbContext db, ITenantContext tenant,
    IGetClassUseCase classAccess, IAssignmentResultCalculator calculator, TimeProvider clock)
{
    public async Task<ClassHistoryDto?> HistoryAsync(Guid classId, int page, CancellationToken ct)
    {
        if (page < 1 || !(await classAccess.ExecuteAsync(classId, ct)).IsSuccess) return null;
        var query = db.AuthorizationAuditLogs.AsNoTracking().Where(a => a.CenterId == tenant.CenterId &&
            a.TargetType == "Class" && a.TargetId == classId.ToString());
        var count = await query.CountAsync(ct);
        return new ClassHistoryDto { Page = page, TotalItems = count, TotalPages = (count + 19) / 20,
            Data = await query.OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.AuthorizationAuditId)
                .Skip((page - 1) * 20).Take(20).Select(a => new ClassHistoryItemDto {
                    ActionType = a.ActionType, ActorUserId = a.ActorUserId,
                    ActorName = a.ActorUserId == null ? "Hệ thống / Migration" : a.ActorUser != null ? a.ActorUser.DisplayName : "Tài khoản không còn tồn tại",
                    Source = a.ActorUserId == null ? "System/Migration" : "User",
                    Reason = a.Reason, BeforeData = a.BeforeData, AfterData = a.AfterData, CreatedAt = a.CreatedAt
                }).ToListAsync(ct) };
    }

    public async Task<ClassAcademicReportDto?> AcademicAsync(Guid classId, CancellationToken ct)
    {
        // A relational repeatable-read snapshot keeps roster, target and grade queries consistent.
        await using var tx = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(IsolationLevel.RepeatableRead, ct) : null;
        var access = await classAccess.ExecuteAsync(classId, ct);
        if (!access.IsSuccess) return null;
        var center = tenant.CenterId!.Value;
        var assignments = await db.Assignments.AsNoTracking().Where(a => a.CenterId == center && a.ClassId == classId &&
            !a.IsDeleted && a.Status != AssignmentStatus.Draft)
            .Select(a => new { a.AssignmentId, a.Title, a.DueAt }).ToListAsync(ct);
        var targets = await db.AssignmentTargets.AsNoTracking().Where(t => t.CenterId == center && t.Assignment != null &&
            t.Assignment.ClassId == classId && !t.Assignment.IsDeleted && t.Assignment.Status != AssignmentStatus.Draft)
            .Select(t => new { t.StudentId, t.AssignmentId }).ToListAsync(ct);
        var students = await db.Students.AsNoTracking().Where(s => s.CenterId == center && !s.IsDeleted &&
            (db.ClassStudents.Any(m => m.CenterId == center && m.ClassId == classId && m.StudentId == s.StudentId && m.Status == ClassStudentStatus.Active) ||
             db.AssignmentTargets.Any(t => t.CenterId == center && t.StudentId == s.StudentId && t.Assignment != null &&
                t.Assignment.ClassId == classId && !t.Assignment.IsDeleted && t.Assignment.Status != AssignmentStatus.Draft)))
            .OrderBy(s => s.FullName).ThenBy(s => s.StudentId).Select(s => new StudentDto {
                StudentId = s.StudentId, FullName = s.FullName, GradeLevel = s.GradeLevel,
                Username = s.User.Username, Status = s.User.Status.ToString(), RowVersion = s.RowVersion.ToString(),
                ActiveClassCount = db.ClassStudents.Count(m => m.CenterId == center && m.StudentId == s.StudentId && m.Status == ClassStudentStatus.Active && m.Class != null && m.Class.Status == ClassStatus.Active)
            }).ToListAsync(ct);
        var scores = await calculator.CalculateForRosterAsync(center, students.Select(s => s.StudentId).ToArray(),
            assignments.Select(a => a.AssignmentId).ToArray(), ct);
        var progress = await db.StudentAssignmentProgresses.AsNoTracking().Where(p => p.CenterId == center && !p.IsDeleted &&
            p.Assignment != null && p.Assignment.ClassId == classId && !p.Assignment.IsDeleted && p.Assignment.Status != AssignmentStatus.Draft)
            .Select(p => new { p.StudentId, p.AssignmentId, p.Status, p.CompletedAt }).ToListAsync(ct);
        var progressMap = progress.ToDictionary(p => (p.StudentId, p.AssignmentId));
        var targetsMap = targets.GroupBy(t => t.StudentId).ToDictionary(g => g.Key, g => g.Select(t => t.AssignmentId).ToHashSet());
        var now = clock.GetUtcNow().UtcDateTime;
        var report = new ClassAcademicReportDto { Class = access.Data!, GeneratedAt = now, TotalAssignments = assignments.Count };
        foreach (var student in students)
        {
            var summary = new StudentAcademicSummaryDto();
            foreach (var a in assignments.Where(a => targetsMap.GetValueOrDefault(student.StudentId)?.Contains(a.AssignmentId) == true))
            {
                var p = progressMap.GetValueOrDefault((student.StudentId, a.AssignmentId));
                var score = scores[(student.StudentId, a.AssignmentId)];
                var status = p?.Status ?? ProgressStatus.NotStarted;
                if (status != ProgressStatus.Completed && a.DueAt < now) status = ProgressStatus.Overdue;
                summary.Records.Add(new StudentAssignmentReportDto {
                    AssignmentId = a.AssignmentId, Title = a.Title, DueAt = a.DueAt, Status = status.ToString(),
                    SubjectId = Guid.Parse(access.Data!.Subject.SubjectId), SubjectName = access.Data.Subject.SubjectName,
                    CompletedQuestionCount = score.AnsweredQuestionCount, TotalQuestionCount = score.TotalQuestionCount,
                    Score = score.InternalAwardedScore, ResultStatus = score.ResultStatus,
                    FeedbackNote = score.FinalTeacherNote, SubmittedAt = p?.CompletedAt
                });
            }
            Summarize(summary);
            report.Students.Add(new ClassStudentReportDto { Student = student, Summary = summary });
        }
        if (tx != null) await tx.CommitAsync(ct);
        return report;
    }

    public static void Summarize(StudentAcademicSummaryDto summary)
    {
        summary.TotalAssigned = summary.Records.Count;
        summary.CompletedCount = summary.Records.Count(r => r.Status == "Completed");
        summary.InProgressCount = summary.Records.Count(r => r.Status == "InProgress");
        summary.NotStartedCount = summary.Records.Count(r => r.Status == "NotStarted");
        summary.OverdueCount = summary.Records.Count(r => r.Status == "Overdue");
        summary.CompletionRate = summary.TotalAssigned == 0 ? 0 : decimal.Round(100m * summary.CompletedCount / summary.TotalAssigned, 2);
        var finalScores = summary.Records.Where(r => r.ResultStatus == "Final" && r.Score.HasValue).Select(r => r.Score!.Value).ToList();
        summary.AverageScore = finalScores.Count == 0 ? null : decimal.Round(finalScores.Average(), 2);
        summary.MinScore = finalScores.Count == 0 ? null : finalScores.Min();
        summary.MaxScore = finalScores.Count == 0 ? null : finalScores.Max();
        summary.AssessmentStatus = summary.OverdueCount > 0 || summary.AverageScore < 5 ? "HighRisk" :
            summary.AverageScore >= 8 ? "Good" : summary.AverageScore.HasValue ? "Developing" :
            summary.CompletedCount > 0 ? "PendingGrading" : "Unknown";
    }
}
