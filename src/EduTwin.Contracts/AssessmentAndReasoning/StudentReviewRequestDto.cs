using System;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.AssessmentAndReasoning;

public sealed class StudentReviewRequestResponse
{
    public StudentReviewRequestDto Data { get; set; } = null!;
    public MetaDto Meta { get; set; } = null!;
}

public sealed class StudentReviewRequestDto
{
    public ulong RequestId { get; set; }
    public ulong AttemptId { get; set; }
    public Guid StudentId { get; set; }
    public ulong QuestionId { get; set; }
    public string StudentComment { get; set; } = string.Empty;
    public string Reason
    {
        get => StudentComment;
        set => StudentComment = value;
    }
    public StudentReviewRequestStatus Status { get; set; }
    public string? TeacherNote { get; set; }
    public Guid? ResolvedByTeacherId { get; set; }
    public DateTime? ResolvedAt { get; set; }
    public DateTime? ReviewedAt
    {
        get => ResolvedAt;
        set => ResolvedAt = value;
    }
    public DateTime CreatedAt { get; set; }
}
