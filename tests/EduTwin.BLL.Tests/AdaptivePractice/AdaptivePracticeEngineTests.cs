using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using EduTwin.BLL.AdaptivePractice;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AdaptivePractice;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.Contracts.Assignments;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.DigitalTwin;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Tests.AdaptivePractice;

public sealed class AdaptivePracticeEngineTests : IDisposable
{
    private readonly EduTwinDbContext _dbContext;
    private readonly TenantContext _tenantContext;
    private readonly AdaptivePracticeEngine _engine;

    private readonly Guid _centerId = Guid.NewGuid();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Guid _subjectId = Guid.NewGuid();
    private readonly ulong _mainTopicId = 101;
    private readonly ulong _prereqTopicId = 100;
    private readonly DateTime _utcNow = new(2026, 10, 16, 12, 0, 0, DateTimeKind.Utc);

    public AdaptivePracticeEngineTests()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(databaseName: $"AdaptivePracticeEngineTests_{Guid.NewGuid():N}")
            .Options;

        _tenantContext = new TenantContext();
        _tenantContext.Initialize(_centerId, _studentId, "Student", 1);
        _dbContext = new EduTwinDbContext(options, _tenantContext);
        _engine = new AdaptivePracticeEngine(_dbContext);

        SeedInitialData();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private void SeedInitialData()
    {
        var center = new Center
        {
            CenterId = _centerId,
            Code = "CTR_TEST",
            Name = "Center Test",
            Status = CenterStatus.Active,
            CreatedAt = _utcNow
        };
        _dbContext.Centers.Add(center);

        var student = new Student
        {
            CenterId = _centerId,
            StudentId = _studentId,
            GradeLevel = 10,
            EnrollmentDate = _utcNow,
            Status = StudentStatus.Active,
            CreatedAt = _utcNow
        };
        _dbContext.Students.Add(student);

        var subject = new Subject
        {
            CenterId = _centerId,
            SubjectId = _subjectId,
            Name = "Toán Học",
            Code = "MATH10",
            Status = SubjectStatus.Active,
            CreatedAt = _utcNow
        };
        _dbContext.Subjects.Add(subject);

        var prereqTopic = new KnowledgeNode
        {
            CenterId = _centerId,
            SubjectId = _subjectId,
            NodeId = _prereqTopicId,
            Name = "Quy tắc chuyển vế và đổi dấu",
            Status = NodeStatus.Active,
            Difficulty = 1,
            CreatedAt = _utcNow
        };
        _dbContext.KnowledgeNodes.Add(prereqTopic);

        var mainTopic = new KnowledgeNode
        {
            CenterId = _centerId,
            SubjectId = _subjectId,
            NodeId = _mainTopicId,
            Name = "Phương trình bậc nhất một ẩn",
            Status = NodeStatus.Active,
            Difficulty = 2,
            CreatedAt = _utcNow
        };
        _dbContext.KnowledgeNodes.Add(mainTopic);

        // Prerequisite edge: prereqTopic is prerequisite of mainTopic
        var edge = new KnowledgeEdge
        {
            EdgeId = 1,
            CenterId = _centerId,
            SubjectId = _subjectId,
            SourceNodeId = _prereqTopicId,
            TargetNodeId = _mainTopicId,
            RelationType = RelationType.PrerequisiteOf,
            Weight = 1.0m,
            CreatedAt = _utcNow
        };
        _dbContext.KnowledgeEdges.Add(edge);

        // Seed 4 questions for main topic (diff 2, 3, 4)
        for (ulong i = 1; i <= 5; i++)
        {
            _dbContext.Questions.Add(new Question
            {
                CenterId = _centerId,
                SubjectId = _subjectId,
                PrimaryTopicNodeId = _mainTopicId,
                QuestionId = i,
                Difficulty = (byte)(i <= 2 ? 2 : (i <= 4 ? 3 : 4)),
                Status = QuestionStatus.Active,
                QuestionText = $"Phương trình bậc nhất số {i}: 2x + {i} = 10. Tìm x?",
                QuestionType = QuestionType.ShortAnswer,
                CorrectAnswer = $"{5 - (int)i / 2}",
                Solution = $"Bước 1: Chuyển {i} sang vế phải đổi dấu thành -{i}.\nBước 2: 2x = {10 - (int)i}.\nBước 3: x = {5 - (int)i / 2}.",
                ExpectedReasoning = "Áp dụng quy tắc chuyển vế đổi dấu và chia hai vế cho hệ số a.",
                LanguageCode = "vi",
                MaxScore = 10m,
                EstimatedTimeSeconds = 60,
                CreatedAt = _utcNow
            });
        }

        // Seed 2 questions for prerequisite topic
        for (ulong i = 11; i <= 12; i++)
        {
            _dbContext.Questions.Add(new Question
            {
                CenterId = _centerId,
                SubjectId = _subjectId,
                PrimaryTopicNodeId = _prereqTopicId,
                QuestionId = i,
                Difficulty = 1,
                Status = QuestionStatus.Active,
                QuestionText = $"Khi chuyển -3 từ vế trái sang vế phải, ta đổi dấu thành gì? Câu {i}",
                QuestionType = QuestionType.ShortAnswer,
                CorrectAnswer = "+3",
                Solution = "Khi chuyển vế một số hạng, ta phải đổi dấu của số hạng đó: âm đổi thành dương.",
                ExpectedReasoning = "Quy tắc chuyển vế đổi dấu.",
                LanguageCode = "vi",
                MaxScore = 10m,
                EstimatedTimeSeconds = 30,
                CreatedAt = _utcNow
            });
        }

        _dbContext.SaveChanges();
    }

