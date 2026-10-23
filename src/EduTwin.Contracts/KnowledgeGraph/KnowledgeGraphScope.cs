namespace EduTwin.Contracts.KnowledgeGraph;

public class KnowledgeGraphQuery
{
    public Guid SubjectId { get; set; }
    public byte? GradeLevel { get; set; }
    public Guid? GraphVersionId { get; set; }
    public Guid? CurriculumId { get; set; }
    public Guid? ClassId { get; set; }
    public string? AcademicYear { get; set; }
    public bool History { get; set; }
}

public class KnowledgeGraphResolvedScope
{
    public string Kind { get; set; } = "Subject";
    public string SubjectId { get; set; } = "";
    public byte? GradeLevel { get; set; }
    public string? GraphVersionId { get; set; }
    public string? CurriculumId { get; set; }
    public string? ClassId { get; set; }
    public string? AcademicYear { get; set; }
    public bool History { get; set; }
    public bool IsLegacy { get; set; }
    public bool ReadOnly { get; set; }
    public string? Message { get; set; }
}

public class KnowledgeNodeProvenanceDto
{
    public string OccurrenceId { get; set; } = "";
    public string NodeId { get; set; } = "";
    public string GraphVersionId { get; set; } = "";
    public byte GradeLevel { get; set; }
    public string? CurriculumId { get; set; }
    public string? ParentNodeId { get; set; }
    public string? CurriculumTitle { get; set; }
    public string? ApplicationId { get; set; }
    public string? ApplicationRole { get; set; }
    public bool IsGradeException { get; set; }
    public string? GradeMismatchReason { get; set; }
}

public class KnowledgeNodeMappingDto
{
    public string NodeId { get; set; } = "";
    public string? ParentNodeId { get; set; }
}

public class KnowledgeEdgeMappingDto
{
    public string EdgeId { get; set; } = "";
    public string RelationType { get; set; } = "PrerequisiteOf";
    public decimal Weight { get; set; } = 1m;
    public Guid? SourceGraphVersionId { get; set; }
}

public class KnowledgeGraphVersionDto
{
    public string GraphVersionId { get; set; } = "";
    public string SubjectId { get; set; } = "";
    public byte GradeLevel { get; set; }
    public string Status { get; set; } = "Draft";
    public bool IsCurrent { get; set; }
    public bool IsLegacy { get; set; }
    public string? Title { get; set; }
    public string? SourceGraphVersionId { get; set; }
    public string RowVersion { get; set; } = "";
    public int NodeCount { get; set; }
    public int EdgeCount { get; set; }
    public IReadOnlyList<KnowledgeNodeMappingDto> NodeMappings { get; set; } = [];
    public IReadOnlyList<KnowledgeEdgeMappingDto> EdgeMappings { get; set; } = [];
}

public class CreateKnowledgeGraphVersionRequest
{
    public Guid SubjectId { get; set; }
    public byte GradeLevel { get; set; }
    public string? Title { get; set; }
    public Guid? SourceGraphVersionId { get; set; }
}

public class UpdateKnowledgeGraphVersionRequest
{
    public string? RowVersion { get; set; }
    public IReadOnlyList<KnowledgeNodeMappingDto> NodeMappings { get; set; } = [];
    public IReadOnlyList<KnowledgeEdgeMappingDto> EdgeMappings { get; set; } = [];
}

public class PublishKnowledgeGraphVersionRequest
{
    public string? RowVersion { get; set; }
}
