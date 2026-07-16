using System;
using EduTwin.DAL.Persistence.Models;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.DAL.Organization;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.Assignments;

namespace EduTwin.DAL.AssessmentAndReasoning;

public class Attempt : ITenantAppendOnlyEntity, IHasRowVersion
{
    public ulong AttemptId { get; set; }
    public Guid CenterId { get; set; }
    public Guid StudentId { get; set; }
    public ulong QuestionId { get; set; }
    public Guid? AssignmentId { get; set; }
    public string FinalAnswer { get; set; } = null!;
    public string? ReasoningText { get; set; }
    public bool? IsCorrect { get; set; }
    public decimal? AwardedScore { get; set; }
    public uint TimeSpentSeconds { get; set; }
    public decimal Confidence { get; set; }
    public uint AnswerChanges { get; set; }
    public bool Skipped { get; set; }
    public string ReasoningLanguage { get; set; } = null!;
    public AttemptStatus Status { get; set; }
    public Guid ClientSubmissionId { get; set; }

    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public ulong RowVersion { get; set; }

    public Student Student { get; set; } = null!;
    public Question Question { get; set; } = null!;
    public Assignment? Assignment { get; set; }
}