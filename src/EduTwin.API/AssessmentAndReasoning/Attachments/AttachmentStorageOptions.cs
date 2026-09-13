namespace EduTwin.API.AssessmentAndReasoning.Attachments;

public sealed class AttachmentStorageOptions
{
    public const string SectionName = "AttachmentStorage";
    public string? RootPath { get; set; }
    public string? DataProtectionKeysPath { get; set; }
    public int GracePeriodHours { get; set; } = 24;
}
