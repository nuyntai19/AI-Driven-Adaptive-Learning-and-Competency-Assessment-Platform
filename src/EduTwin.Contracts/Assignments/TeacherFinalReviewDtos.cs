using System;
using EduTwin.Contracts.Common;

namespace EduTwin.Contracts.Assignments;

public sealed class ApproveAssignmentResultRequest
{
    public Guid StudentId { get; set; }
    public string? Note { get; set; }
    public uint FinalReviewVersion { get; set; }
}

public sealed class AssignmentFinalReviewDto
{
    public string AssignmentId { get; set; } = string.Empty;
    public string StudentId { get; set; } = string.Empty;
    public string TeacherFinalReviewStatus { get; set; } = "Pending";
    public string ReviewedByUserId { get; set; } = string.Empty;
    public DateTime ReviewedAt { get; set; }
    public string? Note { get; set; }
    public uint FinalReviewVersion { get; set; }
}

public sealed class AssignmentFinalReviewResponse
{
    public AssignmentFinalReviewDto Data { get; set; } = null!;
    public MetaDto Meta { get; set; } = null!;
}
