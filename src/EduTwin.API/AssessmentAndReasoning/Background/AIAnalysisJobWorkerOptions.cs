namespace EduTwin.API.AssessmentAndReasoning.Background;

public sealed class AIAnalysisJobWorkerOptions
{
    private static readonly TimeSpan MaximumTimerInterval =
        TimeSpan.FromMilliseconds(uint.MaxValue - 1u);

    public TimeSpan PollInterval { get; set; } = TimeSpan.FromSeconds(3);
    public TimeSpan LeaseDuration { get; set; } = TimeSpan.FromMinutes(5);
    public int BatchSize { get; set; } = 50;
    public int PerCenterBatchSize { get; set; } = 25;

    public void Validate()
    {
        if (PollInterval <= TimeSpan.Zero || PollInterval > MaximumTimerInterval)
        {
            throw new InvalidOperationException(
                "AI job poll interval must be positive and supported by the host timer.");
        }

        if (LeaseDuration <= TimeSpan.Zero
            || LeaseDuration > DateTime.MaxValue - DateTime.UnixEpoch)
        {
            throw new InvalidOperationException(
                "AI job lease duration must be positive and finite for a UTC timestamp.");
        }

        if (BatchSize <= 0)
        {
            throw new InvalidOperationException("AI job batch size must be positive.");
        }

        if (PerCenterBatchSize <= 0)
        {
            throw new InvalidOperationException("AI job per-center batch size must be positive.");
        }
    }
}
