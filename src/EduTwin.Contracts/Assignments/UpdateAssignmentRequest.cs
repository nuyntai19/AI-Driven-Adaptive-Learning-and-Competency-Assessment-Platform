using System;
using System.Collections.Generic;

namespace EduTwin.Contracts.Assignments;

/// <summary>
/// Request body cho PATCH /api/v1/assignments/{id} (API_CONTRACTS.md §51).
/// Chỉ được phép khi Status == Draft.
/// </summary>
public class UpdateAssignmentRequest
{
    public string? Title { get; set; }
    public string? Instructions { get; set; }
    public DateTime? DueAt { get; set; }

    /// <summary>
    /// Danh sách questionId mới. Nếu null → giữ nguyên; nếu cung cấp → replace toàn bộ.
    /// </summary>
    public List<string>? QuestionIds { get; set; }

    public string? TargetMode { get; set; }
    public List<string>? StudentIds { get; set; }

    /// <summary>
    /// Bắt buộc; phải khớp RowVersion hiện tại trong DB.
    /// </summary>
    public string RowVersion { get; set; } = string.Empty;
}
