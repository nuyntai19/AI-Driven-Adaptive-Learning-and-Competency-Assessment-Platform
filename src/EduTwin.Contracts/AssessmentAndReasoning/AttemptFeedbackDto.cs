using System;
using System.Collections.Generic;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.AssessmentAndReasoning;

public sealed class AttemptFeedbackResponse
{
    public AttemptFeedbackDataDto Data { get; set; } = null!;
    public MetaDto Meta { get; set; } = null!;
}

public sealed class AttemptFeedbackDataDto
{
    public string AttemptId { get; set; } = null!;
    public string QuestionId { get; set; } = null!;
    public string Status { get; set; } = null!;
    public AttemptFeedbackGradingDto Grading { get; set; } = null!;
    public AttemptFeedbackStudentSubmissionDto? StudentSubmission { get; set; }
    public AttemptFeedbackTeacherSolutionDto? TeacherSolution { get; set; }
    public AttemptFeedbackAnalysisDto? Analysis { get; set; }
    public AttemptFeedbackTeacherEvaluationDto? TeacherFinalEvaluation { get; set; }
    public StudentReviewRequestDto? ReviewRequest { get; set; }
    public RetryQuotaDto? RetryQuota { get; set; }
    public AttemptFeedbackTwinChangeDto? TwinChange { get; set; }
    public AttemptFeedbackRecommendationDto? Recommendation { get; set; }
}

public sealed class AttemptFeedbackStudentSubmissionDto
{
    public string FinalAnswer { get; set; } = string.Empty;
    public string? ReasoningText { get; set; }
    public decimal Confidence { get; set; }
    public uint TimeSpentSeconds { get; set; }
    public uint AnswerChanges { get; set; }
    public string? AttachmentUrl { get; set; }
}

public sealed class AttemptFeedbackTeacherSolutionDto
{
    public string CorrectAnswer { get; set; } = string.Empty;
    public string Solution { get; set; } = string.Empty;
    public string? ExpectedReasoning { get; set; }
    public AttemptFeedbackGradingCriteriaDto? GradingCriteria { get; set; }
}

public sealed class AttemptFeedbackGradingCriteriaDto
{
    public string ScoringNotes { get; set; } = string.Empty;
    public List<string> RequiredIdeas { get; set; } = new();
    public List<string> CommonErrors { get; set; } = new();
}

public sealed class AttemptFeedbackTeacherEvaluationDto
{
    public bool HasTeacherOverride { get; set; }
    public bool? TeacherIsCorrect { get; set; }
    public decimal? TeacherScore { get; set; }
    public string? TeacherFeedback { get; set; }
    public string? ReviewedByTeacherName { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public AttemptFeedbackGradingDto? OriginalAIRawGrade { get; set; }
}

public sealed class RetryQuotaDto
{
    public byte ManualRetriesUsed { get; set; }
    public byte ManualRetriesRemaining { get; set; }
    public int CooldownRemainingSeconds { get; set; }
    public bool CanRetry { get; set; }
    public DateTime? NextRetryAllowedAt { get; set; }
}

public sealed class AttemptFeedbackGradingDto
{
    public bool? IsCorrect { get; set; }
    public decimal? AwardedScore { get; set; }
    public decimal MaxScore { get; set; }
}

public sealed class AttemptFeedbackAnalysisDto
{
    public string AnalysisId { get; set; } = null!;
    public string SchemaVersion { get; set; } = null!;
    public string? MethodDetected { get; set; }
    public int? ReasoningQuality { get; set; }
    public string? QualityBand { get; set; }
    public string? ErrorType { get; set; }
    public string? Misconception { get; set; }
    public List<string> MissingSteps { get; set; } = new();
    public List<AttemptFeedbackRootCauseNodeDto> RootCauseNodes { get; set; } = new();
    public int? Confidence { get; set; }
    public string Feedback { get; set; } = null!;
    public bool IsFallback { get; set; }
    public bool NeedsTeacherReview { get; set; }
    public bool HasTeacherOverride { get; set; }
    public bool IsRawAI { get; set; } = true;
    public string? Model { get; set; }
}

public sealed class AttemptFeedbackRootCauseNodeDto
{
    public string NodeId { get; set; } = null!;
    public string NodeName { get; set; } = null!;
}

public sealed class AttemptFeedbackTwinChangeDto
{
    public string TopicNodeId { get; set; } = null!;
    public string TopicName { get; set; } = null!;
    public decimal PreviousMastery { get; set; }
    public decimal NewMastery { get; set; }
    public decimal Delta { get; set; }
    public string Explanation { get; set; } = null!;
}

public sealed class AttemptFeedbackRecommendationDto
{
    public string RecommendationId { get; set; } = null!;
    public string Type { get; set; } = null!;
    public string TopicNodeId { get; set; } = null!;
    public string TopicName { get; set; } = null!;
    public string QuestionId { get; set; } = null!;
    public decimal OpportunityScore { get; set; }
    public string Explanation { get; set; } = null!;
}
