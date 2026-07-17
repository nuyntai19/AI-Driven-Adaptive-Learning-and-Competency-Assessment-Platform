using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.IdentityAndTenancy;

public class CenterResolver : ICenterResolver
{
    private readonly EduTwinDbContext _dbContext;

    public CenterResolver(EduTwinDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<CenterResolutionDto?> ResolveByCodeAsync(string centerCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(centerCode))
            throw new ArgumentException("CenterCode cannot be blank.", nameof(centerCode));

        return await _dbContext.Centers
            .AsNoTracking()
            .Where(c => c.CenterCode == centerCode)
            .Select(c => new CenterResolutionDto(c.CenterId, c.CenterCode, c.Status))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
