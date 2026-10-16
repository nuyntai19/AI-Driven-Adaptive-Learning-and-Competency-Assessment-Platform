using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.Contracts.AdaptivePractice;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.AdaptivePractice;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.DigitalTwin;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.AdaptivePractice;

public sealed class AdaptivePracticeEngine : IAdaptivePracticeEngine
{
    private readonly EduTwinDbContext _dbContext;

    public AdaptivePracticeEngine(EduTwinDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<AdaptivePracticeProposalDto> GetProposalAsync(
        Guid centerId,
        Guid studentId,
        Guid subjectId,
        ulong? customTopicId,
        AdaptivePracticeMode mode,
        CancellationToken ct)
    {
        var subject = await _dbContext.Subjects
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.CenterId == centerId && s.SubjectId == subjectId && !s.IsDeleted, ct)
            ?? throw new InvalidOperationException($"Subject '{subjectId}' not found in center.");

        var allTopics = await _dbContext.KnowledgeNodes
            .AsNoTracking()
            .Where(kn => kn.CenterId == centerId && kn.SubjectId == subjectId && kn.Status == NodeStatus.Active && !kn.IsDeleted)
            .OrderBy(kn => kn.NodeId)
            .ToListAsync(ct);

        if (allTopics.Count == 0)
        {
            throw new InvalidOperationException($"No active topics found for subject '{subject.Name}'.");
        }

        var twins = await _dbContext.KnowledgeTwins
            .AsNoTracking()
            .Where(kt => kt.CenterId == centerId && kt.StudentId == studentId && kt.SubjectId == subjectId && !kt.IsDeleted)
            .ToDictionaryAsync(kt => kt.TopicNodeId, ct);

        var topicOptions = allTopics.Select(t => new AdaptivePracticeTopicOptionDto
        {
            TopicNodeId = t.NodeId,
            TopicName = t.Name,
            CurrentMastery = twins.TryGetValue(t.NodeId, out var tw) ? tw.MasteryPercentage : 0m,
            HasPrerequisites = false
        }).ToList();

        // 1. Determine target topic
        KnowledgeNode selectedTopic;
        string selectionReason;
        bool isDiagnostic = false;

        if (customTopicId.HasValue && allTopics.Any(t => t.NodeId == customTopicId.Value))
        {
            selectedTopic = allTopics.First(t => t.NodeId == customTopicId.Value);
            var m = twins.TryGetValue(selectedTopic.NodeId, out var tw) ? tw.MasteryPercentage : 0m;
            selectionReason = $"Học sinh chủ động chọn luyện tập chủ đề '{selectedTopic.Name}' (năng lực hiện tại: {m:F0}%).";
            isDiagnostic = !twins.ContainsKey(selectedTopic.NodeId) || twins[selectedTopic.NodeId].EvidenceCount == 0;
        }
        else if (mode == AdaptivePracticeMode.LearningPath)
        {
            var activePath = await _dbContext.LearningPaths
                .AsNoTracking()
                .Include(lp => lp.Items.Where(i => !i.IsDeleted))
                .SingleOrDefaultAsync(lp => lp.CenterId == centerId && lp.StudentId == studentId && lp.SubjectId == subjectId && lp.Status == LearningPathStatus.Active && !lp.IsDeleted, ct);

            var currentItem = activePath?.Items.FirstOrDefault(i => i.Status == LearningPathItemStatus.Current && !i.IsDeleted);
            if (currentItem != null && allTopics.Any(t => t.NodeId == currentItem.TopicNodeId))
            {
                selectedTopic = allTopics.First(t => t.NodeId == currentItem.TopicNodeId);
                selectionReason = $"Đề xuất từ Lộ trình học tập (Learning Path): {currentItem.Reason}";
            }
            else
            {
                selectedTopic = allTopics[0];
                selectionReason = $"Đề xuất theo nội dung cơ bản của môn học {subject.Name}.";
            }
        }
        else
        {
            // Weakness mode (P0 Default): find topic with lowest mastery (<80%) or lowest evidence count
            var candidates = allTopics
                .Select(t => new
                {
                    Topic = t,
                    Mastery = twins.TryGetValue(t.NodeId, out var tw) ? tw.MasteryPercentage : 0m,
                    EvidenceCount = twins.TryGetValue(t.NodeId, out var tw2) ? tw2.EvidenceCount : 0
                })
                .OrderBy(x => x.Mastery)
                .ThenBy(x => x.EvidenceCount)
                .ToList();

            var picked = candidates.FirstOrDefault(c => c.Mastery < 80m) ?? candidates[0];
            selectedTopic = picked.Topic;

            if (picked.EvidenceCount == 0)
            {
                isDiagnostic = true;
                selectionReason = $"Chưa có đủ bằng chứng đánh giá năng lực về '{selectedTopic.Name}'. Hệ thống đề xuất phiên chẩn đoán ban đầu để xác định chính xác trình độ.";
            }
            else
            {
                selectionReason = $"Dựa trên hồ sơ Digital Twin, chủ đề '{selectedTopic.Name}' hiện có mức độ thành thạo ước lượng {picked.Mastery:F0}%. Cần củng cố để đạt mốc vững vàng.";
            }
        }

        var currentMastery = twins.TryGetValue(selectedTopic.NodeId, out var selectedTwin) ? selectedTwin.MasteryPercentage : 0m;
        var targetMastery = isDiagnostic ? 60m : Math.Min(100m, Math.Max(60m, currentMastery + 15m));

        return new AdaptivePracticeProposalDto
        {
            SubjectId = subjectId,
            SubjectName = subject.Name,
            TopicNodeId = selectedTopic.NodeId,
            TopicName = selectedTopic.Name,
            FocusSkillDescription = $"Rèn luyện và củng cố kỹ năng vận dụng trong '{selectedTopic.Name}'",
            CurrentMastery = currentMastery,
            TargetMastery = targetMastery,
            SelectionReason = selectionReason,
            RecommendedTimeMinutes = 15,
            IsDiagnosticRecommended = isDiagnostic,
            DefaultMode = mode,
            AvailableTopics = topicOptions
        };
    }

