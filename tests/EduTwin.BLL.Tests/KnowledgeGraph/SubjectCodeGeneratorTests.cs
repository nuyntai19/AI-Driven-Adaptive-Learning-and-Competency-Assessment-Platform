using EduTwin.BLL.KnowledgeGraph;
using Xunit;

namespace EduTwin.BLL.Tests.KnowledgeGraph;

public sealed class SubjectCodeGeneratorTests
{
    [Theory]
    [InlineData("Địa lý", "DIALY")]
    [InlineData("Sinh học 11", "SINHHOC11")]
    [InlineData("Tiếng Anh nâng cao", "TIENGANHNANGCAO")]
    [InlineData("!!!", "MH")]
    public void BaseCode_IsReadableAscii(string name, string expected) =>
        Assert.Equal(expected, SubjectCodeGenerator.BaseCode(name));

    [Theory]
    [InlineData("Toán", "MATH")]
    [InlineData("Toán 12", "MATH12")]
    [InlineData("Môn Toán học", "MATH")]
    [InlineData("Vật lý", "PHYS")]
    [InlineData("Vật lí 10", "PHYS10")]
    [InlineData("Lý", "PHYS")]
    [InlineData("Hóa học", "CHEM")]
    [InlineData("Hoá học 11", "CHEM11")]
    [InlineData("Tiếng Anh", "ENG")]
    [InlineData("Tiếng Anh 12", "ENG12")]
    [InlineData("English", "ENG")]
    [InlineData("Physics", "PHYS")]
    public void BaseCode_UsesTheStandardCodeForTheFourProgrammeSubjects(string name, string expected) =>
        Assert.Equal(expected, SubjectCodeGenerator.BaseCode(name));

    [Theory]
    [InlineData("Toán nâng cao")]
    [InlineData("Hóa sinh")]
    [InlineData("Ngữ văn")]
    public void StandardCode_DoesNotGuessForOtherSubjects(string name) =>
        Assert.Null(SubjectCodeGenerator.StandardCode(name));

    [Fact]
    public void BaseCode_LeavesRoomForASuffixWithinTheColumnLimit() =>
        Assert.True(SubjectCodeGenerator.BaseCode(new string('a', 100)).Length + 6 <= 32);

    [Fact]
    public void FirstFree_AppendsTheFirstUnusedNumber()
    {
        Assert.Equal("TOAN", SubjectCodeGenerator.FirstFree("TOAN", ["TOAN12"]));
        Assert.Equal("TOAN-3", SubjectCodeGenerator.FirstFree("TOAN", ["toan", "TOAN-2"]));
        Assert.Equal("PHYS-2", SubjectCodeGenerator.FirstFree(SubjectCodeGenerator.BaseCode("Vật lý"), ["PHYS"]));
    }
}
