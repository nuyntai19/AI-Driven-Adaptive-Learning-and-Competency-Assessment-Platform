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
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Organization;

namespace EduTwin.BLL.KnowledgeGraph;

public class CreateSubjectUseCase : ICreateSubjectUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public CreateSubjectUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
    }

    public async Task<CreateSubjectResult> ExecuteAsync(CreateSubjectRequest request, CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.IsResolved)
            return CreateSubjectResult.Failure(ErrorCodes.ResourceNotFound);

        if (_tenantContext.CenterId == null || _tenantContext.CenterId == Guid.Empty)
            return CreateSubjectResult.Failure(ErrorCodes.ResourceNotFound);

        if (_tenantContext.UserId == null || _tenantContext.UserId == Guid.Empty)
            return CreateSubjectResult.Failure(ErrorCodes.ResourceNotFound);

        if (string.IsNullOrWhiteSpace(_tenantContext.Role))
            return CreateSubjectResult.Failure(ErrorCodes.ResourceNotFound);

        if (_tenantContext.Role != nameof(UserRole.Teacher) && _tenantContext.Role != nameof(UserRole.CenterManager))
            return CreateSubjectResult.Failure(ErrorCodes.ResourceNotFound);

        if (string.IsNullOrWhiteSpace(request.SubjectCode) || request.SubjectCode.Length > 32)
            return CreateSubjectResult.Failure(ErrorCodes.ValidationFailed);

        if (string.IsNullOrWhiteSpace(request.SubjectName) || request.SubjectName.Length > 100)
            return CreateSubjectResult.Failure(ErrorCodes.ValidationFailed);

        if (request.Description?.Length > 500)
            return CreateSubjectResult.Failure(ErrorCodes.ValidationFailed);

        var trimmedCode = request.SubjectCode.Trim();
        var trimmedName = request.SubjectName.Trim();
        var trimmedDesc = request.Description?.Trim();
        if (string.IsNullOrWhiteSpace(trimmedDesc)) trimmedDesc = null;

        var center = await _dbContext.Centers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CenterId == _tenantContext.CenterId, cancellationToken);

        if (center == null || center.Status != EduTwin.Contracts.Organization.CenterStatus.Active || center.IsDeleted)
            return CreateSubjectResult.Failure(ErrorCodes.ResourceNotFound);

        var exists = await _dbContext.Subjects
            .AnyAsync(s => s.CenterId == _tenantContext.CenterId && s.SubjectCode == trimmedCode, cancellationToken);

        if (exists)
            return CreateSubjectResult.Failure(ErrorCodes.DuplicateResource);

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var subject = new Subject
        {
            SubjectId = Guid.NewGuid(),
            CenterId = _tenantContext.CenterId.Value,
            SubjectCode = trimmedCode,
            SubjectName = trimmedName,
            Description = trimmedDesc,
            IsActive = true,
            IsDeleted = false,
            RowVersion = 1,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = _tenantContext.UserId.Value,
            UpdatedBy = _tenantContext.UserId.Value
        };

        _dbContext.Subjects.Add(subject);

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex)
        {
            _dbContext.ChangeTracker.Clear();

            var isDuplicate = false;
            var currentException = (Exception)ex;
            while (currentException != null)
            {
                if (currentException.Message.Contains("ux_subjects_center_id_subject_code", StringComparison.OrdinalIgnoreCase))
                {
                    isDuplicate = true;
                    break;
                }
                currentException = currentException.InnerException!;
            }

            if (isDuplicate)
            {
                return CreateSubjectResult.Failure(ErrorCodes.DuplicateResource);
            }

            throw;
        }

        var dto = new SubjectDto
        {
            SubjectId = subject.SubjectId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(),
            SubjectCode = subject.SubjectCode,
            SubjectName = subject.SubjectName,
            Description = subject.Description,
            IsActive = subject.IsActive,
            RowVersion = subject.RowVersion.ToString(CultureInfo.InvariantCulture)
        };

        return CreateSubjectResult.Success(dto);
    }
}
