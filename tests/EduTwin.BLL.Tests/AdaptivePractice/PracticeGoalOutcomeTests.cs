using EduTwin.Contracts.AdaptivePractice;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.Organization;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTwin.BLL.Tests.AdaptivePractice;

public sealed partial class AdaptivePracticeEngineTests
{
    [Theory]
    [InlineData(0, "NotAchieved")]
    [InlineData(-1, "NotAchieved")]
    [InlineData(1, "PartiallyAchieved")]
    public async Task StudentStopped_ClassifiesActualProgress_WithoutRequiringAQuestionQuota(int answer, string expected)
    {
        await SetTopicEvidenceAsync(_mainTopicId, 61m);
        var session = await StartSessionAsync();
        if (answer != 0) await AnswerCurrentAsync(session.SessionId, answer > 0);
        var summary = await _engine.CloseSessionAsync(_centerId, _studentId, session.SessionId,
            AdaptivePracticeCloseReason.StudentStopped, _utcNow.AddMinutes(1), default);
        Assert.Equal(expected, summary.GoalAchievementStatus);
        Assert.Equal(answer == 0 ? 0 : 1, summary.TotalQuestions);
        Assert.Equal(expected, (await _engine.GetSummaryAsync(_centerId, _studentId, session.SessionId, default))!.GoalAchievementStatus);
    }

    [Theory]
    [InlineData(AdaptivePracticeCloseReason.QuestionLimitReached)]
    [InlineData(AdaptivePracticeCloseReason.TimeBudgetReached)]
    [InlineData(AdaptivePracticeCloseReason.OutOfQuestions)]
    public async Task ReachingALimit_DoesNotCertifyTheGoalWithoutEvidence(AdaptivePracticeCloseReason reason)
    {
        await SetTopicEvidenceAsync(_mainTopicId, 61m);
        var session = await StartSessionAsync();
        await AnswerCurrentAsync(session.SessionId, correctly: true);
        var saved = await _dbContext.AdaptivePracticeSessions.SingleAsync();
        saved.TargetMastery = saved.CurrentMastery;
        await _dbContext.SaveChangesAsync();
        var summary = await _engine.CloseSessionAsync(_centerId, _studentId, session.SessionId, reason, _utcNow.AddMinutes(1), default);
        Assert.Equal("PartiallyAchieved", summary.GoalAchievementStatus);
    }

    [Fact]
    public async Task LegacyStoppedSummary_IsCorrectedOnRead_ForTeacherAndStudent_WithoutRewritingHistory()
    {
        await SetTopicEvidenceAsync(_mainTopicId, 74m);
        var session = await StartSessionAsync();
        await AnswerCurrentAsync(session.SessionId, correctly: false);
        await _engine.CloseSessionAsync(_centerId, _studentId, session.SessionId, AdaptivePracticeCloseReason.StudentStopped, _utcNow.AddMinutes(1), default);
        var legacy = await _dbContext.AdaptivePracticeSessionSummaries.SingleAsync();
        legacy.GoalAchievementStatus = "PartiallyAchieved";
        var teacher = Guid.NewGuid();
        var classId = Guid.NewGuid();
        _dbContext.Classes.Add(new Class { CenterId = _centerId, ClassId = classId, TeacherId = teacher, SubjectId = _subjectId,
            ClassName = "10A", AcademicYear = "2026", Status = ClassStatus.Active, CreatedAt = _utcNow, UpdatedAt = _utcNow });
        _dbContext.ClassStudents.Add(new ClassStudent { CenterId = _centerId, ClassId = classId, StudentId = _studentId,
            JoinedAt = _utcNow, Status = ClassStudentStatus.Active });
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
        var row = Assert.Single(await _engine.GetTeacherSessionsAsync(_centerId, teacher, classId, _subjectId, 20, default));
        Assert.Equal("NotAchieved", row.GoalAchievementStatus);
        Assert.Equal(AdaptivePracticeCloseReason.StudentStopped, row.CloseReason);
        Assert.Equal("NotAchieved", (await _engine.GetSummaryAsync(_centerId, _studentId, session.SessionId, default))!.GoalAchievementStatus);
        Assert.Equal("PartiallyAchieved", (await _dbContext.AdaptivePracticeSessionSummaries.AsNoTracking().SingleAsync()).GoalAchievementStatus);
    }
}
