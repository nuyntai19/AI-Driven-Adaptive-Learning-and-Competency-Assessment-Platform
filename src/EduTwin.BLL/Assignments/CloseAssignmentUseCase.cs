using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Assignments;

public class CloseAssignmentUseCase : ICloseAssignmentUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public CloseAssignmentUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
    }

    public async Task<CloseAssignmentResult> ExecuteAsync(
        Guid assignmentId,
        CloseAssignmentRequest request,
        CancellationToken cancellationToken = default)
    {
        // ── 1. Fail-closed tenant/role gate ────────────────────────────────────
        if (!_tenantContext.IsResolved ||
            !_tenantContext.CenterId.HasValue || _tenantContext.CenterId.Value == Guid.Empty ||
            !_tenantContext.UserId.HasValue || _tenantContext.UserId.Value == Guid.Empty ||
            string.IsNullOrWhiteSpace(_tenantContext.Role) ||
            (!string.Equals(_tenantContext.Role, nameof(UserRole.Teacher), StringComparison.Ordinal) &&
             !string.Equals(_tenantContext.Role, nameof(UserRole.CenterManager), StringComparison.Ordinal)))
        {
            return CloseAssignmentResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (assignmentId == Guid.Empty)
            return CloseAssignmentResult.Failure(ErrorCodes.ResourceNotFound);

        var actorId = _tenantContext.UserId.Value;
        var isTeacher = string.Equals(_tenantContext.Role, nameof(UserRole.Teacher), StringComparison.Ordinal);

        // ── 2. Validate rowVersion format: ASCII digits only, > 0 ──────────────
        if (string.IsNullOrEmpty(request.RowVersion))
            return CloseAssignmentResult.Failure(ErrorCodes.ValidationFailed);

        foreach (var ch in request.RowVersion)
        {
            if (ch < '0' || ch > '9')
                return CloseAssignmentResult.Failure(ErrorCodes.ValidationFailed);
        }

        if (!ulong.TryParse(request.RowVersion, NumberStyles.None, CultureInfo.InvariantCulture, out var clientRowVersion) || clientRowVersion == 0)
            return CloseAssignmentResult.Failure(ErrorCodes.ValidationFailed);

        // ── 3. Load Assignment (Global Query Filter: centerId + !isDeleted) ─────
        var assignment = await _dbContext.Assignments
            .FirstOrDefaultAsync(a => a.AssignmentId == assignmentId, cancellationToken);

        if (assignment == null)
            return CloseAssignmentResult.Failure(ErrorCodes.ResourceNotFound);

        // ── 4. Ownership guard ──────────────────────────────────────────────────
        if (isTeacher)
        {
            var classOwner = await _dbContext.Classes
                .AsNoTracking()
                .Select(c => new { c.ClassId, c.TeacherId })
                .FirstOrDefaultAsync(c => c.ClassId == assignment.ClassId, cancellationToken);

            if (classOwner == null || classOwner.TeacherId != actorId)
                return CloseAssignmentResult.Failure(ErrorCodes.ResourceNotFound);
        }

        // ── 5. State checking ───────────────────────────────────────────────────
        if (assignment.Status != AssignmentStatus.Published)
        {
            // Close chỉ áp dụng cho assignment đang Published
            // Nếu là Closed rồi thì cũng lỗi (InvalidStateTransition)
            return CloseAssignmentResult.Failure(ErrorCodes.InvalidStateTransition);
        }

        // ── 6. Concurrency checking ─────────────────────────────────────────────
        if (assignment.RowVersion != clientRowVersion)
        {
            return CloseAssignmentResult.Failure(ErrorCodes.ConcurrencyConflict);
        }

        // ── 7. Business Logic (Chuyển đổi trạng thái) ───────────────────────────
        assignment.Status = AssignmentStatus.Closed;
        assignment.RowVersion++;

        // Audit update info is automatically handled by SaveChangesAsync through IMutableTenantAggregate interceptor

        await _dbContext.SaveChangesAsync(cancellationToken);

        // ── 8. Map response ─────────────────────────────────────────────────────
        
        // Load targets and questions count for DTO response (như Publish)
        var questionCount = await _dbContext.AssignmentQuestions
            .CountAsync(aq => aq.AssignmentId == assignmentId, cancellationToken);

        var targetCount = await _dbContext.AssignmentTargets
            .CountAsync(at => at.AssignmentId == assignmentId, cancellationToken);

        var dto = new AssignmentDto
        {
            AssignmentId = assignment.AssignmentId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(),
            ClassId = assignment.ClassId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(),
            CreatedByTeacherId = assignment.CreatedByTeacherId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(),
            Title = assignment.Title,
            Instructions = assignment.Instructions,
            DueAt = assignment.DueAt,
            Status = assignment.Status.ToString(),
            QuestionCount = questionCount,
            TargetStudentCount = targetCount,
            Questions = new System.Collections.Generic.List<AssignmentQuestionDto>(),
            Targets = new System.Collections.Generic.List<AssignmentTargetDto>(),
            RowVersion = assignment.RowVersion.ToString(CultureInfo.InvariantCulture)
        };

        return CloseAssignmentResult.Success(dto);
    }
}
