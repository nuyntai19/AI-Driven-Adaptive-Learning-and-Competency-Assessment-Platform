using System;
using EduTwin.DAL.Persistence.Models;
using EduTwin.DAL.CurriculumAndQuestions;

namespace EduTwin.DAL.Assignments;

public class AssignmentQuestion : ITenantJoinEntity
{
    public Guid CenterId { get; set; }
    public Guid AssignmentId { get; set; }
    public ulong QuestionId { get; set; }

    public uint OrderIndex { get; set; }
    public decimal Points { get; set; }

    public DateTime CreatedAt { get; set; }

    public bool IsVoided { get; set; } = false;
    public string? VoidReason { get; set; }
    public DateTime? VoidedAt { get; set; }
    public Guid? VoidedByUserId { get; set; }

    // Navigations
    public Assignment? Assignment { get; set; }
    public Question? Question { get; set; }
}
