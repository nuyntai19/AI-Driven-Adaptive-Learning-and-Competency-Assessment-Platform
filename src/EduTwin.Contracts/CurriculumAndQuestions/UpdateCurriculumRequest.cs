using System;
using System.ComponentModel.DataAnnotations;

namespace EduTwin.Contracts.CurriculumAndQuestions;

public class UpdateCurriculumRequest
{
    public string? Visibility { get; set; }
    [Required]
    [StringLength(250)]
    public string? Title { get; set; }
    
    public string? Description { get; set; }
    
    [Required]
    [Range(10, 12)]
    public byte? GradeLevel { get; set; }
    public Guid? GraphVersionId { get; set; }
    // Optional atomic replacement when changing grade. Omitted means retain the selection.
    public System.Collections.Generic.List<string>? NodeIds { get; set; }
    public System.Collections.Generic.List<string>? ClassIds { get; set; }

    [Required]
    public string? RowVersion { get; set; }
}
