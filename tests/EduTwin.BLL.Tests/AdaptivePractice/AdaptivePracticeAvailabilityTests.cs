using EduTwin.Contracts.AdaptivePractice;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.DigitalTwin;
using EduTwin.DAL.Recommendations;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTwin.BLL.Tests.AdaptivePractice;

public sealed partial class AdaptivePracticeEngineTests
{
    private async Task SetTopicEvidenceAsync(ulong topicId, decimal mastery, int daysAgo = 0)
    {
        _dbContext.KnowledgeTwins.Add(new KnowledgeTwin
        {
            KnowledgeTwinId = topicId + 7000, CenterId = _centerId, StudentId = _studentId,
            SubjectId = _subjectId, TopicNodeId = topicId, MasteryPercentage = mastery, EvidenceCount = 9,
            LastEvidenceAt = DateTime.UtcNow.AddDays(-daysAgo), CreatedAt = _utcNow, UpdatedAt = _utcNow
        });
        await _dbContext.SaveChangesAsync();
    }

    [Theory]
    [InlineData(AssignmentStatus.Published, false)]
    [InlineData(AssignmentStatus.Published, true)]
    [InlineData(AssignmentStatus.Closed, false)]
    [InlineData(AssignmentStatus.Closed, true)]
    [InlineData(AssignmentStatus.Archived, true)]
    public async Task ReleasedAssignmentQuestions_AreOfferedAndDelivered_WithOrWithoutEvidence(AssignmentStatus status, bool hasEvidence)
    {
        if (hasEvidence) await SetTopicEvidenceAsync(_mainTopicId, 61m);
        AddAllMainTopicQuestionsToPublishedAssignment(status == AssignmentStatus.Published ? _utcNow.AddDays(-1) : null, status);

        var proposal = await _engine.GetProposalAsync(_centerId, _studentId, _subjectId, _mainTopicId, AdaptivePracticeMode.CustomTopic, null, default);
        var option = Assert.Single(proposal.AvailableTopics, t => t.TopicNodeId == _mainTopicId);
        Assert.True(option.CanPractice);
        Assert.Null(option.UnavailableReason);
        Assert.Null(proposal.UnavailableReason);
        Assert.Equal(5, option.AvailableQuestionCount);
        Assert.Equal(5, proposal.AvailableQuestionCount);

        var session = await StartSessionAsync();
        Assert.Equal(!hasEvidence, session.IsDiagnosticMode);
        Assert.Equal(5, session.MaximumQuestions);
        var question = await _engine.GetNextQuestionAsync(_centerId, _studentId, session.SessionId, _utcNow, default);
        Assert.NotNull(question);
        Assert.InRange(question.QuestionId, 1ul, 5ul);
    }

    [Theory]
    [InlineData(AssignmentStatus.Draft, true)]
    [InlineData(AssignmentStatus.Published, true)]
    [InlineData(AssignmentStatus.Published, false)]
    public async Task QuestionInSeveralAssignments_RemainsBlockedWhileAnyAssignmentIsOpen(AssignmentStatus openStatus, bool hasDueDate)
    {
        await SetTopicEvidenceAsync(_mainTopicId, 61m);
        AddAllMainTopicQuestionsToPublishedAssignment(_utcNow.AddDays(-1));
        AddAllMainTopicQuestionsToPublishedAssignment(null, AssignmentStatus.Closed);
        AddAllMainTopicQuestionsToPublishedAssignment(hasDueDate ? DateTime.UtcNow.AddDays(1) : null, openStatus);

        var proposal = await _engine.GetProposalAsync(_centerId, _studentId, _subjectId, _mainTopicId, AdaptivePracticeMode.CustomTopic, null, default);
        Assert.Equal(_mainTopicId, proposal.TopicNodeId);
        Assert.Contains("chưa đóng", proposal.UnavailableReason);
        Assert.Equal(0, proposal.AvailableQuestionCount);
        var option = Assert.Single(proposal.AvailableTopics, t => t.TopicNodeId == _mainTopicId);
        Assert.False(option.CanPractice);
        Assert.Equal(proposal.UnavailableReason, option.UnavailableReason);
        await Assert.ThrowsAsync<InvalidOperationException>(() => StartSessionAsync());
        Assert.Empty(await _dbContext.AdaptivePracticeSessions.ToListAsync());
    }

