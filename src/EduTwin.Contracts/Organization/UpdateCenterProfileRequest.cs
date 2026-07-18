using System.ComponentModel.DataAnnotations;

namespace EduTwin.Contracts.Organization;

public class UpdateCenterProfileRequest
{
    [Required(AllowEmptyStrings = false)]
    [StringLength(200, MinimumLength = 1)]
    public string CenterName { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    [StringLength(64, MinimumLength = 1)]
    public string Timezone { get; set; } = string.Empty;

    [Required(AllowEmptyStrings = false)]
    [RegularExpression(@"^[0-9]+$", ErrorMessage = "Phiên bản dữ liệu phải là chuỗi số nguyên dương.")]
    public string RowVersion { get; set; } = string.Empty;
}
