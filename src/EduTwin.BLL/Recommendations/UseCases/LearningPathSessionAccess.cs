using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Organization;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Recommendations;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Recommendations.UseCases;

public sealed class LearningPathUnavailableException(string message) : InvalidOperationException(message);
internal sealed record AccessibleLearningSession(LearningPath Path, List<LearningPathPhaseDto> Phases, LearningPathSessionDto Session);

internal static class LearningPathSessionAccess
{
    internal static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    internal static async Task<AccessibleLearningSession?> LoadAsync(EduTwinDbContext db, ITenantContext tenant,
        Guid subjectId, string sessionId, Guid? classId, bool history, CancellationToken ct)
    {
        if (!tenant.IsResolved || tenant.CenterId is not { } center || tenant.UserId is not { } student ||
            subjectId == Guid.Empty || string.IsNullOrWhiteSpace(sessionId)) return null;
        var scope = await StudentLearningScope.ResolveAsync(db, center, student, subjectId, classId, history, ct);
        if (!scope.Allowed) return null;
        var query = db.LearningPaths.Where(p => p.CenterId == center && p.StudentId == student && p.SubjectId == subjectId && !p.IsDeleted);
        // Recommendations can be replaced after an attempt. Keep a started
        // session attached to its durable path, never to the replacement.
        if (sessionId.Length >= 36 && Guid.TryParse(sessionId[..36], out var pathId)) query = query.Where(p => p.LearningPathId == pathId);
        else query = query.Where(p => p.Status == LearningPathStatus.Active);
        var path = await query.OrderByDescending(p => p.GeneratedAt).FirstOrDefaultAsync(ct);
        if (path?.PlanJson is null) return null;
        var phases = JsonSerializer.Deserialize<List<LearningPathPhaseDto>>(path.PlanJson.RootElement.GetRawText(), JsonOptions) ?? [];
        var session = phases.SelectMany(p => p.Weeks).SelectMany(w => w.Sessions).SingleOrDefault(s => s.SessionId == sessionId);
        if (session is null || (session.ClassId.HasValue && session.ClassId != classId) ||
            !ulong.TryParse(session.TopicNodeId, out var topic) || !scope.Includes(topic)) return null;
        // Free practice has no teacher review boundary. Legacy schedules may
        // contain manual questions; remove those instead of widening permissions.
        var manualIds = await db.Questions.AsNoTracking().Where(q => q.CenterId == center && q.SubjectId == subjectId &&
            (q.QuestionType == EduTwin.Contracts.CurriculumAndQuestions.QuestionType.Essay ||
             q.AnswerEvaluationMode == EduTwin.Contracts.CurriculumAndQuestions.QuestionAnswerEvaluationMode.Manual))
            .Select(q => q.QuestionId).ToListAsync(ct);
        var manualKeys = manualIds.Select(id => id.ToString()).ToHashSet();
        session.RecommendedQuestionIds = session.RecommendedQuestionIds.Where(id => !manualKeys.Contains(id)).ToList();
        session.RowVersion = path.RowVersion.ToString(System.Globalization.CultureInfo.InvariantCulture);
        return new(path, phases, session);
    }
}
