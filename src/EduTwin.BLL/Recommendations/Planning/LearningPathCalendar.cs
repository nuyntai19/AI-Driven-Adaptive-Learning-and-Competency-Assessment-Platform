using EduTwin.Contracts.Recommendations;

namespace EduTwin.BLL.Recommendations.Planning;

/// <summary>Allocates real local study days. Slots describe portions of pausable work, never permission to exceed the daily budget.</summary>
public static class LearningPathCalendar
{
    public static TimeZoneInfo Zone(string id)
    {
        try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
        catch (TimeZoneNotFoundException) { throw new ArgumentException("Múi giờ học không hợp lệ."); }
        catch (InvalidTimeZoneException) { throw new ArgumentException("Múi giờ học không hợp lệ."); }
    }

    public static DateOnly LocalDate(DateTime utc, string zone) => DateOnly.FromDateTime(
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone(zone)));

    public static void Validate(LearningScheduleSettings settings, uint days)
    {
        if (settings == null || settings.StudyDays == null || settings.ExcludedDates == null ||
            settings.StudyDays.Distinct().Count() != settings.StudyDays.Count || settings.StudyDays.Any(d => d is < 0 or > 6) ||
            settings.StudyDays.Count > 0 && settings.StudyDays.Count != days || settings.ExcludedDates.Count > 366 ||
            settings.ReviewReservePercent > 40 || settings.ShortCheckDays > 365 || settings.MockExamDays > settings.ShortCheckDays)
            throw new ArgumentException("Ngày học, dự phòng ôn hoặc mốc kiểm tra không hợp lệ.");
        Zone(settings.TimeZoneId);
    }

    public static List<DateOnly> Days(DateOnly from, DateOnly through, LearningScheduleSettings settings, uint daysPerWeek)
    {
        var days = (settings.StudyDays.Count > 0 ? settings.StudyDays : new[] { 1, 2, 3, 4, 5, 6, 0 }.Take((int)daysPerWeek)).ToHashSet();
        var excluded = settings.ExcludedDates.ToHashSet();
        var result = new List<DateOnly>();
        // Bound all calendar walks, including malformed historical deadlines.
        for (var day = from; day <= through && day.DayNumber - from.DayNumber <= 3650; day = day.AddDays(1))
            if (days.Contains((int)day.DayOfWeek) && !excluded.Contains(day)) result.Add(day);
        return result;
    }

    public static uint Work(ProposedMissionDto mission, uint practiceSize) => mission.EstimatedMinutes +
        (mission.Purpose == "Practice" && mission.EstimatedSets > 1 ? (mission.EstimatedSets - 1) * practiceSize * LearningPathPacing.MinutesPerQuestion : 0);

    public static void Schedule(PathProposalDto plan, DateTime now, IReadOnlySet<string>? completed = null,
        IReadOnlyDictionary<DateOnly, uint>? fixedMinutes = null)
    {
        var today = LocalDate(now, plan.Schedule.TimeZoneId);
        var deadline = plan.DeadlineDate.HasValue ? LocalDate(plan.DeadlineDate.Value, plan.Schedule.TimeZoneId) : (DateOnly?)null;
        var end = deadline ?? today.AddDays(365);
        var days = Days(today, end, plan.Schedule, plan.AvailableDaysPerWeek);
        var budgets = days.ToDictionary(d => d, d => plan.AvailableMinutesPerDay > (fixedMinutes?.GetValueOrDefault(d) ?? 0)
            ? plan.AvailableMinutesPerDay - (fixedMinutes?.GetValueOrDefault(d) ?? 0) : 0);
        var capacity = (uint)budgets.Values.Sum(v => (long)v);
        var all = plan.Milestones.SelectMany(m => m.Missions).Where(m => completed == null || !completed.Contains(m.MissionId)).ToList();
        var estimatedExtra = plan.Milestones.SelectMany(m => m.Missions).Where(m => m.Purpose == "Practice" && m.SkipReason == null)
            .GroupBy(m => m.SkillId).ToDictionary(g => g.Key, g => Math.Max(0, (int)g.Max(m => m.EstimatedSets) - (int)Math.Max((uint)g.Count(), g.Max(m => m.SetNumber))));
        var extraFor = all.Where(m => m.Purpose == "Practice" && m.SkipReason == null).GroupBy(m => m.SkillId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(m => m.SetNumber).First().MissionId);
        uint Cost(ProposedMissionDto m) => m.EstimatedMinutes + (m.Purpose == "Practice" && extraFor.GetValueOrDefault(m.SkillId) == m.MissionId
            ? (uint)estimatedExtra.GetValueOrDefault(m.SkillId) * plan.PracticeQuestionsBySkill.GetValueOrDefault(m.SkillId, plan.PracticeQuestionsPerSet) * 2u : 0u);
        var required = (uint)all.Sum(m => (long)Cost(m));
        // Placement is a baseline, not an exam preparation calendar.
        var reserve = plan.IsPlacement || deadline == null ? 0u : capacity * plan.Schedule.ReviewReservePercent / 100;
        var reserveBudget = reserve;
        var finalDays = new Dictionary<DateOnly, uint>();
        foreach (var day in days.AsEnumerable().Reverse())
        {
            var take = Math.Min(budgets[day], reserveBudget);
            finalDays[day] = take; budgets[day] -= take; reserveBudget -= take;
            if (reserveBudget == 0) break;
        }
        var summary = new LearningScheduleSummaryDto { CalculatedFrom = today, Deadline = deadline,
            CapacityMinutes = capacity, RequiredMinutes = required, ReserveMinutes = reserve,
            Phase = plan.GoalType != "ExamPrep" || deadline == null ? "Balanced" : deadline.Value.DayNumber - today.DayNumber <= plan.Schedule.MockExamDays ? "MockExam"
                : deadline.Value.DayNumber - today.DayNumber <= plan.Schedule.ShortCheckDays ? "ShortChecks" : "Balanced" };
        DateOnly? Allocate(ProposedMissionDto mission, Dictionary<DateOnly, uint> room, uint cost, DateOnly earliest)
        {
            var eligible = days.Where(d => d >= earliest && (!mission.AvailableFrom.HasValue || d >= LocalDate(mission.AvailableFrom.Value, plan.Schedule.TimeZoneId))).ToList();
            var available = (uint)eligible.Sum(d => (long)room.GetValueOrDefault(d));
            if (available == 0 || mission.IsMilestoneExam && available < cost) return null;
            if (mission.IsMilestoneExam)
            {
                var examDay = eligible.FirstOrDefault(d => room.GetValueOrDefault(d) >= cost);
                if (examDay == default) return null;
                mission.StudySlots.Add(new() { Date = examDay, Minutes = cost }); room[examDay] -= cost;
                return examDay;
            }
            cost = Math.Min(cost, available);
            foreach (var day in eligible)
            {
                var take = Math.Min(cost, room.GetValueOrDefault(day));
                if (take == 0) continue;
                mission.StudySlots.Add(new() { Date = day, Minutes = take });
                room[day] -= take; cost -= take;
                if (cost == 0) break;
            }
            return mission.StudySlots.FirstOrDefault()?.Date;
        }
        // All missions for a skill are allocated together. An incomplete prerequisite defers its dependants too.
        var deferred = new HashSet<string>();
        var partial = new HashSet<string>();
        var phaseDeferred = new HashSet<string>();
        var lastDay = new Dictionary<string, DateOnly>();
        foreach (var mission in all) { mission.PlannedDate = null; mission.StudySlots.Clear(); mission.Deferred = false; mission.RemainingUnscheduledMinutes = 0; }
        // The final mock gets its reserved slot first; regular stage checks remain between prerequisite and dependent study.
        foreach (var mock in all.Where(m => m.IsMilestoneExam && m.IsProbe && m.CanStart))
        {
            var cost = Cost(mock);
            // Spend the mock portion of the reserve early enough to leave time for repair. Release the same amount at the end;
            // this swaps capacity rather than charging the reserve twice.
            var release = Math.Min(cost, (uint)finalDays.Values.Sum(v => (long)v));
            foreach (var day in days.AsEnumerable().Reverse())
            {
                var amount = Math.Min(release, finalDays.GetValueOrDefault(day));
                finalDays[day] = finalDays.GetValueOrDefault(day) - amount; budgets[day] += amount; release -= amount;
                if (release == 0) break;
            }
            mock.PlannedDate = Allocate(mock, budgets, cost, today);
            if (mock.PlannedDate == null) { mock.Deferred = true; summary.MissingMinutes += cost; deferred.Add(mock.SkillId); }
        }
        foreach (var milestone in plan.Milestones)
        {
            var earliest = milestone.PrerequisiteSkillIds.Select(id => lastDay.GetValueOrDefault(id, today)).DefaultIfEmpty(today).Max();
            var blocked = milestone.PrerequisiteSkillIds.Any(id => deferred.Contains(id) || partial.Contains(id));
            foreach (var mission in milestone.Missions.Where(m => (completed == null || !completed.Contains(m.MissionId)) && !(m.IsMilestoneExam && m.IsProbe)))
            {
                mission.PlannedDate = null; mission.StudySlots.Clear(); mission.Deferred = false;
                var cost = Cost(mission);
                if (mission.SkipReason == "ExamPhaseLowPriority") { mission.Deferred = true; phaseDeferred.Add(mission.SkillId); continue; }
                if (!mission.CanStart || mission.SkipReason == "ReplacedAfterEvidence") continue;
                if (blocked || deferred.Contains(mission.SkillId)) { mission.Deferred = true; summary.MissingMinutes += cost; deferred.Add(mission.SkillId); continue; }
                var recall = mission.Purpose == "Review";
                var room = recall ? finalDays : budgets;
                // Reviews may use ordinary capacity after filling the reserved final period.
                if (recall && days.Sum(d => (long)room.GetValueOrDefault(d)) < cost) room = budgets;
                mission.PlannedDate = Allocate(mission, room, cost, earliest);
                if (mission.PlannedDate == null)
                {
                    mission.Deferred = true; summary.MissingMinutes += cost; deferred.Add(mission.SkillId);
                }
                else if (!recall)
                {
                    earliest = mission.StudySlots.Last().Date;
                    lastDay[mission.SkillId] = earliest;
                }
                if (mission.PlannedDate != null)
                {
                    mission.RemainingUnscheduledMinutes = cost - (uint)mission.StudySlots.Sum(s => s.Minutes);
                    if (mission.RemainingUnscheduledMinutes > 0)
                    { partial.Add(mission.SkillId); summary.MissingMinutes += mission.RemainingUnscheduledMinutes; }
                }
            }
            milestone.PlannedDate = milestone.Missions.Select(m => m.PlannedDate).Where(d => d.HasValue).Min();
        }
        summary.DeferredSkillIds = deferred.Union(partial).Union(phaseDeferred).ToList();
        summary.Message = summary.MissingMinutes > 0
            ? $"Vượt ngân sách học theo ước tính hiện tại: còn thiếu {summary.MissingMinutes} phút; {deferred.Count} kỹ năng có phần bị hoãn. Hãy giảm phạm vi hoặc tăng thời gian học. Đã dành {reserve} phút cho ôn/thi thử."
            : $"Đã xếp theo ngày học thực tế, tối đa {plan.AvailableMinutesPerDay} phút/ngày; dành {reserve} phút cho ôn/thi thử. Thời lượng là ước tính và sẽ cập nhật sau bài làm.";
        plan.ScheduleSummary = summary;
        if (phaseDeferred.Count > 0) summary.Message += $" Đã tạm hoãn phần học mới của {phaseDeferred.Count} kỹ năng trọng số thấp để ưu tiên thi thử và vá lỗ hổng; nền tảng thiết yếu được giữ.";
        plan.FeasibilityStatus = summary.MissingMinutes > 0 ? "InsufficientTime" : "Feasible";
        plan.FeasibilityMessage = summary.Message;
        var initial = all.Where(m => m.CanStart && !m.Deferred && (!m.AvailableFrom.HasValue || m.AvailableFrom <= now))
            .OrderBy(m => summary.Phase == "MockExam" && m.IsMilestoneExam && m.IsProbe ? 0 : 1).ToList();
        plan.InitialMission = initial.FirstOrDefault(); plan.AlternativeOption = initial.Skip(1).FirstOrDefault();
        plan.EstimatedWeeks = (uint)Math.Max(1, Math.Ceiling(((all.SelectMany(m => m.StudySlots).Select(s => (int?)s.Date.DayNumber).Max() ?? today.DayNumber) - today.DayNumber + 1) / 7m));
    }
}
