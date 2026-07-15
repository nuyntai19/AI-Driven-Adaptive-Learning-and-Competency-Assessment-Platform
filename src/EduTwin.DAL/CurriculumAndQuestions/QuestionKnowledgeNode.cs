using System;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.DAL.Persistence.Models;
using EduTwin.DAL.KnowledgeGraph;

namespace EduTwin.DAL.CurriculumAndQuestions;

public class QuestionKnowledgeNode : ITenantJoinEntity
{
    public Guid CenterId { get; set; }
    public ulong QuestionId { get; set; }
    public ulong NodeId { get; set; }

    public MappingRole MappingRole { get; set; }
    public DateTime CreatedAt { get; set; }

    // Navigations
    public Question? Question { get; set; }
    public KnowledgeNode? Node { get; set; }
}
