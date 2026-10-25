using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.Organization;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.Recommendations;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Recommendations.Planning;

public sealed partial class LearningPathV4Planner
{
    private async Task RefreshScheduleAsync(Guid center, Guid student, Guid subject, Guid? classId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await StudentLockHelper.AcquireStudentLockAsync(db, center, student, ct);
        var path = await db.LearningPaths.Include(p => p.Sessions).Where(p => p.CenterId == center && p.StudentId == student &&
            p.SubjectId == subject && p.PlanSchemaVersion == "4.0" && p.Status == LearningPathStatus.Active && p.Sessions.Any(s => s.ClassId == classId))
            .OrderByDescending(p => p.Version).FirstOrDefaultAsync(ct);
        if (path?.PlanJson == null) return;
        var plan = JsonSerializer.Deserialize<PathProposalDto>(path.PlanJson.RootElement.GetRawText(), LearningPathV4Data.Json);
        if (plan == null || plan.IsPlacement) return;
        var goal = await db.LearningGoals.AsNoTracking().FirstOrDefaultAsync(g => g.CenterId == center && g.StudentId == student && g.GoalId == plan.GoalId, ct);
        if (goal == null) return;
        var settings = LearningAssessmentPolicy.Settings(goal.ScheduleSettings);
        var today = LearningPathCalendar.LocalDate(Now, settings.TimeZoneId);
        var exams = await ReadClassExamsAsync(center, student, subject, classId, ct);
        await ApplyClassExamFollowUpAsync(center, student, path, plan, exams, classId, ct);
        var fixedMinutes = exams.Where(e => e.Status == "Scheduled").GroupBy(e => LearningPathCalendar.LocalDate(e.ExamAt, settings.TimeZoneId))
            .ToDictionary(g => g.Key, g => (uint)g.Sum(e => e.TimeLimitMinutes));
        var openRuns = await db.LearningPathSessionRuns.Where(r => r.CenterId == center && r.StudentId == student &&
            r.Session.LearningPathId == path.LearningPathId && r.Status != SessionRunStatus.Abandoned).ToListAsync(ct);
        var definitions = plan.Milestones.SelectMany(m => m.Missions).ToDictionary(m => m.MissionId);
        fixedMinutes[today] = fixedMinutes.GetValueOrDefault(today) + (uint)openRuns.Where(r => r.Status == SessionRunStatus.Completed && r.CompletedAt.HasValue && LearningPathCalendar.LocalDate(r.CompletedAt.Value, settings.TimeZoneId) == today)
            .Sum(r => (long)(definitions.GetValueOrDefault(r.SessionId.ToString())?.EstimatedMinutes ?? 0));
        // Replanning changes only unstarted work; never abandon or duplicate a run that has submissions.
        var sequence = path.Sessions.Select(s => s.SessionOrder).DefaultIfEmpty(0u).Max();
        foreach (var milestone in plan.Milestones)
        foreach (var mission in milestone.Missions.ToList())
        {
            var session = path.Sessions.FirstOrDefault(s => s.SessionId.ToString() == mission.MissionId);
            if (session == null || session.Status != LearningPathSessionStatus.NotStarted || !mission.PlannedDate.HasValue ||
                mission.PlannedDate >= today || mission.IsMilestoneExam || openRuns.Any(r => r.SessionId == session.SessionId)) continue;
            var successor = JsonSerializer.Deserialize<ProposedMissionDto>(JsonSerializer.Serialize(mission, LearningPathV4Data.Json), LearningPathV4Data.Json)!;
            successor.MissionId = Guid.NewGuid().ToString(); successor.PlannedDate = null; successor.StudySlots.Clear(); successor.SkipReason = null;
            session.Status = LearningPathSessionStatus.Skipped; session.UpdatedAt = Now; session.RowVersion++;
            mission.SkipReason = "MissedStudyDate"; mission.CanStart = false;
            milestone.Missions.Add(successor);
            db.LearningPathSessions.Add(new LearningPathSession { SessionId = Guid.Parse(successor.MissionId), CenterId = center,
                LearningPathId = path.LearningPathId, TopicNodeId = session.TopicNodeId, ClassId = classId, Title = successor.Title,
                Objective = successor.Purpose, Rationale = "Xếp lại phần chưa học từ hôm nay; không tăng trần phút/ngày.",
                PhaseNumber = milestone.Order, SessionOrder = ++sequence, MinimumQuestions = session.MinimumQuestions,
                RequiredCorrectRate = session.RequiredCorrectRate, PolicyVersion = LearningPathV4Data.Policy,
                CreatedAt = Now, UpdatedAt = Now, RowVersion = 1 });
            db.PathDecisions.Add(new PathDecision { CenterId = center, LearningPathId = path.LearningPathId,
                TriggerType = $"MissedStudyDate:{mission.MissionId}", DecisionSummary = $"Bỏ lượt dự kiến {mission.PlannedDate:dd/MM/yyyy}: {mission.Title}. Nội dung chưa học được xếp lại, không tính hoàn thành.",
                PolicyVersion = LearningPathV4Data.Policy, ReasonCodes = JsonSerializer.SerializeToDocument(new[] { "MISSED_STUDY_DATE" }),
                CreatedAt = Now, UpdatedAt = Now, RowVersion = 1 });
        }
        plan.Schedule = settings; plan.TargetScore = goal.TargetScore; plan.DeadlineDate = goal.DeadlineDate;
        plan.AvailableMinutesPerDay = goal.AvailableMinutesPerDay; plan.AvailableDaysPerWeek = goal.AvailableDaysPerWeek;
        var closed = path.Sessions.Where(s => s.Status is LearningPathSessionStatus.Completed or LearningPathSessionStatus.Skipped)
            .Select(s => s.SessionId.ToString()).ToHashSet();
        // Work in progress has its own timer and saved state; preserve it and exclude it from new date allocations.
        closed.UnionWith(openRuns.Where(r => r.Status != SessionRunStatus.Completed).Select(r => r.SessionId.ToString()));
        LearningPathCalendar.Schedule(plan, Now, closed, fixedMinutes);
        plan.ScheduleSummary!.SourceGoalVersion = goal.RowVersion.ToString();
        foreach (var mission in plan.Milestones.SelectMany(m => m.Missions))
        {
            var session = db.LearningPathSessions.Local.FirstOrDefault(s => s.SessionId.ToString() == mission.MissionId);
            if (session == null || session.Status != LearningPathSessionStatus.NotStarted) continue;
            session.PlannedDate = mission.PlannedDate;
            session.WeekNumber = mission.PlannedDate.HasValue ? (uint)System.Globalization.ISOWeek.GetWeekOfYear(mission.PlannedDate.Value.ToDateTime(TimeOnly.MinValue)) : 0;
        }
        var serialized = JsonSerializer.Serialize(plan, LearningPathV4Data.Json);
        if (serialized != path.PlanJson.RootElement.GetRawText())
        {
            path.PlanJson = JsonDocument.Parse(serialized); path.UpdatedAt = Now; path.RowVersion++;
            await StageScheduleWarningAsync(center, student, goal, path.LearningPathId, plan, ct);
            await db.SaveChangesAsync(ct);
        }
        await tx.CommitAsync(ct);
    }

