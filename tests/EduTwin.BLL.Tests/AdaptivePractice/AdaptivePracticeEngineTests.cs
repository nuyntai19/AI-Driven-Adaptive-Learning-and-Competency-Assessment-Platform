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
using EduTwin.Contracts.Organization;
using EduTwin.DAL.Assignments;
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
        _dbContext = new AdaptivePracticeTestDbContext(options, _tenantContext);
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
            CenterCode = "CTR_TEST",
            CenterName = "Center Test",
            Timezone = "Asia/Ho_Chi_Minh",
            Status = CenterStatus.Active,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        };
        _dbContext.Centers.Add(center);

        var student = new Student
        {
            CenterId = _centerId,
            StudentId = _studentId,
            FullName = "Test student",
            GradeLevel = 10,
            CreatedAt = _utcNow
        };
        _dbContext.Students.Add(student);

        var subject = new Subject
        {
            CenterId = _centerId,
            SubjectId = _subjectId,
            SubjectName = "Toán Học",
            SubjectCode = "MATH10",
            IsActive = true,
            CreatedAt = _utcNow
        };
        _dbContext.Subjects.Add(subject);

        var prereqTopic = new KnowledgeNode
        {
            NodeType = NodeType.Topic,
            CenterId = _centerId,
            SubjectId = _subjectId,
            NodeId = _prereqTopicId,
            NodeName = "Quy tắc chuyển vế và đổi dấu",
            NodeCode = "AP_PREREQ",
            IsActive = true,
            CreatedAt = _utcNow
        };
        _dbContext.KnowledgeNodes.Add(prereqTopic);

        var mainTopic = new KnowledgeNode
        {
            NodeType = NodeType.Topic,
            CenterId = _centerId,
            SubjectId = _subjectId,
            NodeId = _mainTopicId,
            NodeName = "Phương trình bậc nhất một ẩn",
            NodeCode = "AP_MAIN",
            IsActive = true,
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
                GradeLevel = 10,
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
                GradeLevel = 10,
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

        foreach (var entry in _dbContext.ChangeTracker.Entries())
        {
            if (entry.Entity is EduTwin.DAL.Persistence.Models.IMutableTenantAggregate aggregate)
                aggregate.UpdatedAt = _utcNow;
        }
        GraphScopeFixture.Approve(_dbContext, _centerId, _subjectId, 10, prereqTopic.NodeId, mainTopic.NodeId);
        _dbContext.SaveChanges();
    }

    [Fact]
    public async Task GetProposal_OnlyOffersTopicsInTheStudentsGradeScope()
    {
        _dbContext.KnowledgeNodes.Add(new KnowledgeNode
        {
            NodeType = NodeType.Topic, CenterId = _centerId, SubjectId = _subjectId, NodeId = 777, NodeName = "Grade 12 topic",
            NodeCode = "G12", IsActive = true, CreatedAt = _utcNow, UpdatedAt = _utcNow
        });
        await _dbContext.SaveChangesAsync();

        var proposal = await _engine.GetProposalAsync(_centerId, _studentId, _subjectId, null, AdaptivePracticeMode.Weakness, null, CancellationToken.None);

        Assert.DoesNotContain(proposal.AvailableTopics, t => t.TopicNodeId == 777);
        Assert.Null(proposal.UnavailableReason);
    }

    [Fact]
    public async Task GetProposal_WithoutPracticeableQuestions_ExplainsInsteadOfProposing()
    {
        await _dbContext.Questions.ForEachAsync(q => q.Status = QuestionStatus.Draft);
        await _dbContext.SaveChangesAsync();

        var proposal = await _engine.GetProposalAsync(_centerId, _studentId, _subjectId, null, AdaptivePracticeMode.Weakness, null, CancellationToken.None);

        Assert.NotNull(proposal.UnavailableReason);
        Assert.Empty(proposal.AvailableTopics);
    }

    [Fact]
    public async Task GetProposal_SpacedReview_NothingStudied_ExplainsThereIsNothingToReview()
    {
        var proposal = await _engine.GetProposalAsync(_centerId, _studentId, _subjectId, null, AdaptivePracticeMode.SpacedReview, null, CancellationToken.None);

        Assert.NotNull(proposal.UnavailableReason);
    }

    [Fact]
    public async Task GetProposal_SpacedReview_PicksTheOverdueStudiedTopic()
    {
        _dbContext.KnowledgeTwins.Add(new EduTwin.DAL.DigitalTwin.KnowledgeTwin
        {
            KnowledgeTwinId = 5001, CenterId = _centerId, StudentId = _studentId, SubjectId = _subjectId,
            TopicNodeId = _mainTopicId, MasteryPercentage = 60m, EvidenceCount = 3,
            LastEvidenceAt = DateTime.UtcNow.AddDays(-30), CreatedAt = _utcNow, UpdatedAt = _utcNow
        });
        await _dbContext.SaveChangesAsync();

        var proposal = await _engine.GetProposalAsync(_centerId, _studentId, _subjectId, null, AdaptivePracticeMode.SpacedReview, null, CancellationToken.None);

        Assert.Equal(_mainTopicId, proposal.TopicNodeId);
        Assert.False(proposal.IsDiagnosticRecommended);
        Assert.Contains("đã đến hạn", proposal.SelectionReason);
        Assert.Equal(65m, proposal.TargetMastery);
    }

    [Fact]
    public async Task CloseSession_Twice_ReturnsTheSameSummaryWithoutFailing()
    {
        var session = await StartSessionAsync();

        var first = await _engine.CloseSessionAsync(_centerId, _studentId, session.SessionId, AdaptivePracticeCloseReason.OutOfQuestions, _utcNow, CancellationToken.None);
        var second = await _engine.CloseSessionAsync(_centerId, _studentId, session.SessionId, AdaptivePracticeCloseReason.StudentStopped, _utcNow.AddMinutes(1), CancellationToken.None);

        Assert.Equal(first.SessionId, second.SessionId);
        Assert.Equal(1, await _dbContext.AdaptivePracticeSessionSummaries.CountAsync());
        Assert.Equal(AdaptivePracticeCloseReason.OutOfQuestions, (await _dbContext.AdaptivePracticeSessions.SingleAsync()).CloseReason);
    }

    private async Task AddMainTopicQuestionsAsync(int count)
    {
        var template = await _dbContext.Questions.AsNoTracking().FirstAsync(q => q.PrimaryTopicNodeId == _mainTopicId);
        for (ulong i = 0; i < (ulong)count; i++)
        {
            template.QuestionId = 90_000 + i;
            _dbContext.Questions.Add(new EduTwin.DAL.CurriculumAndQuestions.Question
            {
                QuestionId = template.QuestionId, CenterId = template.CenterId, SubjectId = template.SubjectId,
                PrimaryTopicNodeId = template.PrimaryTopicNodeId, GradeLevel = template.GradeLevel, Difficulty = template.Difficulty,
                Status = template.Status, QuestionText = template.QuestionText, QuestionType = template.QuestionType,
                CorrectAnswer = template.CorrectAnswer, Solution = template.Solution, ExpectedReasoning = template.ExpectedReasoning,
                LanguageCode = template.LanguageCode, MaxScore = template.MaxScore, EstimatedTimeSeconds = template.EstimatedTimeSeconds,
                CreatedAt = _utcNow, UpdatedAt = _utcNow
            });
        }

        await _dbContext.SaveChangesAsync();
    }

    private async Task<AdaptivePracticeAnswerFeedbackDto> AnswerCurrentAsync(Guid sessionId, bool correctly, DateTime? at = null)
    {
        var q = await _engine.GetNextQuestionAsync(_centerId, _studentId, sessionId, _utcNow, CancellationToken.None);
        var answer = correctly ? (await _dbContext.Questions.FindAsync(q!.QuestionId))!.CorrectAnswer : "khong-dung";
        return await _engine.SubmitAnswerAsync(_centerId, _studentId, sessionId, new SubmitAdaptivePracticeAnswerRequest
        {
            ClientSubmissionId = Guid.NewGuid(), SessionItemId = q!.SessionItemId, QuestionId = q.QuestionId, FinalAnswer = answer, TimeSpentSeconds = 10
        }, at ?? _utcNow, CancellationToken.None);
    }

    [Fact]
    public async Task SubmitAnswer_OnPrerequisiteProbe_CreditsThePrerequisiteNotTheSessionTopic()
    {
        var session = await StartSessionAsync();
        await AnswerCurrentAsync(session.SessionId, correctly: false);
        await AnswerCurrentAsync(session.SessionId, correctly: false);
        var masteryBeforeProbe = (await _dbContext.AdaptivePracticeSessions.SingleAsync()).CurrentMastery;

        await AnswerCurrentAsync(session.SessionId, correctly: true);

        var saved = await _dbContext.AdaptivePracticeSessions.SingleAsync();
        var prerequisite = await _dbContext.KnowledgeTwins.SingleAsync(t => t.TopicNodeId == _prereqTopicId);
        Assert.Equal(masteryBeforeProbe, saved.CurrentMastery);
        Assert.True(prerequisite.MasteryPercentage > 0m);
    }

    [Fact]
    public async Task SubmitAnswer_RecordsPracticeInTwinHistory()
    {
        var session = await StartSessionAsync();

        await AnswerCurrentAsync(session.SessionId, correctly: true);

        var history = await _dbContext.TwinUpdateHistories.SingleAsync();
        var twin = await _dbContext.KnowledgeTwins.SingleAsync();
        Assert.Equal(_mainTopicId, history.TopicNodeId);
        Assert.Equal(twin.MasteryPercentage, history.NewMastery);
        Assert.Equal(history.NewMastery - history.PreviousMastery, history.MasteryDelta);
        Assert.NotNull(history.AttemptId);
    }

    [Fact]
    public async Task SubmitAnswer_PastTimeBudget_ClosesSession()
    {
        var session = await StartSessionAsync();

        var feedback = await AnswerCurrentAsync(session.SessionId, correctly: true, _utcNow.AddHours(2));

        Assert.True(feedback.IsSessionClosed);
        Assert.Equal(AdaptivePracticeCloseReason.TimeBudgetReached, feedback.CloseReason);
    }

    [Fact]
    public async Task GetHint_OfAnotherStudentsSession_IsRejected()
    {
        var session = await StartSessionAsync();
        var q = await _engine.GetNextQuestionAsync(_centerId, _studentId, session.SessionId, _utcNow, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _engine.GetHintAsync(_centerId, Guid.NewGuid(), session.SessionId, q!.SessionItemId, 3, CancellationToken.None));

        Assert.Equal(0, (await _dbContext.AdaptivePracticeSessionItems.SingleAsync()).HintsUsedCount);
    }

    [Fact]
    public void Hints_DoNotRevealTheAnswerAtLevelOne()
    {
        var question = new EduTwin.DAL.CurriculumAndQuestions.Question
        {
            ExpectedReasoning = "Delta = 16 - 16 = 0. Vay phuong trinh co nghiem kep.",
            Solution = "Tinh delta.\nDelta = 0.",
            CorrectAnswer = "0"
        };

        var level1 = AdaptivePracticeHintGenerator.GenerateHint(question, null, 1).HintContent;
        var level2 = AdaptivePracticeHintGenerator.GenerateHint(question, null, 2).HintContent;
        var level3 = AdaptivePracticeHintGenerator.GenerateHint(question, null, 3).HintContent;

        Assert.DoesNotContain("16", level1);
        Assert.Contains("Tinh delta", level2);
        Assert.Contains("[kết quả cần tìm]", level3);
        Assert.DoesNotContain("= 0", level3);
    }

    [Fact]
    public async Task CreateSession_NeverPromisesMoreQuestionsThanTheBankHas()
    {
        var keep = await _dbContext.Questions.Where(q => q.PrimaryTopicNodeId == _mainTopicId).OrderBy(q => q.QuestionId).FirstAsync();
        await _dbContext.Questions.Where(q => q.PrimaryTopicNodeId == _mainTopicId && q.QuestionId != keep.QuestionId)
            .ForEachAsync(q => q.Status = QuestionStatus.Draft);
        await _dbContext.SaveChangesAsync();

        var session = await StartSessionAsync();

        Assert.Equal(1, session.MaximumQuestions);
        Assert.Equal(1, session.MinimumQuestions);
    }

    [Fact]
    public async Task GetProposal_NoEvidence_ReturnsDiagnosticProposal()
    {
        var proposal = await _engine.GetProposalAsync(_centerId, _studentId, _subjectId, null, AdaptivePracticeMode.Weakness, null, CancellationToken.None);

        Assert.NotNull(proposal);
        Assert.Equal(_subjectId, proposal.SubjectId);
        Assert.True(proposal.IsDiagnosticRecommended);
        Assert.Contains("chẩn đoán", proposal.SelectionReason, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(60m, proposal.TargetMastery);
    }

    [Fact]
    public async Task CreateSession_CreatesValidSession_WithConfiguredLimits()
    {
        await AddMainTopicQuestionsAsync(15);
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

    [Theory]
    [InlineData(10, 4, 8)]
    [InlineData(15, 5, 12)]
    [InlineData(20, 6, 15)]
    public async Task CreateSession_OfferedBudgets_RetainDesignedQuestionLimits(int minutes, int minimum, int maximum)
    {
        await AddMainTopicQuestionsAsync(15);
        var request = new CreateAdaptivePracticeSessionRequest
        {
            SubjectId = _subjectId, TopicNodeId = _mainTopicId, TimeBudgetMinutes = minutes
        };
        var session = await _engine.CreateSessionAsync(_centerId, _studentId, request, _utcNow, default);
        Assert.Equal(minimum, session.MinimumQuestions);
        Assert.Equal(maximum, session.MaximumQuestions);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(9)]
    [InlineData(11)]
    [InlineData(21)]
    [InlineData(int.MaxValue)]
    public async Task CreateSession_UnofferedBudget_RejectsBeforePersistence(int minutes)
    {
        var request = new CreateAdaptivePracticeSessionRequest
        {
            SubjectId = _subjectId, TopicNodeId = _mainTopicId, TimeBudgetMinutes = minutes
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _engine.CreateSessionAsync(_centerId, _studentId, request, _utcNow, default));
        Assert.Empty(await _dbContext.AdaptivePracticeSessions.ToListAsync());
    }

    [Theory]
    [InlineData("-0.01")]
    [InlineData("100.01")]
    public async Task CreateSession_OutOfRangeMastery_RejectsBeforePersistence(string target)
    {
        var request = new CreateAdaptivePracticeSessionRequest
        {
            SubjectId = _subjectId, TopicNodeId = _mainTopicId,
            CustomTargetMastery = decimal.Parse(target, System.Globalization.CultureInfo.InvariantCulture)
        };
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _engine.CreateSessionAsync(_centerId, _studentId, request, _utcNow, default));
        Assert.Empty(await _dbContext.AdaptivePracticeSessions.ToListAsync());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public async Task CreateSession_MasterBoundary_IsAccepted(int target)
    {
        var request = new CreateAdaptivePracticeSessionRequest
        {
            SubjectId = _subjectId, TopicNodeId = _mainTopicId, CustomTargetMastery = target
        };
        var session = await _engine.CreateSessionAsync(_centerId, _studentId, request, _utcNow, default);
        Assert.Equal(target, session.TargetMastery);
    }

    [Fact]
    public async Task CreateSession_InvalidRequest_DoesNotResumePausedSession()
    {
        var request = new CreateAdaptivePracticeSessionRequest { SubjectId = _subjectId, TopicNodeId = _mainTopicId };
        var session = await _engine.CreateSessionAsync(_centerId, _studentId, request, _utcNow, default);
        await _engine.PauseSessionAsync(_centerId, _studentId, session.SessionId, _utcNow.AddMinutes(1), default);
        request.TimeBudgetMinutes = 0;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _engine.CreateSessionAsync(_centerId, _studentId, request, _utcNow.AddMinutes(2), default));
        var saved = await _dbContext.AdaptivePracticeSessions.SingleAsync();
        Assert.Equal(AdaptivePracticeSessionStatus.Paused, saved.Status);
        Assert.Equal(_utcNow.AddMinutes(1), saved.UpdatedAt);
    }

    [Fact]
    public async Task CreateSession_TopicFromOtherSubject_DoesNotResumeExistingSession()
    {
        var request = new CreateAdaptivePracticeSessionRequest { SubjectId = _subjectId, TopicNodeId = _mainTopicId };
        var session = await _engine.CreateSessionAsync(_centerId, _studentId, request, _utcNow, default);
        await _engine.PauseSessionAsync(_centerId, _studentId, session.SessionId, _utcNow.AddMinutes(1), default);
        var otherSubject = Guid.NewGuid();
        _dbContext.KnowledgeNodes.Add(new KnowledgeNode
        {
            NodeType = NodeType.Topic,
            CenterId = _centerId, SubjectId = otherSubject, NodeId = 999, NodeName = "Other subject",
            NodeCode = "OTHER", IsActive = true, CreatedAt = _utcNow, UpdatedAt = _utcNow
        });
        await _dbContext.SaveChangesAsync();
        request.TopicNodeId = 999;
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _engine.CreateSessionAsync(_centerId, _studentId, request, _utcNow.AddMinutes(2), default));
        var saved = await _dbContext.AdaptivePracticeSessions.SingleAsync();
        Assert.Equal(AdaptivePracticeSessionStatus.Paused, saved.Status);
        Assert.Equal(_mainTopicId, saved.TopicNodeId);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CreateSession_InactiveAcademicResource_IsRejected(bool disableSubject)
    {
        if (disableSubject) (await _dbContext.Subjects.SingleAsync()).IsActive = false;
        else (await _dbContext.KnowledgeNodes.SingleAsync(n => n.NodeId == _mainTopicId)).IsActive = false;
        await _dbContext.SaveChangesAsync();
        var request = new CreateAdaptivePracticeSessionRequest { SubjectId = _subjectId, TopicNodeId = _mainTopicId };
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            _engine.CreateSessionAsync(_centerId, _studentId, request, _utcNow, default));
        Assert.Empty(await _dbContext.AdaptivePracticeSessions.ToListAsync());
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

    private Task<AdaptivePracticeSessionDto> StartSessionAsync() => _engine.CreateSessionAsync(
        _centerId, _studentId,
        new CreateAdaptivePracticeSessionRequest { SubjectId = _subjectId, TopicNodeId = _mainTopicId, TimeBudgetMinutes = 15 },
        _utcNow, CancellationToken.None);

    private void AddAllMainTopicQuestionsToPublishedAssignment(DateTime? dueAt)
    {
        var assignment = new Assignment
        {
            AssignmentId = Guid.NewGuid(),
            CenterId = _centerId,
            Title = "Bài kiểm tra",
            Status = AssignmentStatus.Published,
            DueAt = dueAt,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        };
        _dbContext.Assignments.Add(assignment);
        for (ulong id = 1; id <= 5; id++)
        {
            _dbContext.AssignmentQuestions.Add(new AssignmentQuestion
            {
                CenterId = _centerId,
                AssignmentId = assignment.AssignmentId,
                QuestionId = id,
                OrderIndex = (uint)id,
                CreatedAt = _utcNow
            });
        }
        _dbContext.SaveChanges();
    }

    [Fact]
    public async Task SubmitAnswer_QuestionNotIssuedForItem_IsRejected()
    {
        var session = await StartSessionAsync();
        var q = await _engine.GetNextQuestionAsync(_centerId, _studentId, session.SessionId, _utcNow, CancellationToken.None);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _engine.SubmitAnswerAsync(_centerId, _studentId, session.SessionId,
            new SubmitAdaptivePracticeAnswerRequest
            {
                ClientSubmissionId = Guid.NewGuid(),
                SessionItemId = q!.SessionItemId,
                QuestionId = q.QuestionId == 1 ? 2UL : 1UL,
                FinalAnswer = "1",
                TimeSpentSeconds = 10
            }, _utcNow, CancellationToken.None));
    }

    [Fact]
    public async Task SubmitAnswer_ResubmittedItem_ReplaysWithoutCountingTwice()
    {
        var session = await StartSessionAsync();
        var q = await _engine.GetNextQuestionAsync(_centerId, _studentId, session.SessionId, _utcNow, CancellationToken.None);
        var correct = (await _dbContext.Questions.FindAsync(q!.QuestionId))!.CorrectAnswer;
        SubmitAdaptivePracticeAnswerRequest Request(Guid id) => new()
        {
            ClientSubmissionId = id,
            SessionItemId = q.SessionItemId,
            QuestionId = q.QuestionId,
            FinalAnswer = correct,
            TimeSpentSeconds = 10
        };

        var submissionId = Guid.NewGuid();
        var first = await _engine.SubmitAnswerAsync(_centerId, _studentId, session.SessionId, Request(submissionId), _utcNow, CancellationToken.None);
        _dbContext.ChangeTracker.Clear();
        var sameId = await _engine.SubmitAnswerAsync(_centerId, _studentId, session.SessionId, Request(submissionId), _utcNow, CancellationToken.None);
        var newId = await _engine.SubmitAnswerAsync(_centerId, _studentId, session.SessionId, Request(Guid.NewGuid()), _utcNow, CancellationToken.None);

        Assert.True(first.IsCorrect);
        Assert.True(sameId.IsCorrect);
        Assert.True(newId.IsCorrect);
        var sessionDb = await _dbContext.AdaptivePracticeSessions.FindAsync(session.SessionId);
        Assert.Equal(1, sessionDb!.QuestionCount);
        Assert.Equal(first.UpdatedEstimatedMastery, newId.UpdatedEstimatedMastery);
    }

    [Fact]
    public async Task SubmitAnswer_LetsTheDatabaseAssignTheAttemptId()
    {
        var session = await StartSessionAsync();
        var q = await _engine.GetNextQuestionAsync(_centerId, _studentId, session.SessionId, _utcNow, CancellationToken.None);
        var correct = (await _dbContext.Questions.FindAsync(q!.QuestionId))!.CorrectAnswer;

        await _engine.SubmitAnswerAsync(_centerId, _studentId, session.SessionId, new SubmitAdaptivePracticeAnswerRequest
        {
            ClientSubmissionId = Guid.NewGuid(),
            SessionItemId = q.SessionItemId,
            QuestionId = q.QuestionId,
            FinalAnswer = correct,
            TimeSpentSeconds = 10
        }, _utcNow, CancellationToken.None);

        var attempt = await _dbContext.Attempts.SingleAsync();
        var item = await _dbContext.AdaptivePracticeSessionItems.SingleAsync(i => i.SessionItemId == q.SessionItemId);
        Assert.InRange(attempt.AttemptId, 1ul, 1_000ul);
        Assert.Equal(attempt.AttemptId, item.LastAttemptId);
    }

    [Fact]
    public async Task SubmitAnswer_UsesQuestionEvaluationMode()
    {
        var session = await StartSessionAsync();
        var q = await _engine.GetNextQuestionAsync(_centerId, _studentId, session.SessionId, _utcNow, CancellationToken.None);
        var entity = (await _dbContext.Questions.FindAsync(q!.QuestionId))!;
        entity.AnswerEvaluationMode = QuestionAnswerEvaluationMode.NumericRational;
        entity.CorrectAnswer = "1/2";
        await _dbContext.SaveChangesAsync();

        var feedback = await _engine.SubmitAnswerAsync(_centerId, _studentId, session.SessionId, new SubmitAdaptivePracticeAnswerRequest
        {
            ClientSubmissionId = Guid.NewGuid(),
            SessionItemId = q.SessionItemId,
            QuestionId = q.QuestionId,
            FinalAnswer = "0.5",
            TimeSpentSeconds = 10
        }, _utcNow, CancellationToken.None);

        Assert.True(feedback.IsCorrect);
    }

    [Fact]
    public async Task EssayQuestions_AreNotSelected()
    {
        foreach (var question in _dbContext.Questions.Where(q => q.PrimaryTopicNodeId == _mainTopicId))
            question.QuestionType = QuestionType.Essay;
        await _dbContext.SaveChangesAsync();

        var session = await StartSessionAsync();

        Assert.Null(await _engine.GetNextQuestionAsync(_centerId, _studentId, session.SessionId, _utcNow, CancellationToken.None));
    }

    [Fact]
    public async Task DiagnosticFallback_DoesNotUseOpenAssignmentQuestions()
    {
        AddAllMainTopicQuestionsToPublishedAssignment(_utcNow.AddDays(1));
        var session = await StartSessionAsync();

        Assert.Null(await _engine.GetNextQuestionAsync(_centerId, _studentId, session.SessionId, _utcNow, CancellationToken.None));
    }

    [Fact]
    public async Task DiagnosticFallback_UsesQuestionsOfClosedAssignments()
    {
        AddAllMainTopicQuestionsToPublishedAssignment(_utcNow.AddDays(-1));
        var session = await StartSessionAsync();

        Assert.NotNull(await _engine.GetNextQuestionAsync(_centerId, _studentId, session.SessionId, _utcNow, CancellationToken.None));
    }

    [Fact]
    public async Task ProtectedQuestions_InPublishedAssignments_AreNotSelected()
    {
        // Put question 1 into a published assignment
        var assignment = new Assignment
        {
            AssignmentId = Guid.NewGuid(),
            CenterId = _centerId,
            Title = "Bài kiểm tra 15 phút",
            Status = AssignmentStatus.Published,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        };
        _dbContext.Assignments.Add(assignment);

        _dbContext.AssignmentQuestions.Add(new AssignmentQuestion
        {
            CenterId = _centerId,
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

    private sealed class AdaptivePracticeTestDbContext(
        DbContextOptions<EduTwinDbContext> options, TenantContext tenantContext)
        : EduTwinDbContext(options, tenantContext);
}