    public async Task<AdaptivePracticeSessionDto> CreateSessionAsync(
        Guid centerId,
        Guid studentId,
        CreateAdaptivePracticeSessionRequest request,
        DateTime utcNow,
        CancellationToken ct)
    {
        // BR-04: Check if student has an existing active or paused session for this subject
        var existingSession = await _dbContext.AdaptivePracticeSessions
            .Include(s => s.Subject)
            .Include(s => s.TopicNode)
            .FirstOrDefaultAsync(s => s.CenterId == centerId
                && s.StudentId == studentId
                && s.SubjectId == request.SubjectId
                && (s.Status == AdaptivePracticeSessionStatus.InProgress || s.Status == AdaptivePracticeSessionStatus.Paused)
                && !s.IsDeleted, ct);

        if (existingSession != null)
        {
            // Resume if paused, and return
            if (existingSession.Status == AdaptivePracticeSessionStatus.Paused)
            {
                existingSession.Status = AdaptivePracticeSessionStatus.InProgress;
                existingSession.UpdatedAt = utcNow;
                await _dbContext.SaveChangesAsync(ct);
            }
            return MapToSessionDto(existingSession, utcNow);
        }

        var subject = await _dbContext.Subjects
            .AsNoTracking()
            .SingleOrDefaultAsync(s => s.CenterId == centerId && s.SubjectId == request.SubjectId && !s.IsDeleted, ct)
            ?? throw new InvalidOperationException($"Subject '{request.SubjectId}' not found.");

        var topic = await _dbContext.KnowledgeNodes
            .AsNoTracking()
            .SingleOrDefaultAsync(kn => kn.CenterId == centerId && kn.NodeId == request.TopicNodeId && !kn.IsDeleted, ct)
            ?? throw new InvalidOperationException($"TopicNode '{request.TopicNodeId}' not found.");

        var twin = await _dbContext.KnowledgeTwins
            .AsNoTracking()
            .SingleOrDefaultAsync(kt => kt.CenterId == centerId && kt.StudentId == studentId && kt.TopicNodeId == request.TopicNodeId && !kt.IsDeleted, ct);

        decimal startMastery = twin?.MasteryPercentage ?? 0m;
        bool isDiagnostic = twin == null || twin.EvidenceCount == 0;
        decimal targetMastery = request.CustomTargetMastery ?? (isDiagnostic ? 60m : Math.Min(100m, Math.Max(60m, startMastery + 15m)));

        // Question limits configuration based on minutes (BR-03, Section V UC-02)
        int minQuestions = request.TimeBudgetMinutes switch
        {
            <= 10 => 4,
            <= 15 => 5,
            _ => 6
        };

        int maxQuestions = request.TimeBudgetMinutes switch
        {
            <= 10 => 8,
            <= 15 => 12,
            _ => 15
        };

        byte initialDifficulty = isDiagnostic ? (byte)3 : (byte)Math.Clamp((int)(startMastery / 20m) + 1, 1, 5);

        string reason = isDiagnostic
            ? $"Phiên chẩn đoán năng lực ban đầu cho '{topic.Name}'."
            : $"Phiên luyện tập thích ứng củng cố '{topic.Name}' từ {startMastery:F0}% lên mục tiêu {targetMastery:F0}%.";

        var session = new AdaptivePracticeSession
        {
            SessionId = Guid.NewGuid(),
            CenterId = centerId,
            StudentId = studentId,
            SubjectId = request.SubjectId,
            TopicNodeId = request.TopicNodeId,
            LearningPathId = request.LearningPathId,
            LearningPathItemId = request.LearningPathItemId,
            PracticeMode = request.PracticeMode,
            StartingMastery = startMastery,
            TargetMastery = targetMastery,
            CurrentMastery = startMastery,
            TimeBudgetMinutes = request.TimeBudgetMinutes,
            MinimumQuestions = minQuestions,
            MaximumQuestions = maxQuestions,
            QuestionCount = 0,
            Status = AdaptivePracticeSessionStatus.InProgress,
            CloseReason = null,
            SelectionReason = reason,
            PolicyVersion = "1.0",
            CurrentDifficulty = initialDifficulty,
            ConsecutiveCorrect = 0,
            ConsecutiveIncorrect = 0,
            IsDiagnosticMode = isDiagnostic,
            DiagnosticQuestionsCount = 0,
            PrerequisiteTestedNodeId = null,
            StartedAt = utcNow,
            CreatedAt = utcNow,
            CreatedBy = studentId,
            UpdatedAt = utcNow
        };

        _dbContext.AdaptivePracticeSessions.Add(session);
        await _dbContext.SaveChangesAsync(ct);

        session.Subject = subject;
        session.TopicNode = topic;

        return MapToSessionDto(session, utcNow);
    }

    public async Task<AdaptivePracticeSessionDto?> GetActiveSessionAsync(
        Guid centerId,
        Guid studentId,
        Guid? subjectId,
        CancellationToken ct)
    {
        var query = _dbContext.AdaptivePracticeSessions
            .Include(s => s.Subject)
            .Include(s => s.TopicNode)
            .Where(s => s.CenterId == centerId
                && s.StudentId == studentId
                && (s.Status == AdaptivePracticeSessionStatus.InProgress || s.Status == AdaptivePracticeSessionStatus.Paused)
                && !s.IsDeleted);

        if (subjectId.HasValue)
        {
            query = query.Where(s => s.SubjectId == subjectId.Value);
        }

        var session = await query
            .OrderByDescending(s => s.StartedAt ?? s.CreatedAt)
            .FirstOrDefaultAsync(ct);

        return session == null ? null : MapToSessionDto(session, DateTime.UtcNow);
    }

    public async Task<AdaptivePracticeSessionDto?> GetSessionByIdAsync(
        Guid centerId,
        Guid studentId,
        Guid sessionId,
        CancellationToken ct)
    {
        var session = await _dbContext.AdaptivePracticeSessions
            .Include(s => s.Subject)
            .Include(s => s.TopicNode)
            .SingleOrDefaultAsync(s => s.CenterId == centerId
                && s.StudentId == studentId
                && s.SessionId == sessionId
                && !s.IsDeleted, ct);

        return session == null ? null : MapToSessionDto(session, DateTime.UtcNow);
    }

    public async Task<AdaptivePracticeSessionDto> ResumeSessionAsync(
        Guid centerId,
        Guid studentId,
        Guid sessionId,
        DateTime utcNow,
        CancellationToken ct)
    {
        var session = await _dbContext.AdaptivePracticeSessions
            .Include(s => s.Subject)
            .Include(s => s.TopicNode)
            .SingleOrDefaultAsync(s => s.CenterId == centerId
                && s.StudentId == studentId
                && s.SessionId == sessionId
                && !s.IsDeleted, ct)
            ?? throw new InvalidOperationException("Session not found.");

        if (session.Status == AdaptivePracticeSessionStatus.Paused)
        {
            session.Status = AdaptivePracticeSessionStatus.InProgress;
            session.UpdatedAt = utcNow;
            await _dbContext.SaveChangesAsync(ct);
        }

        return MapToSessionDto(session, utcNow);
    }

