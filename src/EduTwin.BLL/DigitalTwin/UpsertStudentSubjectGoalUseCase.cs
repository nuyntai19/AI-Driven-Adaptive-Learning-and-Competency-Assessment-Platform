using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL;
using EduTwin.DAL.DigitalTwin;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.DigitalTwin;

public class UpsertStudentSubjectGoalUseCase : IUpsertStudentSubjectGoalUseCase
{
    private readonly ITenantContext _tenantContext;
    private readonly IStudentOwnershipGuard _ownershipGuard;
    private readonly EduTwinDbContext _dbContext;
    private readonly IGoalIdGenerator _goalIdGenerator;
    private readonly TimeProvider _timeProvider;

    public UpsertStudentSubjectGoalUseCase(
        ITenantContext tenantContext,
        IStudentOwnershipGuard ownershipGuard,
        EduTwinDbContext dbContext,
        IGoalIdGenerator goalIdGenerator,
        TimeProvider timeProvider)
    {
        _tenantContext = tenantContext;
        _ownershipGuard = ownershipGuard;
        _dbContext = dbContext;
        _goalIdGenerator = goalIdGenerator;
        _timeProvider = timeProvider;
    }

    public async Task<UpsertStudentSubjectGoalResult> ExecuteAsync(Guid studentId, Guid subjectId, UpsertStudentSubjectGoalRequest request, CancellationToken cancellationToken)
    {
        if (studentId == Guid.Empty || subjectId == Guid.Empty)
            return UpsertStudentSubjectGoalResult.NotFound();

        if (!_tenantContext.IsResolved ||
            _tenantContext.UserId == null || _tenantContext.UserId == Guid.Empty ||
            _tenantContext.CenterId == null || _tenantContext.CenterId == Guid.Empty)
            return UpsertStudentSubjectGoalResult.NotFound();

        if (string.IsNullOrWhiteSpace(_tenantContext.Role))
            return UpsertStudentSubjectGoalResult.NotFound();

        var role = _tenantContext.Role;
        if (!string.Equals(role, UserRole.Student.ToString(), StringComparison.Ordinal) &&
            !string.Equals(role, UserRole.Teacher.ToString(), StringComparison.Ordinal) &&
            !string.Equals(role, UserRole.CenterManager.ToString(), StringComparison.Ordinal))
            return UpsertStudentSubjectGoalResult.NotFound();

        var centerId = _tenantContext.CenterId.Value;
        var userId = _tenantContext.UserId.Value;

        var centerExists = await _dbContext.Centers.AnyAsync(c => c.CenterId == centerId && !c.IsDeleted && c.Status == CenterStatus.Active, cancellationToken);
        if (!centerExists)
            return UpsertStudentSubjectGoalResult.NotFound();

        int[] bits = decimal.GetBits(request.TargetScore);
        int scale = (bits[3] >> 16) & 31;
        if (request.TargetScore < 0m || request.TargetScore > 10m || scale > 2)
            return UpsertStudentSubjectGoalResult.ValidationFailed();

        if (request.RemainingDays < 0 || request.RemainingDays > 3650)
            return UpsertStudentSubjectGoalResult.ValidationFailed();

        ulong? parsedRowVersion = null;
        if (request.RowVersion != null)
        {
            if (request.RowVersion.Length == 0 || !request.RowVersion.All(char.IsAsciiDigit))
                return UpsertStudentSubjectGoalResult.ValidationFailed();

            if (!ulong.TryParse(request.RowVersion, NumberStyles.None, CultureInfo.InvariantCulture, out var version) || version == 0)
                return UpsertStudentSubjectGoalResult.ValidationFailed();

            parsedRowVersion = version;
        }

        var decision = await _ownershipGuard.CheckStudentAccessAsync(studentId, cancellationToken);
        switch (decision)
        {
            case OwnershipDecision.Allowed:
                break;
            case OwnershipDecision.Forbidden:
                return UpsertStudentSubjectGoalResult.Forbidden();
            case OwnershipDecision.NotFound:
            default:
                return UpsertStudentSubjectGoalResult.NotFound();
        }

        var studentValid = await _dbContext.Students
            .Include(s => s.User)
            .AnyAsync(s => s.StudentId == studentId && s.CenterId == centerId && !s.IsDeleted
                        && s.User.RoleName == UserRole.Student && !s.User.IsDeleted && s.User.Status == UserStatus.Active, cancellationToken);
        if (!studentValid)
            return UpsertStudentSubjectGoalResult.NotFound();

        var subjectValid = await _dbContext.Subjects
            .AnyAsync(s => s.SubjectId == subjectId && s.CenterId == centerId && !s.IsDeleted && s.IsActive, cancellationToken);
        if (!subjectValid)
            return UpsertStudentSubjectGoalResult.NotFound();

        var goal = await _dbContext.StudentSubjectGoals
            .FirstOrDefaultAsync(g => g.CenterId == centerId && g.StudentId == studentId && g.SubjectId == subjectId, cancellationToken);

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        if (goal == null)
        {
            if (parsedRowVersion != null)
                return UpsertStudentSubjectGoalResult.ValidationFailed();

            var risk = StudentSubjectGoalRiskCalculator.CalculateRisk(request.TargetScore, 0m, request.RemainingDays);

            int attempts = 0;
            const int MaxAttempts = 3;
            bool success = false;

            while (attempts < MaxAttempts)
            {
                cancellationToken.ThrowIfCancellationRequested();

                attempts++;
                ulong goalId = _goalIdGenerator.GenerateId();
                if (goalId == 0)
                {
                    continue;
                }

                goal = new StudentSubjectGoal
                {
                    GoalId = goalId,
                    CenterId = centerId,
                    StudentId = studentId,
                    SubjectId = subjectId,
                    TargetScore = request.TargetScore,
                    RemainingDays = (uint)request.RemainingDays,
                    CurrentPredictedScore = 0m,
                    RiskScore = risk,
                    CreatedAt = now,
                    CreatedBy = userId,
                    UpdatedAt = now,
                    UpdatedBy = userId,
                    IsDeleted = false,
                    RowVersion = 1
                };

                _dbContext.StudentSubjectGoals.Add(goal);

                try
                {
                    await _dbContext.SaveChangesAsync(cancellationToken);
                    success = true;
                    break;
                }
                catch (DbUpdateException ex)
                {
                    _dbContext.Entry(goal).State = EntityState.Detached;

                    Exception? currentEx = ex;
                    bool isCompositeConstraint = false;
                    bool isPrimaryKeyCollision = false;

                    while (currentEx != null)
                    {
                        var msg = currentEx.Message ?? "";
                        if (msg.Contains("ux_student_subject_goals_center_id_student_id_subject_id", StringComparison.OrdinalIgnoreCase))
                        {
                            isCompositeConstraint = true;
                            break;
                        }
                        bool hasGenericPrimary = msg.Contains("for key 'PRIMARY'", StringComparison.OrdinalIgnoreCase);
                        bool hasTableName = msg.Contains("student_subject_goals", StringComparison.OrdinalIgnoreCase);
                        bool isStudentSubjectGoalPrimary = msg.Contains("student_subject_goals.PRIMARY", StringComparison.OrdinalIgnoreCase) || msg.Contains("`student_subject_goals`.`PRIMARY`", StringComparison.OrdinalIgnoreCase);

                        if (msg.Contains("pk_student_subject_goals", StringComparison.OrdinalIgnoreCase) ||
                            msg.Contains("PK_StudentSubjectGoals", StringComparison.OrdinalIgnoreCase) ||
                            isStudentSubjectGoalPrimary ||
                            (hasGenericPrimary && hasTableName))
                        {
                            isPrimaryKeyCollision = true;
                        }
                        currentEx = currentEx.InnerException;
                    }

                    if (isCompositeConstraint)
                        return UpsertStudentSubjectGoalResult.Conflict();

                    if (isPrimaryKeyCollision)
                    {
                        continue;
                    }

                    throw;
                }
            }

            if (!success)
            {
                throw new InvalidOperationException("Failed to generate a valid unique GoalId.");
            }
        }
        else
        {
            if (parsedRowVersion == null)
                return UpsertStudentSubjectGoalResult.ValidationFailed();

            if (goal.RowVersion != parsedRowVersion.Value)
                return UpsertStudentSubjectGoalResult.Conflict();

            var risk = StudentSubjectGoalRiskCalculator.CalculateRisk(request.TargetScore, goal.CurrentPredictedScore, request.RemainingDays);

            _dbContext.Entry(goal).Property(g => g.RowVersion).OriginalValue = parsedRowVersion.Value;

            goal.TargetScore = request.TargetScore;
            goal.RemainingDays = (uint)request.RemainingDays;
            goal.RiskScore = risk;
            goal.UpdatedAt = now;
            goal.UpdatedBy = userId;
            goal.RowVersion++;

            try
            {
                await _dbContext.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                _dbContext.ChangeTracker.Clear();
                return UpsertStudentSubjectGoalResult.Conflict();
            }
            catch (DbUpdateException ex)
            {
                _dbContext.ChangeTracker.Clear();
                Exception? currentEx = ex;
                bool isCompositeConstraint = false;
                while (currentEx != null)
                {
                    if (currentEx.Message.Contains("ux_student_subject_goals_center_id_student_id_subject_id", StringComparison.OrdinalIgnoreCase))
                    {
                        isCompositeConstraint = true;
                        break;
                    }
                    currentEx = currentEx.InnerException;
                }

                if (isCompositeConstraint)
                    return UpsertStudentSubjectGoalResult.Conflict();

                throw;
            }
        }

        var dto = new StudentSubjectGoalDto
        {
            GoalId = goal!.GoalId.ToString(CultureInfo.InvariantCulture),
            StudentId = goal.StudentId.ToString(),
            SubjectId = goal.SubjectId.ToString(),
            TargetScore = goal.TargetScore,
            RemainingDays = (int)goal.RemainingDays,
            CurrentPredictedScore = goal.CurrentPredictedScore,
            RiskScore = goal.RiskScore,
            RowVersion = goal.RowVersion.ToString(CultureInfo.InvariantCulture)
        };

        return UpsertStudentSubjectGoalResult.Success(dto);
    }
}
