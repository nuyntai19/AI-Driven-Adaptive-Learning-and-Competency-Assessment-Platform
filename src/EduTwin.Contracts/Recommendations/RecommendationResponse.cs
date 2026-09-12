using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.Recommendations;

public sealed class RecommendationDto
{
    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public ulong RecommendationId { get; set; }

    public RecommendationType Type { get; set; }

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public ulong TopicNodeId { get; set; }

    public string TopicName { get; set; } = null!;

    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString)]
    public ulong? QuestionId { get; set; }

    public decimal? OpportunityScore { get; set; }
    public string Explanation { get; set; } = null!;
    public RecommendationStatus Status { get; set; }
    public DateTime GeneratedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
}

public sealed class RecommendationResponse
{
    public RecommendationDto? Data { get; set; }
    public MetaDto Meta { get; set; } = null!;
}

public sealed class DismissRecommendationRequest
{
    [MaxLength(1000)]
    public string? Reason { get; set; }
}
