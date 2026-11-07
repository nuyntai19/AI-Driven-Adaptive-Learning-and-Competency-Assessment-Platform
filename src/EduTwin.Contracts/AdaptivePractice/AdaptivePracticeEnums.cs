using System;

namespace EduTwin.Contracts.AdaptivePractice;

public enum AdaptivePracticeMode
{
    Weakness,       // Luyện theo điểm yếu (P0 - Mặc định)
    LearningPath,   // Luyện theo mục tiêu từ Learning Path (P0)
    CustomTopic,    // Luyện chủ đề tự chọn (P0)
    SpacedReview,   // Ôn tập kiến thức cũ (P1)
    ExamReview      // Ôn tập trước kiểm tra (P1)
}

public enum AdaptivePracticeSessionStatus
{
    Created,
    InProgress,
    AwaitingEvaluation,
    Paused,
    Closed
}

public enum AdaptivePracticeCloseReason
{
    GoalAchievedWithEvidence,   // END01: Đạt mục tiêu với đủ bằng chứng
    QuestionLimitReached,       // END02: Đạt giới hạn số câu tối đa
    TimeBudgetReached,          // END03: Hết thời gian dự kiến và học sinh dừng
    RemediationNeeded,          // END04: Cần học bổ trợ do lỗi lặp lại
    StudentStopped,             // END05: Học sinh chủ động dừng trước hạn
    OutOfQuestions,             // END06: Thiếu câu hỏi hợp lệ trong ngân hàng
    Expired,                    // END07: Phiên hết hiệu lực
    EvaluationFailed            // END08: Không thể hoàn tất đánh giá
}

public enum AdaptiveSessionItemStatus
{
    Presented,
    Answered,
    Skipped,
    PendingEvaluation
}

public static class AdaptivePracticeSelectionReasonCode
{
    public const string DiagnosticProbe = "DIAGNOSTIC_PROBE";
    public const string GapRecovery = "GAP_RECOVERY";
    public const string PrerequisiteProbe = "PREREQUISITE_PROBE";
    public const string DifficultyAdvancement = "DIFFICULTY_ADVANCEMENT";
    public const string StabilityConfirmation = "STABILITY_CONFIRMATION";
    public const string MisconceptionTest = "MISCONCEPTION_TEST";
    public const string RetryWithScaffolding = "RETRY_WITH_SCAFFOLDING";
}
