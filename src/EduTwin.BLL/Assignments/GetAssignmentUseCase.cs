using System;
using System.Collections.Generic;
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

public class GetAssignmentUseCase : IGetAssignmentUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public GetAssignmentUseCase(EduTwinDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<GetAssignmentResult> ExecuteAsync(
        Guid assignmentId,
        CancellationToken cancellationToken = default)
    {
        // 1. Fail-closed tenant/role gate
        if (!_tenantContext.IsResolved ||
            !_tenantContext.CenterId.HasValue || _tenantContext.CenterId.Value == Guid.Empty ||
            !_tenantContext.UserId.HasValue || _tenantContext.UserId.Value == Guid.Empty ||
            string.IsNullOrWhiteSpace(_tenantContext.Role) ||
            (!string.Equals(_tenantContext.Role, nameof(UserRole.Teacher), StringComparison.Ordinal) &&
             !string.Equals(_tenantContext.Role, nameof(UserRole.CenterManager), StringComparison.Ordinal)))
        {
            return GetAssignmentResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (assignmentId == Guid.Empty)
            return GetAssignmentResult.Failure(ErrorCodes.ResourceNotFound);

        var actorId = _tenantContext.UserId.Value;
        var isTeacher = string.Equals(_tenantContext.Role, nameof(UserRole.Teacher), StringComparison.Ordinal);

        // 2. Load assignment with navigations (Global Query Filter applies: centerId + !isDeleted)
        var assignment = await _dbContext.Assignments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.AssignmentId == assignmentId, cancellationToken);

        if (assignment == null)
            return GetAssignmentResult.Failure(ErrorCodes.ResourceNotFound);

        // 3. Ownership guard: Teacher chỉ xem Assignment thuộc Class mình
        if (isTeacher)
        {
            // Load class to check TeacherId
            var classEntity = await _dbContext.Classes
                .AsNoTracking()
                .Select(c => new { c.ClassId, c.TeacherId })
                .FirstOrDefaultAsync(c => c.ClassId == assignment.ClassId, cancellationToken);

            if (classEntity == null || classEntity.TeacherId != actorId)
                return GetAssignmentResult.Failure(ErrorCodes.ResourceNotFound);
        }

        // 4. Load questions
        var assignmentQuestions = await _dbContext.AssignmentQuestions
            .AsNoTracking()
            .Where(aq => aq.AssignmentId == assignmentId)
            .OrderBy(aq => aq.OrderIndex)
            .ToListAsync(cancellationToken);

        // 5. Load targets
        var assignmentTargets = await _dbContext.AssignmentTargets
            .AsNoTracking()
            .Where(at => at.AssignmentId == assignmentId)
            .ToListAsync(cancellationToken);

        var questionDtos = assignmentQuestions
            .Select(aq => new AssignmentQuestionDto
            {
                QuestionId = aq.QuestionId.ToString(CultureInfo.InvariantCulture),
                OrderIndex = aq.OrderIndex,
                Points = aq.Points
            })
            .ToList();

        var targetDtos = assignmentTargets
            .Select(at => new AssignmentTargetDto
            {
                StudentId = at.StudentId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(),
                TargetSource = at.TargetSource.ToString()
            })
            .ToList();

        var dto = new AssignmentDto
        {
            AssignmentId = assignment.AssignmentId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(),
            ClassId = assignment.ClassId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(),
            CreatedByTeacherId = assignment.CreatedByTeacherId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(),
            Title = assignment.Title,
            Instructions = assignment.Instructions,
            DueAt = assignment.DueAt,
            Status = assignment.Status.ToString(),
            QuestionCount = questionDtos.Count,
            TargetStudentCount = targetDtos.Count,
            Questions = questionDtos,
            Targets = targetDtos,
            RowVersion = assignment.RowVersion.ToString(CultureInfo.InvariantCulture)
        };

        return GetAssignmentResult.Success(dto);
    }
}
