namespace EduTwin.API.AssessmentAndReasoning.Attachments;

public sealed class AttachmentStorageOptions
{
    public const string SectionName = "AttachmentStorage";
    public const int MinimumGracePeriodHours = 24;
    public const int DefaultGracePeriodHours = 24;
    public const int TempSafetyMarginHours = 2;

    private int _gracePeriodHours = DefaultGracePeriodHours;
    private int _cleanupIntervalMinutes = 60;

    public string? RootPath { get; set; }
    public string? DataProtectionKeysPath { get; set; }

    public int GracePeriodHours
    {
        get => _gracePeriodHours;
        set => _gracePeriodHours = Math.Max(MinimumGracePeriodHours, value);
    }

    public int CleanupIntervalMinutes
    {
        get => _cleanupIntervalMinutes;
        set => _cleanupIntervalMinutes = Math.Max(1, value);
    }
}
