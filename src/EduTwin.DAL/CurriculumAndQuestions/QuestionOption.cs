using System;
using EduTwin.DAL.Persistence.Models;

namespace EduTwin.DAL.CurriculumAndQuestions;

public class QuestionOption : IMutableTenantAggregate
{
    public ulong OptionId { get; set; }
    public Guid CenterId { get; set; }
    public ulong QuestionId { get; set; }
    
    public string OptionLabel { get; set; } = null!;
    public string OptionText { get; set; } = null!;
    public bool IsCorrect { get; set; }
    public uint OrderIndex { get; set; }

    // Audit and MTA
    public DateTime CreatedAt { get; set; }
    public Guid? CreatedBy { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid? UpdatedBy { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public Guid? DeletedBy { get; set; }
    public ulong RowVersion { get; set; }

    // Navigations
    public Question? Question { get; set; }
}
