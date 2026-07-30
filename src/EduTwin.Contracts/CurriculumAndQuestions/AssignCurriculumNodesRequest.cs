using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace EduTwin.Contracts.CurriculumAndQuestions;

public class AssignCurriculumNodesRequest
{
    [Required]
    public List<string>? NodeIds { get; set; }
    
    [Required]
    public string? RowVersion { get; set; }
}
