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

        if (_tenantContext.UserId == null || _tenantContext.CenterId == null)
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

        if (request.TargetScore < 0m || request.TargetScore > 10m || decimal.Round(request.TargetScore, 2) != request.TargetScore)
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
        if (decision == OwnershipDecision.NotFound) return UpsertStudentSubjectGoalResult.NotFound();
        if (decision == OwnershipDecision.Forbidden) return UpsertStudentSubjectGoalResult.Forbidden();

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

        if (goal == null)
        {
            if (parsedRowVersion != null)
                return UpsertStudentSubjectGoalResult.ValidationFailed();

            var goalId = _goalIdGenerator.GenerateId();
            var risk = StudentSubjectGoalRiskCalculator.CalculateRisk(request.TargetScore, 0m, request.RemainingDays);

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
                CreatedAt = _timeProvider.GetUtcNow().UtcDateTime,
                CreatedBy = userId,
                UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime,
                UpdatedBy = userId,
                IsDeleted = false,
                RowVersion = 1
            };

            _dbContext.StudentSubjectGoals.Add(goal);
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
            goal.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
            goal.UpdatedBy = userId;
            goal.RowVersion++;
        }

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
            var innerMessage = ex.InnerException?.Message ?? "";
            if (innerMessage.Contains("ux_student_subject_goals_center_id_student_id_subject_id", StringComparison.OrdinalIgnoreCase))
            {
                return UpsertStudentSubjectGoalResult.Conflict();
            }
            throw;
        }

        var dto = new StudentSubjectGoalDto
        {
            GoalId = goal.GoalId.ToString(CultureInfo.InvariantCulture),
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
