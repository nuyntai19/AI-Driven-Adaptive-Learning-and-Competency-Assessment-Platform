using System.ComponentModel.DataAnnotations;

namespace EduTwin.Contracts.CurriculumAndQuestions;

public class ArchiveCurriculumRequest
{
    [Required]
    public string? RowVersion { get; set; }
}
