using System.Text.RegularExpressions;

namespace EduTwin.BLL.AssessmentAndReasoning.PreliminaryGrading;

/// <summary>Explicit bounded grammar, not a general symbolic algebra/CAS evaluator.</summary>
public sealed class BoundedMathAnswerNormalizer
{
    private readonly IMathAnswerNormalizer _numbers = new MathAnswerNormalizer();
    private readonly ICoordinateAnswerNormalizer _coordinates = new CoordinateAnswerNormalizer(new MathAnswerNormalizer());

    public bool TryNormalize(string? raw, out string canonical)
    {
        canonical = string.Empty;
        if (string.IsNullOrWhiteSpace(raw) || raw.Length > 2048) return false;
        var text = raw.Trim();
        if (text.StartsWith("$$") && text.EndsWith("$$") && text.Length > 4) text = text[2..^2].Trim();
        else if (text.StartsWith('$') && text.EndsWith('$') && text.Length > 2) text = text[1..^1].Trim();
        else if (text.StartsWith(@"\(") && text.EndsWith(@"\)")) text = text[2..^2].Trim();
        text = text.Replace(@"\left", "").Replace(@"\right", "")
            .Replace(@"\,", " ").Replace(@"\;", " ").Replace(@"\!", "")
            .Replace(@"\quad", " ").Replace('−', '-');

        if (_numbers.TryNormalize(text, out var number))
        {
            canonical = "number:" + number.Value;
            return true;
        }
        // Curly braces denote a set, not an ordered coordinate pair. Restrict
        // this grammar to parentheses so {a;b} is never order-sensitive.
        if (Regex.IsMatch(text, @"^(?:[A-Za-z]\s*)?\(", RegexOptions.CultureInvariant)
            && _coordinates.TryNormalize(text, out var coordinate) && coordinate.HasValue)
        {
            canonical = $"coordinate:{coordinate.Value.X};{coordinate.Value.Y}";
            return true;
        }

        text = Regex.Replace(text, @"^D\s*=\s*", "", RegexOptions.CultureInvariant);
        text = Regex.Replace(text, @"\\(?:mathbb|mathbf|mathrm)\s*\{R\}", "R", RegexOptions.CultureInvariant)
            .Replace("ℝ", "R").Replace(@"\setminus", @"\").Replace(@"\backslash", @"\").Trim();
        if (text == "R") { canonical = "real-except:"; return true; }
        if (text is @"\varnothing" or @"\emptyset" or "∅") { canonical = "finite:"; return true; }
        var realExclusion = Regex.Match(text, @"^R\s*\\\s*(.*)$", RegexOptions.CultureInvariant);
        var members = (realExclusion.Success ? realExclusion.Groups[1].Value : text)
            .Replace(@"\{", "{").Replace(@"\}", "}");
        if (!members.StartsWith('{') || !members.EndsWith('}')) return false;
        var body = members[1..^1].Trim();
        var tokens = body.Length == 0 ? Array.Empty<string>() : body.Split(body.Contains(';') ? ';' : ',');
        if (tokens.Length > 32) return false;
        var values = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var token in tokens)
        {
            if (!_numbers.TryNormalize(token.Trim(), out var value)) return false;
            values.Add(value.Value.ToString());
        }
        canonical = (realExclusion.Success ? "real-except:" : "finite:") + string.Join(";", values);
        return true;
    }
}
