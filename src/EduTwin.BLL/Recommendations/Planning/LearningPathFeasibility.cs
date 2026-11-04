using System.Globalization;
using EduTwin.Contracts.Recommendations;

namespace EduTwin.BLL.Recommendations.Planning;

/// <summary>
/// Turns a scheduled proposal into an honest feasibility verdict: how much study the goal needs,
/// the earliest date that study fits, and what happens if the chosen deadline is earlier.
/// </summary>
public static class LearningPathFeasibility
{
    private static string F(decimal value) => value.ToString("0.#", CultureInfo.InvariantCulture);

    /// <summary>Weighted current score (0-10) from the capability map; only skills with evidence count, never as zero.</summary>
    public static (decimal? Score, int Covered, int Total) FromMasteryMap(IEnumerable<SkillCapabilityStatusDto> skills,
        IReadOnlyDictionary<string, decimal> weights)
    {
        var all = skills.ToList();
        var observed = all.Where(s => s.EvidenceCount > 0 && s.MasteryScore.HasValue).ToList();
        if (observed.Count == 0) return (null, 0, all.Count);
        decimal Weighted(SkillCapabilityStatusDto s)
        {
            var weight = weights.GetValueOrDefault(s.SkillId, 50m);
            return weight > 0 ? weight : 1m;
        }
        var average = observed.Sum(s => s.MasteryScore!.Value * Weighted(s)) / observed.Sum(Weighted);
        return (Math.Round(average / 10m, 1), observed.Count, all.Count);
    }

