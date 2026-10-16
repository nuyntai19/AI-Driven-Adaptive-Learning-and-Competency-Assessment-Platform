using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Recommendations;
using EduTwin.DAL.CurriculumAndQuestions;

namespace EduTwin.BLL.Recommendations.Services;

public class LearningSessionExecutionService : ILearningSessionExecutionService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly EduTwinDbContext _dbContext;
    private readonly ILogger<LearningSessionExecutionService> _logger;
    private readonly TimeProvider _timeProvider;

    public LearningSessionExecutionService(
        EduTwinDbContext dbContext,
        ILogger<LearningSessionExecutionService> logger,
        TimeProvider? timeProvider = null,
        ILearningPathV4Evaluator? v4Evaluator = null)
    {
        _dbContext = dbContext;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
        _v4Evaluator = v4Evaluator;
    }
    private readonly ILearningPathV4Evaluator? _v4Evaluator;

    public async Task<IReadOnlyList<MaterializedSessionSummary>> MaterializeSessionsFromPlanAsync(
        Guid centerId,
        Guid learningPathId,
        CancellationToken ct = default)
    {
        var path = await _dbContext.LearningPaths
            .Include(p => p.Sessions)
            .FirstOrDefaultAsync(p => p.CenterId == centerId && p.LearningPathId == learningPathId, ct);

        if (path is null)
        {
            throw new KeyNotFoundException($"Learning path {learningPathId} not found in center {centerId}.");
        }

        // Idempotency check: if sessions already materialized, return them directly
        if (path.Sessions.Count > 0)
        {
            return path.Sessions
                .OrderBy(s => s.SessionOrder)
                .Select(MapToSummary)
                .ToList();
        }

        if (path.PlanJson is null)
        {
            throw new InvalidOperationException($"Learning path {learningPathId} does not have a PlanJson to materialize.");
        }

        var rawJson = path.PlanJson.RootElement.GetRawText();
        var phases = JsonSerializer.Deserialize<List<LearningPathPhaseDto>>(rawJson, JsonOptions);
        if (phases is null || phases.Count == 0)
        {
            throw new InvalidOperationException($"Learning path {learningPathId} PlanJson is empty or invalid.");
        }

        uint sessionOrder = 1;
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var candidateSessions = new List<LearningPathSession>();

        // Pre-query eligible active questions for snapshot verification
        var subjectQuestions = await _dbContext.Questions.AsNoTracking()
            .Where(q => q.CenterId == centerId && q.SubjectId == path.SubjectId &&
                        q.Status == Contracts.CurriculumAndQuestions.QuestionStatus.Active &&
                        q.QuestionType != Contracts.CurriculumAndQuestions.QuestionType.Essay &&
                        q.AnswerEvaluationMode != Contracts.CurriculumAndQuestions.QuestionAnswerEvaluationMode.Manual)
            .Select(q => new { q.QuestionId, q.PrimaryTopicNodeId })
            .ToListAsync(ct);

        var eligibleQuestionMap = subjectQuestions
            .GroupBy(q => q.PrimaryTopicNodeId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.QuestionId).ToHashSet());

        foreach (var phase in phases)
        {
            foreach (var week in phase.Weeks)
            {
                foreach (var sessionDto in week.Sessions)
                {
                    ulong topicId = ulong.TryParse(sessionDto.TopicNodeId, out var parsedTopic) ? parsedTopic : 0;
                    var scheduledIds = (sessionDto.RecommendedQuestionIds ?? [])
                        .Where(idStr => ulong.TryParse(idStr, out var qid) && qid > 0)
                        .Select(ulong.Parse)
                        .Distinct()
                        .ToList();

                    // Filter scheduled IDs against eligible active questions snapshot
                    eligibleQuestionMap.TryGetValue(topicId, out var activeForTopic);
                    activeForTopic ??= [];

                    var validScheduledIds = scheduledIds.Where(activeForTopic.Contains).ToList();

                    // Partition into disjoint practice and checkpoint sets
                    List<ulong> practiceIds;
                    List<ulong> checkpointIds;

                    if (validScheduledIds.Count > 1)
                    {
                        // Last question is checkpoint, earlier ones are guided practice
                        checkpointIds = [validScheduledIds.Last()];
                        practiceIds = validScheduledIds.Take(validScheduledIds.Count - 1).ToList();
                    }
                    else if (validScheduledIds.Count == 1)
                    {
                        practiceIds = [validScheduledIds[0]];
                        // For checkpoint, pick a different question from activeForTopic if available
                        var alternativeCheckpoint = activeForTopic.FirstOrDefault(id => id != validScheduledIds[0]);
                        checkpointIds = alternativeCheckpoint > 0 ? [alternativeCheckpoint] : [];
                    }
                    else
                    {
                        // Fallback: pick up to 2 distinct questions from active topic questions
                        var topicList = activeForTopic.Take(2).ToList();
                        if (topicList.Count > 1)
                        {
                            practiceIds = [topicList[0]];
                            checkpointIds = [topicList[1]];
                        }
                        else if (topicList.Count == 1)
                        {
                            practiceIds = [topicList[0]];
                            checkpointIds = [];
                        }
                        else
                        {
                            practiceIds = [];
                            checkpointIds = [];
                        }
                    }

                    var practiceDoc = JsonDocument.Parse(JsonSerializer.Serialize(practiceIds));
                    var checkpointDoc = JsonDocument.Parse(JsonSerializer.Serialize(checkpointIds));

                    var sessionEntity = new LearningPathSession
                    {
                        SessionId = Guid.NewGuid(),
                        CenterId = centerId,
                        LearningPathId = learningPathId,
                        LegacySessionId = sessionDto.SessionId,
                        TopicNodeId = topicId,
                        ClassId = sessionDto.ClassId,
                        PhaseNumber = (uint)Math.Max(1, phase.PhaseNumber),
                        WeekNumber = (uint)Math.Max(1, week.WeekNumber),
                        SessionOrder = sessionOrder++,
                        Title = string.IsNullOrWhiteSpace(sessionDto.Title) ? $"Session {week.WeekNumber}.{sessionOrder}" : sessionDto.Title,
                        Objective = string.IsNullOrWhiteSpace(sessionDto.Objective) ? (sessionDto.Title ?? "Session objective") : sessionDto.Objective,
                        Rationale = sessionDto.Type,
                        Status = ParseSessionStatus(sessionDto.Status),
                        PracticeQuestionIds = practiceDoc,
                        CheckpointQuestionIds = checkpointDoc,
                        MinimumQuestions = (uint)Math.Max(1, practiceIds.Count),
                        RequiredCorrectRate = sessionDto.RequiredCorrectRate > 0 ? sessionDto.RequiredCorrectRate : 0.70m,
                        PolicyVersion = "session-policy-v1",
                        CreatedAt = now,
                        UpdatedAt = now,
                        RowVersion = 1ul
                    };

                    candidateSessions.Add(sessionEntity);
                }
            }
        }

        // Execute transactional materialization with race protection
        var strategy = _dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            var isRelational = _dbContext.Database.IsRelational();
            Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction? transaction = null;
            if (isRelational)
            {
                transaction = await _dbContext.Database.BeginTransactionAsync(ct);
            }

            try
            {
                var existingCount = await _dbContext.LearningPathSessions
                    .CountAsync(s => s.CenterId == centerId && s.LearningPathId == learningPathId, ct);

                if (existingCount > 0)
                {
                    if (transaction != null) await transaction.RollbackAsync(ct);
                    return;
                }

                await _dbContext.LearningPathSessions.AddRangeAsync(candidateSessions, ct);
                await _dbContext.SaveChangesAsync(ct);
                if (transaction != null) await transaction.CommitAsync(ct);
            }
            catch (DbUpdateException ex)
            {
                if (transaction != null) await transaction.RollbackAsync(ct);
                _logger.LogWarning(ex, "MaterializeSessions race condition on learning path {LearningPathId}. Using existing persisted sessions.", learningPathId);
            }
            finally
            {
                if (transaction != null) await transaction.DisposeAsync();
            }
        });

        // Always reload from database to return the single persistent truth
        var persistedSessions = await _dbContext.LearningPathSessions
            .AsNoTracking()
            .Where(s => s.CenterId == centerId && s.LearningPathId == learningPathId)
            .OrderBy(s => s.SessionOrder)
            .ToListAsync(ct);

        return persistedSessions.Select(MapToSummary).ToList();
    }

    public async Task<StartSessionRunResult> StartOrResumeRunAsync(
        Guid centerId,
        Guid studentId,
        Guid sessionId,
        CancellationToken ct = default)
    {
        // 1. Check for existing active run (InProgress, Grading, or NeedsReview)
        var activeRun = await _dbContext.LearningPathSessionRuns
            .FirstOrDefaultAsync(r =>
                r.CenterId == centerId &&
                r.StudentId == studentId &&
                r.SessionId == sessionId &&
                (r.Status == SessionRunStatus.InProgress || r.Status == SessionRunStatus.Grading || r.Status == SessionRunStatus.NeedsReview),
                ct);

        if (activeRun is not null)
        {
            return new StartSessionRunResult(
                activeRun.SessionRunId,
                activeRun.SessionId,
                activeRun.StudentId,
                activeRun.RunNumber,
                activeRun.Status,
                activeRun.EvaluationStatus,
                activeRun.CurrentQuestionId,
                activeRun.CheckpointQuestionId,
                IsResumed: true);
        }

        // 2. Load session
        var session = await _dbContext.LearningPathSessions
            .FirstOrDefaultAsync(s => s.CenterId == centerId && s.SessionId == sessionId, ct);

        if (session is null)
        {
            throw new KeyNotFoundException($"Session {sessionId} not found in center {centerId}.");
        }

        if (session.Status == LearningPathSessionStatus.Completed)
        {
            throw new InvalidOperationException("Buổi học này đã hoàn thành, không thể tạo lượt học mới.");
        }

        // Query previously used checkpoint questions from attempts and previous session runs
        var previouslyUsedCheckpointIds = await (
            from a in _dbContext.Attempts.AsNoTracking()
            join r in _dbContext.LearningPathSessionRuns.AsNoTracking() on a.SessionRunId equals r.SessionRunId
            where a.CenterId == centerId && a.StudentId == studentId && r.SessionId == sessionId && r.CheckpointQuestionId != null
            select a.QuestionId
        ).Distinct().ToListAsync(ct);

        var runAssignedCheckpointIds = await _dbContext.LearningPathSessionRuns.AsNoTracking()
            .Where(r => r.CenterId == centerId && r.StudentId == studentId && r.SessionId == sessionId && r.CheckpointQuestionId != null)
            .Select(r => r.CheckpointQuestionId!.Value)
            .Distinct()
            .ToListAsync(ct);

        var usedCheckpointSet = previouslyUsedCheckpointIds.Concat(runAssignedCheckpointIds).ToHashSet();

        var practiceIds = ParseQuestionIds(session.PracticeQuestionIds);
        var checkpointPool = ParseQuestionIds(session.CheckpointQuestionIds);

        var unusedCandidateCheckpoints = checkpointPool.Where(id => !usedCheckpointSet.Contains(id)).ToList();

        var validActiveCheckpoints = unusedCandidateCheckpoints.Count > 0
            ? await _dbContext.Questions.AsNoTracking()
                .Where(q => q.CenterId == centerId &&
                            unusedCandidateCheckpoints.Contains(q.QuestionId) &&
                            q.Status == Contracts.CurriculumAndQuestions.QuestionStatus.Active &&
                            !q.IsDeleted &&
                            q.QuestionType != Contracts.CurriculumAndQuestions.QuestionType.Essay &&
                            q.AnswerEvaluationMode != Contracts.CurriculumAndQuestions.QuestionAnswerEvaluationMode.Manual)
                .Select(q => q.QuestionId)
                .ToListAsync(ct)
            : [];

        // Checkpoint exhaustion check: retry requires a new valid checkpoint
        if (checkpointPool.Count > 0 && validActiveCheckpoints.Count == 0)
        {
            throw new QuestionPoolDepletedException("Ngân hàng câu hỏi không đủ câu hỏi checkpoint mới cho lượt học lại của buổi học này.");
        }

        ulong? checkpointQuestionId = validActiveCheckpoints.Count > 0 ? validActiveCheckpoints[0] : null;

        var validActivePracticeIds = practiceIds.Count > 0
            ? await _dbContext.Questions.AsNoTracking()
                .Where(q => q.CenterId == centerId &&
                            practiceIds.Contains(q.QuestionId) &&
                            q.Status == Contracts.CurriculumAndQuestions.QuestionStatus.Active &&
                            !q.IsDeleted &&
                            q.QuestionType != Contracts.CurriculumAndQuestions.QuestionType.Essay &&
                            q.AnswerEvaluationMode != Contracts.CurriculumAndQuestions.QuestionAnswerEvaluationMode.Manual)
                .Select(q => q.QuestionId)
                .ToListAsync(ct)
            : [];

        var assignedPracticeIds = practiceIds.Where(validActivePracticeIds.Contains).ToList();

        ulong? initialQuestionId = assignedPracticeIds.Count > 0
            ? assignedPracticeIds[0]
            : checkpointQuestionId;

        var practiceDoc = JsonDocument.Parse(JsonSerializer.Serialize(assignedPracticeIds));

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var runNumber = session.TotalRuns + 1;

        var newRun = new LearningPathSessionRun
        {
            CenterId = centerId,
            SessionId = sessionId,
            StudentId = studentId,
            RunNumber = runNumber,
            Status = SessionRunStatus.InProgress,
            EvaluationStatus = SessionRunEvaluationStatus.NotStarted,
            Outcome = SessionRunOutcome.None,
            ActiveKey = 1,
            StartedAt = now,
            LastActivityAt = now,
            CurrentQuestionId = initialQuestionId,
            CheckpointQuestionId = checkpointQuestionId,
            QuestionsAssigned = practiceDoc,
            PolicySnapshot = JsonDocument.Parse(JsonSerializer.Serialize(new
            {
                session.MinimumQuestions,
                session.RequiredCorrectRate,
                session.PolicyVersion
            })),
            TotalQuestions = (uint)(assignedPracticeIds.Count + (checkpointQuestionId.HasValue ? 1 : 0)),
            QuestionsAttempted = 0,
            QuestionsCorrect = 0,
            CreatedAt = now,
            UpdatedAt = now,
            RowVersion = 1ul
        };

        session.TotalRuns++;
        if (session.Status == LearningPathSessionStatus.NotStarted)
        {
            session.Status = LearningPathSessionStatus.InProgress;
        }
        session.UpdatedAt = now;

        var parentPath = await _dbContext.LearningPaths
            .FirstOrDefaultAsync(p => p.CenterId == centerId && p.LearningPathId == session.LearningPathId, ct);
        if (parentPath?.PlanJson != null)
        {
            try
            {
                var rawJson = parentPath.PlanJson.RootElement.GetRawText();
                var phases = JsonSerializer.Deserialize<List<LearningPathPhaseDto>>(rawJson, JsonOptions);
                if (phases != null)
                {
                    var targetDto = phases.SelectMany(p => p.Weeks).SelectMany(w => w.Sessions)
                        .FirstOrDefault(s => s.SessionId == session.LegacySessionId || s.SessionId == session.SessionId.ToString());
                    if (targetDto != null && targetDto.Status != "Completed")
                    {
                        targetDto.Status = "InProgress";
                        targetDto.StartedAtUtc ??= now;
                        parentPath.PlanJson = JsonDocument.Parse(JsonSerializer.Serialize(phases, JsonOptions));
                        parentPath.UpdatedAt = now;
                        parentPath.RowVersion++;
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to synchronize PlanJson for LearningPath {LearningPathId}", session.LearningPathId);
            }
        }

        try
        {
            await _dbContext.LearningPathSessionRuns.AddAsync(newRun, ct);
            await _dbContext.SaveChangesAsync(ct);

            // Update CurrentRunId pointer
            session.CurrentRunId = newRun.SessionRunId;
            await _dbContext.SaveChangesAsync(ct);

            return new StartSessionRunResult(
                newRun.SessionRunId,
                newRun.SessionId,
                newRun.StudentId,
                newRun.RunNumber,
                newRun.Status,
                newRun.EvaluationStatus,
                newRun.CurrentQuestionId,
                newRun.CheckpointQuestionId,
                IsResumed: false);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Duplicate active run race detected for Student {StudentId} Session {SessionId}. Resolving to existing active run.", studentId, sessionId);

            // Concurrency race hit unique index ux_session_runs_center_student_session_active_key
            // Query back the active run created by the concurrent request
            var racedRun = await _dbContext.LearningPathSessionRuns
                .AsNoTracking()
                .FirstOrDefaultAsync(r =>
                    r.CenterId == centerId &&
                    r.StudentId == studentId &&
                    r.SessionId == sessionId &&
                    (r.Status == SessionRunStatus.InProgress || r.Status == SessionRunStatus.Grading),
                    ct);

            if (racedRun is not null)
            {
                return new StartSessionRunResult(
                    racedRun.SessionRunId,
                    racedRun.SessionId,
                    racedRun.StudentId,
                    racedRun.RunNumber,
                    racedRun.Status,
                    racedRun.EvaluationStatus,
                    racedRun.CurrentQuestionId,
                    racedRun.CheckpointQuestionId,
                    IsResumed: true);
            }

            throw;
        }
    }

    public async Task<LearningPathSessionRun> TransitionRunAsync(
        Guid centerId,
        ulong sessionRunId,
        TransitionSessionRunRequest request,
        CancellationToken ct = default)
    {
        var run = await _dbContext.LearningPathSessionRuns
            .Include(r => r.Session)
            .FirstOrDefaultAsync(r => r.CenterId == centerId && r.SessionRunId == sessionRunId, ct);

        if (run is null)
        {
            throw new KeyNotFoundException($"Session run {sessionRunId} not found in center {centerId}.");
        }

        // Validate state machine transitions
        ValidateRunTransition(run.Status, request.TargetStatus);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        run.Status = request.TargetStatus;
        run.LastActivityAt = now;
        run.UpdatedAt = now;

        if (request.TargetEvaluationStatus.HasValue)
        {
            run.EvaluationStatus = request.TargetEvaluationStatus.Value;
        }

        if (request.TargetOutcome.HasValue)
        {
            run.Outcome = request.TargetOutcome.Value;
        }

        if (request.ScoreRate.HasValue) run.ScoreRate = request.ScoreRate.Value;
        if (request.QuestionsAttempted.HasValue) run.QuestionsAttempted = request.QuestionsAttempted.Value;
        if (request.QuestionsCorrect.HasValue) run.QuestionsCorrect = request.QuestionsCorrect.Value;
        if (request.EndingMastery.HasValue) run.EndingMastery = request.EndingMastery.Value;

        if (request.TargetStatus == SessionRunStatus.Completed || request.TargetStatus == SessionRunStatus.Abandoned)
        {
            run.ActiveKey = null; // Release active slot
            run.CompletedAt = now;

            if (request.TargetStatus == SessionRunStatus.Completed)
            {
                // Reflect outcome to parent session status
                if (run.Outcome == SessionRunOutcome.Passed)
                {
                    run.Session.Status = LearningPathSessionStatus.Completed;
                }
                else if (run.Outcome == SessionRunOutcome.NotPassed)
                {
                    run.Session.Status = LearningPathSessionStatus.NeedsReview;
                }
                // InsufficientEvidence preserves InProgress or NeedsReview without marking as Failed
                run.Session.UpdatedAt = now;
            }
        }
        else if (request.TargetStatus == SessionRunStatus.NeedsReview)
        {
            run.Session.Status = LearningPathSessionStatus.NeedsReview;
            run.Session.UpdatedAt = now;
        }

        await _dbContext.SaveChangesAsync(ct);
        return run;
    }

    public async Task<LearningPathSession> TransitionSessionStatusAsync(
        Guid centerId,
        Guid sessionId,
        LearningPathSessionStatus targetStatus,
        CancellationToken ct = default)
    {
        var session = await _dbContext.LearningPathSessions
            .FirstOrDefaultAsync(s => s.CenterId == centerId && s.SessionId == sessionId, ct);

        if (session is null)
        {
            throw new KeyNotFoundException($"Session {sessionId} not found in center {centerId}.");
        }

        ValidateSessionTransition(session.Status, targetStatus);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        session.Status = targetStatus;
        session.UpdatedAt = now;

        await _dbContext.SaveChangesAsync(ct);
        return session;
    }

    public static void ValidateRunTransition(SessionRunStatus current, SessionRunStatus target)
    {
        if (current == target) return;

        bool isValid = (current, target) switch
        {
            (SessionRunStatus.InProgress, SessionRunStatus.Grading) => true,
            (SessionRunStatus.InProgress, SessionRunStatus.Abandoned) => true,
            (SessionRunStatus.Grading, SessionRunStatus.Completed) => true,
            (SessionRunStatus.Grading, SessionRunStatus.NeedsReview) => true,
            (SessionRunStatus.NeedsReview, SessionRunStatus.Completed) => true,
            (SessionRunStatus.NeedsReview, SessionRunStatus.Abandoned) => true,
            _ => false
        };

        if (!isValid)
        {
            throw new SessionStateTransitionException($"Invalid SessionRun transition from '{current}' to '{target}'.");
        }
    }

    public static void ValidateSessionTransition(LearningPathSessionStatus current, LearningPathSessionStatus target)
    {
        if (current == target) return;

        bool isValid = (current, target) switch
        {
            (LearningPathSessionStatus.NotStarted, LearningPathSessionStatus.InProgress) => true,
            (LearningPathSessionStatus.NotStarted, LearningPathSessionStatus.Skipped) => true,
            (LearningPathSessionStatus.InProgress, LearningPathSessionStatus.Completed) => true,
            (LearningPathSessionStatus.InProgress, LearningPathSessionStatus.NeedsReview) => true,
            (LearningPathSessionStatus.InProgress, LearningPathSessionStatus.Skipped) => true,
            (LearningPathSessionStatus.NeedsReview, LearningPathSessionStatus.InProgress) => true,
            (LearningPathSessionStatus.NeedsReview, LearningPathSessionStatus.Completed) => true,
            (LearningPathSessionStatus.NeedsReview, LearningPathSessionStatus.Skipped) => true,
            _ => false
        };

        if (!isValid)
        {
            throw new SessionStateTransitionException($"Invalid LearningPathSession transition from '{current}' to '{target}'.");
        }
    }

    private static LearningPathSessionStatus ParseSessionStatus(string? status) =>
        status switch
        {
            "InProgress" => LearningPathSessionStatus.InProgress,
            "Completed" => LearningPathSessionStatus.Completed,
            "NeedsReview" => LearningPathSessionStatus.NeedsReview,
            "Skipped" => LearningPathSessionStatus.Skipped,
            _ => LearningPathSessionStatus.NotStarted
        };

    private async Task EnsureLearningPathPlanJsonAsync(
        Guid centerId,
        LearningPath path,
        CancellationToken ct)
    {
        if (path.PlanJson != null) return;

        await _dbContext.Entry(path)
            .Collection(p => p.Items)
            .Query()
            .Where(i => !i.IsDeleted)
            .OrderBy(i => i.RankOrder)
            .LoadAsync(ct);

        if (path.Items.Count == 0) return;

        var topicIds = path.Items.Select(i => i.TopicNodeId).Distinct().ToList();
        var topics = await _dbContext.KnowledgeNodes.AsNoTracking()
            .Where(n => n.CenterId == centerId && topicIds.Contains(n.NodeId) && n.IsActive && !n.IsDeleted)
            .ToDictionaryAsync(n => n.NodeId, ct);

        var preference = await _dbContext.StudentLearningPathPreferences.AsNoTracking()
            .FirstOrDefaultAsync(p => p.CenterId == centerId && p.StudentId == path.StudentId && p.SubjectId == path.SubjectId && !p.IsDeleted, ct);

        var weeks = Math.Clamp(preference?.TargetWeeks ?? 4, 1, 52);
        var days = Math.Clamp(preference?.DaysPerWeek ?? 5, 1, 7);
        var minutesPerDay = preference?.MinutesPerDay ?? 30;

        var questions = await _dbContext.Questions.AsNoTracking()
            .Where(q => q.CenterId == centerId && q.SubjectId == path.SubjectId && topicIds.Contains(q.PrimaryTopicNodeId) &&
                        q.Status == QuestionStatus.Active && !q.IsDeleted &&
                        q.QuestionType != QuestionType.Essay &&
                        q.AnswerEvaluationMode != QuestionAnswerEvaluationMode.Manual)
            .OrderBy(q => q.Difficulty).ThenBy(q => q.QuestionId)
            .Select(q => new { q.QuestionId, q.PrimaryTopicNodeId })
            .ToListAsync(ct);

        var questionsByTopic = questions.GroupBy(q => q.PrimaryTopicNodeId)
            .ToDictionary(g => g.Key, g => g.Select(q => q.QuestionId.ToString()).ToList());

        var phases = new List<LearningPathPhaseDto>();
        var phaseCount = Math.Clamp((int)Math.Ceiling(weeks / 4m), 1, 6);
        var weeksPerPhase = (int)Math.Ceiling(weeks / (decimal)phaseCount);
        var orderedItems = path.Items.OrderBy(i => i.RankOrder).ToList();

        for (var phaseIndex = 0; phaseIndex < phaseCount; phaseIndex++)
        {
            var startWeek = phaseIndex * weeksPerPhase + 1;
            var endWeek = Math.Min(weeks, startWeek + weeksPerPhase - 1);
            if (startWeek > weeks) break;

            var phaseWeeks = new List<LearningPathWeekDto>();
            for (var week = startWeek; week <= endWeek; week++)
            {
                var item = orderedItems[Math.Min(orderedItems.Count - 1, (week - 1) * orderedItems.Count / weeks)];
                topics.TryGetValue(item.TopicNodeId, out var node);
                var topicName = node?.NodeName ?? $"Chủ đề {item.TopicNodeId}";
                var questionIds = questionsByTopic.GetValueOrDefault(item.TopicNodeId) ?? [];

                var sessions = new List<LearningPathSessionDto>();
                for (var day = 1; day <= days; day++)
                {
                    var isGuided = (day % 2 != 0);
                    var type = isGuided ? "GuidedPractice" : "AdaptivePractice";
                    var capacity = Math.Clamp(minutesPerDay / 4, 1, 12);
                    var requestedCount = Math.Min(4, capacity);
                    var count = Math.Min(requestedCount, questionIds.Count);
                    var offset = questionIds.Count == 0 ? 0 : ((week - 1) * days + day - 1) * requestedCount % questionIds.Count;
                    var selectedQuestions = Enumerable.Range(0, count).Select(i => questionIds[(offset + i) % questionIds.Count]).ToList();

                    var online = new List<string> { $"Làm {selectedQuestions.Count} câu thật từ Question Bank theo đúng chủ đề." };
                    sessions.Add(new LearningPathSessionDto
                    {
                        SessionId = $"{path.LearningPathId:D}-w{week:D2}-s{day:D2}",
                        Title = isGuided ? $"Luyện có hướng dẫn: {topicName}" : $"Luyện độc lập: {topicName}",
                        Type = type,
                        TopicNodeId = item.TopicNodeId.ToString(),
                        TopicName = topicName,
                        Objective = $"Củng cố kỹ năng và nhận diện lỗi thường gặp trong {topicName}.",
                        EstimatedMinutes = minutesPerDay,
                        OnlineActivities = online,
                        OfflineActivities = new List<string> { "Đọc lại lý thuyết và ghi chú các dạng bài đã làm." },
                        RecommendedQuestionIds = selectedQuestions,
                        RequiredCorrectRate = 0.70m,
                        TargetMastery = 80m,
                        CompletionCriteria = new List<string> { "Hoàn thành các câu hỏi trong phiên", "Đạt tỷ lệ đúng tối thiểu theo quy định" },
                        Status = "NotStarted"
                    });
                }

                phaseWeeks.Add(new LearningPathWeekDto
                {
                    WeekNumber = week,
                    Title = $"Tuần {week} · {topicName}",
                    Objective = $"Nâng mức thành thạo {topicName}.",
                    ProgressPercentage = 0,
                    Sessions = sessions,
                    Checkpoint = new LearningPathCheckpointDto
                    {
                        Type = "Weekly",
                        CurrentMastery = 0,
                        TargetMastery = 80m,
                        RequiredCorrectRate = 0.70m,
                        CompletedTasks = 0,
                        WeakTopics = new List<string> { topicName },
                        Recommendation = "Hoàn thành buổi học để cập nhật hồ sơ năng lực."
                    }
                });
            }

            phases.Add(new LearningPathPhaseDto
            {
                PhaseNumber = phaseIndex + 1,
                PhaseName = $"Giai đoạn {phaseIndex + 1}: Củng cố chuyên đề",
                Timeframe = startWeek == endWeek ? $"Tuần {startWeek}" : $"Tuần {startWeek} - {endWeek}",
                Description = "Học theo thứ tự ưu tiên của lộ trình cá nhân hóa.",
                ProgressPercentage = 0,
                Weeks = phaseWeeks
            });
        }

        path.PlanJson = JsonSerializer.SerializeToDocument(phases, JsonOptions);
        path.PlanSchemaVersion = "2.0";
        path.GenerationStatus = "Ready";
        path.UpdatedAt = _timeProvider.GetUtcNow().UtcDateTime;
        await _dbContext.SaveChangesAsync(ct);
    }

    private async Task<LearningPathSession?> ResolveSessionEntityAsync(
        Guid centerId,
        Guid studentId,
        Guid subjectId,
        string sessionIdOrLegacyId,
        CancellationToken ct, Guid? classId = null)
    {
        if (string.IsNullOrWhiteSpace(sessionIdOrLegacyId)) return null;

        LearningPathSession? session = null;
        if (Guid.TryParse(sessionIdOrLegacyId, out var sessionGuid))
        {
            session = await _dbContext.LearningPathSessions
                .FirstOrDefaultAsync(s => s.CenterId == centerId && s.SessionId == sessionGuid && s.LearningPath.StudentId == studentId && s.LearningPath.SubjectId == subjectId, ct);
        }

        session ??= await _dbContext.LearningPathSessions
            .FirstOrDefaultAsync(s => s.CenterId == centerId && s.LegacySessionId == sessionIdOrLegacyId && s.LearningPath.StudentId == studentId && s.LearningPath.SubjectId == subjectId, ct);

        if (session is null)
        {
            LearningPath? path = null;
            if (sessionIdOrLegacyId.Length >= 36 && Guid.TryParse(sessionIdOrLegacyId[..36], out var targetPathId))
            {
                path = await _dbContext.LearningPaths
                    .Include(p => p.Sessions)
                    .Include(p => p.Items.Where(i => !i.IsDeleted))
                    .FirstOrDefaultAsync(p => p.CenterId == centerId && p.LearningPathId == targetPathId && p.StudentId == studentId && p.SubjectId == subjectId && !p.IsDeleted, ct);
            }

            path ??= await _dbContext.LearningPaths
                .Include(p => p.Sessions)
                .Include(p => p.Items.Where(i => !i.IsDeleted))
                .FirstOrDefaultAsync(p => p.CenterId == centerId &&
                                         p.StudentId == studentId &&
                                         p.SubjectId == subjectId &&
                                         p.Status == LearningPathStatus.Active &&
                                         !p.IsDeleted, ct);

            if (path is not null)
            {
                if (path.PlanJson is null)
                {
                    await EnsureLearningPathPlanJsonAsync(centerId, path, ct);
                }

                if (path.PlanJson is not null)
                {
                    await MaterializeSessionsFromPlanAsync(centerId, path.LearningPathId, ct);

                    if (Guid.TryParse(sessionIdOrLegacyId, out sessionGuid))
                    {
                        session = await _dbContext.LearningPathSessions
                            .FirstOrDefaultAsync(s => s.CenterId == centerId && s.SessionId == sessionGuid && s.LearningPath.StudentId == studentId && s.LearningPath.SubjectId == subjectId, ct);
                    }

                    session ??= await _dbContext.LearningPathSessions
                        .FirstOrDefaultAsync(s => s.CenterId == centerId && s.LegacySessionId == sessionIdOrLegacyId && s.LearningPath.StudentId == studentId && s.LearningPath.SubjectId == subjectId, ct);
                }
            }
        }

        if (session != null)
        {
            var scope = await EduTwin.BLL.Organization.StudentLearningScope.ResolveAsync(_dbContext, centerId, studentId, subjectId, classId ?? session.ClassId, false, ct);
            if (!scope.Includes(session.TopicNodeId) || (classId.HasValue && session.ClassId != classId) || session.PolicyVersion == Planning.LearningPathV4Data.Policy) return null;
        }
        return session;
    }

    public async Task<StartSessionRunResult> StartOrResumeSessionAsync(
        Guid centerId,
        Guid studentId,
        Guid subjectId,
        string sessionIdOrLegacyId,
        Guid? classId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sessionIdOrLegacyId))
        {
            throw new ArgumentException("Session identifier cannot be null or empty.", nameof(sessionIdOrLegacyId));
        }

        var session = await ResolveSessionEntityAsync(centerId, studentId, subjectId, sessionIdOrLegacyId, ct, classId);

        if (session is null)
        {
            throw new KeyNotFoundException($"Session '{sessionIdOrLegacyId}' not found for student in this center.");
        }

        if (session.Status == LearningPathSessionStatus.Skipped)
        {
            throw new InvalidOperationException("Buổi học này đã bị bỏ qua (Skipped).");
        }

        return await StartOrResumeRunAsync(centerId, studentId, session.SessionId, ct);
    }

    public async Task<ResumeSessionRunResult> ResumeSessionAsync(
        Guid centerId,
        Guid studentId,
        Guid subjectId,
        string sessionIdOrLegacyId,
        Guid? classId = null,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(sessionIdOrLegacyId))
        {
            throw new ArgumentException("Session identifier cannot be null or empty.", nameof(sessionIdOrLegacyId));
        }

        var session = await ResolveSessionEntityAsync(centerId, studentId, subjectId, sessionIdOrLegacyId, ct, classId);

        if (session is null)
        {
            throw new KeyNotFoundException($"Session '{sessionIdOrLegacyId}' not found for student in this center.");
        }

        // Check for active run (InProgress or Grading or NeedsReview)
        var activeRun = await _dbContext.LearningPathSessionRuns
            .FirstOrDefaultAsync(r =>
                r.CenterId == centerId &&
                r.StudentId == studentId &&
                r.SessionId == session.SessionId &&
                (r.Status == SessionRunStatus.InProgress || r.Status == SessionRunStatus.Grading || r.Status == SessionRunStatus.NeedsReview),
                ct);

        if (activeRun is not null)
        {
            var attemptedIds = await _dbContext.Attempts
                .AsNoTracking()
                .Where(a => a.CenterId == centerId && a.SessionRunId == activeRun.SessionRunId)
                .Select(a => a.QuestionId)
                .ToListAsync(ct);

            // If CurrentQuestionId is null or already attempted, advance to next unattempted question
            if (activeRun.Status == SessionRunStatus.InProgress &&
                (!activeRun.CurrentQuestionId.HasValue || attemptedIds.Contains(activeRun.CurrentQuestionId.Value)))
            {
                var assignedPractice = ParseQuestionIds(activeRun.QuestionsAssigned);
                var nextPractice = assignedPractice.FirstOrDefault(id => !attemptedIds.Contains(id));
                if (nextPractice > 0)
                {
                    activeRun.CurrentQuestionId = nextPractice;
                }
                else if (activeRun.CheckpointQuestionId.HasValue && !attemptedIds.Contains(activeRun.CheckpointQuestionId.Value))
                {
                    activeRun.CurrentQuestionId = activeRun.CheckpointQuestionId.Value;
                }
                else
                {
                    activeRun.CurrentQuestionId = null;
                }
            }

            return new ResumeSessionRunResult(
                activeRun.SessionRunId,
                session.SessionId,
                session.LegacySessionId,
                session.Title,
                session.Status,
                activeRun.RunNumber,
                activeRun.Status,
                activeRun.EvaluationStatus,
                activeRun.Outcome,
                activeRun.StartedAt,
                activeRun.LastActivityAt,
                activeRun.CompletedAt,
                activeRun.TotalQuestions,
                activeRun.QuestionsAttempted,
                activeRun.QuestionsCorrect,
                activeRun.ScoreRate,
                activeRun.CurrentQuestionId,
                attemptedIds,
                IsCompleted: (activeRun.Status == SessionRunStatus.Completed),
                IsGrading: (activeRun.Status == SessionRunStatus.Grading),
                CanStart: false);
        }

        // No active run: query latest run if any
        var latestRun = await _dbContext.LearningPathSessionRuns
            .Where(r => r.CenterId == centerId && r.StudentId == studentId && r.SessionId == session.SessionId)
            .OrderByDescending(r => r.RunNumber)
            .FirstOrDefaultAsync(ct);

        return new ResumeSessionRunResult(
            latestRun?.SessionRunId,
            session.SessionId,
            session.LegacySessionId,
            session.Title,
            session.Status,
            latestRun?.RunNumber,
            latestRun?.Status,
            latestRun?.EvaluationStatus ?? SessionRunEvaluationStatus.NotStarted,
            latestRun?.Outcome ?? SessionRunOutcome.None,
            latestRun?.StartedAt,
            latestRun?.LastActivityAt,
            latestRun?.CompletedAt,
            latestRun?.TotalQuestions ?? session.MinimumQuestions,
            latestRun?.QuestionsAttempted ?? 0,
            latestRun?.QuestionsCorrect ?? 0,
            latestRun?.ScoreRate,
            CurrentQuestionId: null,
            AttemptedQuestionIds: [],
            IsCompleted: (session.Status == LearningPathSessionStatus.Completed),
            IsGrading: false,
            CanStart: true);
    }

    public async Task<NextQuestionDto?> GetQuestionDtoAsync(
        Guid centerId,
        Guid sessionId,
        ulong questionId,
        bool isCheckpoint,
        CancellationToken ct = default)
    {
        var question = await _dbContext.Questions
            .AsNoTracking()
            .FirstOrDefaultAsync(q =>
                q.CenterId == centerId &&
                q.QuestionId == questionId &&
                q.Status == Contracts.CurriculumAndQuestions.QuestionStatus.Active &&
                !q.IsDeleted &&
                q.QuestionType != Contracts.CurriculumAndQuestions.QuestionType.Essay &&
                q.AnswerEvaluationMode != Contracts.CurriculumAndQuestions.QuestionAnswerEvaluationMode.Manual,
                ct);

        if (question is null) return null;

        var topic = await _dbContext.KnowledgeNodes
            .AsNoTracking()
            .FirstOrDefaultAsync(n => n.CenterId == centerId && n.NodeId == question.PrimaryTopicNodeId && n.IsActive && !n.IsDeleted, ct);

        var options = await _dbContext.QuestionOptions
            .AsNoTracking()
            .Where(o => o.CenterId == centerId && o.QuestionId == questionId && !o.IsDeleted)
            .OrderBy(o => o.OrderIndex)
            .Select(o => new Contracts.CurriculumAndQuestions.StudentQuestionOptionDto
            {
                OptionId = o.OptionId.ToString(),
                Label = o.OptionLabel,
                Text = o.OptionText,
                OrderIndex = o.OrderIndex
            })
            .ToListAsync(ct);

        var session = await _dbContext.LearningPathSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.CenterId == centerId && s.SessionId == sessionId, ct);

        // Security requirement: Checkpoint must NEVER expose hint or explanation before submission!
        string explanation = isCheckpoint ? string.Empty : (session?.Objective ?? string.Empty);
        string activityType = isCheckpoint ? "Checkpoint" : "Practice";

        return new NextQuestionDto
        {
            Strategy = LearningPathStrategy.OpportunityGap,
            Topic = new NextQuestionTopicDto(question.PrimaryTopicNodeId, topic?.NodeName ?? "Topic", 0),
            Explanation = explanation,
            ActivityType = activityType,
            SessionId = sessionId,
            Question = new NextQuestionQuestionDto(
                question.QuestionId,
                question.QuestionType,
                question.Difficulty,
                question.QuestionText,
                question.MaxScore,
                question.EstimatedTimeSeconds,
                question.ReasoningRequired,
                question.LanguageCode,
                options,
                question.AnswerEvaluationMode,
                question.HasImage)
        };
    }

    public static List<ulong> ParseQuestionIds(JsonDocument? json)
    {
        if (json is null) return [];
        try
        {
            return JsonSerializer.Deserialize<List<ulong>>(json.RootElement.GetRawText()) ?? [];
        }
        catch
        {
            return [];
        }
    }

    private static MaterializedSessionSummary MapToSummary(LearningPathSession s)
    {
        var practiceCount = ParseQuestionIds(s.PracticeQuestionIds).Count;
        var checkpointCount = ParseQuestionIds(s.CheckpointQuestionIds).Count;

        return new MaterializedSessionSummary(
            s.SessionId,
            s.LegacySessionId,
            s.SessionOrder,
            s.Title,
            s.Status,
            practiceCount,
            checkpointCount);
    }

    public async Task<EvaluateSessionRunResult> EvaluateSessionRunAsync(
        Guid centerId,
        ulong sessionRunId,
        CancellationToken ct = default)
    {
        var run = await _dbContext.LearningPathSessionRuns
            .Include(r => r.Session)
            .FirstOrDefaultAsync(r => r.CenterId == centerId && r.SessionRunId == sessionRunId, ct);

        if (run is null)
        {
            throw new KeyNotFoundException($"Session run {sessionRunId} not found in center {centerId}.");
        }

        var session = run.Session;
        if (session.PolicyVersion == Planning.LearningPathV4Data.Policy)
        {
            var result = await (_v4Evaluator ?? new LearningPathV4Evaluator(_dbContext, _timeProvider))
                .EvaluateMissionRunAsync(centerId, run.StudentId, sessionRunId, ct);
            return new EvaluateSessionRunResult(run.SessionRunId, run.SessionId, session.LegacySessionId, session.Title,
                session.Status, run.RunNumber, run.Status, run.EvaluationStatus, run.Outcome, run.TotalQuestions,
                run.QuestionsAttempted, 0, 0, null, null, result.HasPendingReview, result.Outcome == "PendingGrading",
                string.Join(" ", result.DecisionReasons), result.EvaluatedAt, run.Status == SessionRunStatus.Completed);
        }
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // Idempotent replay: if already completed, return existing evaluation without mutating outcome or counts
        if (run.Status == SessionRunStatus.Completed)
        {
            var completedAttempts = await _dbContext.Attempts
                .AsNoTracking()
                .Where(a => a.CenterId == centerId && a.SessionRunId == sessionRunId)
                .ToListAsync(ct);

            var chkIds = ParseQuestionIds(session.CheckpointQuestionIds);
            if (run.CheckpointQuestionId.HasValue && !chkIds.Contains(run.CheckpointQuestionId.Value))
            {
                chkIds.Add(run.CheckpointQuestionId.Value);
            }
            var chkAttempts = completedAttempts.Where(a => chkIds.Contains(a.QuestionId)).ToList();
            var chkCorrect = chkAttempts.Count(a => a.IsCorrect == true);

            return new EvaluateSessionRunResult(
                run.SessionRunId,
                run.SessionId,
                session.LegacySessionId,
                session.Title,
                session.Status,
                run.RunNumber,
                run.Status,
                run.EvaluationStatus,
                run.Outcome,
                run.TotalQuestions,
                run.QuestionsAttempted,
                (uint)chkAttempts.Count,
                (uint)chkCorrect,
                run.ScoreRate,
                run.ScoreRate,
                RequiresTeacherReview: false,
                HasPendingAIGrading: false,
                EvaluationDetail: $"Lượt học đã hoàn tất với kết quả {run.Outcome}.",
                EvaluatedAt: run.CompletedAt,
                IsCompleted: true);
        }

        // Load attempts for this run
        var attempts = await _dbContext.Attempts
            .Where(a => a.CenterId == centerId && a.SessionRunId == sessionRunId)
            .ToListAsync(ct);

        var attemptIds = attempts.Select(a => a.AttemptId).ToList();
        var analyses = attemptIds.Count > 0
            ? await _dbContext.ReasoningAnalyses
                .Where(ra => ra.CenterId == centerId && attemptIds.Contains(ra.AttemptId))
                .ToListAsync(ct)
            : [];

        var aiJobs = attemptIds.Count > 0
            ? await _dbContext.AIAnalysisJobs
                .Where(j => j.CenterId == centerId && attemptIds.Contains(j.AttemptId))
                .ToListAsync(ct)
            : [];

        var evidenceAssessments = attemptIds.Count > 0
            ? await _dbContext.EvidenceAssessments
                .Where(e => e.CenterId == centerId && attemptIds.Contains(e.AttemptId))
                .ToListAsync(ct)
            : [];

        var practiceQuestionIds = ParseQuestionIds(run.QuestionsAssigned);
        var checkpointQuestionIds = ParseQuestionIds(session.CheckpointQuestionIds);
        if (run.CheckpointQuestionId.HasValue && !checkpointQuestionIds.Contains(run.CheckpointQuestionId.Value))
        {
            checkpointQuestionIds.Add(run.CheckpointQuestionId.Value);
        }

        var allRequiredQuestionIds = practiceQuestionIds.Concat(checkpointQuestionIds).Distinct().ToList();
        var attemptedQuestionIds = attempts.Select(a => a.QuestionId).Distinct().ToHashSet();

        uint minimumQuestions = session.MinimumQuestions;
        decimal requiredCorrectRate = session.RequiredCorrectRate > 0 ? session.RequiredCorrectRate : 0.80m;
        if (run.PolicySnapshot is not null)
        {
            if (run.PolicySnapshot.RootElement.TryGetProperty("MinimumQuestions", out var minProp))
            {
                minimumQuestions = minProp.GetUInt32();
            }
            if (run.PolicySnapshot.RootElement.TryGetProperty("RequiredCorrectRate", out var rateProp))
            {
                requiredCorrectRate = rateProp.GetDecimal();
            }
        }

        // EG-S01 & EG-S13: Check if activities are incomplete
        var missingAssignedCount = allRequiredQuestionIds.Count(qid => !attemptedQuestionIds.Contains(qid));
        if (missingAssignedCount > 0 && attempts.Count < minimumQuestions)
        {
            return new EvaluateSessionRunResult(
                run.SessionRunId,
                run.SessionId,
                session.LegacySessionId,
                session.Title,
                session.Status,
                run.RunNumber,
                run.Status,
                run.EvaluationStatus,
                SessionRunOutcome.InsufficientEvidence,
                run.TotalQuestions,
                (uint)attempts.Count,
                CheckpointsAttempted: 0,
                CheckpointsCorrect: 0,
                CheckpointScoreRate: null,
                OverallScoreRate: null,
                RequiresTeacherReview: false,
                HasPendingAIGrading: false,
                EvaluationDetail: $"Chưa hoàn thành đủ số câu hỏi tối thiểu ({attempts.Count}/{minimumQuestions}). Còn thiếu {missingAssignedCount} câu.",
                EvaluatedAt: null,
                IsCompleted: false);
        }

        // EG-S11: Asynchronous Grading Check - if any AI analysis job is still Pending or Processing, run status is Grading
        var hasPendingAI = aiJobs.Any(j => j.Status == AIJobStatus.Pending || j.Status == AIJobStatus.Processing);
        var hasPendingAttempt = attempts.Any(a => a.Status == AttemptStatus.PendingAnalysis || a.Status == AttemptStatus.Processing);
        if (hasPendingAI || hasPendingAttempt)
        {
            run.Status = SessionRunStatus.Grading;
            run.EvaluationStatus = SessionRunEvaluationStatus.Processing;
            run.LastActivityAt = now;
            run.UpdatedAt = now;
            run.RowVersion++;
            await _dbContext.SaveChangesAsync(ct);

            return new EvaluateSessionRunResult(
                run.SessionRunId,
                run.SessionId,
                session.LegacySessionId,
                session.Title,
                session.Status,
                run.RunNumber,
                SessionRunStatus.Grading,
                SessionRunEvaluationStatus.Processing,
                SessionRunOutcome.None,
                run.TotalQuestions,
                (uint)attempts.Count,
                CheckpointsAttempted: 0,
                CheckpointsCorrect: 0,
                CheckpointScoreRate: null,
                OverallScoreRate: null,
                RequiresTeacherReview: false,
                HasPendingAIGrading: true,
                EvaluationDetail: "Đang chờ AI phân tích và chấm điểm lập luận...",
                EvaluatedAt: null,
                IsCompleted: false);
        }

        // Checkpoint attempts in this run
        var checkpointAttempts = attempts
            .Where(a => checkpointQuestionIds.Contains(a.QuestionId))
            .GroupBy(a => a.QuestionId)
            .Select(g => g.OrderByDescending(a => a.CreatedAt).First())
            .ToList();

        // EG-S13: Insufficient Evidence if checkpoint pool or checkpoints missing
        if (checkpointQuestionIds.Count > 0 && checkpointAttempts.Count == 0)
        {
            run.Outcome = SessionRunOutcome.InsufficientEvidence;
            run.EvaluationStatus = SessionRunEvaluationStatus.Completed;
            run.Status = SessionRunStatus.Completed;
            run.ActiveKey = null; // Release active slot
            run.CompletedAt = now;
            run.UpdatedAt = now;
            run.RowVersion++;

            session.Status = LearningPathSessionStatus.NeedsReview;
            session.UpdatedAt = now;
            session.RowVersion++;

            await _dbContext.SaveChangesAsync(ct);

            return new EvaluateSessionRunResult(
                run.SessionRunId,
                run.SessionId,
                session.LegacySessionId,
                session.Title,
                session.Status,
                run.RunNumber,
                SessionRunStatus.Completed,
                SessionRunEvaluationStatus.Completed,
                SessionRunOutcome.InsufficientEvidence,
                run.TotalQuestions,
                (uint)attempts.Count,
                CheckpointsAttempted: 0,
                CheckpointsCorrect: 0,
                CheckpointScoreRate: null,
                OverallScoreRate: null,
                RequiresTeacherReview: false,
                HasPendingAIGrading: false,
                EvaluationDetail: "Phiên học thiếu câu hỏi kiểm tra độc lập (checkpoint). Không đủ dữ liệu kết luận.",
                EvaluatedAt: now,
                IsCompleted: true);
        }

        // EG-S12: Review Only When Required
        // Check if any attempt requires teacher review and has NOT been approved/adjusted yet
        bool requiresTeacherReview = false;
        foreach (var att in attempts)
        {
            var analysis = analyses.FirstOrDefault(a => a.AttemptId == att.AttemptId);
            var latestEvidence = evidenceAssessments
                .Where(e => e.AttemptId == att.AttemptId)
                .OrderByDescending(e => e.EvaluatedAt)
                .ThenByDescending(e => e.EvidenceAssessmentId)
                .FirstOrDefault();

            if (analysis is not null)
            {
                bool isTeacherResolved = analysis.ReviewDecision is not null || analysis.ReviewedByUserId is not null;
                if (!isTeacherResolved)
                {
                    if (analysis.NeedsTeacherReview || (latestEvidence is not null && latestEvidence.TrustLevel == EvidenceTrustLevel.ReviewOnly))
                    {
                        requiresTeacherReview = true;
                        break;
                    }
                }
            }
            else if (latestEvidence is not null && latestEvidence.TrustLevel == EvidenceTrustLevel.ReviewOnly)
            {
                if (latestEvidence.SourceType != EvidenceSourceType.TeacherApproval && latestEvidence.SourceType != EvidenceSourceType.TeacherOverride)
                {
                    requiresTeacherReview = true;
                    break;
                }
            }
        }

        if (requiresTeacherReview)
        {
            run.Status = SessionRunStatus.NeedsReview;
            run.EvaluationStatus = SessionRunEvaluationStatus.AwaitingTeacher;
            run.Outcome = SessionRunOutcome.None;
            run.LastActivityAt = now;
            run.UpdatedAt = now;
            run.RowVersion++;

            session.Status = LearningPathSessionStatus.NeedsReview;
            session.UpdatedAt = now;
            session.RowVersion++;

            await _dbContext.SaveChangesAsync(ct);

            return new EvaluateSessionRunResult(
                run.SessionRunId,
                run.SessionId,
                session.LegacySessionId,
                session.Title,
                session.Status,
                run.RunNumber,
                SessionRunStatus.NeedsReview,
                SessionRunEvaluationStatus.AwaitingTeacher,
                SessionRunOutcome.None,
                run.TotalQuestions,
                (uint)attempts.Count,
                (uint)checkpointAttempts.Count,
                CheckpointsCorrect: 0,
                CheckpointScoreRate: null,
                OverallScoreRate: null,
                RequiresTeacherReview: true,
                HasPendingAIGrading: false,
                EvaluationDetail: "Bài làm có câu hỏi cần giáo viên xem xét trước khi hoàn tất phiên học.",
                EvaluatedAt: null,
                IsCompleted: false);
        }

        // Calculate checkpoint correctness (EG-S04)
        uint checkpointCorrectCount = 0;
        foreach (var chk in checkpointAttempts)
        {
            var analysis = analyses.FirstOrDefault(a => a.AttemptId == chk.AttemptId);
            bool isCorrect = analysis?.OverrideIsCorrect ?? chk.IsCorrect ?? false;
            if (isCorrect) checkpointCorrectCount++;
        }

        decimal checkpointScoreRate = checkpointAttempts.Count > 0
            ? Math.Round((decimal)checkpointCorrectCount / checkpointAttempts.Count, 4)
            : 0m;

        uint overallCorrectCount = 0;
        var distinctAttempts = attempts.GroupBy(a => a.QuestionId).Select(g => g.First()).ToList();
        foreach (var att in distinctAttempts)
        {
            var analysis = analyses.FirstOrDefault(a => a.AttemptId == att.AttemptId);
            bool isCorrect = analysis?.OverrideIsCorrect ?? att.IsCorrect ?? false;
            if (isCorrect) overallCorrectCount++;
        }
        decimal overallScoreRate = distinctAttempts.Count > 0
            ? Math.Round((decimal)overallCorrectCount / distinctAttempts.Count, 4)
            : 0m;

        // EG-S04: Checkpoint performance determines outcome
        // Guided 4/5, checkpoint 1/5 -> NotPassed!
        bool passed = checkpointAttempts.Count > 0 && checkpointScoreRate >= requiredCorrectRate;

        var outcome = passed ? SessionRunOutcome.Passed : SessionRunOutcome.NotPassed;

        run.Status = SessionRunStatus.Completed;
        run.EvaluationStatus = SessionRunEvaluationStatus.Completed;
        run.Outcome = outcome;
        run.ScoreRate = checkpointScoreRate;
        run.QuestionsAttempted = (uint)attempts.Count;
        run.QuestionsCorrect = overallCorrectCount;
        run.CompletedAt = now;
        run.LastActivityAt = now;
        run.ActiveKey = null; // Release active slot
        run.UpdatedAt = now;
        run.RowVersion++;

        // EG-S09: Session is Completed ONLY when Run is Passed
        if (passed)
        {
            session.Status = LearningPathSessionStatus.Completed;
        }
        else
        {
            session.Status = LearningPathSessionStatus.NeedsReview; // Needs remediation
        }
        session.UpdatedAt = now;
        session.RowVersion++;

        await _dbContext.SaveChangesAsync(ct);

        return new EvaluateSessionRunResult(
            run.SessionRunId,
            run.SessionId,
            session.LegacySessionId,
            session.Title,
            session.Status,
            run.RunNumber,
            SessionRunStatus.Completed,
            SessionRunEvaluationStatus.Completed,
            outcome,
            run.TotalQuestions,
            (uint)attempts.Count,
            (uint)checkpointAttempts.Count,
            checkpointCorrectCount,
            checkpointScoreRate,
            overallScoreRate,
            RequiresTeacherReview: false,
            HasPendingAIGrading: false,
            EvaluationDetail: passed
                ? $"Đạt yêu cầu checkpoint ({checkpointCorrectCount}/{checkpointAttempts.Count} = {checkpointScoreRate:P0} >= {requiredCorrectRate:P0})."
                : $"Chưa đạt yêu cầu checkpoint ({checkpointCorrectCount}/{checkpointAttempts.Count} = {checkpointScoreRate:P0} < {requiredCorrectRate:P0}). Cần học lại.",
            EvaluatedAt: now,
            IsCompleted: true);
    }

    public async Task<EvaluateSessionRunResult> EvaluateSessionAsync(
        Guid centerId,
        Guid studentId,
        Guid subjectId,
        string sessionIdOrLegacyId,
        Guid? classId = null,
        ulong? sessionRunId = null,
        CancellationToken ct = default)
    {
        // 1. Resolve Session
        var session = await ResolveSessionEntityAsync(centerId, studentId, subjectId, sessionIdOrLegacyId, ct, classId);
        if (session is null)
        {
            throw new KeyNotFoundException($"Session '{sessionIdOrLegacyId}' not found for student in this center.");
        }

        // 2. Resolve Run to evaluate
        LearningPathSessionRun? targetRun = null;
        if (sessionRunId.HasValue)
        {
            targetRun = await _dbContext.LearningPathSessionRuns
                .FirstOrDefaultAsync(r =>
                    r.CenterId == centerId &&
                    r.StudentId == studentId &&
                    r.SessionRunId == sessionRunId.Value &&
                    r.SessionId == session.SessionId,
                    ct);

            if (targetRun is null)
            {
                throw new KeyNotFoundException($"Session run {sessionRunId.Value} not found for student in session {sessionIdOrLegacyId}.");
            }
        }
        else
        {
            // Find active run (InProgress, Grading, NeedsReview)
            targetRun = await _dbContext.LearningPathSessionRuns
                .FirstOrDefaultAsync(r =>
                    r.CenterId == centerId &&
                    r.StudentId == studentId &&
                    r.SessionId == session.SessionId &&
                    (r.Status == SessionRunStatus.InProgress || r.Status == SessionRunStatus.Grading || r.Status == SessionRunStatus.NeedsReview),
                    ct);

            // Or latest run if no active run
            targetRun ??= await _dbContext.LearningPathSessionRuns
                .Where(r => r.CenterId == centerId && r.StudentId == studentId && r.SessionId == session.SessionId)
                .OrderByDescending(r => r.RunNumber)
                .FirstOrDefaultAsync(ct);
        }

        if (targetRun is null)
        {
            return new EvaluateSessionRunResult(
                0ul,
                session.SessionId,
                session.LegacySessionId,
                session.Title,
                session.Status,
                RunNumber: 0,
                RunStatus: SessionRunStatus.InProgress,
                EvaluationStatus: SessionRunEvaluationStatus.NotStarted,
                Outcome: SessionRunOutcome.InsufficientEvidence,
                TotalQuestions: session.MinimumQuestions,
                QuestionsAttempted: 0,
                CheckpointsAttempted: 0,
                CheckpointsCorrect: 0,
                CheckpointScoreRate: null,
                OverallScoreRate: null,
                RequiresTeacherReview: false,
                HasPendingAIGrading: false,
                EvaluationDetail: "Chưa có lượt học nào được thực hiện cho buổi học này.",
                EvaluatedAt: null,
                IsCompleted: false);
        }

        return await EvaluateSessionRunAsync(centerId, targetRun.SessionRunId, ct);
    }
}
