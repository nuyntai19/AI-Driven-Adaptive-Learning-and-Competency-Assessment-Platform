using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.Recommendations;

public sealed record NextQuestionTopicDto(
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    ulong NodeId,
    string NodeName,
    decimal Mastery);

public sealed record NextQuestionQuestionDto(
    [property: JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    ulong QuestionId,
    QuestionType QuestionType,
    byte Difficulty,
    string QuestionText,
    decimal MaxScore,
    uint EstimatedTimeSeconds,
    bool ReasoningRequired,
    string LanguageCode,
    IReadOnlyList<StudentQuestionOptionDto> Options,
    QuestionAnswerEvaluationMode AnswerEvaluationMode = QuestionAnswerEvaluationMode.TextExact);

public sealed class NextQuestionDto
{
    public LearningPathStrategy Strategy { get; set; }

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
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
