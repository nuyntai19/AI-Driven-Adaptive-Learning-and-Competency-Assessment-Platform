using System;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence.Models;

namespace EduTwin.DAL.AssessmentAndReasoning;

public class StudentReviewRequest : ITenantAppendOnlyEntity, IHasRowVersion
{
    public ulong RequestId { get; set; }
    public Guid CenterId { get; set; }
    public ulong AttemptId { get; set; }
    public Guid StudentId { get; set; }
    public ulong QuestionId { get; set; }

    public string StudentComment { get; set; } = string.Empty;
    public StudentReviewRequestStatus Status { get; set; }
    public string? TeacherNote { get; set; }
    public Guid? ResolvedByTeacherId { get; set; }
    public DateTime? ResolvedAt { get; set; }

    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ulong RowVersion { get; set; }

    public Attempt Attempt { get; set; } = null!;
    public Student Student { get; set; } = null!;
    public Question Question { get; set; } = null!;
    public Teacher? ResolvedByTeacher { get; set; }
}
