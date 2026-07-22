using System.Collections.Generic;
using EduTwin.Contracts.KnowledgeGraph;

namespace EduTwin.BLL.KnowledgeGraph;

public class ListKnowledgeNodesResult
{
    public bool IsSuccess { get; private set; }
    public List<KnowledgeNodeDto>? Data { get; private set; }
    public string? ErrorCode { get; private set; }

    public static ListKnowledgeNodesResult Success(List<KnowledgeNodeDto> data) => new() { IsSuccess = true, Data = data };
    public static ListKnowledgeNodesResult Failure(string errorCode) => new() { IsSuccess = false, ErrorCode = errorCode };
}
