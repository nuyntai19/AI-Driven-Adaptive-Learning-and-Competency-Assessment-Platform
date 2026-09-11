using System;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.Recommendations;

public sealed record NextQuestionTopicDto(
    ulong NodeId,
    string NodeName,
    decimal Mastery);

public sealed record NextQuestionQuestionDto(
    ulong QuestionId,
    QuestionType QuestionType,
    byte Difficulty,
    string QuestionText,
    uint EstimatedTimeSeconds,
    bool ReasoningRequired,
    string LanguageCode);

public sealed class NextQuestionDto
{
    public LearningPathStrategy Strategy { get; set; }
    public ulong? RecommendationId { get; set; }
    public NextQuestionTopicDto Topic { get; set; } = null!;
    public NextQuestionQuestionDto? Question { get; set; }
    public string Explanation { get; set; } = null!;
}

public sealed class NextQuestionResponse
{
    public NextQuestionDto? Data { get; set; }
    public MetaDto Meta { get; set; } = null!;
}
