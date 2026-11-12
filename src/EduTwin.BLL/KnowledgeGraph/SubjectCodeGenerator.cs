using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace EduTwin.BLL.KnowledgeGraph;

/// <summary>
/// Builds a readable, center-unique subject code. The four subjects of the programme use one
/// standard English code each (Toán → MATH, Vật lý → PHYS, Hóa học → CHEM, Tiếng Anh → ENG; a grade
/// in the name is kept: "Toán 12" → MATH12); any other name is transliterated ("Sinh học" → SINHHOC).
/// The same catalogue drives knowledge-node codes on the client (utils/knowledgeNodeCode.ts).
/// </summary>
public static class SubjectCodeGenerator
{
    private const int MaxBaseLength = 26;
    private const string FallbackCode = "MH";

    private static readonly (string Code, string[] Aliases)[] StandardSubjects =
    [
        ("MATH", ["TOAN", "TOANHOC", "MATH", "MATHS", "MATHEMATICS"]),
        ("PHYS", ["VATLY", "VATLI", "LY", "LI", "PHYS", "PHYSICS"]),
        ("CHEM", ["HOAHOC", "HOA", "CHEM", "CHEMISTRY"]),
        ("ENG", ["TIENGANH", "ANH", "ANHVAN", "ENG", "ENGLISH"]),
    ];

    public static string BaseCode(string subjectName)
    {
        var standard = StandardCode(subjectName);
        if (standard is not null) return standard;
        var letters = Transliterate(subjectName).Take(MaxBaseLength).ToArray();
        return letters.Length == 0 ? FallbackCode : new string(letters);
    }

    /// <summary>Standard code for Toán/Lý/Hóa/Anh (optionally prefixed by "Môn", with a grade), else null.</summary>
    public static string? StandardCode(string subjectName)
    {
        var chars = Transliterate(subjectName);
        var letters = new string(chars.Where(char.IsAsciiLetter).ToArray());
        var digits = new string(chars.Where(char.IsAsciiDigit).ToArray());
        if (letters.StartsWith("MON", StringComparison.Ordinal) && letters.Length > 3) letters = letters[3..];
        foreach (var (code, aliases) in StandardSubjects)
            if (aliases.Contains(letters, StringComparer.Ordinal)) return code + digits;
        return null;
    }

    private static char[] Transliterate(string text) =>
        text.Replace('đ', 'd').Replace('Đ', 'D').Normalize(NormalizationForm.FormD)
            .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && char.IsAsciiLetterOrDigit(c))
            .Select(char.ToUpperInvariant)
            .ToArray();

    public static string FirstFree(string baseCode, IEnumerable<string> takenCodes)
    {
        var taken = takenCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (!taken.Contains(baseCode)) return baseCode;
        var suffix = 2;
        while (taken.Contains($"{baseCode}-{suffix}")) suffix++;
        return $"{baseCode}-{suffix}";
    }
}
