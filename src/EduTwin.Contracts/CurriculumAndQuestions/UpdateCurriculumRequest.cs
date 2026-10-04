using System;
using System.ComponentModel.DataAnnotations;

namespace EduTwin.Contracts.CurriculumAndQuestions;

public class UpdateCurriculumRequest
{
    [Required]
    [StringLength(250)]
    public string? Title { get; set; }
    
    public string? Description { get; set; }
    
    [Range(10, 12)]
    public byte? GradeLevel { get; set; }

    [Required]
    public string? RowVersion { get; set; }
}
