using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using EduTwin.Contracts.Notifications;
using EduTwin.DAL.Notifications;

namespace EduTwin.BLL.Notifications;

/// <summary>Bounded, conservative heuristics, not a complete personal-data detector.</summary>
public static class NotificationContentPolicy
{
    public const string Restricted = "NOTIFICATION_CONTENT_RESTRICTED";
    private static Regex Rule(string pattern) => new(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled, TimeSpan.FromMilliseconds(100));
    private static readonly Regex Credentials = Rule(@"\b(?:password|passwd|mat\s*khau|api[\s_-]*key|access[\s_-]*token|refresh[\s_-]*token|token|secret|authorization)\s*[:=]\s*\S+|\bbearer\s+[a-z0-9._-]{12,}|\bAIza[a-z0-9_-]{20,}|\beyJ[a-z0-9_-]{8,}\.[a-z0-9_-]{8,}\.[a-z0-9_-]{8,}");
    private static readonly Regex LinksOrMarkup = Rule(@"\b(?:https?|ftp|file|data|javascript)\s*:|\bwww\.|\b[a-z]:[\\/]|\\\\[^\s\\]+\\|<\s*/?\s*[a-z][^>]*>|\b[a-z0-9-]+\.(?:com|vn|net|org|edu|io|app|dev)\b");
    private static readonly Regex Academic = Rule(@"\b(?:bai\s*lam|anh\s*nhap|dap\s*an|loi\s*giai|rubric|attempt|submission|student\d{1,6}|ma\s*hoc\s*sinh|student\s*(?:id|name))\b|\bhoc\s*sinh\s*[:=]\s*\S+|\b(?:diem|score|grade)\b[^\r\n]{0,40}\d");
    private static readonly Regex Personal = Rule(@"\b[\w.+-]+@[\w.-]+\.[a-z]{2,}\b|(?<!\w)(?:\+?84|0)[\s().-]*\d(?:[\s().-]*\d){8,10}(?!\d)|\b(?:cccd|cmnd|passport|ngay\s*sinh|dia\s*chi|ho\s*ten|ten\s*hoc\s*sinh|email|sdt)\s*[:=]\s*\S+");

    public static bool IsRestricted(string? audienceKind, params string?[] text)
    {
        // Callers validate each field first. Keep an independent bound for any future caller.
        if (text.Any(t => t is { Length: > 3000 }) || text.Sum(t => t?.Length ?? 0) > 6000) return true;
        try
        {
            var normalized = Fold(string.Join('\n', text));
            return Credentials.IsMatch(normalized) || LinksOrMarkup.IsMatch(normalized) ||
                (audienceKind is "PlatformSupport" or "CenterManagersRelay" && (Academic.IsMatch(normalized) || Personal.IsMatch(normalized)));
        }
        catch (RegexMatchTimeoutException) { return true; }
        catch (ArgumentException) { return true; }
    }

    public static string? StoredAudience(NotificationMessage message)
    {
        try { return JsonSerializer.Deserialize<NotificationComposeRequest>(message.Audience)?.AudienceKind ?? "PlatformSupport"; }
        catch (JsonException) { return "PlatformSupport"; } // Corrupt context never weakens the privacy guard.
    }

    private static string Fold(string text)
    {
        var output = new StringBuilder(text.Length);
        foreach (var c in text.Normalize(NormalizationForm.FormKD))
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark or UnicodeCategory.Format) continue;
            output.Append(c is 'đ' or 'Đ' ? 'd' : c);
        }
        return output.ToString();
    }
}
