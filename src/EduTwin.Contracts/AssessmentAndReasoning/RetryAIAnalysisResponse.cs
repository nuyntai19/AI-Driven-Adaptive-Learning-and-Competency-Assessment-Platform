using System;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.AssessmentAndReasoning;

public sealed class RetryAIAnalysisResponse
{
    public RetryAIAnalysisDataDto Data { get; set; } = null!;
    public MetaDto Meta { get; set; } = null!;
}

public sealed class RetryAIAnalysisDataDto
{
    public string AttemptId { get; set; } = string.Empty;
    public string JobId { get; set; } = string.Empty;
    public byte ManualRetriesUsed { get; set; }
    public byte ManualRetriesRemaining { get; set; }
    public DateTime? NextRetryAllowedAt { get; set; }
    public int CooldownRemainingSeconds { get; set; }
}
