using System;
using EduTwin.Contracts.Assignments;

namespace EduTwin.BLL.Assignments;

/// <summary>
/// Helper để chiếu (project) trạng thái nghiệp vụ mà không cần lưu trữ vật lý hoặc cron job.
/// </summary>
public static class AssignmentStatusHelper
{
    /// <summary>
    /// Tính toán trạng thái Progress thực tế dựa trên hạn nộp bài (dueAt) và thời gian hiện tại.
    /// Overdue semantics: Nếu trạng thái hiện tại chưa phải Completed, và đã quá hạn nộp bài, thì chiếu thành Overdue.
    /// </summary>
    public static ProgressStatus GetEffectiveProgressStatus(
        ProgressStatus dbStatus, 
        DateTime? dueAt, 
        DateTime utcNow)
    {
        if (dbStatus == ProgressStatus.Completed)
        {
            return ProgressStatus.Completed;
        }

        if (dueAt.HasValue && dueAt.Value < utcNow)
        {
            return ProgressStatus.Overdue;
        }

        return dbStatus;
    }
}
