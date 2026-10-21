using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Platform;
using EduTwin.BLL.Seeding;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Platform;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTwin.BLL.Tests.IdentityAndTenancy;

public sealed class AccountSecurityServiceTests
{
    [Theory]
    [InlineData(UserRole.Student)]
    [InlineData(UserRole.Teacher)]
    [InlineData(UserRole.CenterManager)]
    public async Task OwnAccount_ProfilePasswordAndRevoke_StayWithinCaller(UserRole role)
    {
        var center = Guid.NewGuid(); var actor = Guid.NewGuid(); var other = Guid.NewGuid();
        var tenant = new TenantContext(); tenant.Initialize(center, actor, role.ToString(), 1);
        await using var db = new EduTwinDbContext(new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, tenant);
        var hasher = new PasswordHasher<User>(); var now = DateTime.UtcNow;
        User User(Guid id) => new() { CenterId = center, UserId = id, Username = id.ToString(), DisplayName = "QA",
            RoleName = role, Status = UserStatus.Active, PasswordHash = "", AuthVersion = 1, RowVersion = 1,
            CreatedAt = now, UpdatedAt = now };
        var owner = User(actor); owner.PasswordHash = hasher.HashPassword(owner, "TestOldPassword@2026");
        var neighbor = User(other); neighbor.PasswordHash = hasher.HashPassword(neighbor, "NeighborPassword@2026");
        var neighborHash = neighbor.PasswordHash;
        db.Users.AddRange(owner, neighbor);
        db.RefreshTokens.AddRange(new RefreshToken { CenterId = center, RefreshTokenId = 1, UserId = actor,
            TokenHash = "owner-token", ExpiresAt = now.AddDays(1), CreatedAt = now },
            new RefreshToken { CenterId = center, RefreshTokenId = 2, UserId = other,
            TokenHash = "neighbor-token", ExpiresAt = now.AddDays(1), CreatedAt = now });
        await db.SaveChangesAsync();
        var service = new AccountSecurityService(db, tenant, hasher, TimeProvider.System);
        var profile = await service.GetSecurityProfileAsync("qa");
        Assert.True(profile.IsSuccess); Assert.Equal(actor, profile.Data!.UserId); Assert.Equal(1, profile.Data.ActiveSessionCount);
        // The platform service retains its independent platform-only boundary.
        Assert.False((await new PlatformMeService(db, tenant, hasher, TimeProvider.System).GetSecurityProfileAsync("qa")).IsSuccess);
        var wrong = await service.ChangePasswordAsync(new PlatformChangePasswordRequest {
            CurrentPassword = "WrongPassword@2026", NewPassword = "TestNewPassword@2026", ConfirmPassword = "TestNewPassword@2026" }, "qa");
        Assert.False(wrong.IsSuccess); Assert.Equal(ErrorCodes.AuthInvalidCredentials, wrong.ErrorCode);
        var mismatch = await service.ChangePasswordAsync(new PlatformChangePasswordRequest {
            CurrentPassword = "TestOldPassword@2026", NewPassword = "TestNewPassword@2026", ConfirmPassword = "different" }, "qa");
        Assert.False(mismatch.IsSuccess); Assert.Equal(1u, owner.AuthVersion);
        var changed = await service.ChangePasswordAsync(new PlatformChangePasswordRequest {
            CurrentPassword = "TestOldPassword@2026", NewPassword = "TestNewPassword@2026", ConfirmPassword = "TestNewPassword@2026" }, "qa");
        Assert.True(changed.IsSuccess); Assert.Equal(2u, owner.AuthVersion);
        Assert.NotEqual(PasswordVerificationResult.Failed, hasher.VerifyHashedPassword(owner, owner.PasswordHash, "TestNewPassword@2026"));
        Assert.NotNull((await db.RefreshTokens.SingleAsync(t => t.UserId == actor)).RevokedAt);
        Assert.Null((await db.RefreshTokens.SingleAsync(t => t.UserId == other)).RevokedAt);
        Assert.Equal(neighborHash, neighbor.PasswordHash); Assert.Equal(1u, neighbor.AuthVersion);
        Assert.True((await service.RevokeSessionsAsync(null, "qa")).IsSuccess); Assert.Equal(3u, owner.AuthVersion);
        var audits = await db.AuthorizationAuditLogs.ToListAsync();
        Assert.All(audits, a => { Assert.Equal(center, a.CenterId); Assert.Equal(actor, a.ActorUserId);
            Assert.DoesNotContain("Password@", a.AfterData ?? ""); });
    }

    [Fact]
    public async Task TenantSelfService_RejectsPlatformAccount()
    {
        var tenant = new TenantContext(); tenant.Initialize(AuthorizationBootstrapper.ReservedPlatformCenterId,
            Guid.NewGuid(), nameof(UserRole.PlatformAdmin), 1);
        await using var db = new EduTwinDbContext(new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, tenant);
        var service = new AccountSecurityService(db, tenant, new PasswordHasher<User>(), TimeProvider.System);
        Assert.Equal(ErrorCodes.ForbiddenResource, (await service.GetSecurityProfileAsync("qa")).ErrorCode);
        Assert.Equal(ErrorCodes.ForbiddenResource, (await service.RevokeSessionsAsync(null, "qa")).ErrorCode);
    }
}
