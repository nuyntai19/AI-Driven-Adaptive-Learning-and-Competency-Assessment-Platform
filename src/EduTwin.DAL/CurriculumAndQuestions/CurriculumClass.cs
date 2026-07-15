using System;
using EduTwin.DAL.Persistence.Models;
using EduTwin.DAL.Organization;

namespace EduTwin.DAL.CurriculumAndQuestions;

public class CurriculumClass : ITenantJoinEntity
{
    public Guid CenterId { get; set; }
    public Guid CurriculumId { get; set; }
    public Guid ClassId { get; set; }

    public DateTime AssignedAt { get; set; }
    public Guid AssignedBy { get; set; }

    // Navigations
    public Curriculum? Curriculum { get; set; }
    public Class? Class { get; set; }
}