    [Fact]
    public async Task ExistingSession_DoesNotDeliverQuestionsAddedToAnotherOpenAssignment()
    {
        await SetTopicEvidenceAsync(_mainTopicId, 61m);
        AddAllMainTopicQuestionsToPublishedAssignment(_utcNow.AddDays(-1));
        var session = await StartSessionAsync();
        AddAllMainTopicQuestionsToPublishedAssignment(null);

        Assert.Null(await _engine.GetNextQuestionAsync(_centerId, _studentId, session.SessionId, _utcNow, default));
        Assert.Empty(await _dbContext.AdaptivePracticeSessionItems.ToListAsync());
    }

    [Fact]
    public async Task ReleasedQuestions_ArePartOfTheRegularPool_EvenWhenUnusedQuestionsExist()
    {
        await SetTopicEvidenceAsync(_mainTopicId, 78m);
        AddAllMainTopicQuestionsToPublishedAssignment(_utcNow.AddDays(-1));
        await AddMainTopicQuestionsAsync(1);

        var proposal = await _engine.GetProposalAsync(_centerId, _studentId, _subjectId, _mainTopicId, AdaptivePracticeMode.CustomTopic, null, default);
        Assert.Equal(6, proposal.AvailableQuestionCount);
        var session = await StartSessionAsync();
        Assert.Equal(6, session.MaximumQuestions);
        var question = await _engine.GetNextQuestionAsync(_centerId, _studentId, session.SessionId, _utcNow, default);
        Assert.NotNull(question);
        Assert.Equal(5ul, question.QuestionId); // Released question with the matching difficulty, ahead of the free easy question.
    }

    [Fact]
    public async Task Weakness_ExplainsTheBlockedWeakerTopic_AndUsesTheProfileMasteredThreshold()
    {
        await SetTopicEvidenceAsync(_mainTopicId, 61m);
        await SetTopicEvidenceAsync(_prereqTopicId, 78m);
        AddAllMainTopicQuestionsToPublishedAssignment(null);

        var proposal = await _engine.GetProposalAsync(_centerId, _studentId, _subjectId, null, AdaptivePracticeMode.Weakness, null, default);
        Assert.Equal(_prereqTopicId, proposal.TopicNodeId);
        Assert.Equal(2, proposal.AvailableTopics.Count);
        Assert.Contains("61%", proposal.SelectionReason);
        Assert.Contains("thay thế", proposal.SelectionReason);
        Assert.Contains("đã đạt mốc Đã vững 75%", proposal.SelectionReason);
        Assert.DoesNotContain("Cần củng cố để đạt", proposal.SelectionReason);
    }

    [Theory]
    [InlineData(74, "Cần củng cố để đạt mốc Đã vững 75%")]
    [InlineData(75, "đã đạt mốc Đã vững 75%")]
    public async Task Weakness_Uses75PercentAsTheMasteredBoundary(int mastery, string reason)
    {
        await SetTopicEvidenceAsync(_mainTopicId, mastery);
        await SetTopicEvidenceAsync(_prereqTopicId, 90m);
        var proposal = await _engine.GetProposalAsync(_centerId, _studentId, _subjectId, null, AdaptivePracticeMode.Weakness, null, default);
        Assert.Equal(_mainTopicId, proposal.TopicNodeId);
        Assert.Contains(reason, proposal.SelectionReason);
    }

    [Fact]
    public async Task Weakness_SelectsThe61PercentTopic_WhenItsQuestionsBecomeAvailable()
    {
        await SetTopicEvidenceAsync(_mainTopicId, 61m);
        await SetTopicEvidenceAsync(_prereqTopicId, 78m);
        AddAllMainTopicQuestionsToPublishedAssignment(_utcNow.AddDays(-1));
        var proposal = await _engine.GetProposalAsync(_centerId, _studentId, _subjectId, null, AdaptivePracticeMode.Weakness, null, default);
        Assert.Equal(_mainTopicId, proposal.TopicNodeId);
        Assert.False(proposal.IsDiagnosticRecommended);
        Assert.Equal(5, proposal.AvailableQuestionCount);
    }

