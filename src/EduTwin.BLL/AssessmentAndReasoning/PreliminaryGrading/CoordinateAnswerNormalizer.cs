using System.Text;

namespace EduTwin.BLL.AssessmentAndReasoning.PreliminaryGrading;

public sealed class CoordinateAnswerNormalizer : ICoordinateAnswerNormalizer
{
    private const int MaxRawLength = 300;
    private readonly IMathAnswerNormalizer _mathNormalizer;

    public CoordinateAnswerNormalizer(IMathAnswerNormalizer mathNormalizer)
    {
        _mathNormalizer = mathNormalizer;
    }

    public CoordinateAnswerNormalizer() : this(new MathAnswerNormalizer())
    {
    }

    public bool TryNormalize(string? rawAnswer, out Coordinate2DValue? normalized)
    {
        normalized = null;
        if (string.IsNullOrWhiteSpace(rawAnswer) || rawAnswer.Length > MaxRawLength)
        {
            return false;
        }

        var text = NormalizeLexicalForm(rawAnswer);
        if (!TryStripOptionalPointLabel(ref text) || !TryStripMatchingWrapper(ref text))
        {
            return false;
        }

        if (!TrySplitComponents(text, out var xText, out var yText)
            || !_mathNormalizer.TryNormalize(xText, out var x)
            || !_mathNormalizer.TryNormalize(yText, out var y))
        {
            return false;
        }

        normalized = new Coordinate2DValue(x.Value, y.Value);
        return true;
    }

    private static string NormalizeLexicalForm(string raw)
    {
        var normalized = raw.Normalize(NormalizationForm.FormKC)
            .Replace("\\left", string.Empty, StringComparison.Ordinal)
            .Replace("\\right", string.Empty, StringComparison.Ordinal);

        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            switch (character)
            {
                case '\u00A0':
                    builder.Append(' ');
                    break;
                case '\u200B':
                case '\u200C':
                case '\u200D':
                case '\u2060':
                case '\uFEFF':
                    break;
                case '\u2010':
                case '\u2011':
                case '\u2012':
                case '\u2013':
                case '\u2014':
                case '\u2212':
                case '\uFF0D':
                    builder.Append('-');
                    break;
                default:
                    builder.Append(character);
                    break;
            }
        }

        return builder.ToString().Trim();
    }

    private static bool TryStripOptionalPointLabel(ref string text)
    {
        var firstWrapperIndex = text.IndexOfAny(['(', '[', '{']);
        if (firstWrapperIndex <= 0)
        {
            return firstWrapperIndex == 0;
        }

        var label = text[..firstWrapperIndex].Trim();
        if (label.Length != 1 || !char.IsLetter(label[0]))
        {
            return false;
        }

        text = text[firstWrapperIndex..].Trim();
        return true;
    }

    private static bool TryStripMatchingWrapper(ref string text)
    {
        if (text.Length < 5)
        {
            return false;
        }

        var expectedClose = text[0] switch
        {
            '(' => ')',
            '[' => ']',
            '{' => '}',
            _ => '\0'
        };

        if (expectedClose == '\0' || text[^1] != expectedClose)
        {
            return false;
        }

        text = text[1..^1].Trim();
        return text.Length > 0;
    }

    private static bool TrySplitComponents(string text, out string x, out string y)
    {
        x = string.Empty;
        y = string.Empty;

        var semicolonIndex = text.IndexOf(';');
        if (semicolonIndex >= 0)
        {
            if (semicolonIndex != text.LastIndexOf(';'))
            {
                return false;
            }

            x = text[..semicolonIndex].Trim();
            y = text[(semicolonIndex + 1)..].Trim();
            return x.Length > 0 && y.Length > 0;
        }

        var commaIndex = text.IndexOf(',');
        if (commaIndex < 0 || commaIndex != text.LastIndexOf(','))
        {
            return false;
        }

        x = text[..commaIndex].Trim();
        y = text[(commaIndex + 1)..].Trim();
        return x.Length > 0 && y.Length > 0;
    }
}
