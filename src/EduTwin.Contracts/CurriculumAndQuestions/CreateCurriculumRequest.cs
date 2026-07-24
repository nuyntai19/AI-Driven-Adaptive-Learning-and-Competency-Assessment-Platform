using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace EduTwin.Contracts.CurriculumAndQuestions;

public class CreateCurriculumRequest
{
    public string? TeacherId { get; set; }

    [Required]
    public Guid SubjectId { get; set; }

    [Required]
    [StringLength(250)]
    public string? Title { get; set; }

    public string? Description { get; set; }

    [Required]
    public List<string>? NodeIds { get; set; }
}
