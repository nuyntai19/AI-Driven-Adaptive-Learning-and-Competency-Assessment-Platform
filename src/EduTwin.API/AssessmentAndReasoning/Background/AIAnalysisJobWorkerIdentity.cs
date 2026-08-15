namespace EduTwin.API.AssessmentAndReasoning.Background;

public sealed class AIAnalysisJobWorkerIdentity
{
    public AIAnalysisJobWorkerIdentity(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var normalized = new string(value.Where(character => !char.IsControl(character)).ToArray())
            .Trim();
        if (normalized.Length is 0 or > 100)
        {
            throw new ArgumentException(
                "Worker identity must contain between 1 and 100 normalized characters.",
                nameof(value));
        }

        Value = normalized;
    }

    public string Value { get; }
}
