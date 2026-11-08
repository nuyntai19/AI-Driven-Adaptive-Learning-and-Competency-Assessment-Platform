using System;
using System.Collections.Generic;
using System.Linq;

namespace EduTwin.BLL.AdaptivePractice;

/// <summary>Decides which studied topic is most in need of review.</summary>
public static class SpacedReviewPolicy
{
    public sealed record Candidate(ulong TopicId, decimal Mastery, DateTime LastEvidenceAt);

    public sealed record Choice(ulong TopicId, int IntervalDays, int DaysSinceEvidence, bool IsDue, int DaysUntilDue);

    /// <summary>A weaker topic fades sooner, so it comes back after a shorter gap.</summary>
    public static int IntervalDays(decimal mastery) => mastery switch
    {
        < 50m => 1,
        < 70m => 3,
        < 85m => 7,
        _ => 14
    };

    /// <summary>Picks the topic furthest past its interval, or null when nothing has been studied.</summary>
    public static Choice? Choose(IEnumerable<Candidate> candidates, DateTime utcNow)
    {
        var picked = candidates
            .Select(c => (Candidate: c, Interval: IntervalDays(c.Mastery), Elapsed: (utcNow - c.LastEvidenceAt).TotalDays))
            .OrderByDescending(x => x.Elapsed / x.Interval)
            .ThenBy(x => x.Candidate.Mastery)
            .ThenBy(x => x.Candidate.TopicId)
            .Select(x => (x.Candidate, x.Interval, x.Elapsed, Found: true))
            .FirstOrDefault();

        if (!picked.Found)
        {
            return null;
        }

        var isDue = picked.Elapsed >= picked.Interval;
        return new Choice(
            picked.Candidate.TopicId,
            picked.Interval,
            Math.Max(0, (int)Math.Floor(picked.Elapsed)),
            isDue,
            isDue ? 0 : Math.Max(1, (int)Math.Ceiling(picked.Interval - picked.Elapsed)));
    }
}
