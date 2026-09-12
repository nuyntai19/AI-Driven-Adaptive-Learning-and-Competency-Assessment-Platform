using System;
using System.Collections.Generic;
using System.Text.Json;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.Recommendations;
using Xunit;

namespace EduTwin.BLL.Tests.Recommendations;

public sealed class UInt64JsonSerializationTests
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    [Fact]
    public void NextQuestionResponse_SerializesLargeUInt64PropertiesAsString()
    {
        const ulong largeNodeId = 18446744073709551614UL;
        const ulong largeQuestionId = ulong.MaxValue; // 18446744073709551615
        const ulong largeRecId = 18446744073709551613UL;

        var dto = new NextQuestionDto
        {
            Strategy = LearningPathStrategy.LinearFallback,
            RecommendationId = largeRecId,
            Explanation = "Test explanation",
            Topic = new NextQuestionTopicDto(largeNodeId, "Chủ đề lớn", 75.5m),
            Question = new NextQuestionQuestionDto(
                QuestionId: largeQuestionId,
                QuestionType: QuestionType.MultipleChoice,
                Difficulty: 3,
                QuestionText: "Câu hỏi trắc nghiệm?",
                MaxScore: 10m,
                EstimatedTimeSeconds: 60,
                ReasoningRequired: false,
                LanguageCode: "vi",
                Options: new List<StudentQuestionOptionDto>())
        };

        var json = JsonSerializer.Serialize(dto, JsonOptions);

        // Assert that large 64-bit integer IDs are serialized as JSON strings to prevent JavaScript precision loss (> 2^53 - 1)
        Assert.Contains($"\"recommendationId\":\"{largeRecId}\"", json);
        Assert.Contains($"\"nodeId\":\"{largeNodeId}\"", json);
        Assert.Contains($"\"questionId\":\"{largeQuestionId}\"", json);

        // Deserialization round-trip works seamlessly
        var roundTripped = JsonSerializer.Deserialize<NextQuestionDto>(json, JsonOptions);
        Assert.NotNull(roundTripped);
        Assert.Equal(largeRecId, roundTripped.RecommendationId);
        Assert.Equal(largeNodeId, roundTripped.Topic.NodeId);
        Assert.NotNull(roundTripped.Question);
        Assert.Equal(largeQuestionId, roundTripped.Question.QuestionId);
    }

    [Fact]
    public void RecommendationDto_SerializesUInt64PropertiesAsString()
    {
        const ulong recId = ulong.MaxValue;
        const ulong topicId = 18446744073709551610UL;
        const ulong questionId = 18446744073709551609UL;

        var rec = new RecommendationDto
        {
            RecommendationId = recId,
            Type = RecommendationType.TopicAndQuestion,
            TopicNodeId = topicId,
            TopicName = "Topic",
            QuestionId = questionId,
            Explanation = "Explanation",
            Status = RecommendationStatus.Active,
            GeneratedAt = DateTime.UtcNow
        };

        var json = JsonSerializer.Serialize(rec, JsonOptions);

        Assert.Contains($"\"recommendationId\":\"{recId}\"", json);
        Assert.Contains($"\"topicNodeId\":\"{topicId}\"", json);
        Assert.Contains($"\"questionId\":\"{questionId}\"", json);

        var roundTripped = JsonSerializer.Deserialize<RecommendationDto>(json, JsonOptions);
        Assert.NotNull(roundTripped);
        Assert.Equal(recId, roundTripped.RecommendationId);
        Assert.Equal(topicId, roundTripped.TopicNodeId);
        Assert.Equal(questionId, roundTripped.QuestionId);
    }

    [Fact]
    public void LearningPathItemDto_SerializesUInt64PropertiesAsString()
    {
        const ulong itemId = ulong.MaxValue;
        const ulong topicId = 18446744073709551600UL;
        const ulong questionId = 18446744073709551599UL;

        var item = new LearningPathItemDto
        {
            LearningPathItemId = itemId,
            TopicNodeId = topicId,
            TopicName = "Topic Item",
            RecommendedQuestionId = questionId,
            RankOrder = 1,
            Reason = "Reason",
            Status = LearningPathItemStatus.Current
        };

        var json = JsonSerializer.Serialize(item, JsonOptions);

        Assert.Contains($"\"learningPathItemId\":\"{itemId}\"", json);
        Assert.Contains($"\"topicNodeId\":\"{topicId}\"", json);
        Assert.Contains($"\"recommendedQuestionId\":\"{questionId}\"", json);

        var roundTripped = JsonSerializer.Deserialize<LearningPathItemDto>(json, JsonOptions);
        Assert.NotNull(roundTripped);
        Assert.Equal(itemId, roundTripped.LearningPathItemId);
        Assert.Equal(topicId, roundTripped.TopicNodeId);
        Assert.Equal(questionId, roundTripped.RecommendedQuestionId);
    }
}
