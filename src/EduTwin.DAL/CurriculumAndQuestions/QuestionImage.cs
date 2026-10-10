using EduTwin.DAL.Persistence.Models;

namespace EduTwin.DAL.CurriculumAndQuestions;

// Kept separately from Question so bank/list queries never load image bytes.
public sealed class QuestionImage : ITenantJoinEntity
{
    public Guid CenterId { get; set; }
    public ulong QuestionId { get; set; }
    public byte[] Data { get; set; } = [];
    public string Sha256 { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public Question? Question { get; set; }
}
