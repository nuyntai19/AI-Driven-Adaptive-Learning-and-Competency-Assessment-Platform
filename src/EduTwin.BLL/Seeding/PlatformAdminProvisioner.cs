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

        // 2. Check if a PlatformAdmin user already exists in PLATFORM tenant
        var existingAdmin = await dbContext.Users
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(u => u.CenterId == ReservedPlatformCenterId &&
                                     u.RoleName == UserRole.PlatformAdmin &&
                                     !u.IsDeleted, cancellationToken);

        if (existingAdmin is not null)
        {
            logger.LogInformation("PlatformAdmin user already exists ({UserId}). Skipping credential modification.", existingAdmin.UserId);
            await authBootstrapper.BootstrapPlatformAsync(ReservedPlatformCenterId, existingAdmin.UserId, cancellationToken);
            return;
        }

        // 3. Create initial PlatformAdmin user if missing
        var bootstrapOptions = options.Value;
        if (string.IsNullOrWhiteSpace(bootstrapOptions.AdminPassword))
        {
            throw new InvalidOperationException("Missing mandatory PlatformBootstrap:AdminPassword configuration. PlatformAdmin cannot be provisioned.");
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
