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
            // Get classIds for this teacher
            var teacherClassIds = await _dbContext.Classes
                .AsNoTracking()
                .Where(c => c.TeacherId == actorId)
                .Select(c => c.ClassId)
                .ToListAsync(cancellationToken);

            assignmentsQuery = assignmentsQuery.Where(a => teacherClassIds.Contains(a.ClassId));
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
            .ToListAsync(cancellationToken);

        if (assignments.Count == 0)
            return ListAssignmentsResult.Success(new List<AssignmentDto>(), totalItems);

        // 6. Load questions counts per assignment
        var assignmentIds = assignments.Select(a => a.AssignmentId).ToList();

        var questionCounts = await _dbContext.AssignmentQuestions
            .AsNoTracking()
            .Where(aq => assignmentIds.Contains(aq.AssignmentId))
            .GroupBy(aq => aq.AssignmentId)
            .Select(g => new { AssignmentId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var questionCountDict = questionCounts.ToDictionary(x => x.AssignmentId, x => x.Count);

        var targetCounts = await _dbContext.AssignmentTargets
            .AsNoTracking()
            .Where(at => assignmentIds.Contains(at.AssignmentId))
            .GroupBy(at => at.AssignmentId)
            .Select(g => new { AssignmentId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var targetCountDict = targetCounts.ToDictionary(x => x.AssignmentId, x => x.Count);

        // 7. Map to DTOs (list view — questions/targets arrays empty for performance)
        var dtos = assignments.Select(a => new AssignmentDto
        {
            AssignmentId = a.AssignmentId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(),
            ClassId = a.ClassId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(),
            CreatedByTeacherId = a.CreatedByTeacherId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(),
            Title = a.Title,
            Instructions = a.Instructions,
            DueAt = a.DueAt,
            Status = a.Status.ToString(),
            QuestionCount = questionCountDict.TryGetValue(a.AssignmentId, out var qc) ? qc : 0,
            TargetStudentCount = targetCountDict.TryGetValue(a.AssignmentId, out var tc) ? tc : 0,
            Questions = new List<AssignmentQuestionDto>(),
            Targets = new List<AssignmentTargetDto>(),
            RowVersion = a.RowVersion.ToString(CultureInfo.InvariantCulture)
        }).ToList();

        return ListAssignmentsResult.Success(dtos, totalItems);
    }
}
