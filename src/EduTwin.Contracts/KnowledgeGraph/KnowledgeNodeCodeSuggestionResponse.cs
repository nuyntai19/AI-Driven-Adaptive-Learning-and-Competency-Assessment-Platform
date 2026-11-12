using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.KnowledgeGraph;

/// <summary>Next free standard node code for a prefix such as MATH10_T_PTBH (e.g. MATH10_T_PTBH_03).</summary>
public class KnowledgeNodeCodeSuggestionDto
{
    public string Code { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    public int Sequence { get; set; }
}

public class KnowledgeNodeCodeSuggestionResponse
{
    public KnowledgeNodeCodeSuggestionDto Data { get; set; } = null!;
    public MetaDto Meta { get; set; } = null!;
}
