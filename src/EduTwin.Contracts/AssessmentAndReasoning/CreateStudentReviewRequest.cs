using System.ComponentModel.DataAnnotations;

namespace EduTwin.Contracts.AssessmentAndReasoning;

public sealed class CreateStudentReviewRequest
{
    [Required(ErrorMessage = "Lý do yêu cầu xem xét không được để trống.")]
    [StringLength(1000, MinimumLength = 5, ErrorMessage = "Lý do yêu cầu xem xét phải từ 5 đến 1000 ký tự.")]
    public string StudentComment { get; set; } = string.Empty;

    public string? Reason
    {
        get => StudentComment;
        set
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                StudentComment = value;
            }
        }
    }
}
