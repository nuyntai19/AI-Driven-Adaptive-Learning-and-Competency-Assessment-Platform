using System;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.DAL;
using EduTwin.DAL.Assignments;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Assignments;

public class SaveAssignmentDraftUseCase : ISaveAssignmentDraftUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public SaveAssignmentDraftUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
    }

    public async Task<SaveAssignmentDraftResult> ExecuteAsync(
        Guid assignmentId,
        SaveAssignmentDraftRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!_tenantContext.IsResolved ||
            !_tenantContext.CenterId.HasValue || _tenantContext.CenterId.Value == Guid.Empty ||
            !_tenantContext.UserId.HasValue || _tenantContext.UserId.Value == Guid.Empty ||
            !string.Equals(_tenantContext.Role, nameof(UserRole.Student), StringComparison.Ordinal))
        {
            return SaveAssignmentDraftResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var currentUserId = _tenantContext.UserId.Value;
        var centerId = _tenantContext.CenterId.Value;
        var utcNow = _timeProvider.GetUtcNow().UtcDateTime;

        var assignment = await _dbContext.Assignments
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.CenterId == centerId && a.AssignmentId == assignmentId && !a.IsDeleted, cancellationToken);

        if (assignment == null)
        {
            return SaveAssignmentDraftResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (assignment.Status != AssignmentStatus.Published)
        {
            return SaveAssignmentDraftResult.Failure(ErrorCodes.AssignmentNotAvailable);
        }

        var isTarget = await _dbContext.AssignmentTargets
            .AsNoTracking()
            .AnyAsync(at => at.CenterId == centerId && at.AssignmentId == assignmentId && at.StudentId == currentUserId, cancellationToken);

        if (!isTarget)
        {
            return SaveAssignmentDraftResult.Failure(ErrorCodes.ForbiddenResource);
        }

        var progress = await _dbContext.StudentAssignmentProgresses
            .FirstOrDefaultAsync(p =>
                p.CenterId == centerId &&
                p.StudentId == currentUserId &&
                p.AssignmentId == assignmentId &&
                !p.IsDeleted, cancellationToken);

        if (progress == null)
        {
            return SaveAssignmentDraftResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (progress.Status == ProgressStatus.Completed)
        {
            return SaveAssignmentDraftResult.Failure(ErrorCodes.AssignmentNotAvailable);
        }

        // Saving draft requires the assignment to have been started via StartStudentAssignment
        if (await StudentAssignmentScope.SuspendedAsync(_dbContext, centerId, assignment.ClassId, cancellationToken))
            return SaveAssignmentDraftResult.Failure(ErrorCodes.AssignmentNotAvailable);

        if (progress.StartedAt == null)
        {
            return SaveAssignmentDraftResult.Failure(ErrorCodes.AssignmentNotAvailable);
        }

        // Authoritative expiration check: reject saving new draft answers after deadline
        DateTime? effectiveExpiresAt = null;
        if (assignment.DueAt.HasValue)
        {
            effectiveExpiresAt = assignment.DueAt.Value;
        }
        if (assignment.TimeLimitMinutes.HasValue && assignment.TimeLimitMinutes.Value > 0 && progress.StartedAt.HasValue)
        {
            var timeLimitExpiresAt = progress.StartedAt.Value.AddMinutes(assignment.TimeLimitMinutes.Value);
            effectiveExpiresAt = effectiveExpiresAt.HasValue && effectiveExpiresAt.Value < timeLimitExpiresAt
                ? effectiveExpiresAt.Value
                : timeLimitExpiresAt;
        }

        if (effectiveExpiresAt.HasValue && utcNow > effectiveExpiresAt.Value)
        {
            return SaveAssignmentDraftResult.Failure(ErrorCodes.AssignmentNotAvailable);
        }

        // Sanitize and deduplicate draft answers: only save questions belonging to this assignment
        var validQuestionIds = await _dbContext.AssignmentQuestions
            .Where(aq => aq.CenterId == centerId && aq.AssignmentId == assignmentId)
            .Select(aq => (long)aq.QuestionId)
            .ToHashSetAsync(cancellationToken);

        var sanitizedAnswers = (request.Answers ?? new())
            .Where(a => validQuestionIds.Contains(a.QuestionId))
            .GroupBy(a => a.QuestionId)
            .Select(g => g.Last())
            .ToList();

        // Check draft version to prevent stale out-of-order snapshots from overwriting newer drafts
        var (currentAnswers, currentVersion) = DraftAnswersHelper.ParseDraft(progress.DraftAnswersJson);

        // Rule 1: Request lacking version must not silently overwrite an already versioned draft
        if (!request.DraftVersion.HasValue && currentVersion > 0)
        {
            return SaveAssignmentDraftResult.Failure(ErrorCodes.ConcurrencyConflict, currentVersion);
        }

        // Rule 2: Older version than current server version
        if (request.DraftVersion.HasValue && request.DraftVersion.Value < currentVersion)
        {
            if (AreDraftAnswersEquivalent(currentAnswers, sanitizedAnswers))
            {
                return SaveAssignmentDraftResult.Success(currentVersion);
            }
            return SaveAssignmentDraftResult.Failure(ErrorCodes.ConcurrencyConflict, currentVersion);
        }

        // Rule 3: Equal version to current server version: only idempotent if same payload, otherwise conflict
        if (request.DraftVersion.HasValue && request.DraftVersion.Value == currentVersion)
        {
            if (AreDraftAnswersEquivalent(currentAnswers, sanitizedAnswers))
            {
                return SaveAssignmentDraftResult.Success(currentVersion);
            }
            return SaveAssignmentDraftResult.Failure(ErrorCodes.ConcurrencyConflict, currentVersion);
        }

        var newVersion = request.DraftVersion ?? (currentVersion + 1);
        progress.DraftAnswersJson = DraftAnswersHelper.SerializeDraft(newVersion, sanitizedAnswers);
        progress.DraftSavedAt = utcNow;
        progress.UpdatedAt = utcNow;
        progress.UpdatedBy = currentUserId;
        progress.RowVersion++;

        if (progress.Status == ProgressStatus.NotStarted)
        {
            progress.Status = ProgressStatus.InProgress;
        }

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            // Reload and check if already saved with equal or newer version
            _dbContext.ChangeTracker.Clear();
            var reloaded = await _dbContext.StudentAssignmentProgresses
                .FirstOrDefaultAsync(p => p.CenterId == centerId && p.AssignmentId == assignmentId && p.StudentId == currentUserId, CancellationToken.None);

            if (reloaded != null)
            {
                var (reloadedAnswers, reloadedVersion) = DraftAnswersHelper.ParseDraft(reloaded.DraftAnswersJson);
                if (request.DraftVersion.HasValue)
                {
                    if (request.DraftVersion.Value <= reloadedVersion)
                    {
                        if (AreDraftAnswersEquivalent(reloadedAnswers, sanitizedAnswers))
                        {
                            return SaveAssignmentDraftResult.Success(reloadedVersion);
                        }
                        return SaveAssignmentDraftResult.Failure(ErrorCodes.ConcurrencyConflict, reloadedVersion);
                    }
                }
                else if (reloadedVersion > 0)
                {
                    return SaveAssignmentDraftResult.Failure(ErrorCodes.ConcurrencyConflict, reloadedVersion);
                }

                // If our request was actually strictly newer, retry save once
                reloaded.DraftAnswersJson = DraftAnswersHelper.SerializeDraft(newVersion, sanitizedAnswers);
                reloaded.DraftSavedAt = utcNow;
                reloaded.UpdatedAt = utcNow;
                reloaded.UpdatedBy = currentUserId;
                reloaded.RowVersion++;
                await _dbContext.SaveChangesAsync(CancellationToken.None);
            }
        }

        return SaveAssignmentDraftResult.Success(newVersion);
    }

    private static bool AreDraftAnswersEquivalent(
        System.Collections.Generic.List<AssignmentDraftAnswerItemDto> a,
        System.Collections.Generic.List<AssignmentDraftAnswerItemDto> b)
    {
        if (a.Count != b.Count) return false;
        var dictA = a.ToDictionary(x => x.QuestionId);
        foreach (var itemB in b)
        {
            if (!dictA.TryGetValue(itemB.QuestionId, out var itemA)) return false;
            if (!string.Equals(itemA.FinalAnswer?.Trim() ?? "", itemB.FinalAnswer?.Trim() ?? "", StringComparison.Ordinal)) return false;
            if (!string.Equals(itemA.AnswerDisplayLatex?.Trim() ?? "", itemB.AnswerDisplayLatex?.Trim() ?? "", StringComparison.Ordinal)) return false;
            if (!string.Equals(itemA.ReasoningText?.Trim() ?? "", itemB.ReasoningText?.Trim() ?? "", StringComparison.Ordinal)) return false;
            if (!string.Equals(itemA.DrawingUploadToken?.Trim() ?? "", itemB.DrawingUploadToken?.Trim() ?? "", StringComparison.Ordinal)) return false;
            if (itemA.Confidence != itemB.Confidence) return false;
            if (itemA.TimeSpentSeconds != itemB.TimeSpentSeconds) return false;
            if (itemA.AnswerChanges != itemB.AnswerChanges) return false;
        }
        return true;
    }
}