    public async Task<AdaptivePracticeSessionDto> PauseSessionAsync(
        Guid centerId,
        Guid studentId,
        Guid sessionId,
        DateTime utcNow,
        CancellationToken ct)
    {
        var session = await _dbContext.AdaptivePracticeSessions
            .Include(s => s.Subject)
            .Include(s => s.TopicNode)
            .SingleOrDefaultAsync(s => s.CenterId == centerId
                && s.StudentId == studentId
                && s.SessionId == sessionId
                && !s.IsDeleted, ct)
            ?? throw new InvalidOperationException("Session not found.");

        if (session.Status == AdaptivePracticeSessionStatus.InProgress)
        {
            session.Status = AdaptivePracticeSessionStatus.Paused;
            session.PausedAt = utcNow;
            session.UpdatedAt = utcNow;
            await _dbContext.SaveChangesAsync(ct);
        }

        return MapToSessionDto(session, utcNow);
    }

    public async Task<AdaptivePracticeSummaryDto> CloseSessionAsync(
        Guid centerId,
        Guid studentId,
        Guid sessionId,
        AdaptivePracticeCloseReason closeReason,
        DateTime utcNow,
        CancellationToken ct)
    {
        var session = await _dbContext.AdaptivePracticeSessions
            .Include(s => s.Items)
            .Include(s => s.TopicNode)
            .SingleOrDefaultAsync(s => s.CenterId == centerId
                && s.StudentId == studentId
                && s.SessionId == sessionId
                && !s.IsDeleted, ct)
            ?? throw new InvalidOperationException("Session not found.");

        session.Status = AdaptivePracticeSessionStatus.Closed;
        session.CloseReason = closeReason;
        session.CompletedAt = utcNow;
        session.UpdatedAt = utcNow;

        var summary = await GenerateAndPersistSummaryAsync(session, utcNow, ct);
        await EvaluateLearningPathCompletionAsync(session, utcNow, ct);

        await _dbContext.SaveChangesAsync(ct);
        return summary;
    }

    public async Task<AdaptivePracticeQuestionDto?> GetNextQuestionAsync(
        Guid centerId,
        Guid studentId,
        Guid sessionId,
        DateTime utcNow,
        CancellationToken ct)
    {
        var session = await _dbContext.AdaptivePracticeSessions
            .Include(s => s.Items)
            .Include(s => s.TopicNode)
            .SingleOrDefaultAsync(s => s.CenterId == centerId
                && s.StudentId == studentId
                && s.SessionId == sessionId
                && !s.IsDeleted, ct);

        if (session == null || session.Status != AdaptivePracticeSessionStatus.InProgress)
        {
            return null;
        }

        // BR-05: If there is already an item presented and unanswered, return that item!
        var existingPresented = session.Items.FirstOrDefault(i => i.Status == AdaptiveSessionItemStatus.Presented);
        if (existingPresented != null)
        {
            return await BuildQuestionDtoAsync(centerId, existingPresented, session, ct);
        }

        // Check if session reached max question limit
        if (session.QuestionCount >= session.MaximumQuestions)
        {
            await CloseSessionAsync(centerId, studentId, sessionId, AdaptivePracticeCloseReason.QuestionLimitReached, utcNow, ct);
            return null;
        }

        // Multi-Stage Question Selector (UC-04)
        // Stage 1: Filter eligible questions (active, not deleted, same center & subject)
        var protectedAssignmentQuestionIds = await _dbContext.AssignmentQuestions
            .Where(aq => aq.Assignment.CenterId == centerId
                && (aq.Assignment.Status == AssignmentStatus.Published || aq.Assignment.Status == AssignmentStatus.Draft)
                && !aq.Assignment.IsDeleted)
            .Select(aq => aq.QuestionId)
            .Distinct()
            .ToListAsync(ct);

        var protectedSet = new HashSet<ulong>(protectedAssignmentQuestionIds);
        var sessionPresentedQuestionIds = new HashSet<ulong>(session.Items.Select(i => i.QuestionId));

        ulong targetNodeId = session.PrerequisiteTestedNodeId ?? session.TopicNodeId;

        // Stage 2: Determine target difficulty and selection code
        byte targetDifficulty;
        string selectionReasonCode;
        string selectionReasonText;

        if (session.IsDiagnosticMode && session.DiagnosticQuestionsCount < 2)
        {
            targetDifficulty = 3;
            selectionReasonCode = AdaptivePracticeSelectionReasonCode.DiagnosticProbe;
            selectionReasonText = "Câu hỏi chẩn đoán xác định trình độ và mức độ nắm bắt ban đầu.";
        }
        else if (session.PrerequisiteTestedNodeId.HasValue)
        {
            targetDifficulty = Math.Clamp((byte)(session.CurrentDifficulty - 1), (byte)1, (byte)3);
            selectionReasonCode = AdaptivePracticeSelectionReasonCode.PrerequisiteProbe;
            selectionReasonText = "Kiểm tra và củng cố kiến thức nền liên quan trước khi tiếp tục chủ đề chính.";
        }
        else if (session.ConsecutiveCorrect >= 2)
        {
            targetDifficulty = Math.Clamp((byte)(session.CurrentDifficulty + 1), (byte)1, (byte)5);
            selectionReasonCode = AdaptivePracticeSelectionReasonCode.DifficultyAdvancement;
            selectionReasonText = "Nâng độ khó nhằm thử thách năng lực sau chuỗi câu trả lời chính xác ổn định.";
        }
        else if (session.ConsecutiveIncorrect >= 2)
        {
            targetDifficulty = Math.Clamp((byte)(session.CurrentDifficulty - 1), (byte)1, (byte)5);
            selectionReasonCode = AdaptivePracticeSelectionReasonCode.RetryWithScaffolding;
            selectionReasonText = "Điều chỉnh độ khó phù hợp để củng cố lại phương pháp giải quyết vấn đề.";
        }
        else if (session.ConsecutiveCorrect == 1)
        {
            targetDifficulty = session.CurrentDifficulty;
            selectionReasonCode = AdaptivePracticeSelectionReasonCode.StabilityConfirmation;
            selectionReasonText = "Kiểm tra độ thành thạo và tính ổn định qua dạng câu hỏi tương đương.";
        }
        else
        {
            targetDifficulty = session.CurrentDifficulty;
            selectionReasonCode = AdaptivePracticeSelectionReasonCode.GapRecovery;
            selectionReasonText = "Luyện tập củng cố kỹ năng trọng tâm của chủ đề mục tiêu.";
        }

        // Fetch candidate questions for the node
        var candidateQuestions = await _dbContext.Questions
            .Where(q => q.CenterId == centerId
                && q.SubjectId == session.SubjectId
                && q.PrimaryTopicNodeId == targetNodeId
                && q.Status == QuestionStatus.Active
                && !q.IsDeleted)
            .ToListAsync(ct);

        // Exclude protected and already used in current session
        var availablePool = candidateQuestions
            .Where(q => !protectedSet.Contains(q.QuestionId) && !sessionPresentedQuestionIds.Contains(q.QuestionId))
            .ToList();

        // If candidate pool for prerequisite is empty, fallback to primary topic
        if (availablePool.Count == 0 && session.PrerequisiteTestedNodeId.HasValue)
        {
            targetNodeId = session.TopicNodeId;
            session.PrerequisiteTestedNodeId = null;
            availablePool = candidateQuestions
                .Where(q => q.PrimaryTopicNodeId == targetNodeId && !protectedSet.Contains(q.QuestionId) && !sessionPresentedQuestionIds.Contains(q.QuestionId))
                .ToList();
        }

        if (availablePool.Count == 0)
        {
            // BR-14: Out of questions in bank
            await CloseSessionAsync(centerId, studentId, sessionId, AdaptivePracticeCloseReason.OutOfQuestions, utcNow, ct);
            return null;
        }

        // Stage 3: Rank candidates
        // Get attempt history for student in this center to prioritize unattempted
        var pastAttempts = await _dbContext.Attempts
            .Where(a => a.CenterId == centerId && a.StudentId == studentId)
            .Select(a => new { a.QuestionId, a.CreatedAt })
            .ToListAsync(ct);

        var lastAttemptTime = pastAttempts
            .GroupBy(a => a.QuestionId)
            .ToDictionary(g => g.Key, g => g.Max(x => x.CreatedAt));

        var chosenQuestion = availablePool
            .OrderBy(q => lastAttemptTime.ContainsKey(q.QuestionId) ? 1 : 0) // unattempted first
            .ThenBy(q => Math.Abs(q.Difficulty - targetDifficulty))          // difficulty closeness
            .ThenBy(q => lastAttemptTime.GetValueOrDefault(q.QuestionId, DateTime.MinValue)) // least recently attempted
            .ThenBy(q => q.QuestionId)                                      // deterministic tie break
            .First();

        // Update session difficulty state
        session.CurrentDifficulty = chosenQuestion.Difficulty;
        session.UpdatedAt = utcNow;

        // Stage 4: Create SessionItem
        var sessionItem = new AdaptivePracticeSessionItem
        {
            SessionItemId = AdaptivePracticeIdGenerator.NewId(),
            CenterId = centerId,
            SessionId = sessionId,
            QuestionId = chosenQuestion.QuestionId,
            KnowledgeNodeId = targetNodeId,
            Difficulty = chosenQuestion.Difficulty,
            SequenceNumber = session.QuestionCount + 1,
            SelectionReasonCode = selectionReasonCode,
            SelectionReasonText = selectionReasonText,
            QuestionVersion = "1.0",
            PresentedAt = utcNow,
            Status = AdaptiveSessionItemStatus.Presented,
            LastAttemptId = null,
            HintsUsedCount = 0,
            CreatedAt = utcNow,
            CreatedBy = studentId
        };

        _dbContext.AdaptivePracticeSessionItems.Add(sessionItem);
        await _dbContext.SaveChangesAsync(ct);

        sessionItem.Question = chosenQuestion;
        var node = await _dbContext.KnowledgeNodes.AsNoTracking().SingleOrDefaultAsync(kn => kn.CenterId == centerId && kn.NodeId == targetNodeId, ct);
        sessionItem.KnowledgeNode = node!;

        return await BuildQuestionDtoAsync(centerId, sessionItem, session, ct);
    }

