using System;
using System.ComponentModel.DataAnnotations;

namespace EduTwin.Contracts.CurriculumAndQuestions;

public class PublishCurriculumRequest
{
    [Required]
    public string? RowVersion { get; set; }
}
