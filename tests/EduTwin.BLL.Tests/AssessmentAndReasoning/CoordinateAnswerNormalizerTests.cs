using EduTwin.BLL.AssessmentAndReasoning.PreliminaryGrading;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning;

public sealed class CoordinateAnswerNormalizerTests
{
    private readonly CoordinateAnswerNormalizer _normalizer = new();

    [Theory]
    [InlineData("(1,1)", "(1, 1)")]
    [InlineData("( 1 ; 1 )", "(1,1)")]
    [InlineData("\\left(1,1\\right)", "(1;1)")]
    [InlineData("I(1;1)", "[1,1]")]
    [InlineData("(1/2;2/4)", "(0.5;0.5)")]
    [InlineData("(−1;2)", "(-1;2)")]
    [InlineData("(1,5;2,5)", "(1.5;2.5)")]
    public void TryNormalize_EquivalentForms_ReturnSameValue(string left, string right)
    {
        Assert.True(_normalizer.TryNormalize(left, out var normalizedLeft));
        Assert.True(_normalizer.TryNormalize(right, out var normalizedRight));
        Assert.Equal(normalizedLeft, normalizedRight);
    }

    [Theory]
    [InlineData("(1,1,1)")]
    [InlineData("(1;)")]
    [InlineData("(1)")]
    [InlineData("not a coordinate")]
    [InlineData("1,1")]
    public void TryNormalize_MalformedInput_ReturnsFalse(string answer)
    {
        Assert.False(_normalizer.TryNormalize(answer, out _));
    }

    [Fact]
    public void TryNormalize_ZeroWidthCharacters_AreRemovedSafely()
    {
        Assert.True(_normalizer.TryNormalize("(1;\u200B2)", out var actual));
        Assert.True(_normalizer.TryNormalize("(1;2)", out var expected));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void TryNormalize_SwappedCoordinates_RemainDifferent()
    {
        Assert.True(_normalizer.TryNormalize("(1;2)", out var first));
        Assert.True(_normalizer.TryNormalize("(2;1)", out var second));
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void TryNormalize_InputOverLimit_ReturnsFalse()
    {
        Assert.False(_normalizer.TryNormalize($"({new string('1', 301)};2)", out _));
    }
}
