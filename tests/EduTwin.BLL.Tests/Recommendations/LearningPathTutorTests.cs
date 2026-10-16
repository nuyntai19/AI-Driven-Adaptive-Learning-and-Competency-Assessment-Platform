using EduTwin.BLL.Recommendations.Services;
using EduTwin.Contracts.Recommendations;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTwin.BLL.Tests.Recommendations;

public sealed class LearningPathTutorTests
{
    private sealed class Tutor : ILearningTutorFeedbackGenerator
    {
        public int Calls; public LearningTutorContext? Last; public bool Unavailable;
        public Task<LearningTutorFeedbackDto> GenerateAsync(LearningTutorContext context, CancellationToken cancellationToken)
        { Calls++; Last=context; return Task.FromResult(Unavailable ? new LearningTutorFeedbackDto { Comment="Provider unavailable" } :
            new LearningTutorFeedbackDto { Status="Generated", Comment="Em đang nhầm quy tắc.",AiSolution="Dùng thì phù hợp với ngữ cảnh.",Understanding="Misunderstood",Correction="Hãy đối chiếu dấu hiệu thời gian.",Provider="Test",Model="test-only",GeneratedAt=DateTime.UtcNow }); }
    }
    [Fact]
    public async Task StudyTutor_ReadsActualSubmittedUnderstanding_CachesReview_AndNeverCreatesEvidence()
    {
        using var f=new LearningPathV4Fixture();var mission=await f.Activate("Study","101");var run=await f.Execution.StartMissionAsync(f.Center,f.Student,Guid.Parse(mission.MissionId));var a=run.Activities[0];
        const string answer="Em nghĩ thì hiện tại đơn luôn dùng cho mọi câu có yesterday.";
        await f.Execution.SubmitActivityAsync(f.Center,f.Student,a.ActivityRunId,new SubmitActivityRequest {ClientSubmissionId=Guid.NewGuid(),FinalAnswer=answer});
        var generator=new Tutor();var service=new LearningActivityExecutionService(f.Db,TimeProvider.System,tutor:generator);
        var result=await service.GetAiFeedbackAsync(f.Center,f.Student,a.ActivityRunId);
        Assert.Equal("Generated",result.Status);Assert.Equal("Misunderstood",result.Understanding);Assert.Equal(answer,generator.Last!.StudentResponse);
        await service.GetAiFeedbackAsync(f.Center,f.Student,a.ActivityRunId);Assert.Equal(1,generator.Calls);
        var restored=await service.GetMissionRunAsync(f.Center,f.Student,Guid.Parse(mission.MissionId),run.SessionRunId);
        Assert.Equal(answer,restored.Activities[0].SubmittedResponse);Assert.Equal("Generated",restored.Activities[0].AiFeedback!.Status);
        Assert.Equal(1,await f.Db.LearningPathSessionRuns.CountAsync());Assert.Empty(await f.Db.Attempts.ToListAsync());Assert.Empty(await f.Db.EvidenceAssessments.ToListAsync());Assert.Empty(await f.Db.KnowledgeTwins.ToListAsync());
    }
    [Fact]
    public async Task TutorCannotReadUnsubmittedWork_OtherStudent_OrAnUnfinishedIndependentCheck()
    {
        using var f=new LearningPathV4Fixture();var mission=await f.Activate("IndependentCheck","101");var run=await f.Execution.StartMissionAsync(f.Center,f.Student,Guid.Parse(mission.MissionId));var a=run.Activities[0];
        var generator=new Tutor();var service=new LearningActivityExecutionService(f.Db,TimeProvider.System,tutor:generator);
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.GetAiFeedbackAsync(f.Center,f.Student,a.ActivityRunId));
        await Assert.ThrowsAsync<KeyNotFoundException>(()=>service.GetAiFeedbackAsync(f.Center,Guid.NewGuid(),a.ActivityRunId));
        await f.Execution.SubmitActivityAsync(f.Center,f.Student,a.ActivityRunId,new SubmitActivityRequest {ClientSubmissionId=Guid.NewGuid(),QuestionId=a.Question!.Question!.QuestionId,FinalAnswer="wrong"});
        await Assert.ThrowsAsync<InvalidOperationException>(()=>service.GetAiFeedbackAsync(f.Center,f.Student,a.ActivityRunId));Assert.Equal(0,generator.Calls);
    }
    [Fact]
    public async Task UnavailableTutor_IsNotPassedOrCachedAsGenerated_AndCanRetry()
    {
        using var f=new LearningPathV4Fixture();var mission=await f.Activate("Study","101");var run=await f.Execution.StartMissionAsync(f.Center,f.Student,Guid.Parse(mission.MissionId));var a=run.Activities[0];
        await f.Execution.SubmitActivityAsync(f.Center,f.Student,a.ActivityRunId,new SubmitActivityRequest {ClientSubmissionId=Guid.NewGuid(),FinalAnswer="Cách hiểu cần kiểm tra"});
        var generator=new Tutor {Unavailable=true};var service=new LearningActivityExecutionService(f.Db,TimeProvider.System,tutor:generator);
        Assert.Equal("Unavailable",(await service.GetAiFeedbackAsync(f.Center,f.Student,a.ActivityRunId)).Status);
        Assert.Null((await service.GetActivityRunAsync(f.Center,f.Student,a.ActivityRunId)).AiFeedback);
        generator.Unavailable=false;Assert.Equal("Generated",(await service.GetAiFeedbackAsync(f.Center,f.Student,a.ActivityRunId)).Status);Assert.Equal(2,generator.Calls);
    }
    [Fact]
    public async Task AddedQuestionStock_UnlocksAnExistingBlockedCheckWithoutRegeneratingThePath()
    {
        using var f=new LearningPathV4Fixture();var extra=await f.Db.Questions.Where(q=>q.PrimaryTopicNodeId==101 && q.QuestionId>10103).ToListAsync();f.Db.Questions.RemoveRange(extra);await f.Db.SaveChangesAsync();
        var mission=await f.Activate("IndependentCheck","101");Assert.False(mission.CanStart);
        f.Db.Questions.AddRange(extra);await f.Db.SaveChangesAsync();var agenda=await f.Planner.GetLearningAgendaAsync(f.Center,f.Student,f.English);
        Assert.True(agenda.Missions.Single(m=>m.MissionId==mission.MissionId).CanStart);
        Assert.Equal(5,(await f.Execution.StartMissionAsync(f.Center,f.Student,Guid.Parse(mission.MissionId))).Activities.Count);
    }
}
