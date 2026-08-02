using System;
using System.Collections.Generic;

namespace EduTwin.Contracts.Assignments;

/// <summary>
/// Request body cho POST /api/v1/assignments (API_CONTRACTS.md §50).
/// centerId KHÔNG được gửi từ client; lấy từ JWT/ITenantContext.
/// </summary>
public class CreateAssignmentRequest
{
    /// <summary>
    /// ID của Class sở hữu. Teacher gọi phải là Teacher của Class này.
    /// </summary>
    public Guid ClassId { get; set; }

    public string Title { get; set; } = string.Empty;
    public string? Instructions { get; set; }
    public DateTime? DueAt { get; set; }

    /// <summary>
    /// Danh sách questionId (string của BIGINT UNSIGNED) theo thứ tự.
    /// Mỗi Question phải Active và cùng Subject với Class.
    /// </summary>
    public List<string> QuestionIds { get; set; } = new();

    /// <summary>
    /// WholeClass hoặc SelectedStudents.
    /// GapGroup UI gửi SelectedStudents và truyền snapshot StudentIds.
    /// </summary>
    public string TargetMode { get; set; } = string.Empty;

    /// <summary>
    /// Bắt buộc khi TargetMode == SelectedStudents.
    /// Null hoặc rỗng hợp lệ khi TargetMode == WholeClass (server ignore).
    /// </summary>
    public List<string>? StudentIds { get; set; }
}
