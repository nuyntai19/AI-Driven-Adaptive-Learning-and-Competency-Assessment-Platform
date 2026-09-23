using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace EduTwin.Contracts.AssessmentAndReasoning;

public sealed class VoidAssignmentQuestionRequest
{
    private string _voidReason = string.Empty;

    [Required(ErrorMessage = "Lý do hủy câu không được để trống.")]
    [StringLength(1000, MinimumLength = 5, ErrorMessage = "Lý do hủy câu phải từ 5 đến 1000 ký tự.")]
    [JsonPropertyName("voidReason")]
    public string VoidReason
    {
        get => _voidReason;
        set => _voidReason = value ?? string.Empty;
    }

    /// <summary>
    /// Alias for clients sending "reason" instead of "voidReason".
    /// </summary>
    [JsonPropertyName("reason")]
    public string? ReasonAlias
    {
        get => _voidReason;
        set
        {
            if (!string.IsNullOrWhiteSpace(value) && string.IsNullOrWhiteSpace(_voidReason))
            {
                _voidReason = value.Trim();
            }
        }
    }

    private bool _archiveQuestionInBank = true;

    [JsonPropertyName("archiveQuestionInBank")]
    public bool ArchiveQuestionInBank
    {
        get => _archiveQuestionInBank;
        set => _archiveQuestionInBank = value;
    }

    /// <summary>
    /// Alias for clients sending "quarantineInBank" instead of "archiveQuestionInBank".
    /// </summary>
    [JsonPropertyName("quarantineInBank")]
    public bool? QuarantineAlias
    {
        get => _archiveQuestionInBank;
        set
        {
            if (value.HasValue)
            {
                _archiveQuestionInBank = value.Value;
            }
        }
    }
}
