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

    /// <summary>Storage provider: "FileSystem" (default) or "Cloud" / "Supabase".</summary>
    public string Provider { get; set; } = "FileSystem";

    /// <summary>Endpoint or base URL for Cloud Object Storage (e.g. Supabase Storage: https://[ref].supabase.co/storage/v1).</summary>
    public string? CloudBaseUrl { get; set; }

    /// <summary>API key or Service Role secret for Cloud Object Storage.</summary>
    public string? CloudApiKey { get; set; }

    /// <summary>Private bucket name (default: "edutwin-attachments").</summary>
    public string BucketName { get; set; } = "edutwin-attachments";

    /// <summary>Data Protection key ring storage: "Auto" (default), "Database", or "FileSystem".</summary>
    public string DataProtectionKeyStorage { get; set; } = "Auto";

    /// <summary>Optional secret used to AES-256-GCM encrypt Data Protection keys at rest in MySQL.</summary>
    public string? DataProtectionKeyEncryptionSecret { get; set; }

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
