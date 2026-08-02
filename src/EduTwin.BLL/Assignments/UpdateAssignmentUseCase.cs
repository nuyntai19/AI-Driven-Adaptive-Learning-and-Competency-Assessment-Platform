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
using EduTwin.Contracts.Organization;
using EduTwin.DAL.Assignments;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Assignments;

public class UpdateAssignmentUseCase : IUpdateAssignmentUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public UpdateAssignmentUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
    }

    public async Task<UpdateAssignmentResult> ExecuteAsync(
        Guid assignmentId,
        UpdateAssignmentRequest request,
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
            return UpdateAssignmentResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (assignmentId == Guid.Empty)
            return UpdateAssignmentResult.Failure(ErrorCodes.ResourceNotFound);

        var actorId = _tenantContext.UserId.Value;
        var isTeacher = string.Equals(_tenantContext.Role, nameof(UserRole.Teacher), StringComparison.Ordinal);

        // 2. Validate rowVersion format (strict: ASCII digits, > 0)
        if (string.IsNullOrWhiteSpace(request.RowVersion))
            return UpdateAssignmentResult.Failure(ErrorCodes.ValidationFailed);

        foreach (var ch in request.RowVersion)
        {
            if (ch < '0' || ch > '9')
                return UpdateAssignmentResult.Failure(ErrorCodes.ValidationFailed);
        }

        if (!ulong.TryParse(request.RowVersion, NumberStyles.None, CultureInfo.InvariantCulture, out var clientRowVersion) || clientRowVersion == 0)
            return UpdateAssignmentResult.Failure(ErrorCodes.ValidationFailed);

        // 3. Load Assignment (Global Query Filter: centerId + !isDeleted)
        var assignment = await _dbContext.Assignments
            .FirstOrDefaultAsync(a => a.AssignmentId == assignmentId, cancellationToken);

        if (assignment == null)
            return UpdateAssignmentResult.Failure(ErrorCodes.ResourceNotFound);

        // 4. Ownership guard
        if (isTeacher)
        {
            var classEntity = await _dbContext.Classes
                .AsNoTracking()
                .Select(c => new { c.ClassId, c.TeacherId })
                .FirstOrDefaultAsync(c => c.ClassId == assignment.ClassId, cancellationToken);

            if (classEntity == null || classEntity.TeacherId != actorId)
                return UpdateAssignmentResult.Failure(ErrorCodes.ResourceNotFound);
        }

        // 5. State machine: only Draft allowed
        if (assignment.Status != AssignmentStatus.Draft)
            return UpdateAssignmentResult.Failure(ErrorCodes.InvalidStateTransition);

        // 6. rowVersion concurrency check
        if (assignment.RowVersion != clientRowVersion)
            return UpdateAssignmentResult.Failure(ErrorCodes.ConcurrencyConflict);

        // 7. Validate Title if provided
        if (request.Title != null)
        {
            if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 250)
                return UpdateAssignmentResult.Failure(ErrorCodes.ValidationFailed);
        }

        // 8. Validate and parse new questionIds if provided
        List<ulong>? newParsedQuestionIds = null;
        if (request.QuestionIds != null)
        {
            newParsedQuestionIds = new List<ulong>();
            var seenIds = new HashSet<ulong>();

            foreach (var qIdStr in request.QuestionIds)
            {
                if (string.IsNullOrEmpty(qIdStr))
                    return UpdateAssignmentResult.Failure(ErrorCodes.ValidationFailed);

                foreach (var ch in qIdStr)
                {
                    if (ch < '0' || ch > '9')
                        return UpdateAssignmentResult.Failure(ErrorCodes.ValidationFailed);
                }

                if (!ulong.TryParse(qIdStr, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedQId) || parsedQId == 0)
                    return UpdateAssignmentResult.Failure(ErrorCodes.ValidationFailed);

                if (!seenIds.Add(parsedQId))
                    return UpdateAssignmentResult.Failure(ErrorCodes.ValidationFailed);

                newParsedQuestionIds.Add(parsedQId);
            }

            // Validate each question: Active + same Subject as Class
            if (newParsedQuestionIds.Count > 0)
            {
                var classEntity = await _dbContext.Classes
                    .AsNoTracking()
                    .Select(c => new { c.ClassId, c.SubjectId })
                    .FirstOrDefaultAsync(c => c.ClassId == assignment.ClassId, cancellationToken);

                if (classEntity == null)
                    return UpdateAssignmentResult.Failure(ErrorCodes.ResourceNotFound);

                var dbQuestions = await _dbContext.Questions
                    .AsNoTracking()
                    .Where(q => newParsedQuestionIds.Contains(q.QuestionId))
                    .Select(q => new { q.QuestionId, q.SubjectId, q.Status })
                    .ToListAsync(cancellationToken);

                if (dbQuestions.Count != newParsedQuestionIds.Count)
                    return UpdateAssignmentResult.Failure(ErrorCodes.ResourceNotFound);

                foreach (var q in dbQuestions)
                {
                    if (q.Status != EduTwin.Contracts.CurriculumAndQuestions.QuestionStatus.Active)
                        return UpdateAssignmentResult.Failure(ErrorCodes.ValidationFailed);

                    if (q.SubjectId != classEntity.SubjectId)
                        return UpdateAssignmentResult.Failure(ErrorCodes.ValidationFailed);
                }
            }
        }

        // 9. Validate TargetMode and studentIds if provided
        List<Guid>? newParsedStudentIds = null;
        string? newTargetMode = null;
        if (request.TargetMode != null)
        {
            var targetMode = request.TargetMode.Trim();
            var isWholeClass = string.Equals(targetMode, "WholeClass", StringComparison.Ordinal);
            var isSelectedStudents = string.Equals(targetMode, "SelectedStudents", StringComparison.Ordinal);

            if (!isWholeClass && !isSelectedStudents)
                return UpdateAssignmentResult.Failure(ErrorCodes.ValidationFailed);

            newTargetMode = targetMode;

            if (isSelectedStudents && request.StudentIds != null)
            {
                newParsedStudentIds = new List<Guid>();
                var seenStudents = new HashSet<Guid>();

                foreach (var sIdStr in request.StudentIds)
                {
                    if (!Guid.TryParse(sIdStr, out var parsedSId) || parsedSId == Guid.Empty)
                        return UpdateAssignmentResult.Failure(ErrorCodes.ValidationFailed);

                    if (!seenStudents.Add(parsedSId))
                        return UpdateAssignmentResult.Failure(ErrorCodes.ValidationFailed);

                    newParsedStudentIds.Add(parsedSId);
                }
            }
        }

        // 10. Apply updates and persist atomically
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        if (request.Title != null)
            assignment.Title = request.Title;

        if (request.Instructions != null)
            assignment.Instructions = request.Instructions;

        // Allow clearing DueAt by sending null explicitly — we use a sentinel here.
        // If the caller wants to clear DueAt, they'd send null; we accept it.
        assignment.DueAt = request.DueAt;
        assignment.UpdatedAt = now;
        assignment.UpdatedBy = actorId;
        // RowVersion incremented by DbContext.UpdateRowVersions()

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // Replace AssignmentQuestions atomically if provided
            List<AssignmentQuestion> finalQuestions;
            if (newParsedQuestionIds != null)
            {
                var existingAQs = await _dbContext.AssignmentQuestions
                    .Where(aq => aq.AssignmentId == assignmentId)
                    .ToListAsync(cancellationToken);

                _dbContext.AssignmentQuestions.RemoveRange(existingAQs);

                finalQuestions = new List<AssignmentQuestion>();
                for (var i = 0; i < newParsedQuestionIds.Count; i++)
                {
                    finalQuestions.Add(new AssignmentQuestion
                    {
                        CenterId = assignment.CenterId,
                        AssignmentId = assignmentId,
                        QuestionId = newParsedQuestionIds[i],
                        OrderIndex = (uint)(i + 1),
                        Points = 1m,
                        CreatedAt = now
                    });
                }

                _dbContext.AssignmentQuestions.AddRange(finalQuestions);
            }
            else
            {
                // Load existing for response DTO
                finalQuestions = await _dbContext.AssignmentQuestions
                    .Where(aq => aq.AssignmentId == assignmentId)
                    .OrderBy(aq => aq.OrderIndex)
                    .ToListAsync(cancellationToken);
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            // Build response DTO
            var questionDtos = finalQuestions
                .OrderBy(aq => aq.OrderIndex)
                .Select(aq => new AssignmentQuestionDto
                {
                    QuestionId = aq.QuestionId.ToString(CultureInfo.InvariantCulture),
                    OrderIndex = aq.OrderIndex,
                    Points = aq.Points
                })
                .ToList();

            var targetDtos = await _dbContext.AssignmentTargets
                .AsNoTracking()
                .Where(at => at.AssignmentId == assignmentId)
                .Select(at => new AssignmentTargetDto
                {
                    StudentId = at.StudentId.ToString(),
                    TargetSource = at.TargetSource.ToString()
                })
                .ToListAsync(cancellationToken);

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

            return UpdateAssignmentResult.Success(dto);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
