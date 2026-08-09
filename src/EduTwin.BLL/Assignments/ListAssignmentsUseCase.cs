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

public class ListAssignmentsUseCase : IListAssignmentsUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public ListAssignmentsUseCase(EduTwinDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<ListAssignmentsResult> ExecuteAsync(
        ListAssignmentsQuery query,
        CancellationToken cancellationToken = default)
    {
        // 1. Fail-closed gate
        if (!_tenantContext.IsResolved ||
            !_tenantContext.CenterId.HasValue || _tenantContext.CenterId.Value == Guid.Empty ||
            !_tenantContext.UserId.HasValue || _tenantContext.UserId.Value == Guid.Empty ||
            string.IsNullOrWhiteSpace(_tenantContext.Role) ||
            (!string.Equals(_tenantContext.Role, nameof(UserRole.Teacher), StringComparison.Ordinal) &&
             !string.Equals(_tenantContext.Role, nameof(UserRole.CenterManager), StringComparison.Ordinal)))
        {
            return ListAssignmentsResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var actorId = _tenantContext.UserId.Value;
        var isTeacher = string.Equals(_tenantContext.Role, nameof(UserRole.Teacher), StringComparison.Ordinal);

        // 2. Validate pagination
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize < 1 ? 20 : query.PageSize > 100 ? 100 : query.PageSize;

        // 3. Validate status filter if provided
        AssignmentStatus? statusFilter = null;
        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (!Enum.TryParse<AssignmentStatus>(query.Status, ignoreCase: false, out var parsedStatus))
                return ListAssignmentsResult.Failure(ErrorCodes.ValidationFailed);
            statusFilter = parsedStatus;
        }

        // 4. Build query — Global Query Filter handles centerId + !isDeleted
        var assignmentsQuery = _dbContext.Assignments.AsNoTracking();

        // Teacher scope: only assignments for their classes
        if (isTeacher)
        {
            // Use a correlated EXISTS instead of Contains(List<Guid>). The MySQL
            // provider cannot type-map a Guid collection against varchar(36).
            assignmentsQuery = assignmentsQuery.Where(a =>
                _dbContext.Classes.Any(c => c.ClassId == a.ClassId && c.TeacherId == actorId));
        }

        if (query.ClassId.HasValue && query.ClassId.Value != Guid.Empty)
            assignmentsQuery = assignmentsQuery.Where(a => a.ClassId == query.ClassId.Value);

        if (statusFilter.HasValue)
            assignmentsQuery = assignmentsQuery.Where(a => a.Status == statusFilter.Value);

        // 5. Count and paginate
        var totalItems = await assignmentsQuery.LongCountAsync(cancellationToken);

        var assignments = await assignmentsQuery
            .OrderByDescending(a => a.CreatedAt)
            .ThenBy(a => a.AssignmentId)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(a => new
            {
                a.AssignmentId,
                a.ClassId,
                a.CreatedByTeacherId,
                a.Title,
                a.Instructions,
                a.DueAt,
                a.Status,
                a.RowVersion,
                QuestionCount = _dbContext.AssignmentQuestions.Count(aq => aq.AssignmentId == a.AssignmentId),
                TargetStudentCount = _dbContext.AssignmentTargets.Count(at => at.AssignmentId == a.AssignmentId)
            })
            .ToListAsync(cancellationToken);

        if (assignments.Count == 0)
            return ListAssignmentsResult.Success(new List<AssignmentDto>(), totalItems);

        // 6. Map to DTOs (list view — questions/targets arrays empty for performance)
        var dtos = assignments.Select(a => new AssignmentDto
        {
            AssignmentId = a.AssignmentId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(),
            ClassId = a.ClassId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(),
            CreatedByTeacherId = a.CreatedByTeacherId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(),
            Title = a.Title,
            Instructions = a.Instructions,
            DueAt = a.DueAt,
            Status = a.Status.ToString(),
            QuestionCount = a.QuestionCount,
            TargetStudentCount = a.TargetStudentCount,
            Questions = new List<AssignmentQuestionDto>(),
            Targets = new List<AssignmentTargetDto>(),
            RowVersion = a.RowVersion.ToString(CultureInfo.InvariantCulture)
        }).ToList();

        return ListAssignmentsResult.Success(dtos, totalItems);
    }
}