    public async Task<AdaptivePracticeHintDto> GetHintAsync(
        Guid centerId,
        Guid studentId,
        Guid sessionId,
        ulong sessionItemId,
        int requestedLevel,
        CancellationToken ct)
    {
        var item = await _dbContext.AdaptivePracticeSessionItems
            .Include(i => i.Question)
            .Include(i => i.KnowledgeNode)
            .SingleOrDefaultAsync(i => i.CenterId == centerId && i.SessionId == sessionId && i.SessionItemId == sessionItemId, ct)
            ?? throw new InvalidOperationException("Session item not found.");

        item.HintsUsedCount = Math.Max(item.HintsUsedCount, requestedLevel);
        await _dbContext.SaveChangesAsync(ct);

        return AdaptivePracticeHintGenerator.GenerateHint(item.Question, item.KnowledgeNode, requestedLevel);
    }

    public async Task<AdaptivePracticeAnswerFeedbackDto> SubmitAnswerAsync(
        Guid centerId,
        Guid studentId,
        Guid sessionId,
        SubmitAdaptivePracticeAnswerRequest request,
        DateTime utcNow,
        CancellationToken ct)
    {
        var session = await _dbContext.AdaptivePracticeSessions
            .Include(s => s.Items)
            .Include(s => s.TopicNode)
            .SingleOrDefaultAsync(s => s.CenterId == centerId && s.StudentId == studentId && s.SessionId == sessionId && !s.IsDeleted, ct)
            ?? throw new InvalidOperationException("Adaptive practice session not found.");

        if (session.Status != AdaptivePracticeSessionStatus.InProgress)
        {
            throw new InvalidOperationException("Cannot submit answer to a session that is not in progress.");
        }

        var item = session.Items.FirstOrDefault(i => i.SessionItemId == request.SessionItemId)
            ?? throw new InvalidOperationException("Session question item not found.");

        // Idempotency: check if an attempt with ClientSubmissionId was already saved
        var existingAttempt = await _dbContext.Attempts
            .AsNoTracking()
            .SingleOrDefaultAsync(a => a.CenterId == centerId && a.StudentId == studentId && a.ClientSubmissionId == request.ClientSubmissionId, ct);

        if (existingAttempt != null)
        {
            // Replay existing result
            return BuildFeedbackDto(existingAttempt.IsCorrect ?? false, existingAttempt.AwardedScore ?? 0m, item.Question, item.HintsUsedCount, session, null);
        }

        var question = await _dbContext.Questions
            .SingleOrDefaultAsync(q => q.CenterId == centerId && q.QuestionId == request.QuestionId && !q.IsDeleted, ct)
            ?? throw new InvalidOperationException("Question not found.");

        // Grade the answer
        bool isCorrect = EvaluateAnswer(question, request.FinalAnswer);
        decimal awardedScore = isCorrect ? question.MaxScore : 0m;

        // Persist Attempt (BR-15, Section VII.3)
        var attempt = new Attempt
        {
            AttemptId = AdaptivePracticeIdGenerator.NewId(),
            CenterId = centerId,
            StudentId = studentId,
            QuestionId = question.QuestionId,
            AssignmentId = null,
            FinalAnswer = request.FinalAnswer,
            ReasoningText = request.ReasoningText,
            IsCorrect = isCorrect,
            AwardedScore = awardedScore,
            PreliminaryGradingReasonCode = isCorrect ? "CORRECT_MATCH" : "INCORRECT_MATCH",
            TimeSpentSeconds = request.TimeSpentSeconds,
            Confidence = request.StudentConfidence ?? 0.8m,
            AnswerChanges = 0,
            Skipped = false,
            ReasoningLanguage = "vi",
            Status = AttemptStatus.Completed,
            ClientSubmissionId = request.ClientSubmissionId,
            CreatedAt = utcNow,
            CreatedBy = studentId,
            UpdatedAt = utcNow,
            RowVersion = 1ul
        };

        _dbContext.Attempts.Add(attempt);

        // Update SessionItem
        item.AnsweredAt = utcNow;
        item.Status = AdaptiveSessionItemStatus.Answered;
        item.LastAttemptId = attempt.AttemptId;

        // Update Session counters & adaptation logic (UC-06)
        session.QuestionCount++;
        session.UpdatedAt = utcNow;

        if (session.IsDiagnosticMode)
        {
            session.DiagnosticQuestionsCount++;
            if (session.DiagnosticQuestionsCount >= 1 && (isCorrect || session.DiagnosticQuestionsCount >= 2))
            {
                session.IsDiagnosticMode = false;
            }
        }

        if (isCorrect)
        {
            session.ConsecutiveCorrect++;
            session.ConsecutiveIncorrect = 0;

            if (session.PrerequisiteTestedNodeId.HasValue)
            {
                // Cleared prerequisite check! Return to main topic
                session.PrerequisiteTestedNodeId = null;
            }

            // Evidence weight adjusted by support level (BR-19)
            decimal masteryGain = item.HintsUsedCount switch
            {
                0 => 4.5m + (question.Difficulty * 0.5m), // Independent: full gain
                1 => 2.5m,                                // Level 1 hint
                _ => 1.0m                                 // Level 2 or 3 hint (scaffolded)
            };

            session.CurrentMastery = Math.Min(100m, session.CurrentMastery + masteryGain);
        }
        else
        {
            session.ConsecutiveIncorrect++;
            session.ConsecutiveCorrect = 0;

            // Controlled drop (BR-21: do not drop mastery heavily on 1 wrong answer)
            session.CurrentMastery = Math.Max(0m, session.CurrentMastery - 2.0m);

            // If consecutive errors >= 2, check for prerequisite to probe (AP-16, BR-23)
            if (session.ConsecutiveIncorrect >= 2 && !session.PrerequisiteTestedNodeId.HasValue)
            {
                var prereqEdge = await _dbContext.KnowledgeEdges
                    .AsNoTracking()
                    .Where(e => e.CenterId == centerId
                        && e.SubjectId == session.SubjectId
                        && e.TargetNodeId == session.TopicNodeId
                        && e.RelationType == RelationType.PrerequisiteOf
                        && !e.IsDeleted)
                    .OrderBy(e => e.Weight)
                    .FirstOrDefaultAsync(ct);

                if (prereqEdge != null)
                {
                    session.PrerequisiteTestedNodeId = prereqEdge.SourceNodeId;
                }
            }
        }

        // Sync with KnowledgeTwin in Digital Twin (anti-duplicate)
        var twin = await _dbContext.KnowledgeTwins
            .SingleOrDefaultAsync(kt => kt.CenterId == centerId
                && kt.StudentId == studentId
                && kt.TopicNodeId == session.TopicNodeId
                && !kt.IsDeleted, ct);

        if (twin == null)
        {
            twin = new KnowledgeTwin
            {
                KnowledgeTwinId = AdaptivePracticeIdGenerator.NewId(),
                CenterId = centerId,
                StudentId = studentId,
                SubjectId = session.SubjectId,
                TopicNodeId = session.TopicNodeId,
                MasteryPercentage = session.CurrentMastery,
                EvidenceCount = 1,
                LastAttemptId = attempt.AttemptId,
                LastEvidenceAt = utcNow,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            };
            _dbContext.KnowledgeTwins.Add(twin);
        }
        else
        {
            twin.MasteryPercentage = session.CurrentMastery;
            twin.EvidenceCount++;
            twin.LastAttemptId = attempt.AttemptId;
            twin.LastEvidenceAt = utcNow;
            twin.UpdatedAt = utcNow;
        }

        // Check Close Conditions (UC-08)
        bool shouldClose = false;
        AdaptivePracticeCloseReason? closeReason = null;

        // END01: Target reached with sufficient evidence (min questions met & consecutive correct >= 2)
        if (session.CurrentMastery >= session.TargetMastery && session.QuestionCount >= session.MinimumQuestions && session.ConsecutiveCorrect >= 2)
        {
            shouldClose = true;
            closeReason = AdaptivePracticeCloseReason.GoalAchievedWithEvidence;
        }
        // END02: Hard question limit reached
        else if (session.QuestionCount >= session.MaximumQuestions)
        {
            shouldClose = true;
            closeReason = AdaptivePracticeCloseReason.QuestionLimitReached;
        }
        // END04: Remediation needed (repeated failure on prerequisite)
        else if (session.ConsecutiveIncorrect >= 3)
        {
            shouldClose = true;
            closeReason = AdaptivePracticeCloseReason.RemediationNeeded;
        }

        AdaptivePracticeSummaryDto? summaryDto = null;
        if (shouldClose && closeReason.HasValue)
        {
            session.Status = AdaptivePracticeSessionStatus.Closed;
            session.CloseReason = closeReason;
            session.CompletedAt = utcNow;
            summaryDto = await GenerateAndPersistSummaryAsync(session, utcNow, ct);
            await EvaluateLearningPathCompletionAsync(session, utcNow, ct);
        }

        await _dbContext.SaveChangesAsync(ct);

        return BuildFeedbackDto(isCorrect, awardedScore, question, item.HintsUsedCount, session, summaryDto);
    }

