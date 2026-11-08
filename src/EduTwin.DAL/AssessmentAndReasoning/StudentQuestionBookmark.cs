using System;
using EduTwin.DAL.Persistence.Models;

namespace EduTwin.DAL.AssessmentAndReasoning;

/// <summary>A question the student flagged as hard, to return to once its answer is available.</summary>
public class StudentQuestionBookmark : ITenantAppendOnlyEntity
{
    public Guid CenterId { get; set; }
    public Guid StudentId { get; set; }
    public ulong QuestionId { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
}
