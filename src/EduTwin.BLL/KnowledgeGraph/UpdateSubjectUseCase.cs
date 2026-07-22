using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.KnowledgeGraph;

public class UpdateSubjectUseCase : IUpdateSubjectUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public UpdateSubjectUseCase(EduTwinDbContext dbContext, ITenantContext tenantContext, TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
    }

    public async Task<UpdateSubjectResult> ExecuteAsync(Guid subjectId, UpdateSubjectRequest request, CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsResolved ||
            _tenantContext.CenterId == null ||
            _tenantContext.CenterId == Guid.Empty ||
            _tenantContext.UserId == null ||
            _tenantContext.UserId == Guid.Empty ||
            string.IsNullOrWhiteSpace(_tenantContext.Role))
        {
            return UpdateSubjectResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (_tenantContext.Role != nameof(UserRole.Teacher) && _tenantContext.Role != nameof(UserRole.CenterManager))
        {
            return UpdateSubjectResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (subjectId == Guid.Empty)
        {
            return UpdateSubjectResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var validationResults = new List<ValidationResult>();
        var validationContext = new ValidationContext(request, null, null);
        if (!Validator.TryValidateObject(request, validationContext, validationResults, true))
        {
            return UpdateSubjectResult.Failure(ErrorCodes.ValidationFailed);
        }

        var centerId = _tenantContext.CenterId.Value;

        var center = await _dbContext.Centers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.CenterId == centerId, cancellationToken);

        if (center == null || center.Status != EduTwin.Contracts.Organization.CenterStatus.Active)
        {
            return UpdateSubjectResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var subject = await _dbContext.Subjects
            .FirstOrDefaultAsync(x => x.SubjectId == subjectId && x.CenterId == centerId, cancellationToken);

        if (subject == null)
        {
            return UpdateSubjectResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (!ulong.TryParse(request.RowVersion, NumberStyles.None, CultureInfo.InvariantCulture, out var requestRowVersion) || requestRowVersion == 0)
        {
            return UpdateSubjectResult.Failure(ErrorCodes.ValidationFailed);
        }

        if (subject.RowVersion != requestRowVersion)
        {
            return UpdateSubjectResult.Failure(ErrorCodes.ConcurrencyConflict);
        }

        var subjectCode = request.SubjectCode.Trim();
        var subjectName = request.SubjectName.Trim();
        var description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();

        var existingSubject = await _dbContext.Subjects
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.CenterId == centerId && x.SubjectCode == subjectCode && x.SubjectId != subjectId, cancellationToken);

        if (existingSubject != null)
        {
            return UpdateSubjectResult.Failure(ErrorCodes.DuplicateResource);
        }

        subject.SubjectCode = subjectCode;
        subject.SubjectName = subjectName;
        subject.Description = description;
        subject.IsActive = request.IsActive!.Value;
        subject.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        subject.UpdatedBy = _tenantContext.UserId.Value;

        _dbContext.Entry(subject).Property(x => x.RowVersion).OriginalValue = requestRowVersion;

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            _dbContext.ChangeTracker.Clear();
            return UpdateSubjectResult.Failure(ErrorCodes.ConcurrencyConflict);
        }
        catch (DbUpdateException ex)
        {
            _dbContext.ChangeTracker.Clear();
            Exception? current = ex;
            while (current != null)
            {
                if (current.Message.Contains("ux_subjects_center_id_subject_code", StringComparison.OrdinalIgnoreCase))
                {
                    return UpdateSubjectResult.Failure(ErrorCodes.DuplicateResource);
                }
                current = current.InnerException;
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

        return UpdateSubjectResult.Success(dto);
    }
}
