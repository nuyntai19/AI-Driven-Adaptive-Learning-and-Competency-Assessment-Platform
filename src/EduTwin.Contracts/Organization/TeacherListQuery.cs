using System.ComponentModel.DataAnnotations;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.Organization;

public class TeacherListQuery
{
    [Range(1, int.MaxValue)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;

    [StringLength(200)]
    public string? Search { get; set; }

    [EnumDataType(typeof(UserStatus))]
    public UserStatus? Status { get; set; }
}
