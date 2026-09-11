using System;

namespace EduTwin.BLL.Recommendations;

public static class RecommendationExplanationBuilder
{
    public static string BuildOpportunityGapExplanation(
        string topicName,
        decimal opportunityScore,
        decimal mastery,
        decimal examImportance,
        decimal prerequisiteReadiness) =>
        $"Chủ đề '{topicName}' được ưu tiên đề xuất với điểm cơ hội {opportunityScore:F1}/100 " +
        $"(Độ thành thạo: {mastery:F1}%, Tầm quan trọng đề thi: {examImportance:F0}, Độ sẵn sàng tiên quyết: {prerequisiteReadiness * 100m:F0}%).";

    public static string BuildLinearFallbackExplanation(int effectiveEvidenceCount) =>
        $"Lộ trình học tập tuần tự theo chương trình (học sinh có {effectiveEvidenceCount}/3 bằng chứng tin cậy để kích hoạt thuật toán thích ứng).";

    public static string BuildMaintenanceReviewExplanation(string topicName, decimal mastery) =>
        $"Chế độ ôn tập củng cố: Học sinh đã đạt độ thành thạo trên 80% ở tất cả các chủ đề. " +
        $"Đề xuất củng cố chủ đề '{topicName}' (Độ thành thạo: {mastery:F1}%).";

    public static string BuildBlockedExplanation() =>
        "Lộ trình học tập tạm thời chưa có đề xuất do toàn bộ chủ đề đang bị khóa bởi điều kiện tiên quyết.";
}
