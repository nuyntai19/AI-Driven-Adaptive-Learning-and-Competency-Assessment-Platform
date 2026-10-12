using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Recommendations.UseCases;

public interface IUpdateLearningPathSessionUseCase
{
    Task<bool> ExecuteAsync(Guid subjectId, string sessionId, UpdateLearningPathSessionRequest request, CancellationToken cancellationToken);
    Task<LearningPathSessionDto?> GetAsync(Guid subjectId, string sessionId, Guid? classId, bool history, CancellationToken ct)
        => Task.FromResult<LearningPathSessionDto?>(null);
}

public sealed class UpdateLearningPathSessionUseCase : IUpdateLearningPathSessionUseCase
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] AllowedStatuses = ["InProgress", "Completed", "NeedsReview", "Skipped", "Evaluate"];
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public UpdateLearningPathSessionUseCase(EduTwinDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<LearningPathSessionDto?> GetAsync(Guid subjectId, string sessionId, Guid? classId, bool history, CancellationToken ct)
        => (await LearningPathSessionAccess.LoadAsync(_dbContext, _tenantContext, subjectId, sessionId, classId, history, ct))?.Session;

    public async Task<bool> ExecuteAsync(Guid subjectId, string sessionId, UpdateLearningPathSessionRequest request, CancellationToken cancellationToken)
    {
        if (!AllowedStatuses.Contains(request.Status, StringComparer.Ordinal)) return false;
        var accessible = await LearningPathSessionAccess.LoadAsync(_dbContext, _tenantContext, subjectId, sessionId, request.ClassId, request.History, cancellationToken);
        if (accessible is null) return false;
        var (path, phases, session) = accessible;
        if (request.ExpectedRowVersion is not null && request.ExpectedRowVersion != session.RowVersion)
            throw new DbUpdateConcurrencyException("Learning path session version is stale.");
        if (session.Status == "Completed") return request.Status is "Completed" or "Evaluate";
        if (request.Status == "InProgress")
        {
            if (session.Status is not ("NotStarted" or "InProgress" or "NeedsReview" or "Skipped")) return false;
            if (session.Status == "InProgress") return true;
            session.StartedAtUtc = DateTime.UtcNow;
            session.Status = "InProgress";
        }
        else if (request.Status is "Completed" or "Evaluate")
        {
            if (session.StartedAtUtc is null || session.Status is not ("InProgress" or "NeedsReview")) return false;
            var questionIds = session.RecommendedQuestionIds.Select(ulong.Parse).Distinct().ToList();
            if (questionIds.Count == 0)
            {
                // Explicit acknowledgement for theory/offline activities is distinct
                // from evidence of correctly answering practice questions.
                if (!request.AcknowledgedActivities) return request.Status == "Evaluate";
                session.Status = "Completed";
            }
            else
            {
                var topicId = ulong.Parse(session.TopicNodeId);
                var activeCount = await _dbContext.Questions.CountAsync(q => q.CenterId == path.CenterId && q.SubjectId == subjectId &&
                    q.PrimaryTopicNodeId == topicId && questionIds.Contains(q.QuestionId) &&
                    q.Status == EduTwin.Contracts.CurriculumAndQuestions.QuestionStatus.Active && !q.IsDeleted, cancellationToken);
                if (activeCount != questionIds.Count) return false;
                var attempts = await _dbContext.Attempts.AsNoTracking().Where(a => a.CenterId == path.CenterId && a.StudentId == path.StudentId &&
                    a.AssignmentId == null && questionIds.Contains(a.QuestionId) && a.CreatedAt >= session.StartedAtUtc && !a.Skipped)
                    .OrderByDescending(a => a.CreatedAt).ThenByDescending(a => a.AttemptId)
                    .Select(a => new { a.QuestionId, IsCorrect = _dbContext.ReasoningAnalyses
                        .Where(r => r.CenterId == a.CenterId && r.AttemptId == a.AttemptId)
                        .Select(r => r.OverrideIsCorrect).FirstOrDefault() ?? a.IsCorrect }).ToListAsync(cancellationToken);
                var latest = attempts.GroupBy(a => a.QuestionId).Select(g => g.First()).ToList();
                if (latest.Count != questionIds.Count) return request.Status == "Evaluate";
                var passed = latest.All(a => a.IsCorrect.HasValue) &&
                    (decimal)latest.Count(a => a.IsCorrect == true) / questionIds.Count >= session.RequiredCorrectRate;
                if (!passed && request.Status == "Completed") return false;
                session.Status = passed ? "Completed" : "NeedsReview";
            }
        }
        else
        {
            if (session.Status == "NotStarted" && request.Status == "NeedsReview") return false;
            session.Status = request.Status;
        }
        foreach (var week in phases.SelectMany(p => p.Weeks))
        {
            var completed = week.Sessions.Count(s => s.Status == "Completed");
            week.ProgressPercentage = week.Sessions.Count == 0 ? 0 : Math.Round((decimal)completed / week.Sessions.Count * 100, 1);
            week.Checkpoint.CompletedTasks = completed;
        }
        foreach (var phase in phases)
        {
            var sessions = phase.Weeks.SelectMany(w => w.Sessions).ToList();
            phase.ProgressPercentage = sessions.Count == 0 ? 0 : Math.Round((decimal)sessions.Count(s => s.Status == "Completed") / sessions.Count * 100, 1);
        }
        // Items represent recommendation mastery, not self-acknowledged tasks.
        if (path.Status == LearningPathStatus.Active && phases.SelectMany(p => p.Weeks).SelectMany(w => w.Sessions).All(s => s.Status == "Completed"))
            path.Status = LearningPathStatus.Completed;
        path.PlanJson = JsonSerializer.SerializeToDocument(phases, JsonOptions);
        path.UpdatedAt = DateTime.UtcNow;
        path.UpdatedBy = path.StudentId;
        path.RowVersion++;
        await _dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}
