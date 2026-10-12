using System.Diagnostics.Metrics;

namespace EduTwin.BLL.AssessmentAndReasoning.Processing;

public static class AIProcessingMetrics
{
    public static readonly Meter Meter = new("EduTwin.AIProcessing", "1.0");
    public static readonly Histogram<double> Duration = Meter.CreateHistogram<double>("edutwin.ai.stage.duration", "ms");
    public static readonly Histogram<double> QueueWait = Meter.CreateHistogram<double>("edutwin.ai.queue.wait", "ms");
    public static readonly Counter<long> Outcomes = Meter.CreateCounter<long>("edutwin.ai.outcomes");
    public static readonly Counter<long> CheckpointHits = Meter.CreateCounter<long>("edutwin.ai.checkpoint.hits");
    public static readonly Counter<long> Tokens = Meter.CreateCounter<long>("edutwin.ai.tokens");
    public static readonly Histogram<int> BatchSize = Meter.CreateHistogram<int>("edutwin.ai.microbatch.size", "questions");
}
