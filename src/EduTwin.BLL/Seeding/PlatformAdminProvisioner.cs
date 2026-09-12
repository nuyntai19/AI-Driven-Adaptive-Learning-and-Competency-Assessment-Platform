using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using EduTwin.BLL.Platform;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Seeding;

public sealed class PlatformAdminProvisioner(
    EduTwinDbContext dbContext,
    IPasswordHasher<User> passwordHasher,
    AuthorizationBootstrapper authBootstrapper,
    IOptions<PlatformBootstrapOptions> options,
    TimeProvider timeProvider,
    ILogger<PlatformAdminProvisioner> logger)
{
    public static readonly Guid ReservedPlatformCenterId =
        AuthorizationBootstrapper.ReservedPlatformCenterId;

    public static readonly Guid ReservedPlatformAdminUserId =
        Guid.Parse("00000000-0000-0000-0000-000000000002");

    public async Task EnsureAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // 1. Ensure Root Tenant PLATFORM exists
        var platformCenter = await dbContext.Centers
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(c => c.CenterId == ReservedPlatformCenterId, cancellationToken);

        if (platformCenter is null)
        {
            platformCenter = new Center
            {
                CenterId = ReservedPlatformCenterId,
                CenterCode = "PLATFORM",
                CenterName = "EduTwin Platform Administration",
                Timezone = "Asia/Ho_Chi_Minh",
                Status = CenterStatus.Active,
                RowVersion = 1,
                CreatedAt = now,
                UpdatedAt = now
            };
            dbContext.Centers.Add(platformCenter);
            await dbContext.SaveChangesAsync(cancellationToken);
            logger.LogInformation("Provisioned Root Tenant PLATFORM ({CenterId})", ReservedPlatformCenterId);
        }
        else if (platformCenter.CenterCode != "PLATFORM" || platformCenter.Status != CenterStatus.Active || platformCenter.IsDeleted)
        {
            throw new InvalidOperationException(
                $"Root Tenant PLATFORM ({ReservedPlatformCenterId}) is in a malformed state (CenterCode='{platformCenter.CenterCode}', Status='{platformCenter.Status}', IsDeleted={platformCenter.IsDeleted}).");
        }

        // Invariant: No ordinary non-PlatformAdmin users inside Root Tenant PLATFORM
        var unauthorizedUsersInPlatform = await dbContext.Users
            .IgnoreQueryFilters()
            .Where(u => u.CenterId == ReservedPlatformCenterId && u.RoleName != UserRole.PlatformAdmin && !u.IsDeleted)
            .ToListAsync(cancellationToken);
        if (unauthorizedUsersInPlatform.Count > 0)
        {
            throw new InvalidOperationException(
                $"Root Tenant PLATFORM contains {unauthorizedUsersInPlatform.Count} unauthorized non-PlatformAdmin user(s).");
        }

        // Invariant: No PlatformAdmin users outside Root Tenant PLATFORM
        var platformAdminsOutsidePlatform = await dbContext.Users
            .IgnoreQueryFilters()
            .Where(u => u.CenterId != ReservedPlatformCenterId && u.RoleName == UserRole.PlatformAdmin && !u.IsDeleted)
            .ToListAsync(cancellationToken);
        if (platformAdminsOutsidePlatform.Count > 0)
        {
            throw new InvalidOperationException(
                $"Found {platformAdminsOutsidePlatform.Count} PlatformAdmin user(s) outside Root Tenant PLATFORM.");
        }

        // 2. Check if active PlatformAdmin user already exists in PLATFORM tenant
        var activeAdmins = await dbContext.Users
            .IgnoreQueryFilters()
            .Where(u => u.CenterId == ReservedPlatformCenterId &&
                        u.RoleName == UserRole.PlatformAdmin &&
                        u.Status == UserStatus.Active &&
                        !u.IsDeleted)
            .ToListAsync(cancellationToken);

        if (activeAdmins.Count > 1)
        {
            throw new InvalidOperationException(
                $"Multiple active PlatformAdmin users ({activeAdmins.Count}) found in Root Tenant PLATFORM.");
        }

        if (activeAdmins.Count == 1)
        {
            var existingAdmin = activeAdmins[0];
            logger.LogInformation("PlatformAdmin user already exists ({UserId}). Skipping credential modification.", existingAdmin.UserId);
            await authBootstrapper.BootstrapPlatformAsync(ReservedPlatformCenterId, existingAdmin.UserId, cancellationToken);
            return;
        }

        // 3. Create initial PlatformAdmin user if missing
        var bootstrapOptions = options.Value;
        if (string.IsNullOrWhiteSpace(bootstrapOptions.AdminPassword) || bootstrapOptions.AdminPassword.Length < 12)
        {
            throw new InvalidOperationException("Missing or invalid PlatformBootstrap:AdminPassword configuration (must be at least 12 characters). PlatformAdmin cannot be provisioned.");
        }

        var newAdmin = new User
        {
            UserId = ReservedPlatformAdminUserId,
            CenterId = ReservedPlatformCenterId,
            Username = bootstrapOptions.AdminUsername,
            DisplayName = bootstrapOptions.AdminDisplayName,
            RoleName = UserRole.PlatformAdmin,
            Status = UserStatus.Active,
            RowVersion = 1,
            AuthVersion = 1,
            CreatedAt = now,
            UpdatedAt = now
        };
        newAdmin.PasswordHash = passwordHasher.HashPassword(newAdmin, bootstrapOptions.AdminPassword);

        dbContext.Users.Add(newAdmin);
        await dbContext.SaveChangesAsync(cancellationToken);

        // Bootstrap platform system role and permissions
        await authBootstrapper.BootstrapPlatformAsync(ReservedPlatformCenterId, newAdmin.UserId, cancellationToken);

        dbContext.AuthorizationAuditLogs.Add(new AuthorizationAuditLog
        {
            CenterId = ReservedPlatformCenterId,
            ActorUserId = newAdmin.UserId,
            TargetUserId = null,
            ActionType = "PlatformAdminBootstrap",
            TargetType = "User",
            TargetId = newAdmin.UserId.ToString("D"),
            AfterData = "{\"role\":\"PlatformAdmin\",\"status\":\"Active\"}",
            Reason = "Khởi tạo tài khoản quản trị nền tảng ban đầu.",
            TraceId = "bootstrap:platform-admin",
            CreatedAt = now
        });
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Successfully provisioned initial PlatformAdmin ({UserId}) in Root Tenant PLATFORM.", newAdmin.UserId);
    }
}
