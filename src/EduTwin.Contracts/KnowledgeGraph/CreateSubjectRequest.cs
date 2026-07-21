using System.ComponentModel.DataAnnotations;

namespace EduTwin.Contracts.KnowledgeGraph;

public class CreateSubjectRequest
{
    [Required(AllowEmptyStrings = false)]
    [MaxLength(32)]
    public string SubjectCode { get; set; } = default!;

    [Required(AllowEmptyStrings = false)]
    [MaxLength(100)]
    public string SubjectName { get; set; } = default!;

    [MaxLength(500)]
    public string? Description { get; set; }
}
