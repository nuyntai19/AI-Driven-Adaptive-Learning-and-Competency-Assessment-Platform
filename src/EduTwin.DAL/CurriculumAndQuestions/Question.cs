using System;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.DAL.Persistence.Models;
using EduTwin.DAL.Organization;
using EduTwin.DAL.KnowledgeGraph;

namespace EduTwin.DAL.CurriculumAndQuestions;

public class Question : IMutableTenantAggregate
{
    public ulong QuestionId { get; set; }
    public Guid CenterId { get; set; }
    public Guid SubjectId { get; set; }
    public ulong PrimaryTopicNodeId { get; set; }
    public Guid CreatedByTeacherId { get; set; }
    
    public QuestionType QuestionType { get; set; }
    public byte Difficulty { get; set; }
    public string QuestionText { get; set; } = null!;
    public string CorrectAnswer { get; set; } = null!;
    public string Solution { get; set; } = null!;
    public string? ExpectedReasoning { get; set; }
    public GradingCriteria GradingCriteria { get; set; } = new();
    
    public decimal MaxScore { get; set; }
    public uint EstimatedTimeSeconds { get; set; }
    public bool ReasoningRequired { get; set; }
    public string LanguageCode { get; set; } = null!;
    public QuestionStatus Status { get; set; }

    // Audit and MTA
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
    public ulong RowVersion { get; set; }

    // Navigations
    public Subject? Subject { get; set; }
    public KnowledgeNode? PrimaryTopicNode { get; set; }
    public Teacher? CreatedByTeacher { get; set; }
}
