using System.Collections.Generic;
using EduTwin.BLL.KnowledgeGraph;
using Xunit;

namespace EduTwin.BLL.Tests.KnowledgeGraph;

public class KnowledgeNodeHierarchyCycleDetectorTests
{
    private readonly KnowledgeNodeHierarchyCycleDetector _sut;

    public KnowledgeNodeHierarchyCycleDetectorTests()
    {
        _sut = new KnowledgeNodeHierarchyCycleDetector();
    }

    [Fact]
    public void HasCycle_NullParent_ReturnsFalse()
    {
        var map = new Dictionary<ulong, ulong?>();
        var result = _sut.HasCycle(1, null, map);
        Assert.False(result);
    }

    [Fact]
    public void HasCycle_DirectSelfParent_ReturnsTrue()
    {
        var map = new Dictionary<ulong, ulong?>();
        var result = _sut.HasCycle(1, 1, map);
        Assert.True(result);
    }

    [Fact]
    public void HasCycle_DirectChildSelectedAsParent_ReturnsTrue()
    {
        var map = new Dictionary<ulong, ulong?>
        {
            { 2, 1 } // 2 is child of 1
        };
        // Target 1 wants to be child of 2
        var result = _sut.HasCycle(1, 2, map);
        Assert.True(result);
    }

    [Fact]
    public void HasCycle_DeepDescendantSelectedAsParent_ReturnsTrue()
    {
        var map = new Dictionary<ulong, ulong?>
        {
            { 2, 1 },
            { 3, 2 },
            { 4, 3 }
        };
        // Target 1 wants to be child of 4
        var result = _sut.HasCycle(1, 4, map);
        Assert.True(result);
    }

    [Fact]
    public void HasCycle_ValidAncestorOrRootOrSibling_ReturnsFalse()
    {
        var map = new Dictionary<ulong, ulong?>
        {
            { 2, null },
            { 3, 2 },
            { 4, 2 }
        };

        Assert.False(_sut.HasCycle(1, 3, map));
        Assert.False(_sut.HasCycle(1, 2, map));
        Assert.False(_sut.HasCycle(1, 4, map));
    }

    [Fact]
    public void HasCycle_ExistingCorruptRepeatedChain_FailClosed_DoesNotHang()
    {
        var map = new Dictionary<ulong, ulong?>
        {
            { 2, 3 },
            { 3, 4 },
            { 4, 2 }
        };

        // Target 1 wants to be child of 2
        // Walks: 2 -> 3 -> 4 -> 2 (detects cycle!)
        var result = _sut.HasCycle(1, 2, map);
        Assert.True(result);
    }

    [Fact]
    public void HasCycle_SameInput_IsDeterministic()
    {
        var map = new Dictionary<ulong, ulong?>
        {
            { 2, 1 },
            { 3, 2 },
            { 4, 3 }
        };
        var result1 = _sut.HasCycle(1, 4, map);
        var result2 = _sut.HasCycle(1, 4, map);
        var result3 = _sut.HasCycle(1, 4, map);

        Assert.True(result1);
        Assert.True(result2);
        Assert.True(result3);
        Assert.Equal(3, map.Count); // map not mutated
    }

    [Fact]
    public void HasCycle_LongFiniteChain_CompletesWithoutRecursion()
    {
        var map = new Dictionary<ulong, ulong?>();
        ulong current = 2;
        for (int i = 0; i < 10000; i++)
        {
            map[current] = current + 1;
            current++;
        }
        map[current] = null; // end of chain

        var result = _sut.HasCycle(1, 2, map);
        Assert.False(result);
    }
}
