using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Recommendations.UseCases;

public interface IGetStudentLearningPathPreferenceUseCase
{
    Task<StudentLearningPathPreferenceDto?> ExecuteAsync(Guid subjectId, CancellationToken cancellationToken);
}

public sealed class GetStudentLearningPathPreferenceUseCase : IGetStudentLearningPathPreferenceUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public GetStudentLearningPathPreferenceUseCase(EduTwinDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
    }

    public async Task<StudentLearningPathPreferenceDto?> ExecuteAsync(Guid subjectId, CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsResolved || !_tenantContext.CenterId.HasValue || !_tenantContext.UserId.HasValue)
        {
            throw new InvalidOperationException("Tenant context is not resolved.");
        }

        var centerId = _tenantContext.CenterId.Value;
        var studentId = _tenantContext.UserId.Value;

        var pref = await _dbContext.StudentLearningPathPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.CenterId == centerId && p.StudentId == studentId && p.SubjectId == subjectId && !p.IsDeleted, cancellationToken);

        if (pref is null)
        {
            return null;
        }

        List<ulong> weakIds = new();
        List<ulong> focusIds = new();

        try
        {
            if (pref.WeakTopicNodeIds != null)
            {
                weakIds = JsonSerializer.Deserialize<List<ulong>>(pref.WeakTopicNodeIds.RootElement.GetRawText()) ?? new();
            }
            if (pref.FocusTopicNodeIds != null)
            {
                focusIds = JsonSerializer.Deserialize<List<ulong>>(pref.FocusTopicNodeIds.RootElement.GetRawText()) ?? new();
            }
        }
        catch
        {
            // fallback if json parsing fails
        }

        return new StudentLearningPathPreferenceDto
        {
            SubjectId = pref.SubjectId,
            SelfAssessedLevel = pref.SelfAssessedLevel,
            WeakTopicNodeIds = weakIds,
            FocusTopicNodeIds = focusIds,
            GoalType = pref.GoalType,
            TargetMastery = pref.TargetMastery,
            TargetWeeks = pref.TargetWeeks,
            MinutesPerDay = pref.MinutesPerDay,
            DaysPerWeek = pref.DaysPerWeek,
            Pace = pref.Pace,
            PreferredMode = pref.PreferredMode,
            Note = pref.Note,
            UpdatedAt = pref.UpdatedAt
        };
    }
}
