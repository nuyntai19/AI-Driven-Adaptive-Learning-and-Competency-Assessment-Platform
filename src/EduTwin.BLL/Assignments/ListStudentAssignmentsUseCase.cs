using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.Common;
using EduTwin.DAL;
using EduTwin.DAL.Persistence;
using EduTwin.BLL.IdentityAndTenancy;

namespace EduTwin.BLL.Assignments;

public class ListStudentAssignmentsUseCase : IListStudentAssignmentsUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public ListStudentAssignmentsUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
    }

    public async Task<ListStudentAssignmentsResult> ExecuteAsync(ListStudentAssignmentsQuery query, CancellationToken cancellationToken)
    {
        var currentUserId = _tenantContext.UserId;
        var centerId = _tenantContext.CenterId;
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        var baseQuery = _dbContext.StudentAssignmentProgresses
            .AsNoTracking()
            .Include(p => p.Assignment)
            .Where(p => p.CenterId == centerId && 
                        p.StudentId == currentUserId && 
                        p.Assignment != null && 
                        !p.Assignment.IsDeleted && 
                        (p.Assignment.Status == AssignmentStatus.Published || p.Assignment.Status == AssignmentStatus.Closed));

        if (!string.IsNullOrEmpty(query.Status))
        {
            if (Enum.TryParse<ProgressStatus>(query.Status, out var statusFilter))
            {
                if (statusFilter == ProgressStatus.Overdue)
                {
                    baseQuery = baseQuery.Where(p => 
                        p.Status != ProgressStatus.Completed && 
                        p.Assignment!.DueAt.HasValue && 
                        p.Assignment.DueAt.Value < utcNow);
                }
                else
                {
                    baseQuery = baseQuery.Where(p => 
                        p.Status == statusFilter && 
                        (!p.Assignment!.DueAt.HasValue || p.Assignment.DueAt.Value >= utcNow || p.Status == ProgressStatus.Completed));
                }
            }
            else
            {
                return ListStudentAssignmentsResult.Failure(ErrorCodes.ValidationFailed);
            }
        }

        var totalItems = await baseQuery.CountAsync(cancellationToken);

        var pageSize = query.PageSize < 1 ? 20 : query.PageSize > 100 ? 100 : query.PageSize;
        var page = query.Page < 1 ? 1 : query.Page;
        var skip = (page - 1) * pageSize;

        var items = await baseQuery
            .OrderByDescending(p => p.Assignment!.CreatedAt)
            .ThenBy(p => p.AssignmentId)
            .Skip(skip)
            .Take(pageSize)
            .Select(p => new StudentAssignmentDto
            {
                AssignmentId = p.AssignmentId.ToString(),
                Title = p.Assignment!.Title,
                Instructions = p.Assignment.Instructions,
                DueAt = p.Assignment.DueAt,
                Progress = new StudentAssignmentProgressDto
                {
                    Status = AssignmentStatusHelper.GetEffectiveProgressStatus(p.Status, p.Assignment.DueAt, utcNow).ToString(),
                    CompletedQuestionCount = (int)p.CompletedQuestionCount,
                    TotalQuestionCount = (int)p.TotalQuestionCount
                }
            })
            .ToListAsync(cancellationToken);

        var totalPages = pageSize == 0 ? 0 : (int)Math.Ceiling((double)totalItems / pageSize);

        var response = new StudentAssignmentListResponse
        {
            Data = items,
            Meta = new PagedMetaDto
            {
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                TotalPages = totalPages,
            }
        };

        return ListStudentAssignmentsResult.Success(response);
    }
}
