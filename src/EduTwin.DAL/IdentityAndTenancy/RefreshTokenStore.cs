using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

using EduTwin.DAL.Persistence;

namespace EduTwin.DAL.IdentityAndTenancy;

public class RefreshTokenStore : IRefreshTokenStore
{
    private readonly EduTwinDbContext _dbContext;
    public RefreshTokenStore(EduTwinDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<Guid?> ResolveCenterIdAsync(string tokenHash, CancellationToken cancellationToken)
    {
        // Refresh cookies are opaque and have not established a TenantContext yet.
        // Therefore, we must temporarily bypass the Global Query Filter to resolve the CenterId.
        // We only project CenterId to avoid fetching entire entity data cross-tenants.
        return await _dbContext.RefreshTokens
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(t => t.TokenHash == tokenHash)
            .Select(t => (Guid?)t.CenterId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<RefreshToken?> GetTokenAsync(string tokenHash, CancellationToken cancellationToken)
    {
        // Operates under the current Tenant scope
        return _dbContext.RefreshTokens
            .Include(t => t.User)
            .FirstOrDefaultAsync(t => t.TokenHash == tokenHash, cancellationToken);
    }

    public async Task<bool> RotateTokenAsync(
        ulong oldTokenId,
        string reason,
        string? clientIp,
        DateTime nowUtc,
        RefreshToken newToken,
        CancellationToken cancellationToken)
    {
        using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        // 1. Insert new token
        _dbContext.RefreshTokens.Add(newToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        // 2. Conditionally update old token to prevent reuse / concurrency
        var affectedRows = await _dbContext.RefreshTokens
            .Where(t => t.RefreshTokenId == oldTokenId && t.RevokedAt == null && t.ReplacedByTokenId == null && t.ExpiresAt > nowUtc)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.RevokedAt, nowUtc)
                .SetProperty(p => p.RevokedByIp, clientIp)
                .SetProperty(p => p.RevokeReason, reason)
                .SetProperty(p => p.ReplacedByTokenId, newToken.RefreshTokenId),
                cancellationToken);

        if (affectedRows == 1)
        {
            await transaction.CommitAsync(cancellationToken);
            return true;
        }

        // If no rows were affected, the old token was already revoked/replaced, or not found.
        // Rollback the transaction to prevent saving the orphan successor token.
        await transaction.RollbackAsync(cancellationToken);
        _dbContext.Entry(newToken).State = EntityState.Detached;
        return false;
    }

    public async Task<bool> RevokeTokenAsync(
        ulong tokenId,
        string reason,
        string? clientIp,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        var affectedRows = await _dbContext.RefreshTokens
            .Where(t => t.RefreshTokenId == tokenId && t.RevokedAt == null)
            .ExecuteUpdateAsync(s => s
                .SetProperty(p => p.RevokedAt, nowUtc)
                .SetProperty(p => p.RevokedByIp, clientIp)
                .SetProperty(p => p.RevokeReason, reason),
                cancellationToken);

        return affectedRows == 1;
    }
}
