using System.ComponentModel.DataAnnotations;

namespace EduTwin.Contracts.KnowledgeGraph;

public class CreateSubjectRequest
{
    /// <summary>Optional: the server generates a code from the name when omitted.</summary>
    [MaxLength(32)]
    public string? SubjectCode { get; set; }

    [Required(AllowEmptyStrings = false)]
    [MaxLength(100)]
    public string SubjectName { get; set; } = default!;

    [MaxLength(500)]
    public string? Description { get; set; }
}
