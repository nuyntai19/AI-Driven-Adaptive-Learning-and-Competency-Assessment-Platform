using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.KnowledgeGraph;

public class GetSubjectUseCase : IGetSubjectUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public GetSubjectUseCase(EduTwinDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<GetSubjectResult> ExecuteAsync(Guid subjectId, CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsResolved)
            return GetSubjectResult.Failure(ErrorCodes.ResourceNotFound);

        if (_tenantContext.CenterId == null || _tenantContext.CenterId == Guid.Empty)
            return GetSubjectResult.Failure(ErrorCodes.ResourceNotFound);

        if (_tenantContext.UserId == null || _tenantContext.UserId == Guid.Empty)
            return GetSubjectResult.Failure(ErrorCodes.ResourceNotFound);

        if (string.IsNullOrWhiteSpace(_tenantContext.Role))
            return GetSubjectResult.Failure(ErrorCodes.ResourceNotFound);

        if (_tenantContext.Role != nameof(UserRole.Student) &&
            _tenantContext.Role != nameof(UserRole.Teacher) &&
            _tenantContext.Role != nameof(UserRole.CenterManager))
            return GetSubjectResult.Failure(ErrorCodes.ResourceNotFound);

        if (subjectId == Guid.Empty)
            return GetSubjectResult.Failure(ErrorCodes.ResourceNotFound);

        var centerId = _tenantContext.CenterId.Value;

        var center = await _dbContext.Centers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.CenterId == centerId, cancellationToken);

        if (center == null || center.Status != EduTwin.Contracts.Organization.CenterStatus.Active)
            return GetSubjectResult.Failure(ErrorCodes.ResourceNotFound);

        var subject = await _dbContext.Subjects
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.SubjectId == subjectId && x.CenterId == centerId, cancellationToken);

        if (subject == null)
            return GetSubjectResult.Failure(ErrorCodes.ResourceNotFound);

        var dto = new SubjectDto
        {
            SubjectId = subject.SubjectId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(),
            SubjectCode = subject.SubjectCode,
            SubjectName = subject.SubjectName,
            Description = subject.Description,
            IsActive = subject.IsActive,
            RowVersion = subject.RowVersion.ToString(CultureInfo.InvariantCulture)
        };

        return GetSubjectResult.Success(dto);
    }
}
