using System.Security.Cryptography;
using System.Text;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.IdentityAndTenancy;

namespace EduTwin.DAL.Seeding;

public static class AuthorizationPermissionCatalog
{
    public static readonly DateTime CatalogTimestampUtc =
        new(2026, 9, 9, 0, 0, 0, DateTimeKind.Utc);

    private static readonly HashSet<string> SensitiveCodes =
    [
        "authorization.roles.create", "authorization.roles.update", "authorization.roles.archive",
        "authorization.roles.manage_permissions", "authorization.user_roles.assign", "authorization.audit.read",
        "organization.center.update", "organization.teachers.create", "organization.teachers.update",
        "organization.teachers.delete", "organization.students.create", "organization.students.update",
        "organization.students.delete", "organization.classes.create", "organization.classes.update",
        "organization.classes.manage_members", "knowledge.subjects.delete", "knowledge.nodes.delete",
        "knowledge.edges.delete", "curriculum.curriculums.publish", "curriculum.questions.publish",
        "assignments.assignments.publish", "assignments.assignments.close", "twin.reasoning.override"
    ];

    private static readonly string[] StudentCodes =
    [
        "learning.attempts.submit", "learning.attempts.read_own", "twin.student.read_own",
        "recommendations.student.read_own", "dashboards.student.read_own"
    ];

    private static readonly string[] TeacherCodes = ["dashboards.teacher.read_scoped"];

    private static readonly string[] CenterManagerCodes =
    [
        "authorization.permissions.read", "authorization.roles.read", "authorization.roles.create",
        "authorization.roles.update", "authorization.roles.archive", "authorization.roles.manage_permissions",
        "authorization.user_roles.read", "authorization.user_roles.assign", "authorization.audit.read",
        "organization.center.read", "organization.center.update", "organization.teachers.create",
        "organization.teachers.update", "organization.teachers.delete", "organization.students.delete",
        "organization.classes.create", "organization.classes.update", "knowledge.subjects.delete",
        "knowledge.nodes.delete", "dashboards.center.read"
    ];

    private static readonly string[] TeacherOrManagerCodes =
    [
        "organization.teachers.read", "organization.students.create", "organization.students.update",
        "organization.classes.read", "organization.classes.manage_members", "knowledge.subjects.create",
        "knowledge.subjects.update", "knowledge.nodes.create", "knowledge.nodes.update",
        "knowledge.edges.create", "knowledge.edges.update", "knowledge.edges.delete",
        "curriculum.curriculums.read", "curriculum.curriculums.create", "curriculum.curriculums.update",
        "curriculum.curriculums.publish", "curriculum.questions.read", "curriculum.questions.create",
        "curriculum.questions.update", "curriculum.questions.publish", "assignments.assignments.create",
        "assignments.assignments.update", "assignments.assignments.publish", "assignments.assignments.close",
        "learning.attempts.read_scoped", "twin.student.read_scoped", "twin.reasoning.review",
        "twin.reasoning.override"
    ];

    private static readonly string[] AllAccountTypeCodes =
    [
        "organization.students.read", "knowledge.subjects.read", "knowledge.nodes.read",
        "knowledge.edges.read", "assignments.assignments.read"
    ];

    public static IReadOnlyList<Permission> CreatePermissions()
    {
        return BuildAccountTypeMap()
            .Keys
            .Order(StringComparer.Ordinal)
            .Select(code =>
            {
                var parts = code.Split('.');
                return new Permission
                {
                    PermissionId = CreateDeterministicId(code),
                    PermissionCode = code,
                    ModuleName = ToDisplayName(parts[0]),
                    ResourceName = ToDisplayName(parts[1]),
                    ActionName = parts[2],
                    Description = $"Cho phép {parts[2]} {parts[1]} trong phạm vi được cấp.",
                    IsSensitive = SensitiveCodes.Contains(code),
                    IsDelegable = true,
                    Status = PermissionStatus.Active,
                    CreatedAt = CatalogTimestampUtc,
                    UpdatedAt = CatalogTimestampUtc
                };
            })
            .ToArray();
    }

    public static IReadOnlyList<PermissionAccountType> CreateAccountTypeMappings()
    {
        return BuildAccountTypeMap()
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .SelectMany(pair => pair.Value.OrderBy(value => value).Select(accountType =>
                new PermissionAccountType
                {
                    PermissionId = CreateDeterministicId(pair.Key),
                    AccountType = accountType,
                    CreatedAt = CatalogTimestampUtc
                }))
            .ToArray();
    }

    public static Guid CreateDeterministicId(string permissionCode)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes($"edutwin:permission:v1:{permissionCode}"));
        var bytes = hash[..16];
        bytes[7] = (byte)((bytes[7] & 0x0F) | 0x50);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }

    public static Guid CreateSystemRoleId(Guid centerId, UserRole accountType)
    {
        var hash = Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(
            $"edutwin:system-role:v1:{centerId:D}:{accountType}"))).ToLowerInvariant();

        return Guid.Parse(
            $"{hash[..8]}-{hash[8..12]}-{hash[12..16]}-{hash[16..20]}-{hash[20..32]}");
    }

    private static Dictionary<string, HashSet<UserRole>> BuildAccountTypeMap()
    {
        var result = new Dictionary<string, HashSet<UserRole>>(StringComparer.Ordinal);
        Add(result, StudentCodes, UserRole.Student);
        Add(result, TeacherCodes, UserRole.Teacher);
        Add(result, CenterManagerCodes, UserRole.CenterManager);
        Add(result, TeacherOrManagerCodes, UserRole.Teacher, UserRole.CenterManager);
        Add(result, AllAccountTypeCodes, UserRole.Student, UserRole.Teacher, UserRole.CenterManager);
        return result;
    }

    private static void Add(
        Dictionary<string, HashSet<UserRole>> target,
        IEnumerable<string> codes,
        params UserRole[] accountTypes)
    {
        foreach (var code in codes)
        {
            if (!target.TryGetValue(code, out var allowed))
            {
                allowed = [];
                target.Add(code, allowed);
            }

            allowed.UnionWith(accountTypes);
        }
    }

    private static string ToDisplayName(string value) =>
        string.Concat(value[0].ToString().ToUpperInvariant(), value.AsSpan(1));
}
