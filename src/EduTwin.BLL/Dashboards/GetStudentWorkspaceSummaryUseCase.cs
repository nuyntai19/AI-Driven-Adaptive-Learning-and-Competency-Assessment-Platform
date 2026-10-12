using System.Linq.Expressions;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Assignments;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.Dashboards;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Dashboards;

public interface IGetStudentWorkspaceSummaryUseCase
{
    Task<StudentWorkspaceSummaryDto?> ExecuteAsync(Guid? subjectId, CancellationToken cancellationToken = default);
    Task<StudentWorkspaceSummaryDto?> ExecuteAsync(Guid? subjectId, Guid? classId, bool? history, CancellationToken cancellationToken = default)
        => ExecuteAsync(subjectId, cancellationToken);
}

public sealed class GetStudentWorkspaceSummaryUseCase(
    EduTwinDbContext db, ITenantContext tenant, TimeProvider timeProvider) : IGetStudentWorkspaceSummaryUseCase
{
    public async Task<StudentWorkspaceSummaryDto?> ExecuteAsync(Guid? subjectId, CancellationToken cancellationToken = default)
        => await ExecuteAsync(subjectId, null, null, cancellationToken);

    public async Task<StudentWorkspaceSummaryDto?> ExecuteAsync(Guid? subjectId, Guid? classId, bool? history, CancellationToken cancellationToken = default)
    {
        if (!tenant.IsResolved || tenant.CenterId is not { } centerId || centerId == Guid.Empty ||
            tenant.UserId is not { } studentId || studentId == Guid.Empty || tenant.Role != nameof(UserRole.Student))
            return null;

        var timezoneId = await db.Centers.AsNoTracking()
            .Where(c => c.CenterId == centerId && !c.IsDeleted && c.Status == CenterStatus.Active)
            .Select(c => c.Timezone).SingleOrDefaultAsync(cancellationToken);
        if (timezoneId is null || !await db.Students.AsNoTracking()
            .AnyAsync(s => s.CenterId == centerId && s.StudentId == studentId && !s.IsDeleted, cancellationToken)) return null;
        var timezone = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
        var now = timeProvider.GetUtcNow();
        var today = DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(now, timezone).DateTime);
        if (history.HasValue && classId is { } selectedClass &&
            !await StudentAssignmentScope.ValidClassAsync(db, centerId, studentId, selectedClass, subjectId, history.Value, cancellationToken)) return null;

        // Same visibility as the student's assignment list, regardless of list pagination.
        var assignments = db.StudentAssignmentProgresses.AsNoTracking()
            .Where(p => p.CenterId == centerId && p.StudentId == studentId && p.Assignment != null &&
                !p.Assignment.IsDeleted && (p.Assignment.Status == AssignmentStatus.Published || p.Assignment.Status == AssignmentStatus.Closed));
        if (subjectId is { } selectedSubject && selectedSubject != Guid.Empty)
            assignments = assignments.Where(p => db.Classes.Any(c => c.CenterId == centerId &&
                c.ClassId == p.Assignment!.ClassId && c.SubjectId == selectedSubject));
        if (classId.HasValue) assignments = assignments.Where(p => p.Assignment!.ClassId == classId.Value);
        if (history.HasValue) assignments = StudentAssignmentScope.InView(assignments, db, centerId, studentId, history.Value);
        var count = await assignments.CountAsync(cancellationToken);

        // Submission time, not AI completion/login time. A skipped question is not study activity.
        var activity = db.Attempts.AsNoTracking()
            .Where(a => a.CenterId == centerId && a.StudentId == studentId && !a.Skipped && a.CreatedAt <= now.UtcDateTime);
        var activeDays = await ReadActiveDaysAsync(activity, today, timezone, cancellationToken);
        var studiedToday = activeDays.Contains(today.DayNumber);
        var cursor = studiedToday ? today : today.AddDays(-1);
        var windowEnd = today;
        var streak = 0;
        while (true)
        {
            if (cursor.DayNumber < windowEnd.DayNumber - 63)
            {
                windowEnd = cursor;
                activeDays = await ReadActiveDaysAsync(activity, windowEnd, timezone, cancellationToken);
            }
            if (!activeDays.Contains(cursor.DayNumber)) break;
            streak++;
            cursor = cursor.AddDays(-1);
        }
        return new StudentWorkspaceSummaryDto
        {
            AssignmentCount = count, DailyStreak = streak, StudiedToday = studiedToday,
            LocalDate = today.ToString("yyyy-MM-dd"), Timezone = timezoneId, GeneratedAt = now.UtcDateTime
        };
    }

    // Aggregate at most 64 local calendar days per query, returning one integer per
    // active day instead of every question submission. UTC bounds handle DST and
    // fractional offsets without relying on MySQL timezone-table installation.
    internal static async Task<HashSet<int>> ReadActiveDaysAsync(IQueryable<Attempt> activity,
        DateOnly lastDay, TimeZoneInfo timezone, CancellationToken cancellationToken)
    {
        var firstDay = lastDay.AddDays(-63);
        var lower = UtcStart(firstDay, timezone);
        var upper = UtcStart(lastDay.AddDays(1), timezone);
        var parameter = Expression.Parameter(typeof(Attempt), "a");
        var createdAt = Expression.Property(parameter, nameof(Attempt.CreatedAt));
        Expression bucket = Expression.Constant(-1);
        for (var day = firstDay; day <= lastDay; day = day.AddDays(1))
        {
            var inDay = Expression.AndAlso(
                Expression.GreaterThanOrEqual(createdAt, Expression.Constant(UtcStart(day, timezone))),
                Expression.LessThan(createdAt, Expression.Constant(UtcStart(day.AddDays(1), timezone))));
            bucket = Expression.Condition(inDay, Expression.Constant(day.DayNumber), bucket);
        }
        var selector = Expression.Lambda<Func<Attempt, int>>(bucket, parameter);
        return (await activity.Where(a => a.CreatedAt >= lower && a.CreatedAt < upper)
            .Select(selector).Distinct().ToArrayAsync(cancellationToken)).ToHashSet();
    }

    private static DateTime UtcStart(DateOnly day, TimeZoneInfo timezone)
    {
        var local = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        while (timezone.IsInvalidTime(local)) local = local.AddMinutes(1);
        if (timezone.IsAmbiguousTime(local))
            return DateTime.SpecifyKind(local - timezone.GetAmbiguousTimeOffsets(local).Max(), DateTimeKind.Utc);
        return TimeZoneInfo.ConvertTimeToUtc(local, timezone);
    }
}