    private async Task AddCurrentLearningPathAsync(bool hasCurrentItem)
    {
        var path = new LearningPath
        {
            LearningPathId = Guid.NewGuid(), CenterId = _centerId, StudentId = _studentId, SubjectId = _subjectId,
            Status = LearningPathStatus.Active, GeneratedAt = _utcNow, CreatedAt = _utcNow, UpdatedAt = _utcNow
        };
        if (hasCurrentItem) path.Items.Add(new LearningPathItem
        {
            LearningPathItemId = 8800, CenterId = _centerId, TopicNodeId = _mainTopicId,
            Status = LearningPathItemStatus.Current, Reason = "Mục tiêu hiện tại", CreatedAt = _utcNow, UpdatedAt = _utcNow
        });
        _dbContext.LearningPaths.Add(path);
        await _dbContext.SaveChangesAsync();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LearningPath_ExplainsMissingOrBlockedCurrentItem_BeforeOfferingAnAlternative(bool hasCurrentItem)
    {
        await AddCurrentLearningPathAsync(hasCurrentItem);
        AddAllMainTopicQuestionsToPublishedAssignment(null);
        var proposal = await _engine.GetProposalAsync(_centerId, _studentId, _subjectId, null, AdaptivePracticeMode.LearningPath, null, default);
        Assert.Equal(_prereqTopicId, proposal.TopicNodeId);
        Assert.Contains(hasCurrentItem ? "Mục đang học" : "chưa có mục đang học", proposal.SelectionReason);
        Assert.Contains("thay thế", proposal.SelectionReason);
        Assert.Equal(AdaptivePracticeMode.LearningPath, proposal.DefaultMode);
    }

    [Fact]
    public async Task LearningPath_FollowsCurrentItem_WhenQuestionsAreAvailable()
    {
        await AddCurrentLearningPathAsync(true);
        var proposal = await _engine.GetProposalAsync(_centerId, _studentId, _subjectId, null, AdaptivePracticeMode.LearningPath, null, default);
        Assert.Equal(_mainTopicId, proposal.TopicNodeId);
        Assert.Contains("Mục tiêu hiện tại", proposal.SelectionReason);
        Assert.DoesNotContain("thay thế", proposal.SelectionReason);
    }

    [Fact]
    public async Task CustomTopic_DoesNotSilentlyReplaceAnOutOfScopeChoice()
    {
        var proposal = await _engine.GetProposalAsync(_centerId, _studentId, _subjectId, 777ul, AdaptivePracticeMode.CustomTopic, null, default);
        Assert.NotNull(proposal.UnavailableReason);
        Assert.Equal(0ul, proposal.TopicNodeId);
        Assert.Equal(2, proposal.AvailableTopics.Count);
    }

    [Fact]
    public async Task SpacedReview_LabelsEarlyReview_AndExplainsAnUnavailablePriorityTopic()
    {
        await SetTopicEvidenceAsync(_mainTopicId, 61m, 30);
        await SetTopicEvidenceAsync(_prereqTopicId, 78m);
        AddAllMainTopicQuestionsToPublishedAssignment(null);
        var proposal = await _engine.GetProposalAsync(_centerId, _studentId, _subjectId, null, AdaptivePracticeMode.SpacedReview, null, default);
        Assert.Equal(_prereqTopicId, proposal.TopicNodeId);
        Assert.Contains("Ôn sớm", proposal.SelectionReason);
        Assert.Contains("tạm chưa luyện được", proposal.SelectionReason);
        Assert.Contains("thay thế", proposal.SelectionReason);
        Assert.Equal(83m, proposal.TargetMastery);
    }

    [Fact]
    public async Task SpacedReview_WhenStudiedTopicsAreBlocked_DoesNotClaimNothingWasStudied()
    {
        await SetTopicEvidenceAsync(_mainTopicId, 61m, 30);
        AddAllMainTopicQuestionsToPublishedAssignment(null);
        var proposal = await _engine.GetProposalAsync(_centerId, _studentId, _subjectId, null, AdaptivePracticeMode.SpacedReview, null, default);
        Assert.Contains("Các chủ đề đã học", proposal.UnavailableReason);
        Assert.DoesNotContain("Bạn chưa học", proposal.UnavailableReason);
        Assert.Equal(2, proposal.AvailableTopics.Count);
    }
}
