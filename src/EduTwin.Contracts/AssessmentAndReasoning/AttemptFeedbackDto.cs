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
    public AttemptFeedbackAnalysisDto? Analysis { get; set; }
    public AttemptFeedbackTwinChangeDto? TwinChange { get; set; }
    public AttemptFeedbackRecommendationDto? Recommendation { get; set; }
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
