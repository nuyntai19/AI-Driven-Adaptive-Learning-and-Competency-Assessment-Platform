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

public class GetAssignmentProgressUseCase : IGetAssignmentProgressUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public GetAssignmentProgressUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
    }

    public async Task<GetAssignmentProgressResult> ExecuteAsync(
        Guid assignmentId,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.IsResolved ||
            !_tenantContext.CenterId.HasValue || _tenantContext.CenterId.Value == Guid.Empty ||
            !_tenantContext.UserId.HasValue || _tenantContext.UserId.Value == Guid.Empty ||
            string.IsNullOrWhiteSpace(_tenantContext.Role) ||
            !string.Equals(_tenantContext.Role, nameof(UserRole.Teacher), StringComparison.Ordinal) ||
            assignmentId == Guid.Empty)
        {
            return GetAssignmentProgressResult.Failure(ErrorCodes.ForbiddenResource);
        }

        var actorId = _tenantContext.UserId.Value;
        var centerId = _tenantContext.CenterId.Value;

        var assignment = await _dbContext.Assignments
            .AsNoTracking()
            .Where(item => item.AssignmentId == assignmentId)
            .Select(item => new { item.ClassId, item.DueAt })
            .FirstOrDefaultAsync(cancellationToken);

        if (assignment == null)
            return GetAssignmentProgressResult.Failure(ErrorCodes.ResourceNotFound);

        var ownsClass = await _dbContext.Classes
            .AsNoTracking()
            .AnyAsync(
                item => item.ClassId == assignment.ClassId && item.TeacherId == actorId,
                cancellationToken);

        if (!ownsClass)
            return GetAssignmentProgressResult.Failure(ErrorCodes.ForbiddenResource);

        var progressRows = await _dbContext.StudentAssignmentProgresses
            .AsNoTracking()
            .Where(item => item.AssignmentId == assignmentId && item.Student != null)
            .Select(item => new
            {
                item.StudentId,
                FullName = item.Student!.FullName,
                item.Status,
                item.CompletedQuestionCount,
                item.TotalQuestionCount,
                item.TeacherFinalReviewStatus,
                item.FinalReviewVersion,
                item.CompletedAt
            })
            .OrderBy(item => item.FullName)
            .ThenBy(item => item.StudentId)
            .ToListAsync(cancellationToken);

        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;
        var questions = await _dbContext.AssignmentQuestions.AsNoTracking()
            .Where(q => q.CenterId == centerId && q.AssignmentId == assignmentId).ToListAsync(cancellationToken);
        // Load the owned assignment once. Avoid parameterized Guid-list Contains,
        // which the MySQL EF provider cannot reliably map. Only progress rows are projected below.
        var attempts = await _dbContext.Attempts.AsNoTracking()
            .Where(a => a.CenterId == centerId && a.AssignmentId == assignmentId)
            .ToListAsync(cancellationToken);
        var attemptsByStudent = attempts.ToLookup(a => a.StudentId);
        var rubric = await AssignmentRubricReviewState.LoadAsync(_dbContext, centerId, assignmentId, null, cancellationToken);
        var data = progressRows
            .Select(item => new AssignmentProgressItemDto
            {
                StudentId = item.StudentId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(),
                FullName = item.FullName,
                Status = AssignmentStatusHelper
                    .GetEffectiveProgressStatus(item.Status, assignment.DueAt, utcNow)
                    .ToString(),
                CompletedQuestionCount = checked((int)item.CompletedQuestionCount),
                TotalQuestionCount = checked((int)item.TotalQuestionCount),
                TeacherFinalReviewStatus = item.TeacherFinalReviewStatus.ToString(),
                FinalReviewVersion = item.FinalReviewVersion,
                CompletedAt = item.CompletedAt,
                FinalReviewEligibility = AssignmentFinalReviewPolicy.Evaluate(questions, attemptsByStudent[item.StudentId], rubric.QuestionIds, rubric.GradedAttemptIds)
            })
            .ToList();

        return GetAssignmentProgressResult.Success(data);
    }
}