    public async Task<AdaptivePracticeSummaryDto?> GetSummaryAsync(
        Guid centerId,
        Guid studentId,
        Guid sessionId,
        CancellationToken ct)
    {
        var summary = await _dbContext.AdaptivePracticeSessionSummaries
            .Include(s => s.RecommendedNextTopic)
            .SingleOrDefaultAsync(s => s.CenterId == centerId && s.SessionId == sessionId && !s.IsDeleted, ct);

        if (summary == null) return null;

        return new AdaptivePracticeSummaryDto
        {
            SessionId = summary.SessionId,
            TotalQuestions = summary.TotalQuestions,
            CorrectCount = summary.CorrectCount,
            IncorrectCount = summary.IncorrectCount,
            PartialCount = summary.PartialCount,
            PendingCount = summary.PendingCount,
            TimeSpentSeconds = summary.TimeSpentSeconds,
            StartingMastery = summary.StartingMastery,
            EndingMastery = summary.EndingMastery,
            GoalAchievementStatus = summary.GoalAchievementStatus,
            StrongSkills = DeserializeStringList(summary.StrongSkillIds),
            WeakSkills = DeserializeStringList(summary.WeakSkillIds),
            RemediationSkills = DeserializeStringList(summary.RemediationSkillIds),
            SummaryConclusion = summary.SummaryConclusion,
            RecommendedNextAction = summary.RecommendedNextAction,
            RecommendedNextTopicId = summary.RecommendedNextTopicId,
            RecommendedNextTopicName = summary.RecommendedNextTopic?.Name
        };
    }

