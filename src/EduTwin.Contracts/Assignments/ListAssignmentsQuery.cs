using System;

namespace EduTwin.Contracts.Assignments;

/// <summary>
/// Query parameters cho GET /api/v1/assignments (API_CONTRACTS.md §51).
/// </summary>
public class ListAssignmentsQuery
{
    public Guid? ClassId { get; set; }
    public string? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
