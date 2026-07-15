using System;
using EduTwin.DAL.Persistence.Models;
using EduTwin.DAL.KnowledgeGraph;

namespace EduTwin.DAL.CurriculumAndQuestions;

public class CurriculumNode : ITenantJoinEntity
{
    public Guid CenterId { get; set; }
    public Guid CurriculumId { get; set; }
    public ulong NodeId { get; set; }

    public uint OrderIndex { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigations
    public Curriculum? Curriculum { get; set; }
    public KnowledgeNode? Node { get; set; }
}