    public async Task<IReadOnlyList<AdaptivePracticeHistoryItemDto>> GetHistoryAsync(
        Guid centerId,
        Guid studentId,
        Guid? subjectId,
        int limit,
        CancellationToken ct)
    {
        var query = _dbContext.AdaptivePracticeSessions
            .AsNoTracking()
            .Include(s => s.Subject)
            .Include(s => s.TopicNode)
            .Where(s => s.CenterId == centerId && s.StudentId == studentId && s.Status == AdaptivePracticeSessionStatus.Closed && !s.IsDeleted);

        if (subjectId.HasValue)
        {
            query = query.Where(s => s.SubjectId == subjectId.Value);
        }

        var list = await query
            .OrderByDescending(s => s.CompletedAt ?? s.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

        var sessionIds = list.Select(s => s.SessionId).ToList();
        var summaries = await _dbContext.AdaptivePracticeSessionSummaries
            .AsNoTracking()
            .Where(sum => sum.CenterId == centerId && sessionIds.Contains(sum.SessionId))
            .ToDictionaryAsync(sum => sum.SessionId, ct);

        return list.Select(s =>
        {
            summaries.TryGetValue(s.SessionId, out var sum);
            return new AdaptivePracticeHistoryItemDto
            {
                SessionId = s.SessionId,
                SubjectName = s.Subject.Name,
                TopicName = s.TopicNode.Name,
                PracticeMode = s.PracticeMode,
                TotalQuestions = sum?.TotalQuestions ?? s.QuestionCount,
                CorrectCount = sum?.CorrectCount ?? 0,
                StartingMastery = s.StartingMastery,
                EndingMastery = s.CurrentMastery,
                Status = s.Status,
                CloseReason = s.CloseReason,
                CompletedAt = s.CompletedAt,
                TimeSpentSeconds = sum?.TimeSpentSeconds ?? 0
            };
        }).ToList();
    }

    public async Task<IReadOnlyList<TeacherAdaptivePracticeSessionDto>> GetTeacherSessionsAsync(
        Guid centerId,
        Guid teacherId,
        Guid? classId,
        Guid? subjectId,
        int limit,
        CancellationToken ct)
    {
        var query = _dbContext.AdaptivePracticeSessions
            .AsNoTracking()
            .Include(s => s.Student).ThenInclude(st => st.User)
            .Include(s => s.Subject)
            .Include(s => s.TopicNode)
            .Include(s => s.Summary)
            .Where(s => s.CenterId == centerId && s.Status == AdaptivePracticeSessionStatus.Closed && !s.IsDeleted);

        if (subjectId.HasValue)
        {
            query = query.Where(s => s.SubjectId == subjectId.Value);
        }

        var list = await query
            .OrderByDescending(s => s.CompletedAt ?? s.CreatedAt)
            .Take(limit)
            .ToListAsync(ct);

        return list.Select(s => new TeacherAdaptivePracticeSessionDto
        {
            SessionId = s.SessionId,
            StudentId = s.StudentId,
            StudentName = s.Student.User?.FullName ?? "Học sinh",
            ClassName = "Lớp học",
            SubjectName = s.Subject.Name,
            TopicName = s.TopicNode.Name,
            TotalQuestions = s.Summary?.TotalQuestions ?? s.QuestionCount,
            CorrectCount = s.Summary?.CorrectCount ?? 0,
            GoalAchievementStatus = s.Summary?.GoalAchievementStatus ?? (s.CloseReason == AdaptivePracticeCloseReason.GoalAchievedWithEvidence ? "Achieved" : "NotAchieved"),
            CloseReason = s.CloseReason,
            CompletedAt = s.CompletedAt,
            RequiresIntervention = s.CloseReason == AdaptivePracticeCloseReason.RemediationNeeded,
            InterventionReason = s.CloseReason == AdaptivePracticeCloseReason.RemediationNeeded ? "Học sinh gặp khó khăn liên tục ở kiến thức nền và cần được củng cố bổ trợ." : null
        }).ToList();
    }

    public async Task<IReadOnlyList<TeacherAdaptivePracticeAlertDto>> GetTeacherAlertsAsync(
        Guid centerId,
        Guid teacherId,
        Guid? classId,
        CancellationToken ct)
    {
        // Alerts for students who had remediation needed in last 7 days
        var thresholdDate = DateTime.UtcNow.AddDays(-7);
        var alertSessions = await _dbContext.AdaptivePracticeSessions
            .AsNoTracking()
            .Include(s => s.Student).ThenInclude(st => st.User)
            .Include(s => s.Subject)
            .Include(s => s.TopicNode)
            .Where(s => s.CenterId == centerId
                && s.CloseReason == AdaptivePracticeCloseReason.RemediationNeeded
                && s.CompletedAt >= thresholdDate
                && !s.IsDeleted)
            .OrderByDescending(s => s.CompletedAt)
            .Take(20)
            .ToListAsync(ct);

        return alertSessions.Select(s => new TeacherAdaptivePracticeAlertDto
        {
            AlertId = s.SessionId.ToString("N"),
            StudentId = s.StudentId,
            StudentName = s.Student.User?.FullName ?? "Học sinh",
            ClassName = "Lớp học",
            SubjectId = s.SubjectId,
            SubjectName = s.Subject.Name,
            TopicNodeId = s.TopicNodeId,
            TopicName = s.TopicNode.Name,
            AlertType = "PREREQUISITE_DEFICIT",
            Description = $"Học sinh gặp lỗi lặp lại trong phiên '{s.TopicNode.Name}' dù đã nhận gợi ý. Hệ thống kiến nghị giáo viên can thiệp hoặc giao bài củng cố kiến thức nền.",
            CreatedAt = s.CompletedAt ?? s.UpdatedAt
        }).ToList();
    }

    // --- Private Helpers ---

    private static AdaptivePracticeSessionDto MapToSessionDto(AdaptivePracticeSession s, DateTime utcNow)
    {
        int elapsed = (int)((s.CompletedAt ?? utcNow) - (s.StartedAt ?? s.CreatedAt)).TotalSeconds;
        return new AdaptivePracticeSessionDto
        {
            SessionId = s.SessionId,
            StudentId = s.StudentId,
            CenterId = s.CenterId,
            SubjectId = s.SubjectId,
            SubjectName = s.Subject?.Name ?? "",
            TopicNodeId = s.TopicNodeId,
            TopicName = s.TopicNode?.Name ?? "",
            PracticeMode = s.PracticeMode,
            StartingMastery = s.StartingMastery,
            TargetMastery = s.TargetMastery,
            CurrentMastery = s.CurrentMastery,
            TimeBudgetMinutes = s.TimeBudgetMinutes,
            MinimumQuestions = s.MinimumQuestions,
            MaximumQuestions = s.MaximumQuestions,
            QuestionCount = s.QuestionCount,
            Status = s.Status,
            CloseReason = s.CloseReason,
            SelectionReason = s.SelectionReason,
            StartedAt = s.StartedAt,
            PausedAt = s.PausedAt,
            CompletedAt = s.CompletedAt,
            ElapsedSeconds = Math.Max(0, elapsed),
            IsDiagnosticMode = s.IsDiagnosticMode,
            CurrentDifficulty = s.CurrentDifficulty
        };
    }

    private async Task<AdaptivePracticeQuestionDto> BuildQuestionDtoAsync(
        Guid centerId,
        AdaptivePracticeSessionItem item,
        AdaptivePracticeSession session,
        CancellationToken ct)
    {
        var question = item.Question ?? await _dbContext.Questions
            .SingleAsync(q => q.CenterId == centerId && q.QuestionId == item.QuestionId, ct);

        var node = item.KnowledgeNode ?? await _dbContext.KnowledgeNodes
            .SingleAsync(kn => kn.CenterId == centerId && kn.NodeId == item.KnowledgeNodeId, ct);

        var options = await _dbContext.QuestionOptions
            .AsNoTracking()
            .Where(o => o.CenterId == centerId && o.QuestionId == question.QuestionId && !o.IsDeleted)
            .OrderBy(o => o.OrderIndex)
            .Select(o => new StudentQuestionOptionDto
            {
                OptionId = o.OptionId.ToString(CultureInfo.InvariantCulture),
                Label = o.Label,
                Text = o.Text,
                OrderIndex = o.OrderIndex
            })
            .ToListAsync(ct);

        return new AdaptivePracticeQuestionDto
        {
            SessionItemId = item.SessionItemId,
            QuestionId = question.QuestionId,
            TopicNodeId = node.NodeId,
            TopicName = node.Name,
            Difficulty = question.Difficulty,
            QuestionText = question.QuestionText,
            QuestionType = question.QuestionType,
            Options = options,
            HasImage = question.HasImage,
            MaxScore = question.MaxScore,
            EstimatedTimeSeconds = question.EstimatedTimeSeconds,
            SelectionReasonCode = item.SelectionReasonCode,
            SelectionReasonText = item.SelectionReasonText,
            SequenceNumber = item.SequenceNumber,
            TotalQuestionsEstimatedMin = session.MinimumQuestions,
            TotalQuestionsEstimatedMax = session.MaximumQuestions,
            AvailableHintsCount = 3,
            HintsUsedCount = item.HintsUsedCount,
            IsPrerequisiteCheck = session.PrerequisiteTestedNodeId.HasValue && session.PrerequisiteTestedNodeId.Value == node.NodeId
        };
    }

    private static bool EvaluateAnswer(Question question, string submittedAnswer)
    {
        if (string.IsNullOrWhiteSpace(submittedAnswer)) return false;
        var cleanSubmitted = submittedAnswer.Trim();
        var cleanCorrect = question.CorrectAnswer.Trim();

        // Exact match
        if (string.Equals(cleanSubmitted, cleanCorrect, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Numeric equivalence check (e.g. "3.0" vs "3", "0.5" vs "1/2")
        if (double.TryParse(cleanSubmitted, NumberStyles.Float, CultureInfo.InvariantCulture, out var subVal) &&
            double.TryParse(cleanCorrect, NumberStyles.Float, CultureInfo.InvariantCulture, out var corVal))
        {
            return Math.Abs(subVal - corVal) < 0.001;
        }

        return false;
    }

    private static AdaptivePracticeAnswerFeedbackDto BuildFeedbackDto(
        bool isCorrect,
        decimal score,
        Question question,
        int hintsUsed,
        AdaptivePracticeSession session,
        AdaptivePracticeSummaryDto? summary)
    {
        string stepGuidance = isCorrect
            ? "Tuyệt vời! Bạn đã hoàn thành câu hỏi này một cách chính xác."
            : (!string.IsNullOrWhiteSpace(question.Solution)
                ? $"Lời giải tham khảo:\n{question.Solution}"
                : "Hãy kiểm tra lại từng bước biến đổi đại số và quy tắc dấu.");

        string nextAction = session.Status == AdaptivePracticeSessionStatus.Closed
            ? "Phiên luyện tập đã kết thúc. Xem tổng kết năng lực để tiếp tục lộ trình học."
            : (isCorrect
                ? "Sẵn sàng cho câu hỏi tiếp theo để củng cố độ thành thạo."
                : "Hệ thống sẽ điều chỉnh câu hỏi tiếp theo ở mức độ phù hợp hơn.");

        return new AdaptivePracticeAnswerFeedbackDto
        {
            IsCorrect = isCorrect,
            AwardedScore = score,
            CorrectAnswer = question.CorrectAnswer,
            SolutionExplanation = question.Solution,
            StepGuidance = stepGuidance,
            HintsUsed = hintsUsed,
            MisconceptionHypothesis = !isCorrect && hintsUsed == 0 ? "Có thể bạn còn nhầm lẫn trong quy tắc biến đổi vế hoặc dấu âm." : null,
            NextActionRecommendation = nextAction,
            IsSessionClosed = session.Status == AdaptivePracticeSessionStatus.Closed,
            CloseReason = session.CloseReason,
            SessionSummary = summary,
            UpdatedEstimatedMastery = session.CurrentMastery
        };
    }

    private async Task<AdaptivePracticeSummaryDto> GenerateAndPersistSummaryAsync(
        AdaptivePracticeSession session,
        DateTime utcNow,
        CancellationToken ct)
    {
        var items = await _dbContext.AdaptivePracticeSessionItems
            .Include(i => i.Question)
            .Where(i => i.CenterId == session.CenterId && i.SessionId == session.SessionId)
            .ToListAsync(ct);

        var attempts = await _dbContext.Attempts
            .AsNoTracking()
            .Where(a => a.CenterId == session.CenterId && a.StudentId == session.StudentId)
            .ToListAsync(ct);

        var attemptMap = attempts.ToDictionary(a => a.AttemptId);

        int total = items.Count;
        int correct = 0;
        int incorrect = 0;
        int partial = 0;
        int timeSpent = 0;

        foreach (var item in items)
        {
            if (item.LastAttemptId.HasValue && attemptMap.TryGetValue(item.LastAttemptId.Value, out var att))
            {
                timeSpent += (int)att.TimeSpentSeconds;
                if (att.IsCorrect == true) correct++;
                else incorrect++;
            }
        }

        string goalStatus = session.CloseReason switch
        {
            AdaptivePracticeCloseReason.GoalAchievedWithEvidence => "Achieved",
            AdaptivePracticeCloseReason.RemediationNeeded => "RemediationRecommended",
            AdaptivePracticeCloseReason.QuestionLimitReached => session.CurrentMastery >= session.TargetMastery ? "Achieved" : "PartiallyAchieved",
            _ => "PartiallyAchieved"
        };

        var strongSkills = new List<string>();
        var weakSkills = new List<string>();
        var remediationSkills = new List<string>();

        if (correct > 0) strongSkills.Add($"Vận dụng kiến thức cơ bản trong {session.TopicNode.Name}");
        if (session.ConsecutiveCorrect >= 2) strongSkills.Add("Tính toán và suy luận độc lập ổn định");

        if (incorrect > 0) weakSkills.Add($"Các dạng câu hỏi chứa biến thể nâng cao của {session.TopicNode.Name}");
        if (session.CloseReason == AdaptivePracticeCloseReason.RemediationNeeded)
        {
            remediationSkills.Add($"Kiến thức nền tảng bổ trợ cho {session.TopicNode.Name}");
        }

        string conclusion = goalStatus switch
        {
            "Achieved" => $"Chúc mừng! Bạn đã hoàn thành xuất sắc phiên luyện tập và đạt mục tiêu năng lực {session.CurrentMastery:F0}%. Bằng chứng học tập đã được cập nhật vào hồ sơ Digital Twin.",
            "RemediationRecommended" => $"Bạn đã rất nỗ lực, tuy nhiên một số kiến thức nền liên quan cần được ôn tập lại để tránh lặp lại lỗi sai. EduTwin đề xuất bạn xem lại bài giảng bổ trợ.",
            _ => $"Phiên luyện tập đã kết thúc với {correct}/{total} câu đúng. Năng lực hiện tại đạt {session.CurrentMastery:F0}%."
        };

        string nextAction = goalStatus switch
        {
            "Achieved" => "Quay lại Lộ trình học tập để tiếp tục chặng kiến thức tiếp theo.",
            "RemediationRecommended" => "Thực hiện phiên củng cố kiến thức nền 5–10 phút.",
            _ => "Luyện tập thêm một phiên ngắn để củng cố vững chắc mục tiêu năng lực."
        };

        var summary = new AdaptivePracticeSessionSummary
        {
            SessionId = session.SessionId,
            CenterId = session.CenterId,
            StudentId = session.StudentId,
            TotalQuestions = total,
            CorrectCount = correct,
            IncorrectCount = incorrect,
            PartialCount = partial,
            PendingCount = 0,
            TimeSpentSeconds = timeSpent > 0 ? timeSpent : (int)(utcNow - (session.StartedAt ?? session.CreatedAt)).TotalSeconds,
            StartingMastery = session.StartingMastery,
            EndingMastery = session.CurrentMastery,
            GoalAchievementStatus = goalStatus,
            StrongSkillIds = JsonSerializer.Serialize(strongSkills),
            WeakSkillIds = JsonSerializer.Serialize(weakSkills),
            RemediationSkillIds = JsonSerializer.Serialize(remediationSkills),
            SummaryConclusion = conclusion,
            RecommendedNextAction = nextAction,
            RecommendedNextTopicId = null,
            SummaryStatus = "Finalized",
            CreatedAt = utcNow,
            CreatedBy = session.StudentId,
            UpdatedAt = utcNow
        };

        _dbContext.AdaptivePracticeSessionSummaries.Add(summary);
        return new AdaptivePracticeSummaryDto
        {
            SessionId = summary.SessionId,
            TotalQuestions = summary.TotalQuestions,
            CorrectCount = summary.CorrectCount,
            IncorrectCount = summary.IncorrectCount,
            PartialCount = summary.PartialCount,
            PendingCount = summary.PendingCount,
            TimeSpentSeconds = summary.TimeSpentSeconds,
            StartingMastery = summary.StartingMastery,
            EndingMastery = summary.EndingMastery,
            GoalAchievementStatus = summary.GoalAchievementStatus,
            StrongSkills = strongSkills,
            WeakSkills = weakSkills,
            RemediationSkills = remediationSkills,
            SummaryConclusion = summary.SummaryConclusion,
            RecommendedNextAction = summary.RecommendedNextAction,
            RecommendedNextTopicId = null,
            RecommendedNextTopicName = null
        };
    }

    private async Task EvaluateLearningPathCompletionAsync(AdaptivePracticeSession session, DateTime utcNow, CancellationToken ct)
    {
        // Anti-Jitter Rule (Section II.2, VIII.3): Practice session updates item status if goal achieved, but does NOT regenerate whole LP.
        if (session.LearningPathId.HasValue && session.CloseReason == AdaptivePracticeCloseReason.GoalAchievedWithEvidence)
        {
            var item = await _dbContext.LearningPathItems
                .SingleOrDefaultAsync(i => i.CenterId == session.CenterId
                    && i.LearningPathId == session.LearningPathId.Value
                    && i.TopicNodeId == session.TopicNodeId
                    && !i.IsDeleted, ct);

            if (item != null && item.Status == LearningPathItemStatus.Current)
            {
                item.Status = LearningPathItemStatus.Completed;
                item.UpdatedAt = utcNow;
            }
        }
    }

    private static IReadOnlyList<string> DeserializeStringList(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try
        {
            return JsonSerializer.Deserialize<List<string>>(json) ?? [];
        }
        catch
        {
            return [];
        }
    }
}