    [Fact]
    public async Task GetProposal_NoEvidence_ReturnsDiagnosticProposal()
    {
        var proposal = await _engine.GetProposalAsync(_centerId, _studentId, _subjectId, null, AdaptivePracticeMode.Weakness, CancellationToken.None);

        Assert.NotNull(proposal);
        Assert.Equal(_subjectId, proposal.SubjectId);
        Assert.True(proposal.IsDiagnosticRecommended);
        Assert.Contains("chẩn đoán", proposal.SelectionReason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(60m, proposal.TargetMastery);
    }

    [Fact]
    public async Task CreateSession_CreatesValidSession_WithConfiguredLimits()
    {
        var request = new CreateAdaptivePracticeSessionRequest
        {
            SubjectId = _subjectId,
            TopicNodeId = _mainTopicId,
            PracticeMode = AdaptivePracticeMode.Weakness,
            TimeBudgetMinutes = 15
        };

        var session = await _engine.CreateSessionAsync(_centerId, _studentId, request, _utcNow, CancellationToken.None);

        Assert.NotNull(session);
        Assert.Equal(AdaptivePracticeSessionStatus.InProgress, session.Status);
        Assert.Equal(5, session.MinimumQuestions);
        Assert.Equal(12, session.MaximumQuestions);
        Assert.Equal(_mainTopicId, session.TopicNodeId);
        Assert.True(session.IsDiagnosticMode);
    }

    [Fact]
    public async Task CreateSession_ExistingActiveSession_ResumesAndReturnsSameSession()
    {
        var request = new CreateAdaptivePracticeSessionRequest
        {
            SubjectId = _subjectId,
            TopicNodeId = _mainTopicId,
            TimeBudgetMinutes = 15
        };

        var firstSession = await _engine.CreateSessionAsync(_centerId, _studentId, request, _utcNow, CancellationToken.None);
        var secondSession = await _engine.CreateSessionAsync(_centerId, _studentId, request, _utcNow.AddMinutes(2), CancellationToken.None);

        Assert.Equal(firstSession.SessionId, secondSession.SessionId);
    }

    [Fact]
    public async Task GetNextQuestion_SelectsAppropriateQuestion_AndRegistersItem()
    {
        var createRequest = new CreateAdaptivePracticeSessionRequest
        {
            SubjectId = _subjectId,
            TopicNodeId = _mainTopicId,
            TimeBudgetMinutes = 15
        };

        var session = await _engine.CreateSessionAsync(_centerId, _studentId, createRequest, _utcNow, CancellationToken.None);
        var q = await _engine.GetNextQuestionAsync(_centerId, _studentId, session.SessionId, _utcNow, CancellationToken.None);

        Assert.NotNull(q);
        Assert.Equal(1, q.SequenceNumber);
        Assert.Equal(_mainTopicId, q.TopicNodeId);
        Assert.Equal(AdaptivePracticeSelectionReasonCode.DiagnosticProbe, q.SelectionReasonCode);

        // Calling GetNextQuestion again before answering returns the SAME question (BR-05)
        var qAgain = await _engine.GetNextQuestionAsync(_centerId, _studentId, session.SessionId, _utcNow, CancellationToken.None);
        Assert.Equal(q.SessionItemId, qAgain?.SessionItemId);
    }

    [Fact]
    public async Task SubmitAnswer_Correct_UpdatesMasteryAndStreak()
    {
        var createRequest = new CreateAdaptivePracticeSessionRequest
        {
            SubjectId = _subjectId,
            TopicNodeId = _mainTopicId,
            TimeBudgetMinutes = 15
        };

        var session = await _engine.CreateSessionAsync(_centerId, _studentId, createRequest, _utcNow, CancellationToken.None);
        var q = await _engine.GetNextQuestionAsync(_centerId, _studentId, session.SessionId, _utcNow, CancellationToken.None);
        Assert.NotNull(q);

        var questionEntity = await _dbContext.Questions.FindAsync(q.QuestionId);
        var submitRequest = new SubmitAdaptivePracticeAnswerRequest
        {
            ClientSubmissionId = Guid.NewGuid(),
            SessionItemId = q.SessionItemId,
            QuestionId = q.QuestionId,
            FinalAnswer = questionEntity!.CorrectAnswer,
            TimeSpentSeconds = 45
        };

        var feedback = await _engine.SubmitAnswerAsync(_centerId, _studentId, session.SessionId, submitRequest, _utcNow, CancellationToken.None);

        Assert.True(feedback.IsCorrect);
        Assert.True(feedback.UpdatedEstimatedMastery > 0m);

        var sessionDb = await _dbContext.AdaptivePracticeSessions.FindAsync(session.SessionId);
        Assert.Equal(1, sessionDb!.QuestionCount);
        Assert.Equal(1, sessionDb.ConsecutiveCorrect);
        Assert.Equal(0, sessionDb.ConsecutiveIncorrect);
    }

    [Fact]
    public async Task SubmitAnswer_WithHintsUsed_GivesDiscountedMastery()
    {
        var createRequest = new CreateAdaptivePracticeSessionRequest
        {
            SubjectId = _subjectId,
            TopicNodeId = _mainTopicId,
            TimeBudgetMinutes = 15
        };

        var session = await _engine.CreateSessionAsync(_centerId, _studentId, createRequest, _utcNow, CancellationToken.None);
        var q = await _engine.GetNextQuestionAsync(_centerId, _studentId, session.SessionId, _utcNow, CancellationToken.None);
        Assert.NotNull(q);

        // Student requests level 2 hint
        var hint = await _engine.GetHintAsync(_centerId, _studentId, session.SessionId, q.SessionItemId, 2, CancellationToken.None);
        Assert.Equal(2, hint.HintLevel);

        var questionEntity = await _dbContext.Questions.FindAsync(q.QuestionId);
        var submitRequest = new SubmitAdaptivePracticeAnswerRequest
        {
            ClientSubmissionId = Guid.NewGuid(),
            SessionItemId = q.SessionItemId,
            QuestionId = q.QuestionId,
            FinalAnswer = questionEntity!.CorrectAnswer,
            TimeSpentSeconds = 50
        };

        var feedback = await _engine.SubmitAnswerAsync(_centerId, _studentId, session.SessionId, submitRequest, _utcNow, CancellationToken.None);

        Assert.True(feedback.IsCorrect);
        Assert.Equal(2, feedback.HintsUsed);
        Assert.Equal(1.0m, feedback.UpdatedEstimatedMastery); // Discounted gain for Level 2 hint
    }

    [Fact]
    public async Task SubmitAnswer_ConsecutiveErrors_ProbesPrerequisiteNode()
    {
        var createRequest = new CreateAdaptivePracticeSessionRequest
        {
            SubjectId = _subjectId,
            TopicNodeId = _mainTopicId,
            TimeBudgetMinutes = 15
        };

        var session = await _engine.CreateSessionAsync(_centerId, _studentId, createRequest, _utcNow, CancellationToken.None);

        // Question 1 wrong
        var q1 = await _engine.GetNextQuestionAsync(_centerId, _studentId, session.SessionId, _utcNow, CancellationToken.None);
        await _engine.SubmitAnswerAsync(_centerId, _studentId, session.SessionId, new SubmitAdaptivePracticeAnswerRequest
        {
            ClientSubmissionId = Guid.NewGuid(),
            SessionItemId = q1!.SessionItemId,
            QuestionId = q1.QuestionId,
            FinalAnswer = "WRONG_1",
            TimeSpentSeconds = 30
        }, _utcNow, CancellationToken.None);

        // Question 2 wrong
        var q2 = await _engine.GetNextQuestionAsync(_centerId, _studentId, session.SessionId, _utcNow, CancellationToken.None);
        await _engine.SubmitAnswerAsync(_centerId, _studentId, session.SessionId, new SubmitAdaptivePracticeAnswerRequest
        {
            ClientSubmissionId = Guid.NewGuid(),
            SessionItemId = q2!.SessionItemId,
            QuestionId = q2.QuestionId,
            FinalAnswer = "WRONG_2",
            TimeSpentSeconds = 30
        }, _utcNow, CancellationToken.None);

        var sessionDb = await _dbContext.AdaptivePracticeSessions.FindAsync(session.SessionId);
        Assert.Equal(2, sessionDb!.ConsecutiveIncorrect);
        Assert.Equal(_prereqTopicId, sessionDb.PrerequisiteTestedNodeId);

        // Question 3 should now be from the prerequisite node!
        var q3 = await _engine.GetNextQuestionAsync(_centerId, _studentId, session.SessionId, _utcNow, CancellationToken.None);
        Assert.NotNull(q3);
        Assert.Equal(_prereqTopicId, q3.TopicNodeId);
        Assert.Equal(AdaptivePracticeSelectionReasonCode.PrerequisiteProbe, q3.SelectionReasonCode);
    }

    [Fact]
    public async Task ProtectedQuestions_InPublishedAssignments_AreNotSelected()
    {
        // Put question 1 into a published assignment
        var assignment = new Assignment
        {
            AssignmentId = Guid.NewGuid(),
            CenterId = _centerId,
            SubjectId = _subjectId,
            Title = "Bài kiểm tra 15 phút",
            Status = AssignmentStatus.Published,
            CreatedAt = _utcNow
        };
        _dbContext.Assignments.Add(assignment);

        _dbContext.AssignmentQuestions.Add(new AssignmentQuestion
        {
            AssignmentId = assignment.AssignmentId,
            QuestionId = 1,
            OrderIndex = 0,
            CreatedAt = _utcNow
        });
        await _dbContext.SaveChangesAsync();

        var createRequest = new CreateAdaptivePracticeSessionRequest
        {
            SubjectId = _subjectId,
            TopicNodeId = _mainTopicId,
            TimeBudgetMinutes = 15
        };

        var session = await _engine.CreateSessionAsync(_centerId, _studentId, createRequest, _utcNow, CancellationToken.None);
        var q = await _engine.GetNextQuestionAsync(_centerId, _studentId, session.SessionId, _utcNow, CancellationToken.None);

        Assert.NotNull(q);
        // Question 1 must NOT be selected!
        Assert.NotEqual((ulong)1, q.QuestionId);
    }

    [Fact]
    public async Task CloseSession_GeneratesComprehensiveSummary_WithSkillsBreakdown()
    {
        var createRequest = new CreateAdaptivePracticeSessionRequest
        {
            SubjectId = _subjectId,
            TopicNodeId = _mainTopicId,
            TimeBudgetMinutes = 15
        };

        var session = await _engine.CreateSessionAsync(_centerId, _studentId, createRequest, _utcNow, CancellationToken.None);
        var summary = await _engine.CloseSessionAsync(_centerId, _studentId, session.SessionId, AdaptivePracticeCloseReason.StudentStopped, _utcNow, CancellationToken.None);

        Assert.NotNull(summary);
        Assert.Equal(session.SessionId, summary.SessionId);
        Assert.NotEmpty(summary.SummaryConclusion);
        Assert.NotEmpty(summary.RecommendedNextAction);

        var sessionDb = await _dbContext.AdaptivePracticeSessions.FindAsync(session.SessionId);
        Assert.Equal(AdaptivePracticeSessionStatus.Closed, sessionDb!.Status);
        Assert.Equal(AdaptivePracticeCloseReason.StudentStopped, sessionDb.CloseReason);
    }
}
