using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace EduTwin.Contracts.CurriculumAndQuestions;

public class AssignCurriculumClassesRequest
{
    [Required]
    public List<Guid>? ClassIds { get; set; }
    
    [Required]
    public string? RowVersion { get; set; }
}
