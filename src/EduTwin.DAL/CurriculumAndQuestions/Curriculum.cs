using System;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.DAL.Persistence.Models;
using EduTwin.DAL.Organization;

namespace EduTwin.DAL.CurriculumAndQuestions;

public class Curriculum : IMutableTenantAggregate
{
    public Guid CurriculumId { get; set; }
    public Guid CenterId { get; set; }
    public Guid TeacherId { get; set; }
    public Guid SubjectId { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public string? SourceFile { get; set; }
    public ReviewStatus ReviewStatus { get; set; }

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
    public Teacher? Teacher { get; set; }
    public Subject? Subject { get; set; }
}
