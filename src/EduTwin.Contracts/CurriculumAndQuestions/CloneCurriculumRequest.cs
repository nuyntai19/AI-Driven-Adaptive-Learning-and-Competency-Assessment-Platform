using System.ComponentModel.DataAnnotations;

namespace EduTwin.Contracts.CurriculumAndQuestions;

public class CloneCurriculumRequest
{
    [StringLength(200, ErrorMessage = "Tên giáo trình không được vượt quá 200 ký tự.")]
    public string? Title { get; set; }
}
