using System.Text;
using System.Text.RegularExpressions;

namespace EduTwin.BLL.AssessmentAndReasoning;

/// <summary>
/// Hides the answer inside hint text without breaking formulas. Solutions mix prose and math ($...$, $$...$$, \(...\),
/// \[...\]); a prose placeholder such as "[kết quả cần tìm]" or "___" written inside a formula is rendered by KaTeX as
/// garbled letters or a subscript error. Inside math the answer becomes a box (\boxed{?}); in prose the given text.
/// </summary>
public static class MathAwareMask
{
    public const string MathPlaceholder = @"\boxed{?}";

    private static readonly Regex MathSegments = new(
        @"\$\$.+?\$\$|\$[^$]+?\$|\\\(.+?\\\)|\\\[.+?\\\]",
        RegexOptions.Singleline | RegexOptions.Compiled, TimeSpan.FromMilliseconds(200));

    public static string Replace(string text, Regex answer, string prosePlaceholder)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var result = new StringBuilder(text.Length);
        var position = 0;
        foreach (Match math in MathSegments.Matches(text))
        {
            result.Append(answer.Replace(text[position..math.Index], prosePlaceholder));
            result.Append(answer.Replace(math.Value, MathPlaceholder));
            position = math.Index + math.Length;
        }
        result.Append(answer.Replace(text[position..], prosePlaceholder));
        return result.ToString();
    }
}
