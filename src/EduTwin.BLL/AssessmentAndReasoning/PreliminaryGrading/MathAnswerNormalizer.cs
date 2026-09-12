using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Numerics;
using System.Text.RegularExpressions;

namespace EduTwin.BLL.AssessmentAndReasoning.PreliminaryGrading;

public class MathAnswerNormalizer : IMathAnswerNormalizer
{
    // Regex for mixed number: [+-]?integer followed by space followed by fraction [+-]?num/den
    private static readonly Regex MixedNumberRegex = new(
        @"^([+-]?\d+)\s+([+-]?\d+)\s*/\s*([+-]?\d+)$",
        RegexOptions.Compiled);

    // Regex for simple fraction: [+-]?num / [+-]?den
    private static readonly Regex SimpleFractionRegex = new(
        @"^([+-]?\d+)\s*/\s*([+-]?\d+)$",
        RegexOptions.Compiled);

    public bool TryNormalize(string? rawAnswer, [NotNullWhen(true)] out RationalFraction? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(rawAnswer))
            return false;

        var text = rawAnswer.Trim();

        // 1. Try mixed number: e.g. "1 1/2", "-2 3/4"
        var mixedMatch = MixedNumberRegex.Match(text);
        if (mixedMatch.Success)
        {
            if (!BigInteger.TryParse(mixedMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var whole) ||
                !BigInteger.TryParse(mixedMatch.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var fracNum) ||
                !BigInteger.TryParse(mixedMatch.Groups[3].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var fracDen))
            {
                return false;
            }

            if (fracDen == 0)
                return false;

            // In mixed numbers, fraction part should not have independent signs conflicting with whole sign
            // Convert to canonical improper fraction:
            // If whole is negative: -(abs(whole) * fracDen + abs(fracNum)) / fracDen
            // If whole >= 0: (whole * fracDen + abs(fracNum)) / fracDen
            BigInteger num;
            BigInteger den = BigInteger.Abs(fracDen);

            if (whole < 0)
            {
                num = -(BigInteger.Abs(whole) * den + BigInteger.Abs(fracNum));
            }
            else
            {
                num = whole * den + BigInteger.Abs(fracNum);
            }

            normalized = ToCanonical(num, den);
            return true;
        }

        // 2. Try simple fraction: e.g. "1/2", "-3/4", "6/2"
        var fracMatch = SimpleFractionRegex.Match(text);
        if (fracMatch.Success)
        {
            if (!BigInteger.TryParse(fracMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var num) ||
                !BigInteger.TryParse(fracMatch.Groups[2].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var den))
            {
                return false;
            }

            if (den == 0)
                return false;

            normalized = ToCanonical(num, den);
            return true;
        }

        // 3. Try integer or finite decimal: e.g. "42", "-7", "0.5", "3,14", "-0.025"
        // Standardize comma to dot for decimal parsing
        var decimalText = text.Replace(',', '.');

        // Check if valid integer: e.g. "42", "-7"
        if (BigInteger.TryParse(decimalText, NumberStyles.Integer, CultureInfo.InvariantCulture, out var integerVal))
        {
            normalized = ToCanonical(integerVal, BigInteger.One);
            return true;
        }

        // Check if valid finite decimal with decimal point:
        int dotIndex = decimalText.IndexOf('.');
        if (dotIndex >= 0 && dotIndex == decimalText.LastIndexOf('.'))
        {
            var integerPartStr = decimalText[..dotIndex];
            var fractionalPartStr = decimalText[(dotIndex + 1)..];

            // Allow leading sign on integer part
            bool isNegative = false;
            if (integerPartStr.StartsWith('-'))
            {
                isNegative = true;
                integerPartStr = integerPartStr[1..];
            }
            else if (integerPartStr.StartsWith('+'))
            {
                integerPartStr = integerPartStr[1..];
            }

            // If integer part is empty (e.g. ".5"), treat as "0"
            if (string.IsNullOrEmpty(integerPartStr))
                integerPartStr = "0";

            // Fractional part must be digits only, non-empty
            if (string.IsNullOrEmpty(fractionalPartStr))
                fractionalPartStr = "0";

            if (!IsAllDigits(integerPartStr) || !IsAllDigits(fractionalPartStr))
                return false;

            if (!BigInteger.TryParse(integerPartStr, NumberStyles.None, CultureInfo.InvariantCulture, out var intPart))
                return false;

            int decDigits = fractionalPartStr.Length;
            BigInteger denominator = BigInteger.Pow(10, decDigits);

            if (!BigInteger.TryParse(fractionalPartStr, NumberStyles.None, CultureInfo.InvariantCulture, out var fracPart))
                return false;

            BigInteger totalNumerator = intPart * denominator + fracPart;
            if (isNegative)
                totalNumerator = -totalNumerator;

            normalized = ToCanonical(totalNumerator, denominator);
            return true;
        }

        return false;
    }

    public bool AreEquivalent(string? answerA, string? answerB)
    {
        if (!TryNormalize(answerA, out var fracA) || !TryNormalize(answerB, out var fracB))
            return false;

        return fracA.Value.Numerator == fracB.Value.Numerator &&
               fracA.Value.Denominator == fracB.Value.Denominator;
    }

    private static bool IsAllDigits(string str)
    {
        if (string.IsNullOrEmpty(str)) return false;
        foreach (char c in str)
        {
            if (c < '0' || c > '9')
                return false;
        }
        return true;
    }

    private static RationalFraction ToCanonical(BigInteger num, BigInteger den)
    {
        if (den < 0)
        {
            num = -num;
            den = -den;
        }

        if (num == 0)
        {
            return new RationalFraction(BigInteger.Zero, BigInteger.One);
        }

        var gcd = BigInteger.GreatestCommonDivisor(BigInteger.Abs(num), den);
        return new RationalFraction(num / gcd, den / gcd);
    }
}
