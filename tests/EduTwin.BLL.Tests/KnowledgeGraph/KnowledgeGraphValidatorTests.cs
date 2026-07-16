using System;
using System.Collections.Generic;
using Xunit;
using EduTwin.BLL.KnowledgeGraph;
using EduTwin.DAL.KnowledgeGraph;

namespace EduTwin.BLL.Tests.KnowledgeGraph;

public class KnowledgeGraphValidatorTests
{
    private readonly KnowledgeGraphValidator _validator = new();

    [Fact]
    public void ValidateDag_ValidDag_ShouldNotThrow()
    {
        var edges = new List<KnowledgeEdge>
        {
            new() { SourceNodeId = 1, TargetNodeId = 2, RelationType = EduTwin.Contracts.KnowledgeGraph.RelationType.PrerequisiteOf },
            new() { SourceNodeId = 2, TargetNodeId = 3, RelationType = EduTwin.Contracts.KnowledgeGraph.RelationType.PrerequisiteOf }
        };

        var exception = Record.Exception(() => _validator.ValidateDag(edges));
        Assert.Null(exception);
    }

    [Fact]
    public void ValidateDag_SelfLoop_ShouldThrowInvalidOperationException()
    {
        var edges = new List<KnowledgeEdge>
        {
            new() { SourceNodeId = 1, TargetNodeId = 1, RelationType = EduTwin.Contracts.KnowledgeGraph.RelationType.PrerequisiteOf }
        };

        Assert.Throws<InvalidOperationException>(() => _validator.ValidateDag(edges));
    }

    [Fact]
    public void ValidateDag_Cycle_ShouldThrowInvalidOperationException()
    {
        var edges = new List<KnowledgeEdge>
        {
            new() { SourceNodeId = 1, TargetNodeId = 2, RelationType = EduTwin.Contracts.KnowledgeGraph.RelationType.PrerequisiteOf },
            new() { SourceNodeId = 2, TargetNodeId = 3, RelationType = EduTwin.Contracts.KnowledgeGraph.RelationType.PrerequisiteOf },
            new() { SourceNodeId = 3, TargetNodeId = 1, RelationType = EduTwin.Contracts.KnowledgeGraph.RelationType.PrerequisiteOf }
        };

        Assert.Throws<InvalidOperationException>(() => _validator.ValidateDag(edges));
    }
}
