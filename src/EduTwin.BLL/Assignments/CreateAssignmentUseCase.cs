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

public class CreateAssignmentUseCase : ICreateAssignmentUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public CreateAssignmentUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
    }

    public async Task<CreateAssignmentResult> ExecuteAsync(
        CreateAssignmentRequest request,
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
            return CreateAssignmentResult.Failure(ErrorCodes.ResourceNotFound);
        }

        var centerId = _tenantContext.CenterId.Value;
        var actorId = _tenantContext.UserId.Value;
        var isTeacher = string.Equals(_tenantContext.Role, nameof(UserRole.Teacher), StringComparison.Ordinal);

        // 2. Validate basic request fields
        if (request.ClassId == Guid.Empty)
            return CreateAssignmentResult.Failure(ErrorCodes.ValidationFailed);

        if (string.IsNullOrWhiteSpace(request.Title) || request.Title.Length > 250)
            return CreateAssignmentResult.Failure(ErrorCodes.ValidationFailed);

        if (request.QuestionIds == null)
            return CreateAssignmentResult.Failure(ErrorCodes.ValidationFailed);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        if (request.DueAt.HasValue && request.DueAt.Value <= now)
            return CreateAssignmentResult.Failure(ErrorCodes.ValidationFailed);

        // TargetMode validation
        var targetMode = request.TargetMode?.Trim();
        var isWholeClass = string.Equals(targetMode, "WholeClass", StringComparison.Ordinal);
        var isSelectedStudents = string.Equals(targetMode, "SelectedStudents", StringComparison.Ordinal);

        if (!isWholeClass && !isSelectedStudents)
            return CreateAssignmentResult.Failure(ErrorCodes.ValidationFailed);

        if (isSelectedStudents && (request.StudentIds == null || request.StudentIds.Count == 0))
            return CreateAssignmentResult.Failure(ErrorCodes.ValidationFailed);

        // 3. Parse and validate questionIds
        var parsedQuestionIds = new List<ulong>();
        var seenQuestionIds = new HashSet<ulong>();

        foreach (var qIdStr in request.QuestionIds)
        {
            if (string.IsNullOrEmpty(qIdStr))
                return CreateAssignmentResult.Failure(ErrorCodes.ValidationFailed);

            foreach (var ch in qIdStr)
            {
                if (ch < '0' || ch > '9')
                    return CreateAssignmentResult.Failure(ErrorCodes.ValidationFailed);
            }

            if (!ulong.TryParse(qIdStr, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedQId) || parsedQId == 0)
                return CreateAssignmentResult.Failure(ErrorCodes.ValidationFailed);

            if (!seenQuestionIds.Add(parsedQId))
                return CreateAssignmentResult.Failure(ErrorCodes.ValidationFailed);

            parsedQuestionIds.Add(parsedQId);
        }

        // 4. Parse studentIds if SelectedStudents
        var parsedStudentIds = new List<Guid>();
        if (isSelectedStudents)
        {
            var seenStudentIds = new HashSet<Guid>();
            foreach (var sIdStr in request.StudentIds!)
            {
                if (!Guid.TryParse(sIdStr, out var parsedSId) || parsedSId == Guid.Empty)
                    return CreateAssignmentResult.Failure(ErrorCodes.ValidationFailed);

                if (!seenStudentIds.Add(parsedSId))
                    return CreateAssignmentResult.Failure(ErrorCodes.ValidationFailed);

                parsedStudentIds.Add(parsedSId);
            }
        }

        // 5. Load and verify Class ownership
        var classEntity = await _dbContext.Classes
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ClassId == request.ClassId, cancellationToken);

        if (classEntity == null)
            return CreateAssignmentResult.Failure(ErrorCodes.ResourceNotFound);

        // Teacher ownership: Teacher chỉ được tạo Assignment cho Class của mình
        if (isTeacher && classEntity.TeacherId != actorId)
            return CreateAssignmentResult.Failure(ErrorCodes.ResourceNotFound);

        // Class phải Active
        if (classEntity.Status != ClassStatus.Active)
            return CreateAssignmentResult.Failure(ErrorCodes.ResourceNotFound);

        var classSubjectId = classEntity.SubjectId;

        // 6. Validate Questions: Active + cùng SubjectId với Class
        if (parsedQuestionIds.Count > 0)
        {
            var dbQuestions = await _dbContext.Questions
                .AsNoTracking()
                .Where(q => parsedQuestionIds.Contains(q.QuestionId))
                .Select(q => new { q.QuestionId, q.SubjectId, q.Status })
                .ToListAsync(cancellationToken);

            if (dbQuestions.Count != parsedQuestionIds.Count)
                return CreateAssignmentResult.Failure(ErrorCodes.ResourceNotFound);

            foreach (var q in dbQuestions)
            {
                // Must be Active
                if (q.Status != EduTwin.Contracts.CurriculumAndQuestions.QuestionStatus.Active)
                    return CreateAssignmentResult.Failure(ErrorCodes.ValidationFailed);

                // Must belong to same subject as Class
                if (q.SubjectId != classSubjectId)
                    return CreateAssignmentResult.Failure(ErrorCodes.ValidationFailed);
            }
        }

        // 7. Validate StudentIds membership (SelectedStudents mode)
        if (isSelectedStudents && parsedStudentIds.Count > 0)
        {
            // MySQL stores GUIDs as varchar(36). Its EF provider cannot reliably
            // translate Contains(List<Guid>) for that mapping, so keep the bounded
            // class-membership query server-side and compare the requested IDs in memory.
            var activeMemberIds = await _dbContext.ClassStudents
                .AsNoTracking()
                .Where(cs => cs.ClassId == classEntity.ClassId &&
                             cs.Status == ClassStudentStatus.Active)
                .Select(cs => cs.StudentId)
                .ToListAsync(cancellationToken);

            var activeMemberIdSet = activeMemberIds.ToHashSet();
            if (parsedStudentIds.Any(studentId => !activeMemberIdSet.Contains(studentId)))
                return CreateAssignmentResult.Failure(ErrorCodes.ValidationFailed);
        }

        // 8. Atomic persistence
        var assignmentId = Guid.NewGuid();

        var assignment = new Assignment
        {
            AssignmentId = assignmentId,
            CenterId = centerId,
            ClassId = classEntity.ClassId,
            CreatedByTeacherId = isTeacher ? actorId : classEntity.TeacherId,
            Title = request.Title,
            Instructions = request.Instructions,
            DueAt = request.DueAt,
            Status = AssignmentStatus.Draft,
            PublishedAt = null,
            IsDeleted = false,
            RowVersion = 1,
            CreatedAt = now,
            UpdatedAt = now,
            CreatedBy = actorId,
            UpdatedBy = actorId
        };

        var assignmentQuestions = new List<AssignmentQuestion>();
        for (var i = 0; i < parsedQuestionIds.Count; i++)
        {
            assignmentQuestions.Add(new AssignmentQuestion
            {
                CenterId = centerId,
                AssignmentId = assignmentId,
                QuestionId = parsedQuestionIds[i],
                OrderIndex = (uint)(i + 1),
                Points = 1m,
                CreatedAt = now
            });
        }

        // Draft AssignmentTargets: chỉ lưu khi SelectedStudents.
        // WholeClass không lưu (Publish sẽ snapshot active members tại publish time).
        var draftTargets = new List<AssignmentTarget>();
        if (isSelectedStudents)
        {
            foreach (var sid in parsedStudentIds)
            {
                draftTargets.Add(new AssignmentTarget
                {
                    CenterId = centerId,
                    AssignmentId = assignmentId,
                    StudentId = sid,
                    TargetSource = EduTwin.Contracts.Assignments.TargetSource.SelectedStudents,
                    CreatedAt = now,
                    CreatedBy = actorId
                });
            }
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            _dbContext.Assignments.Add(assignment);
            if (assignmentQuestions.Count > 0)
                _dbContext.AssignmentQuestions.AddRange(assignmentQuestions);
            if (draftTargets.Count > 0)
                _dbContext.AssignmentTargets.AddRange(draftTargets);

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        // 9. Build response DTO
        var questionDtos = assignmentQuestions
            .OrderBy(aq => aq.OrderIndex)
            .Select(aq => new AssignmentQuestionDto
            {
                QuestionId = aq.QuestionId.ToString(CultureInfo.InvariantCulture),
                OrderIndex = aq.OrderIndex,
                Points = aq.Points
            })
            .ToList();

        var targetDtos = draftTargets
            .Select(t => new AssignmentTargetDto
            {
                StudentId = t.StudentId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(),
                TargetSource = t.TargetSource.ToString()
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

        return CreateAssignmentResult.Success(dto);
    }
}
