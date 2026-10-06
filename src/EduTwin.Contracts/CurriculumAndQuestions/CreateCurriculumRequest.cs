using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace EduTwin.Contracts.CurriculumAndQuestions;

public class CreateCurriculumRequest
{
    public string? TeacherId { get; set; }
    public string Visibility { get; set; } = "Private";

    [Required]
    public Guid SubjectId { get; set; }

    [Required]
    [StringLength(250)]
    public string? Title { get; set; }

    public string? Description { get; set; }
    [Required]
    [Range(10, 12)]
    public byte? GradeLevel { get; set; }

    [Required]
    public List<string>? NodeIds { get; set; }
}
