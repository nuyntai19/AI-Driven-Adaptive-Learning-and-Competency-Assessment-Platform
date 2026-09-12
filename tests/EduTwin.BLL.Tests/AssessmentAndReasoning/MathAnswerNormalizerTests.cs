using System.Numerics;
using EduTwin.BLL.AssessmentAndReasoning.PreliminaryGrading;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning;

public class MathAnswerNormalizerTests
{
    private readonly MathAnswerNormalizer _normalizer = new();

    [Theory]
    [InlineData("0", "0", "1")]
    [InlineData("+0", "0", "1")]
    [InlineData("-0", "0", "1")]
    [InlineData("42", "42", "1")]
    [InlineData("-7", "-7", "1")]
    [InlineData("+15", "15", "1")]
    public void TryNormalize_Integers_ReturnsCanonicalFraction(string input, string expectedNum, string expectedDen)
    {
        var success = _normalizer.TryNormalize(input, out var frac);

        Assert.True(success);
        Assert.NotNull(frac);
        Assert.Equal(BigInteger.Parse(expectedNum), frac.Value.Numerator);
        Assert.Equal(BigInteger.Parse(expectedDen), frac.Value.Denominator);
    }

    [Theory]
    [InlineData("1/2", "1", "2")]
    [InlineData("2/4", "1", "2")]
    [InlineData("3/6", "1", "2")]
    [InlineData("-3/4", "-3", "4")]
    [InlineData("3/-4", "-3", "4")]
    [InlineData("-3/-4", "3", "4")]
    [InlineData("6/2", "3", "1")]
    [InlineData("15/5", "3", "1")]
    [InlineData(" 1 / 2 ", "1", "2")]
    public void TryNormalize_Fractions_ReducesToLowestTerms(string input, string expectedNum, string expectedDen)
    {
        var success = _normalizer.TryNormalize(input, out var frac);

        Assert.True(success);
        Assert.NotNull(frac);
        Assert.Equal(BigInteger.Parse(expectedNum), frac.Value.Numerator);
        Assert.Equal(BigInteger.Parse(expectedDen), frac.Value.Denominator);
    }

    [Theory]
    [InlineData("1 1/2", "3", "2")]
    [InlineData("2 3/4", "11", "4")]
    [InlineData("-1 1/2", "-3", "2")]
    [InlineData("-2 3/4", "-11", "4")]
    [InlineData("3 2/4", "7", "2")]
    [InlineData(" 1   1/2 ", "3", "2")]
    public void TryNormalize_MixedNumbers_ConvertsToImproperFraction(string input, string expectedNum, string expectedDen)
    {
        var success = _normalizer.TryNormalize(input, out var frac);

        Assert.True(success);
        Assert.NotNull(frac);
        Assert.Equal(BigInteger.Parse(expectedNum), frac.Value.Numerator);
        Assert.Equal(BigInteger.Parse(expectedDen), frac.Value.Denominator);
    }

    [Theory]
    [InlineData("0.5", "1", "2")]
    [InlineData("0,5", "1", "2")]
    [InlineData("0.25", "1", "4")]
    [InlineData("0,25", "1", "4")]
    [InlineData("-1.75", "-7", "4")]
    [InlineData("-1,75", "-7", "4")]
    [InlineData("3.14", "157", "50")]
    [InlineData("0.000", "0", "1")]
    [InlineData("3.0", "3", "1")]
    [InlineData(".5", "1", "2")]
    public void TryNormalize_FiniteDecimals_ConvertsToCanonicalFraction(string input, string expectedNum, string expectedDen)
    {
        var success = _normalizer.TryNormalize(input, out var frac);

        Assert.True(success);
        Assert.NotNull(frac);
        Assert.Equal(BigInteger.Parse(expectedNum), frac.Value.Numerator);
        Assert.Equal(BigInteger.Parse(expectedDen), frac.Value.Denominator);
    }

    [Fact]
    public void TryNormalize_VeryLargeNumbers_HandledWithoutOverflow()
    {
        var largeNum = "123456789012345678901234567890";
        var success = _normalizer.TryNormalize(largeNum, out var frac);

        Assert.True(success);
        Assert.NotNull(frac);
        Assert.Equal(BigInteger.Parse(largeNum), frac.Value.Numerator);
        Assert.Equal(BigInteger.One, frac.Value.Denominator);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    [InlineData("abc")]
    [InlineData("1/0")]
    [InlineData("-2/0")]
    [InlineData("1 1/0")]
    [InlineData("1.2.3")]
    [InlineData("1/2/3")]
    [InlineData("1 2 3")]
    [InlineData("x + 1")]
    public void TryNormalize_InvalidInputs_ReturnsFalse(string? input)
    {
        var success = _normalizer.TryNormalize(input, out var frac);

        Assert.False(success);
        Assert.Null(frac);
    }

    [Theory]
    [InlineData("1/2", "0.5", true)]
    [InlineData("0.5", "1/2", true)]
    [InlineData("2/4", "3/6", true)]
    [InlineData("1 1/2", "1.5", true)]
    [InlineData("-3/4", "-0.75", true)]
    [InlineData("0", "0.0", true)]
    [InlineData("1/3", "0.33", false)] // 1/3 is repeating, not 33/100
    [InlineData("1/2", "-1/2", false)]
    [InlineData("1/2", "abc", false)]
    [InlineData("abc", "def", false)]
    [InlineData(@"\frac{3}{2}", "3/2", true)] // Visual LaTeX matches semantic fraction
    [InlineData(@"\frac{3}{2}", "1.5", true)]
    [InlineData(@"\frac{-3}{4}", "-0.75", true)]
    [InlineData(@"\frac{2}{4}", "1/2", true)]
    public void AreEquivalent_EquivalenceChecks_ReturnsExpectedResult(string answerA, string answerB, bool expectedEquivalent)
    {
        var result = _normalizer.AreEquivalent(answerA, answerB);

        Assert.Equal(expectedEquivalent, result);
    }

    [Theory]
    [InlineData("1 1/-2")]
    [InlineData("1 -1/2")]
    [InlineData("-1 -1/2")]
    [InlineData("-1 1/-2")]
    public void TryNormalize_AmbiguousMixedNumberSigns_ReturnsFalse(string input)
    {
        var success = _normalizer.TryNormalize(input, out var frac);

        Assert.False(success);
        Assert.Null(frac);
    }

    [Fact]
    public void TryNormalize_ExceedsMaxRawLength_ReturnsFalse()
    {
        var overlyLongInput = new string('1', 129);
        var success = _normalizer.TryNormalize(overlyLongInput, out var frac);

        Assert.False(success);
        Assert.Null(frac);
    }

    [Fact]
    public void TryNormalize_ExceedsMaxDecimalDigits_ReturnsFalse()
    {
        var overlyLongDecimal = "0." + new string('1', 31);
        var success = _normalizer.TryNormalize(overlyLongDecimal, out var frac);

        Assert.False(success);
        Assert.Null(frac);
    }
}
