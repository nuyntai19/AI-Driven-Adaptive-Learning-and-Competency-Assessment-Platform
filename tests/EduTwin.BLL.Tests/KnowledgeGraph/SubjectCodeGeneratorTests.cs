using EduTwin.BLL.KnowledgeGraph;
using Xunit;

namespace EduTwin.BLL.Tests.KnowledgeGraph;

public sealed class SubjectCodeGeneratorTests
{
    [Theory]
    [InlineData("Toán 12", "TOAN12")]
    [InlineData("Địa lý", "DIALY")]
    [InlineData("Tiếng Anh nâng cao", "TIENGANHNANGCAO")]
    [InlineData("!!!", "MH")]
    public void BaseCode_IsReadableAscii(string name, string expected) =>
        Assert.Equal(expected, SubjectCodeGenerator.BaseCode(name));

    [Fact]
    public void BaseCode_LeavesRoomForASuffixWithinTheColumnLimit() =>
        Assert.True(SubjectCodeGenerator.BaseCode(new string('a', 100)).Length + 6 <= 32);

    [Fact]
    public void FirstFree_AppendsTheFirstUnusedNumber()
    {
        Assert.Equal("TOAN", SubjectCodeGenerator.FirstFree("TOAN", ["TOAN12"]));
        Assert.Equal("TOAN-3", SubjectCodeGenerator.FirstFree("TOAN", ["toan", "TOAN-2"]));
    }
}
