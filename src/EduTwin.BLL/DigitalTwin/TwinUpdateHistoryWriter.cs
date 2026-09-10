using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.DigitalTwin;
using EduTwin.DAL.DigitalTwin;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.DigitalTwin;

public sealed class TwinUpdateHistoryWriter : ITwinUpdateHistoryWriter
{
    private const string MasteryCalculationVersion = "mastery-v1";
    private readonly EduTwinDbContext _dbContext;

    public TwinUpdateHistoryWriter(EduTwinDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public Task<TwinUpdateHistory> WriteAsync(
        Guid centerId,
        Guid studentId,
        Guid subjectId,
        ulong topicNodeId,
        ulong? attemptId,
        ulong? analysisId,
        TwinEventSource eventSource,
        MasteryCalculationResult calculation,
        DateTime utcNow,
        Guid? createdBy,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(calculation);

        var breakdownJson = JsonSerializer.SerializeToDocument(calculation.Breakdown);

        var history = new TwinUpdateHistory
        {
            CenterId = centerId,
            StudentId = studentId,
            SubjectId = subjectId,
            TopicNodeId = topicNodeId,
            AttemptId = (attemptId.HasValue && attemptId.Value > 0) ? attemptId.Value : null,
            AnalysisId = (analysisId.HasValue && analysisId.Value > 0) ? analysisId.Value : null,
            EventSource = eventSource,
            PreviousMastery = calculation.PreviousMastery,
            NewMastery = calculation.NewMastery,
            MasteryDelta = calculation.Delta,
            EffectiveReasoningQuality = calculation.Breakdown.IsFallback
                ? null
                : (calculation.Breakdown.NormalizedReasoningQuality.HasValue
                    ? calculation.Breakdown.NormalizedReasoningQuality.Value * 100m
                    : null),
            CalculationVersion = MasteryCalculationVersion,
            CalculationBreakdown = breakdownJson,
            Explanation = calculation.Explanation,
            CreatedAt = utcNow,
            CreatedBy = createdBy
        };

        _dbContext.TwinUpdateHistories.Add(history);
        return Task.FromResult(history);
    }
}
