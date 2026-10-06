namespace EduTwin.BLL.AssessmentAndReasoning.Feedback;

/// <summary>One presentation policy for both student feedback and teacher review.</summary>
public static class AnalysisFeedbackPresentation
{
    public static string Resolve(string? origin, string? feedback, bool? effectiveCorrectness) =>
        origin == "LegacySystem"
            ? effectiveCorrectness == true
                ? "Đáp án được bộ chấm tự động công nhận đúng. Đây là thông báo hệ thống; nhận xét AI cũ không được lưu riêng."
                : effectiveCorrectness == false
                    ? "Đáp án chưa khớp kết quả bộ chấm tự động. Đây là thông báo hệ thống, không phải nhận xét riêng của AI."
                    : "Kết quả đang chờ giáo viên xác nhận; nhận xét AI cũ không được lưu riêng."
            : feedback ?? string.Empty;
}
