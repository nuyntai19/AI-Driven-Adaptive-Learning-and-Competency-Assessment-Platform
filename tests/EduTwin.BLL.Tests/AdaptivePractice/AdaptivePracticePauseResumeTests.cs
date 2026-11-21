using EduTwin.Contracts.AdaptivePractice;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTwin.BLL.Tests.AdaptivePractice;

public sealed partial class AdaptivePracticeEngineTests
{
    [Fact]
    public async Task RepeatedPauseResume_PreservesTheUnansweredSecondQuestion_AndExcludesPausedTime()
    {
        var session = await StartSessionAsync();
        await AnswerCurrentAsync(session.SessionId, correctly: true);
        var secondQuestion = await _engine.GetNextQuestionAsync(_centerId, _studentId, session.SessionId, _utcNow.AddSeconds(5), default);
        Assert.NotNull(secondQuestion);
        Assert.Equal(2, secondQuestion.SequenceNumber);

        for (var cycle = 0; cycle < 5; cycle++)
        {
            var pauseTime = _utcNow.AddSeconds(10 + cycle * 90);
            var paused = await _engine.PauseSessionAsync(_centerId, _studentId, session.SessionId, pauseTime, default);
            Assert.Equal(AdaptivePracticeSessionStatus.Paused, paused.Status);
            Assert.Null(await _engine.GetNextQuestionAsync(_centerId, _studentId, session.SessionId, pauseTime.AddSeconds(5), default));

            // Re-entering the page uses a new EF unit of work; no tracked state should be required.
            _dbContext.ChangeTracker.Clear();
            var resumed = await _engine.ResumeSessionAsync(_centerId, _studentId, session.SessionId, pauseTime.AddMinutes(1), default);
            Assert.Equal(AdaptivePracticeSessionStatus.InProgress, resumed.Status);
            Assert.Equal(paused.ElapsedSeconds, resumed.ElapsedSeconds);
            Assert.Null(resumed.PausedAt);
            var question = await _engine.GetNextQuestionAsync(_centerId, _studentId, session.SessionId, pauseTime.AddMinutes(1), default);
            Assert.NotNull(question);
            Assert.Equal(secondQuestion.SessionItemId, question.SessionItemId);
            Assert.Equal(secondQuestion.QuestionId, question.QuestionId);
            Assert.Equal(2, question.SequenceNumber);

            var retriedResume = await _engine.ResumeSessionAsync(_centerId, _studentId, session.SessionId, pauseTime.AddMinutes(1), default);
            Assert.Equal(resumed.StartedAt, retriedResume.StartedAt);
            Assert.Equal(resumed.ElapsedSeconds, retriedResume.ElapsedSeconds);
        }

        Assert.Equal(2, await _dbContext.AdaptivePracticeSessionItems.CountAsync());
        Assert.Equal(1, await _dbContext.Attempts.CountAsync());
        Assert.Equal(1, (await _dbContext.AdaptivePracticeSessions.SingleAsync()).QuestionCount);
    }
}
