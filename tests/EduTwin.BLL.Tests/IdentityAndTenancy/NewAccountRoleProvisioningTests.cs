using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Organization;
using EduTwin.BLL.Seeding;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EduTwin.BLL.Tests.IdentityAndTenancy;

public sealed class NewAccountRoleProvisioningTests
{
    private sealed class Tenant : ITenantContext, ITenantIdAccessor
    {
        public Guid? CenterId { get; set; } = Guid.NewGuid();
        public Guid? UserId { get; set; } = Guid.NewGuid();
        public string? Role { get; set; } = nameof(UserRole.CenterManager);
        public uint? AuthVersion => 1;
        public bool IsResolved => true;
    }
    private static async Task<(EduTwinDbContext Db, Tenant Tenant)> Setup()
    {
        var tenant = new Tenant();
        var db = new EduTwinDbContext(new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options, tenant);
        db.Centers.Add(new Center { CenterId = tenant.CenterId!.Value, CenterCode = "TEST", CenterName = "Test", Timezone = "UTC", Status = CenterStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        db.Users.Add(new User { UserId = tenant.UserId!.Value, CenterId = tenant.CenterId.Value, Username = "manager", PasswordHash = "test", DisplayName = "Manager", RoleName = UserRole.CenterManager, Status = UserStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        await new AuthorizationBootstrapper(db, TimeProvider.System).EnsureCenterAsync(tenant.CenterId.Value);
        return (db, tenant);
    }
    private static CreateTeacherUseCase TeacherSut(EduTwinDbContext db, Tenant tenant) => new(db, tenant, TimeProvider.System, new PasswordHasher<User>());
    private static CreateTeacherRequest TeacherRequest() => new() { Username = "teacher.new", DisplayName = "New Teacher", TemporaryPassword = "SyntheticPassword123!" };

    [Fact]
    public async Task NewTeacher_ImmediatelyHasDefaultPermissions_NotManagerOrPlatformPrivileges()
    {
        var (db, tenant) = await Setup(); await using var scope = db;
        var result = await TeacherSut(db, tenant).ExecuteAsync(TeacherRequest());
        Assert.True(result.IsSuccess);
        var userId = Guid.Parse(result.Data!.TeacherId);
        var authorization = await new AuthorizationSnapshotReader(db).ReadForUserAsync(userId);
        Assert.Single(authorization.Roles);
        Assert.Contains("dashboards.teacher.read_scoped", authorization.Permissions);
        Assert.Contains("curriculum.questions.read", authorization.Permissions);
        Assert.All(EduTwin.DAL.Seeding.AuthorizationPermissionCatalog.SensitiveSharedAcademicCodes,
            code => Assert.Contains(code, authorization.Permissions));
        Assert.DoesNotContain("organization.teachers.create", authorization.Permissions);
        Assert.DoesNotContain(authorization.Permissions, p => p.StartsWith("platform.", StringComparison.Ordinal));
        tenant.UserId = userId; tenant.Role = nameof(UserRole.Teacher);
        Assert.True(await new PermissionEvaluator(db, tenant).HasPermissionAsync("dashboards.teacher.read_scoped"));
    }
    [Fact]
    public async Task NewStudent_ImmediatelyHasStudentPermissions_WithAudit()
    {
        var (db, tenant) = await Setup(); await using var scope = db;
        var result = await new CreateStudentUseCase(db, tenant, new PasswordHasher<User>(), TimeProvider.System,
            NullLogger<CreateStudentUseCase>.Instance).ExecuteAsync(new CreateStudentRequest
            { Username = "student.new", FullName = "New Student", TemporaryPassword = "SyntheticPassword123!", GradeLevel = 12, ClassIds = [] });
        Assert.True(result.IsSuccess);
        var authorization = await new AuthorizationSnapshotReader(db).ReadForUserAsync(result.Data!.StudentId);
        Assert.Single(authorization.Roles);
        Assert.Contains("assignments.assignments.read", authorization.Permissions);
        Assert.DoesNotContain("dashboards.teacher.read_scoped", authorization.Permissions);
        Assert.Single(await db.AuthorizationAuditLogs.Where(a => a.TargetUserId == result.Data.StudentId && a.ActionType == "UserSystemRoleAssigned").ToArrayAsync());
    }
    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task MissingOrArchivedDefaultRole_DoesNotCreateAccount(bool exists, bool active)
    {
        var (db, tenant) = await Setup(); await using var scope = db;
        var role = await db.AuthorizationRoles.SingleAsync(r => r.AccountType == UserRole.Teacher);
        role.IsSystemRole = exists;
        role.Status = active ? AuthorizationRoleStatus.Active : AuthorizationRoleStatus.Archived;
        await db.SaveChangesAsync();
        var result = await TeacherSut(db, tenant).ExecuteAsync(TeacherRequest());
        Assert.False(result.IsSuccess); Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        Assert.False(await db.Users.AnyAsync(u => u.Username == "teacher.new"));
        Assert.Empty(await db.Teachers.ToArrayAsync());
    }
    [Fact]
    public async Task OtherCenterRole_IsNotUsed_WhenOwnRoleIsMissing()
    {
        var (db, tenant) = await Setup(); await using var scope = db;
        var ownRole = await db.AuthorizationRoles.SingleAsync(r => r.AccountType == UserRole.Teacher);
        ownRole.IsSystemRole = false;
        db.AuthorizationRoles.Add(new AuthorizationRole { RoleId = Guid.NewGuid(), CenterId = Guid.NewGuid(),
            AccountType = UserRole.Teacher, IsSystemRole = true, RoleCode = "OTHER_TEACHER", RoleName = "Other Teacher", Status = AuthorizationRoleStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        Assert.False((await TeacherSut(db, tenant).ExecuteAsync(TeacherRequest())).IsSuccess);
        Assert.False(await db.Users.AnyAsync(u => u.Username == "teacher.new"));
    }
    [Fact]
    public async Task CreatingAnotherTeacher_DoesNotReactivateAnExistingRevokedRole()
    {
        var (db, tenant) = await Setup(); await using var scope = db;
        var role = await db.AuthorizationRoles.SingleAsync(r => r.AccountType == UserRole.Teacher);
        var previousUserId = Guid.NewGuid();
        db.UserRoleAssignments.Add(new UserRoleAssignment { CenterId = tenant.CenterId!.Value, UserId = previousUserId,
            RoleId = role.RoleId, AccountType = UserRole.Teacher, Status = UserRoleAssignmentStatus.Revoked, AssignedByUserId = tenant.UserId!.Value, AssignedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        Assert.True((await TeacherSut(db, tenant).ExecuteAsync(TeacherRequest())).IsSuccess);
        Assert.Equal(UserRoleAssignmentStatus.Revoked, (await db.UserRoleAssignments.SingleAsync(a => a.UserId == previousUserId)).Status);
    }
}
