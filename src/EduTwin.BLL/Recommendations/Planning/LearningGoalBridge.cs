using EduTwin.BLL.DigitalTwin;
using EduTwin.DAL.DigitalTwin;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Recommendations;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Recommendations.Planning;

/// <summary>LearningGoal owns the accepted objective. The former Twin record remains a compatibility/risk projection.</summary>
public static class LearningGoalBridge
{
    public static async Task StageFromTwinAsync(EduTwinDbContext db, StudentSubjectGoal twin, DateTime now, CancellationToken ct)
    {
        var goal = db.LearningGoals.Local.FirstOrDefault(g => g.CenterId == twin.CenterId && g.StudentId == twin.StudentId &&
            g.SubjectId == twin.SubjectId && g.Status == "Active") ?? await db.LearningGoals.Where(g => g.CenterId == twin.CenterId &&
            g.StudentId == twin.StudentId && g.SubjectId == twin.SubjectId && g.Status == "Active").OrderByDescending(g => g.CreatedAt).FirstOrDefaultAsync(ct);
        if (goal == null)
        {
            goal = new LearningGoal { GoalId = Guid.NewGuid(), CenterId = twin.CenterId, StudentId = twin.StudentId,
                SubjectId = twin.SubjectId, GoalType = "ExamPrep", Title = "Mục tiêu môn học", Status = "Active", CreatedAt = now, UpdatedAt = now };
            db.LearningGoals.Add(goal);
        }
        var settings = LearningAssessmentPolicy.Settings(goal.ScheduleSettings);
        var local = LearningPathCalendar.LocalDate(now, settings.TimeZoneId);
        var localEnd = local.AddDays((int)twin.RemainingDays).ToDateTime(new TimeOnly(23, 59, 59));
        goal.TargetScore = twin.TargetScore;
        goal.DeadlineDate = TimeZoneInfo.ConvertTimeToUtc(localEnd, LearningPathCalendar.Zone(settings.TimeZoneId));
        goal.UpdatedAt = now; goal.UpdatedBy = twin.UpdatedBy; goal.RowVersion++;
    }

    public static async Task StageProjectionAsync(EduTwinDbContext db, LearningGoal goal, DateTime now, CancellationToken ct)
    {
        if (goal.TargetScore == null) return;
        var twin = await db.StudentSubjectGoals.FirstOrDefaultAsync(g => g.CenterId == goal.CenterId && g.StudentId == goal.StudentId && g.SubjectId == goal.SubjectId, ct);
        if (twin == null)
        {
            twin = new StudentSubjectGoal { GoalId = new CryptographicGoalIdGenerator().GenerateId(), CenterId = goal.CenterId,
                StudentId = goal.StudentId, SubjectId = goal.SubjectId, CreatedAt = now, RowVersion = 1 };
            db.StudentSubjectGoals.Add(twin);
        }
        twin.TargetScore = goal.TargetScore.Value;
        twin.RemainingDays = RemainingDays(goal, now);
        twin.RiskScore = StudentSubjectGoalRiskCalculator.CalculateRisk(twin.TargetScore, twin.CurrentPredictedScore, (int)twin.RemainingDays);
        twin.UpdatedAt = now; twin.RowVersion++;
    }

    public static uint RemainingDays(LearningGoal goal, DateTime now)
    {
        var zone = LearningAssessmentPolicy.Settings(goal.ScheduleSettings).TimeZoneId;
        return goal.DeadlineDate.HasValue ? (uint)Math.Max(0, LearningPathCalendar.LocalDate(goal.DeadlineDate.Value, zone).DayNumber - LearningPathCalendar.LocalDate(now, zone).DayNumber) : 0;
    }
}
