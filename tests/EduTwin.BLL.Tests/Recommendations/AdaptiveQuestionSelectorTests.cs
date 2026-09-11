using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Recommendations;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.Persistence;
using Xunit;

namespace EduTwin.BLL.Tests.Recommendations;

public sealed class AdaptiveQuestionSelectorTests : IDisposable
{
    private readonly EduTwinDbContext _dbContext;
    private readonly TenantContext _tenantContext;
    private readonly Guid _centerId = Guid.NewGuid();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Guid _subjectId = Guid.NewGuid();
    private readonly ulong _topicNodeId = 42;
    private readonly DateTime _utcNow = new(2026, 9, 11, 10, 0, 0, DateTimeKind.Utc);

    public AdaptiveQuestionSelectorTests()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(databaseName: $"AdaptiveQuestionTests_{Guid.NewGuid():N}")
            .Options;

        _tenantContext = new TenantContext();
        _tenantContext.Initialize(_centerId, _studentId, "Student", 1);
        _dbContext = new EduTwinDbContext(options, _tenantContext);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task SelectQuestion_PrefersUnattemptedQuestion_OverAttemptedQuestion()
    {
        // 2 questions: Q1 (difficulty 3) attempted, Q2 (difficulty 4) unattempted. Target difficulty is 3 (mastery 50%).
        var q1 = new Question
        {
            CenterId = _centerId,
            SubjectId = _subjectId,
            PrimaryTopicNodeId = _topicNodeId,
            QuestionId = 1,
            Difficulty = 3,
            Status = QuestionStatus.Active,
            QuestionText = "Q1",
            QuestionType = QuestionType.MultipleChoice,
            CorrectAnswer = "A",
            Solution = "S",
            LanguageCode = "vi",
            MaxScore = 10m,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        };
        var q2 = new Question
        {
            CenterId = _centerId,
            SubjectId = _subjectId,
            PrimaryTopicNodeId = _topicNodeId,
            QuestionId = 2,
            Difficulty = 4,
            Status = QuestionStatus.Active,
            QuestionText = "Q2",
            QuestionType = QuestionType.MultipleChoice,
            CorrectAnswer = "A",
            Solution = "S",
            LanguageCode = "vi",
            MaxScore = 10m,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        };
        _dbContext.Questions.AddRange(q1, q2);

        // Record student attempt on Q1 10 days ago (not in recency window)
        _dbContext.Attempts.Add(new Attempt
        {
            CenterId = _centerId,
            StudentId = _studentId,
            QuestionId = 1,
            AttemptId = 100,
            FinalAnswer = "A",
            ReasoningLanguage = "vi",
            Status = AttemptStatus.Completed,
            ClientSubmissionId = Guid.NewGuid(),
            CreatedAt = _utcNow.AddDays(-10),
            UpdatedAt = _utcNow.AddDays(-10),
            Question = q1
        });
        await _dbContext.SaveChangesAsync();

        var selector = new AdaptiveQuestionSelector(_dbContext);
        var selected = await selector.SelectQuestionAsync(_centerId, _studentId, _topicNodeId, currentMastery: 50m, CancellationToken.None);

        Assert.NotNull(selected);
        Assert.Equal(2ul, selected.QuestionId); // Q2 is unattempted, so it is prioritized!
    }

    [Fact]
    public async Task SelectQuestion_RelaxesRecencyConstraint_WhenAllQuestionsWereRecentlyAttempted()
    {
        // Topic has only 2 active questions: Q1 and Q2.
        // Student's last 3 attempts are: Q1 (1 hour ago), Q2 (2 hours ago), Q1 (3 hours ago).
        // Both Q1 and Q2 are in the last 3 attempts window.
        // System must NOT return null, but relax recency constraint and pick LRU (Q2 was attempted 2 hours ago vs Q1 1 hour ago).
        var q1 = new Question
        {
            CenterId = _centerId,
            SubjectId = _subjectId,
            PrimaryTopicNodeId = _topicNodeId,
            QuestionId = 1,
            Difficulty = 3,
            Status = QuestionStatus.Active,
            QuestionText = "Q1",
            QuestionType = QuestionType.MultipleChoice,
            CorrectAnswer = "A",
            Solution = "S",
            LanguageCode = "vi",
            MaxScore = 10m,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        };
        var q2 = new Question
        {
            CenterId = _centerId,
            SubjectId = _subjectId,
            PrimaryTopicNodeId = _topicNodeId,
            QuestionId = 2,
            Difficulty = 3,
            Status = QuestionStatus.Active,
            QuestionText = "Q2",
            QuestionType = QuestionType.MultipleChoice,
            CorrectAnswer = "A",
            Solution = "S",
            LanguageCode = "vi",
            MaxScore = 10m,
            CreatedAt = _utcNow,
            UpdatedAt = _utcNow
        };
        _dbContext.Questions.AddRange(q1, q2);

        _dbContext.Attempts.AddRange(
            new Attempt { CenterId = _centerId, StudentId = _studentId, QuestionId = 1, AttemptId = 1, FinalAnswer = "A", ReasoningLanguage = "vi", Status = AttemptStatus.Completed, ClientSubmissionId = Guid.NewGuid(), CreatedAt = _utcNow.AddHours(-3), UpdatedAt = _utcNow.AddHours(-3), Question = q1 },
            new Attempt { CenterId = _centerId, StudentId = _studentId, QuestionId = 2, AttemptId = 2, FinalAnswer = "B", ReasoningLanguage = "vi", Status = AttemptStatus.Completed, ClientSubmissionId = Guid.NewGuid(), CreatedAt = _utcNow.AddHours(-2), UpdatedAt = _utcNow.AddHours(-2), Question = q2 },
            new Attempt { CenterId = _centerId, StudentId = _studentId, QuestionId = 1, AttemptId = 3, FinalAnswer = "C", ReasoningLanguage = "vi", Status = AttemptStatus.Completed, ClientSubmissionId = Guid.NewGuid(), CreatedAt = _utcNow.AddHours(-1), UpdatedAt = _utcNow.AddHours(-1), Question = q1 }
        );
        await _dbContext.SaveChangesAsync();

        var selector = new AdaptiveQuestionSelector(_dbContext);
        var selected = await selector.SelectQuestionAsync(_centerId, _studentId, _topicNodeId, currentMastery: 50m, CancellationToken.None);

        Assert.NotNull(selected);
        Assert.Equal(2ul, selected.QuestionId); // Q2 is least recently attempted (2 hours ago vs 1 hour ago)
    }

    [Fact]
    public async Task SelectQuestion_ReturnsNull_OnlyWhenNoActiveQuestionsExist()
    {
        var selector = new AdaptiveQuestionSelector(_dbContext);
        var selected = await selector.SelectQuestionAsync(_centerId, _studentId, _topicNodeId, currentMastery: 50m, CancellationToken.None);

        Assert.Null(selected);
    }
}
