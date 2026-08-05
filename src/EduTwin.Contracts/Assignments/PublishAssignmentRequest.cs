namespace EduTwin.Contracts.Assignments;

/// <summary>
/// Request body cho POST /api/v1/assignments/{id}/publish (API_CONTRACTS.md §51).
/// centerId KHÔNG được gửi từ client; lấy từ JWT/ITenantContext.
/// </summary>
public class PublishAssignmentRequest
{
    /// <summary>
    /// Optimistic concurrency token. Bắt buộc, phải là ASCII digits > 0.
    /// Nếu không khớp với DB => 409 CONCURRENCY_CONFLICT.
    /// </summary>
    public string RowVersion { get; set; } = string.Empty;
}