    private async Task StageScheduleWarningAsync(Guid center, Guid student, LearningGoal goal, Guid pathId, PathProposalDto plan, CancellationToken ct)
    {
        if (notices == null || plan.ScheduleSummary is not { } summary || summary.MissingMinutes == 0 && summary.DeferredSkillIds.Count == 0) return;
        var names = plan.Milestones.Where(m => m.SkillIds.Any(summary.DeferredSkillIds.Contains)).Select(m => m.Title).Distinct();
        var body = summary.Message + " Phần bị hoãn: " + string.Join(", ", names);
        if (body.Length > 1900) body = body[..1900];
        var recipients = new List<Guid> { student };
        if (goal.ClassId.HasValue)
        {
            var teacher = await db.ClassStudents.Where(m => m.CenterId == center && m.StudentId == student && m.ClassId == goal.ClassId &&
                m.Status == ClassStudentStatus.Active && m.RemovedAt == null && m.Class.Status == ClassStatus.Active)
                .Select(m => (Guid?)m.Class.TeacherId).FirstOrDefaultAsync(ct);
            if (teacher.HasValue && await db.Users.AnyAsync(u => u.CenterId == center && u.UserId == teacher && u.Status == Contracts.IdentityAndTenancy.UserStatus.Active, ct)) recipients.Add(teacher.Value);
        }
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(body + string.Join(",", recipients.Order()))))[..20];
        await notices.StageNoticeAsync($"PathBudget:{pathId:N}:{fingerprint}", "Lộ trình cần điều chỉnh thời gian hoặc phạm vi", body, recipients, ct);
    }

    private async Task PopulateForecastAsync(LearningAgendaDto agenda, Guid center, Guid student, Guid subject, Guid? classId, CancellationToken ct)
    {
        var forecast = await LearningGoalForecast.ReadAsync(db, center, student, subject, classId, Now, agenda.TargetSkillIds, ct);
        agenda.PredictedScore = forecast.Score;
        agenda.ForecastMessage = forecast.Message
            + (agenda.TargetScore.HasValue && agenda.PredictedScore < agenda.TargetScore ? $" Đang dưới mức mục tiêu {agenda.TargetScore}/10; nên ưu tiên các phần trọng số cao còn yếu." : "");
    }
}
