using EduTwin.API.AssessmentAndReasoning.AI;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.AI;

public sealed class GeminiCredentialAvailabilityTests
{
    [Theory]
    [InlineData(401)] [InlineData(403)] [InlineData(404)]
    public void PermanentFailureSkipsOnlyThatCredentialAndModelForBoundedTime(int status)
    {
        var clock = new Clock(); var state = new GeminiCredentialAvailability(clock);
        Assert.True(state.MarkFailure("fake-old-project", "legacy-model", status));
        Assert.False(state.IsAvailable("fake-old-project", "legacy-model"));
        Assert.True(state.IsAvailable("fake-old-project", "new-model"));
        Assert.True(state.IsAvailable("fake-working-project", "legacy-model"));
        clock.Now = clock.Now.AddMinutes(16);
        Assert.True(state.IsAvailable("fake-old-project", "legacy-model"));
    }
    [Theory]
    [InlineData(429)] [InlineData(503)] [InlineData(400)]
    public void RateLimitOrServerFailureIsNotTreatedAsRevokedCredential(int status)
    {
        var state = new GeminiCredentialAvailability(new Clock());
        Assert.False(state.MarkFailure("fake", "model", status)); Assert.True(state.IsAvailable("fake", "model"));
    }
    private sealed class Clock : TimeProvider { public DateTimeOffset Now = new(2026,10,7,6,0,0,TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now; }
}
