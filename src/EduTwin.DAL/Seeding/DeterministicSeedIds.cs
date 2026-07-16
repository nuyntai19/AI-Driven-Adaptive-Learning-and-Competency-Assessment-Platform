using System;

namespace EduTwin.DAL.Seeding;

public static class DeterministicSeedIds
{
    // Centers
    public static readonly Guid CenterAId = new("10000000-0000-0000-0000-000000000001");
    public static readonly Guid CenterBId = new("20000000-0000-0000-0000-000000000002");

    // Common properties
    public static readonly DateTime AuditTimeUtc = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public const ulong InitialRowVersion = 1UL;

    // Center A Specific Base IDs
    public const ulong CenterANodeIdBase = 10000;
    public const ulong CenterAEdgeIdBase = 10000;
    public const ulong CenterAQuestionIdBase = 10000;
    public const ulong CenterAOptionIdBase = 10000;
    public const ulong CenterAGoalIdBase = 10000;

    // Center B Specific Base IDs
    public const ulong CenterBNodeIdBase = 20000;
    public const ulong CenterBEdgeIdBase = 20000;
    public const ulong CenterBQuestionIdBase = 20000;
    public const ulong CenterBOptionIdBase = 20000;
    public const ulong CenterBGoalIdBase = 20000;

    // Subjects
    public static readonly Guid CenterAMathSubjectId = new("30000000-0000-0000-0000-000000000003");
    public static readonly Guid CenterAEnglishSubjectId = new("40000000-0000-0000-0000-000000000004");
    public static readonly Guid CenterBMathSubjectId = new("30000000-0000-0000-0000-000000000009");
    public static readonly Guid CenterBEnglishSubjectId = new("40000000-0000-0000-0000-000000000009");

    // Classes
    public static readonly Guid CenterAMathClassId = new("50000000-0000-0000-0000-000000000005");
    public static readonly Guid CenterAEnglishClassId = new("60000000-0000-0000-0000-000000000006");
    public static readonly Guid CenterBMathClassId = new("70000000-0000-0000-0000-000000000007");
    public static readonly Guid CenterBEnglishClassId = new("80000000-0000-0000-0000-000000000008");

    // Curriculums
    public static readonly Guid CenterAMathCurriculumId = new("90000000-0000-0000-0000-000000000009");
    public static readonly Guid CenterAEnglishCurriculumId = new("a0000000-0000-0000-0000-00000000000a");
    public static readonly Guid CenterBMathCurriculumId = new("b0000000-0000-0000-0000-00000000000b");
    public static readonly Guid CenterBEnglishCurriculumId = new("c0000000-0000-0000-0000-00000000000c");

    // Center A Users
    public static readonly Guid CenterAManagerId = new("d0000000-0000-0000-0001-000000000001");
    public static readonly Guid CenterATeacherMathId = new("d0000000-0000-0000-0001-000000000002");
    public static readonly Guid CenterATeacherEnglishId = new("d0000000-0000-0000-0001-000000000003");
    public static readonly Guid CenterAStudent01Id = new("d0000000-0000-0000-0001-000000000004");
    public static readonly Guid CenterAStudent02Id = new("d0000000-0000-0000-0001-000000000005");
    public static readonly Guid CenterAStudent03Id = new("d0000000-0000-0000-0001-000000000006");
    public static readonly Guid CenterAStudent04Id = new("d0000000-0000-0000-0001-000000000007");
    public static readonly Guid CenterAStudent05Id = new("d0000000-0000-0000-0001-000000000008");

    // Center B Users
    public static readonly Guid CenterBManagerId = new("d0000000-0000-0000-0002-000000000001");
    public static readonly Guid CenterBTeacherMathId = new("d0000000-0000-0000-0002-000000000002");
    public static readonly Guid CenterBTeacherEnglishId = new("d0000000-0000-0000-0002-000000000003");
    public static readonly Guid CenterBStudent01Id = new("d0000000-0000-0000-0002-000000000004");
    public static readonly Guid CenterBStudent02Id = new("d0000000-0000-0000-0002-000000000005");
    public static readonly Guid CenterBStudent03Id = new("d0000000-0000-0000-0002-000000000006");
    public static readonly Guid CenterBStudent04Id = new("d0000000-0000-0000-0002-000000000007");
    public static readonly Guid CenterBStudent05Id = new("d0000000-0000-0000-0002-000000000008");
}
