using System.Globalization;
using System.Text.RegularExpressions;

namespace EduTwin.API.AssessmentAndReasoning.AI;

public sealed record GeminiQuotaFailure(bool Daily, TimeSpan? RetryAfter);

public static partial class GeminiQuotaFailureClassifier
{
    // GenerateContent SDK exposes error details in Message, not Interactions error codes.
    // Inspect only known quota identifiers; never log the raw provider body or credentials.
    public static GeminiQuotaFailure Classify(string details)
    {
        var daily = DailyQuota().IsMatch(details);
        var match = RetryDelay().Match(details);
        TimeSpan? delay = match.Success && double.TryParse(match.Groups[1].Value, NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out var seconds)
            ? TimeSpan.FromSeconds(Math.Clamp(seconds, 0, 86400)) : null;
        return new(daily, delay);
    }

    [GeneratedRegex(@"GenerateRequestsPerDay|RequestsPerDayPerProject|requests_per_day|generate_requests_per_day", RegexOptions.IgnoreCase)]
    private static partial Regex DailyQuota();
    [GeneratedRegex(@"retryDelay[^0-9]{1,12}(\d{1,5}(?:\.\d+)?)s", RegexOptions.IgnoreCase)]
    private static partial Regex RetryDelay();
}
