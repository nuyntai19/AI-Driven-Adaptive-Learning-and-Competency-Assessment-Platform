using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence.Models;

namespace EduTwin.DAL.CurriculumAndQuestions;

// Ending an application retains its original dates, actor and grade snapshots.
public class ClassCurriculumApplication : ITenantJoinEntity
{
    public Guid ApplicationId { get; set; }
    public Guid CenterId { get; set; }
    public Guid ClassId { get; set; }
    public Guid CurriculumId { get; set; }
    public Guid SubjectId { get; set; }
    public string ApplicationRole { get; set; } = "Primary";
    public DateTime StartedAt { get; set; }
    public Guid AssignedBy { get; set; }
    public DateTime? EndedAt { get; set; }
    public Guid? EndedBy { get; set; }
    public string? ChangeReason { get; set; }
    public string? EndReason { get; set; }
    public byte? ClassGradeAtStart { get; set; }
    public byte? CurriculumGradeAtStart { get; set; }
    public bool IsGradeException { get; set; }
    public string? GradeMismatchReason { get; set; }
    public Guid? ExceptionApprovedBy { get; set; }
    public DateTime? ExceptionApprovedAt { get; set; }
    public string? CurrentPrimaryKey { get; private set; }
    public string? CurrentCurriculumKey { get; private set; }
    public Class Class { get; set; } = null!;
    public Curriculum Curriculum { get; set; } = null!;
}
