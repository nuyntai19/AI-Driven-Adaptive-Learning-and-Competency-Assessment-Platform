using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EduTwin.BLL.Organization;

public class DeleteClassUseCase : IDeleteClassUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DeleteClassUseCase> _logger;

    public DeleteClassUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        TimeProvider timeProvider,
        ILogger<DeleteClassUseCase> logger)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<DeleteClassResult> ExecuteAsync(
        Guid classId,
        uint? expectedRowVersion = null,
        string? traceId = null,
        CancellationToken cancellationToken = default)
    {
        if (classId == Guid.Empty)
        {
            return DeleteClassResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (!_tenantContext.IsResolved ||
            !_tenantContext.CenterId.HasValue ||
            _tenantContext.CenterId.Value == Guid.Empty ||
            !_tenantContext.UserId.HasValue ||
            _tenantContext.UserId.Value == Guid.Empty ||
            string.IsNullOrWhiteSpace(_tenantContext.Role))
        {
            return DeleteClassResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (!string.Equals(_tenantContext.Role, nameof(UserRole.CenterManager), StringComparison.Ordinal))
        {
            return DeleteClassResult.Failure(ErrorCodes.ForbiddenResource, "Chỉ Quản lý trung tâm mới có quyền xóa lớp học.");
        }

        var centerId = _tenantContext.CenterId.Value;
        var managerId = _tenantContext.UserId.Value;

        var center = await _dbContext.Centers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CenterId == centerId, cancellationToken);

        if (center == null || center.IsDeleted || center.Status != CenterStatus.Active)
        {
            return DeleteClassResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var existingClass = await _dbContext.Classes
            .FirstOrDefaultAsync(c => c.ClassId == classId && c.CenterId == centerId && !c.IsDeleted, cancellationToken);

        if (existingClass == null)
        {
            return DeleteClassResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (expectedRowVersion.HasValue)
        {
            if (existingClass.RowVersion != expectedRowVersion.Value)
            {
                return DeleteClassResult.Failure(ErrorCodes.ConcurrencyConflict);
            }

            _dbContext.Entry(existingClass).Property(c => c.RowVersion).OriginalValue = expectedRowVersion.Value;
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Check 1: Any members historically associated with the class (Active or Removed)
            var hasMembers = await _dbContext.ClassStudents
                .AnyAsync(cs => cs.CenterId == centerId && cs.ClassId == classId, cancellationToken);

            if (hasMembers)
            {
                await transaction.RollbackAsync(cancellationToken);
                return DeleteClassResult.Failure(
                    ErrorCodes.InvalidStateTransition,
                    "Lớp học đã có thành viên hoặc lịch sử học tập nên không thể xóa. Vui lòng chuyển trạng thái lớp sang Lưu trữ (Archive).");
            }

            // Check 2: Any curriculum linked to this class
            var hasCurriculum = await _dbContext.CurriculumClasses
                .AnyAsync(cc => cc.CenterId == centerId && cc.ClassId == classId, cancellationToken);

            if (hasCurriculum)
            {
                await transaction.RollbackAsync(cancellationToken);
                return DeleteClassResult.Failure(
                    ErrorCodes.InvalidStateTransition,
                    "Lớp học đã được liên kết với giáo trình nên không thể xóa. Vui lòng chuyển trạng thái lớp sang Lưu trữ (Archive).");
            }

            // Check 3: Any assignments created for this class
            var hasAssignments = await _dbContext.Assignments
                .AnyAsync(a => a.CenterId == centerId && a.ClassId == classId, cancellationToken);

            if (hasAssignments)
            {
                await transaction.RollbackAsync(cancellationToken);
                return DeleteClassResult.Failure(
                    ErrorCodes.InvalidStateTransition,
                    "Lớp học đã có bài tập hoặc lịch sử giao bài nên không thể xóa. Vui lòng chuyển trạng thái lớp sang Lưu trữ (Archive).");
            }

            var now = _timeProvider.GetUtcNow().UtcDateTime;
            var originalClassName = existingClass.ClassName;
            var originalAcademicYear = existingClass.AcademicYear;

            // Free unique constraint (center_id, class_name, academic_year) for re-creation
            var suffix = $"#del#{classId:N}";
            var maxBaseLength = 150 - suffix.Length;
            var truncatedBase = originalClassName.Length > maxBaseLength
                ? originalClassName[..maxBaseLength]
                : originalClassName;
            existingClass.ClassName = $"{truncatedBase}{suffix}";

            existingClass.IsDeleted = true;
            existingClass.DeletedAt = now;
            existingClass.DeletedBy = managerId;
            existingClass.UpdatedAt = now;
            existingClass.UpdatedBy = managerId;
            existingClass.RowVersion++;

            _dbContext.AuthorizationAuditLogs.Add(new AuthorizationAuditLog
            {
                CenterId = centerId,
                ActorUserId = managerId,
                ActionType = "ClassDeleted",
                TargetType = "Class",
                TargetId = classId.ToString("D"),
                TargetUserId = null,
                BeforeData = JsonSerializer.Serialize(new
                {
                    ClassId = classId,
                    ClassName = originalClassName,
                    AcademicYear = originalAcademicYear,
                    SubjectId = existingClass.SubjectId,
                    TeacherId = existingClass.TeacherId,
                    GradeLevel = existingClass.GradeLevel,
                    Status = existingClass.Status.ToString()
                }),
                AfterData = JsonSerializer.Serialize(new
                {
                    IsDeleted = true,
                    RenamedTo = existingClass.ClassName
                }),
                Reason = "Class soft-deleted by CenterManager.",
                TraceId = traceId ?? string.Empty,
                CreatedAt = now,
                CreatedBy = managerId
            });

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return DeleteClassResult.Success();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            _logger.LogWarning(ex, "Concurrency conflict when soft-deleting class {ClassId}", classId);
            _dbContext.ChangeTracker.Clear();
            return DeleteClassResult.Failure(ErrorCodes.ConcurrencyConflict);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
