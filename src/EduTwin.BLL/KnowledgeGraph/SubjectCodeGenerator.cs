using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace EduTwin.BLL.KnowledgeGraph;

/// <summary>Builds a readable, center-unique subject code: "Toán 12" becomes TOAN12.</summary>
public static class SubjectCodeGenerator
{
    private const int MaxBaseLength = 26;
    private const string FallbackCode = "MH";

    public static string BaseCode(string subjectName)
    {
        var letters = subjectName.Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && char.IsAsciiLetterOrDigit(c))
            .Select(char.ToUpperInvariant)
            .Take(MaxBaseLength)
            .ToArray();
        return letters.Length == 0 ? FallbackCode : new string(letters);
    }

    public static string FirstFree(string baseCode, IEnumerable<string> takenCodes)
    {
        var taken = takenCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!taken.Contains(baseCode)) return baseCode;
        var suffix = 2;
        while (taken.Contains($"{baseCode}-{suffix}")) suffix++;
        return $"{baseCode}-{suffix}";
    }
}