    public static void Annotate(PathProposalDto plan, decimal? currentScore, string source, string coverage, DateTime now)
    {
        plan.CurrentEstimatedScore = currentScore;
        plan.CurrentScoreSource = source;
        plan.CurrentScoreCoverage = coverage;
        if (plan.IsPlacement) return;
        var required = plan.ScheduleSummary?.RequiredMinutes
            ?? (uint)plan.Milestones.SelectMany(m => m.Missions).Sum(m => (long)LearningPathCalendar.Work(m,
                plan.PracticeQuestionsBySkill.GetValueOrDefault(m.SkillId, plan.PracticeQuestionsPerSet)));
        var reserve = plan.Schedule.ReviewReservePercent;
        var gross = reserve is > 0 and < 100 ? (uint)Math.Ceiling(required * 100m / (100m - reserve)) : required;
        plan.RequiredStudyMinutes = gross;
        if (gross == 0)
        {
            plan.MinimumStudyDays = 0;
            plan.FeasibilityStatus = "Feasible";
            plan.FeasibilityMessage = "Phần đã chọn đã có bằng chứng đạt yêu cầu; không cần thêm thời gian học tối thiểu cho mục tiêu này.";
            if (plan.ScheduleSummary != null) plan.ScheduleSummary.Message = plan.FeasibilityMessage;
            return;
        }
        var minutes = Math.Max(1u, plan.AvailableMinutesPerDay);
        var daysPerWeek = Math.Max(1u, plan.AvailableDaysPerWeek);
        var daysNeeded = (uint)Math.Ceiling(gross / (decimal)minutes);
        plan.MinimumStudyDays = daysNeeded;
        var today = LearningPathCalendar.LocalDate(now, plan.Schedule.TimeZoneId);
        var studyDays = LearningPathCalendar.Days(today, today.AddDays(3650), plan.Schedule, plan.AvailableDaysPerWeek);
        var earliest = studyDays.Count >= daysNeeded ? studyDays[(int)daysNeeded - 1] : today.AddDays(3650);
        var suggestedDays = (uint)Math.Ceiling(daysNeeded * 1.25m);
        var suggested = studyDays.Count >= suggestedDays ? studyDays[(int)suggestedDays - 1] : studyDays.Count > 0 ? studyDays[^1] : earliest;
        plan.EarliestFeasibleDate = earliest.ToDateTime(new TimeOnly(16, 59), DateTimeKind.Utc);
        plan.SuggestedDeadline = suggested.ToDateTime(new TimeOnly(16, 59), DateTimeKind.Utc);
        plan.EstimatedWeeks = (uint)Math.Max(1, Math.Ceiling(daysNeeded / (decimal)daysPerWeek));
        var deadline = plan.DeadlineDate.HasValue ? LearningPathCalendar.LocalDate(plan.DeadlineDate.Value, plan.Schedule.TimeZoneId) : (DateOnly?)null;
        var target = plan.TargetScore;
        string status, message;
        if (deadline == null)
        {
            status = "Feasible";
            message = currentScore.HasValue && target.HasValue
                ? $"Điểm hiện tại ước tính {F(currentScore.Value)}/10. Để đạt {F(target.Value)}/10 cần khoảng {gross} phút học (≈{daysNeeded} ngày học với {minutes} phút/ngày, {daysPerWeek} ngày/tuần). Ngày đích đề xuất: {suggested:dd/MM/yyyy} (sớm nhất có thể: {earliest:dd/MM/yyyy}). Đặt mốc ngắn hơn ngày sớm nhất sẽ bị coi là không khả thi, hệ thống sẽ không cho chọn tuyến."
                : $"Chưa đủ bằng chứng để ước tính điểm hiện tại; hãy làm kiểm tra đầu vào để có con số chính xác hơn. Khối lượng học dự kiến khoảng {gross} phút (≈{daysNeeded} ngày học với {minutes} phút/ngày, {daysPerWeek} ngày/tuần). Ngày đích đề xuất: {suggested:dd/MM/yyyy} (sớm nhất: {earliest:dd/MM/yyyy}).";
        }
        else if (deadline.Value < earliest)
        {
            status = "InsufficientTime";
            var calendarDays = deadline.Value.DayNumber - today.DayNumber;
            if (currentScore.HasValue && target.HasValue)
            {
                var availableDays = LearningPathCalendar.Days(today, deadline.Value, plan.Schedule, plan.AvailableDaysPerWeek).Count;
                var availableGross = availableDays * (decimal)minutes * (100m - reserve) / 100m;
                var ratio = Math.Min(1m, availableGross / gross);
                var achievable = Math.Round(currentScore.Value + (target.Value - currentScore.Value) * ratio, 1);
                plan.AchievableScore = achievable;
                message = $"Không có lộ trình phù hợp để tăng từ {F(currentScore.Value)} lên {F(target.Value)} điểm trong {calendarDays} ngày (tới {deadline:dd/MM/yyyy}): cần tối thiểu {daysNeeded} ngày học (≈{gross} phút), sớm nhất {earliest:dd/MM/yyyy}, đề xuất {suggested:dd/MM/yyyy}. Với mốc đã chọn, điểm cao nhất ước tính đạt được khoảng {F(achievable)}/10. Hãy dời mốc tới ngày đề xuất, tăng phút học mỗi ngày hoặc số ngày học mỗi tuần, hoặc hạ điểm mục tiêu.";
            }
            else if (target.HasValue)
                message = $"Không có lộ trình phù hợp trong {calendarDays} ngày (tới {deadline:dd/MM/yyyy}) để đạt {F(target.Value)} điểm: cần tối thiểu {daysNeeded} ngày học (≈{gross} phút), sớm nhất {earliest:dd/MM/yyyy}, đề xuất {suggested:dd/MM/yyyy}. Hãy dời mốc, tăng thời gian học mỗi ngày/tuần, hoặc hạ điểm mục tiêu.";
            else
                message = $"Không có lộ trình phù hợp trong {calendarDays} ngày (tới {deadline:dd/MM/yyyy}): cần tối thiểu {daysNeeded} ngày học (≈{gross} phút), sớm nhất {earliest:dd/MM/yyyy}, đề xuất {suggested:dd/MM/yyyy}.";
        }
        else if (deadline.Value < suggested)
        {
            status = "TightBudget";
            message = $"Rất gấp: mốc {deadline:dd/MM/yyyy} chỉ vừa trên mức tối thiểu (cần {daysNeeded} ngày học; sớm nhất {earliest:dd/MM/yyyy}). Trễ vài buổi là trượt mốc"
                + (target.HasValue ? $" {F(target.Value)} điểm" : "") + $"; ngày an toàn hơn: {suggested:dd/MM/yyyy}.";
        }
        else
        {
            status = "Feasible";
            message = $"Khả thi: cần khoảng {gross} phút (≈{daysNeeded} ngày học); mốc {deadline:dd/MM/yyyy} đủ thời gian so với ngày sớm nhất {earliest:dd/MM/yyyy}."
                + (currentScore.HasValue && target.HasValue ? $" Điểm hiện tại ước tính {F(currentScore.Value)}/10, mục tiêu {F(target.Value)}/10." : "");
        }
        plan.FeasibilityStatus = status;
        plan.FeasibilityMessage = message;
        if (plan.ScheduleSummary != null) plan.ScheduleSummary.Message = message;
    }
}
