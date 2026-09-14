using System;
using System.Text.RegularExpressions;

namespace EduTwin.BLL.Platform;

public static class PlatformAuditSanitizer
{
    private static readonly Regex CredentialPattern = new(
        @"(password|passwd|mật\s*khẩu|secret|bearer|token|access_token|refresh_token|apikey|api_key|credential|private_key)\s*[:=]\s*[^\s,;]+",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex JwtPattern = new(
        @"ey[A-Za-z0-9_-]{10,}\.[A-Za-z0-9_-]{10,}(\.[A-Za-z0-9_-]+)?",
        RegexOptions.Compiled);

    private static readonly Regex HighEntropyTokenPattern = new(
        @"\b[A-Za-z0-9_\-]{32,}\b",
        RegexOptions.Compiled);

    public static bool ValidateAndSanitizeReason(string? reason, out string sanitizedReason, out string? errorMessage, int minLength = 3)
    {
        sanitizedReason = string.Empty;
        errorMessage = null;

        if (string.IsNullOrWhiteSpace(reason))
        {
            errorMessage = "Lý do là bắt buộc.";
            return false;
        }

        var trimmed = reason.Trim();
        if (trimmed.Length < minLength)
        {
            errorMessage = $"Lý do phải có ít nhất {minLength} ký tự.";
            return false;
        }

        if (trimmed.Length > 500)
        {
            errorMessage = "Lý do không được vượt quá 500 ký tự.";
            return false;
        }

        // Fail-closed rejection if explicit credentials or tokens are leaked in reason
        if (CredentialPattern.IsMatch(trimmed) || JwtPattern.IsMatch(trimmed))
        {
            errorMessage = "Lý do chứa thông tin nhạy cảm hoặc chứng chỉ bí mật. Vui lòng không đính kèm mật khẩu hoặc token trong lý do.";
            return false;
        }

        // Defense-in-depth: Redact any high-entropy token strings
        sanitizedReason = HighEntropyTokenPattern.Replace(trimmed, "[REDACTED]");
        return true;
    }

    public static string SanitizeReason(string? reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            return string.Empty;
        }

        var sanitized = CredentialPattern.Replace(reason, "$1=[REDACTED]");
        sanitized = JwtPattern.Replace(sanitized, "[REDACTED_JWT]");
        sanitized = HighEntropyTokenPattern.Replace(sanitized, "[REDACTED]");
        return sanitized;
    }
}
