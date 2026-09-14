using System;
using System.Collections.Generic;

namespace EduTwin.BLL.Platform;

public static class PlatformTimezoneValidator
{
    private static readonly HashSet<string> CanonicalIanaTimezones = new(StringComparer.OrdinalIgnoreCase)
    {
        // UTC
        "UTC",

        // Asia
        "Asia/Ho_Chi_Minh",
        "Asia/Bangkok",
        "Asia/Singapore",
        "Asia/Tokyo",
        "Asia/Seoul",
        "Asia/Shanghai",
        "Asia/Hong_Kong",
        "Asia/Taipei",
        "Asia/Jakarta",
        "Asia/Manila",
        "Asia/Kuala_Lumpur",
        "Asia/Kolkata",
        "Asia/Dubai",

        // Europe
        "Europe/London",
        "Europe/Paris",
        "Europe/Berlin",
        "Europe/Rome",
        "Europe/Madrid",
        "Europe/Amsterdam",
        "Europe/Brussels",
        "Europe/Zurich",
        "Europe/Warsaw",
        "Europe/Athens",

        // America
        "America/New_York",
        "America/Chicago",
        "America/Denver",
        "America/Los_Angeles",
        "America/Phoenix",
        "America/Toronto",
        "America/Vancouver",
        "America/Sao_Paulo",
        "America/Buenos_Aires",

        // Australia & Pacific
        "Australia/Sydney",
        "Australia/Melbourne",
        "Australia/Brisbane",
        "Australia/Perth",
        "Pacific/Auckland"
    };

    public static bool IsValid(string? timezone, out string normalizedIanaId)
    {
        normalizedIanaId = string.Empty;
        if (string.IsNullOrWhiteSpace(timezone))
        {
            return false;
        }

        var trimmed = timezone.Trim();

        // 1. Direct canonical IANA lookup
        if (CanonicalIanaTimezones.TryGetValue(trimmed, out var canonical))
        {
            normalizedIanaId = canonical;
            return true;
        }

        // 2. Cross-platform translation from Windows ID to canonical IANA ID
        if (TimeZoneInfo.TryConvertWindowsIdToIanaId(trimmed, out var convertedIana))
        {
            if (CanonicalIanaTimezones.TryGetValue(convertedIana, out var canonicalConverted))
            {
                normalizedIanaId = canonicalConverted;
                return true;
            }

            normalizedIanaId = convertedIana;
            return true;
        }

        // 3. Fallback: Check System TimeZoneInfo and translate to IANA if system uses Windows ID
        if (TimeZoneInfo.TryFindSystemTimeZoneById(trimmed, out var tz))
        {
            if (TimeZoneInfo.TryConvertWindowsIdToIanaId(tz.Id, out var ianaFromSystem))
            {
                normalizedIanaId = ianaFromSystem;
                return true;
            }

            if (CanonicalIanaTimezones.TryGetValue(tz.Id, out var systemCanonical))
            {
                normalizedIanaId = systemCanonical;
                return true;
            }

            // Only allow if it has standard IANA format (Area/Location)
            if (tz.Id.Contains('/'))
            {
                normalizedIanaId = tz.Id;
                return true;
            }
        }

        return false;
    }
}
