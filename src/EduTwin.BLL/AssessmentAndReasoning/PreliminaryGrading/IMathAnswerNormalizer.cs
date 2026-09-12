using System.Diagnostics.CodeAnalysis;
using System.Numerics;

namespace EduTwin.BLL.AssessmentAndReasoning.PreliminaryGrading;

public readonly record struct RationalFraction(BigInteger Numerator, BigInteger Denominator)
{
    public override string ToString() => Denominator == 1 ? Numerator.ToString() : $"{Numerator}/{Denominator}";
}

public interface IMathAnswerNormalizer
{
    /// <summary>
    /// Attempts to parse and normalize a student or reference answer into an irreducible canonical rational fraction P/Q where Q > 0 and gcd(|P|, Q) == 1.
    /// Handles integers, fractions, mixed numbers, and finite decimals.
    /// </summary>
    bool TryNormalize(string? rawAnswer, [NotNullWhen(true)] out RationalFraction? normalized);

    /// <summary>
    /// Compares two raw answers under rational arithmetic equivalence.
    /// </summary>
    bool AreEquivalent(string? answerA, string? answerB);
}
