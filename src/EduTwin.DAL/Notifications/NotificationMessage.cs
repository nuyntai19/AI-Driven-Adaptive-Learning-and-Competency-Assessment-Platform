using EduTwin.DAL.Persistence.Models;

namespace EduTwin.DAL.Notifications;

public sealed class NotificationMessage : ITenantAppendOnlyEntity
{
    public Guid MessageId { get; set; }
    public Guid CenterId { get; set; }
    public Guid SenderUserId { get; set; }
    public Guid ClientMessageId { get; set; }
    public string SenderName { get; set; } = "";
    public uint SenderAuthVersion { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string Priority { get; set; } = "Normal";
    public string Category { get; set; } = "Information";
    public bool RequiresAcknowledgement { get; set; }
    public bool AllowReply { get; set; }
    public Guid? CorrectionOfMessageId { get; set; }
    public string Audience { get; set; } = "";
    public string Fingerprint { get; set; } = "";
    public int RecipientCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
}
