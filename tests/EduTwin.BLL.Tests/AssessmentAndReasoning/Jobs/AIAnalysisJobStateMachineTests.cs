using EduTwin.BLL.AssessmentAndReasoning;
using EduTwin.BLL.AssessmentAndReasoning.Jobs;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.DAL.AssessmentAndReasoning;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Jobs;

public sealed class AIAnalysisJobStateMachineTests
{
    private static readonly DateTime UtcNow =
        new(2026, 8, 12, 4, 30, 0, DateTimeKind.Utc);

    private readonly AIAnalysisJobStateMachine _sut = new();

    [Fact]
    public void Claim_WhenAvailableAtEqualsNow_Succeeds()
    {
        var job = CreateJob(AIJobStatus.Pending);
        job.AvailableAt = UtcNow;
        var leaseUntil = UtcNow.AddMinutes(5);

        var result = _sut.Claim(job, UtcNow, "worker-01", leaseUntil);

        Assert.Equal(AIAnalysisJobTransitionResult.Success, result);
        Assert.Equal(AIJobStatus.Processing, job.Status);
        Assert.Equal(UtcNow, job.StartedAt);
        Assert.Null(job.CompletedAt);
        Assert.Equal("worker-01", job.LeaseOwner);
        Assert.Equal(leaseUntil, job.LeaseUntil);
        Assert.Equal(UtcNow, job.UpdatedAt);
    }

    [Fact]
    public void Claim_WhenAvailableAtIsInPast_Succeeds()
    {
        var job = CreateJob(AIJobStatus.Pending);
        job.AvailableAt = UtcNow.AddTicks(-1);

        var result = _sut.Claim(job, UtcNow, "worker-02", UtcNow.AddSeconds(1));

        Assert.Equal(AIAnalysisJobTransitionResult.Success, result);
        Assert.Equal(AIJobStatus.Processing, job.Status);
    }

    [Fact]
    public void Claim_WhenAvailableAtIsInFuture_ReturnsNotAvailableWithoutMutation()
    {
        var job = CreateJob(AIJobStatus.Pending);
        job.AvailableAt = UtcNow.AddTicks(1);
        var before = Snapshot(job);

        var result = _sut.Claim(job, UtcNow, "worker", UtcNow.AddMinutes(1));

        Assert.Equal(AIAnalysisJobTransitionResult.NotAvailable, result);
        Assert.Equal(before, Snapshot(job));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\r\n\t")]
    public void Claim_WhenLeaseOwnerIsBlank_ReturnsInvalidLeaseWithoutMutation(string? owner)
    {
        var job = CreateJob(AIJobStatus.Pending);
        var before = Snapshot(job);

        var result = _sut.Claim(job, UtcNow, owner, UtcNow.AddMinutes(1));

        Assert.Equal(AIAnalysisJobTransitionResult.InvalidLease, result);
        Assert.Equal(before, Snapshot(job));
    }

    [Fact]
    public void Claim_NormalizesLeaseOwnerBeforePersisting()
    {
        var job = CreateJob(AIJobStatus.Pending);

        var result = _sut.Claim(
            job,
            UtcNow,
            " \tworker\r\n-03\u0000 ",
            UtcNow.AddMinutes(1));

        Assert.Equal(AIAnalysisJobTransitionResult.Success, result);
        Assert.Equal("worker-03", job.LeaseOwner);
    }

    [Fact]
    public void Claim_WhenNormalizedLeaseOwnerExceedsMaximum_ReturnsInvalidLeaseWithoutMutation()
    {
        var job = CreateJob(AIJobStatus.Pending);
        var before = Snapshot(job);

        var result = _sut.Claim(
            job,
            UtcNow,
            $" \r{new string('w', 101)}\n ",
            UtcNow.AddMinutes(1));

        Assert.Equal(AIAnalysisJobTransitionResult.InvalidLease, result);
        Assert.Equal(before, Snapshot(job));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Claim_WhenLeaseDurationIsNotPositive_ReturnsInvalidLeaseWithoutMutation(int ticks)
    {
        var job = CreateJob(AIJobStatus.Pending);
        var before = Snapshot(job);

        var result = _sut.Claim(job, UtcNow, "worker", UtcNow.AddTicks(ticks));

        Assert.Equal(AIAnalysisJobTransitionResult.InvalidLease, result);
        Assert.Equal(before, Snapshot(job));
    }

    [Fact]
    public void Claim_PreservesIdentityRetryAndAuditCreationFields()
    {
        var job = CreateJob(AIJobStatus.Pending, retryCount: 1);
        var identity = CaptureIdentity(job);

        var result = _sut.Claim(job, UtcNow, "worker", UtcNow.AddMinutes(1));

        Assert.Equal(AIAnalysisJobTransitionResult.Success, result);
        Assert.Equal(identity, CaptureIdentity(job));
        Assert.Equal((byte)1, job.RetryCount);
    }

    [Theory]
    [InlineData(AIJobStatus.Processing)]
    [InlineData(AIJobStatus.Completed)]
    [InlineData(AIJobStatus.FallbackCompleted)]
    [InlineData(AIJobStatus.FailedTerminal)]
    public void Claim_FromNonPendingState_ReturnsInvalidTransitionWithoutMutation(AIJobStatus status)
    {
        var job = CreateJob(status);
        var before = Snapshot(job);

        var result = _sut.Claim(job, UtcNow, "worker", UtcNow.AddMinutes(1));

        Assert.Equal(AIAnalysisJobTransitionResult.InvalidTransition, result);
        Assert.Equal(before, Snapshot(job));
    }

    [Fact]
    public void Retry_WhenFirstRetry_Succeeds()
    {
        var job = CreateJob(AIJobStatus.Processing, retryCount: 0);
        var availableAt = UtcNow.AddMinutes(2);

        var result = _sut.Retry(job, UtcNow, availableAt, "transient", "try later");

        Assert.Equal(AIAnalysisJobTransitionResult.Success, result);
        Assert.Equal(AIJobStatus.Pending, job.Status);
        Assert.Equal((byte)1, job.RetryCount);
        Assert.Equal(availableAt, job.AvailableAt);
        Assert.Null(job.StartedAt);
        Assert.Null(job.CompletedAt);
        Assert.Null(job.LeaseOwner);
        Assert.Null(job.LeaseUntil);
        Assert.Equal("transient", job.LastErrorCode);
        Assert.Equal("try later", job.LastErrorMessage);
        Assert.Equal(UtcNow, job.UpdatedAt);
    }

    [Fact]
    public void Retry_WhenRetryAlreadyUsed_ReturnsRetryExhaustedWithoutMutation()
    {
        var job = CreateJob(AIJobStatus.Processing, retryCount: 1);
        var before = Snapshot(job);

        var result = _sut.Retry(job, UtcNow, UtcNow.AddMinutes(1));

        Assert.Equal(AIAnalysisJobTransitionResult.RetryExhausted, result);
        Assert.Equal(before, Snapshot(job));
    }

    [Theory]
    [InlineData(AIJobStatus.Pending)]
    [InlineData(AIJobStatus.Completed)]
    [InlineData(AIJobStatus.FallbackCompleted)]
    [InlineData(AIJobStatus.FailedTerminal)]
    public void Retry_FromNonProcessingState_ReturnsInvalidTransitionWithoutMutation(AIJobStatus status)
    {
        var job = CreateJob(status);
        var before = Snapshot(job);

        var result = _sut.Retry(job, UtcNow, UtcNow.AddMinutes(1));

        Assert.Equal(AIAnalysisJobTransitionResult.InvalidTransition, result);
        Assert.Equal(before, Snapshot(job));
    }

    [Fact]
    public void Retry_WhenAvailableAtPrecedesNow_ReturnsInvalidInputWithoutMutation()
    {
        var job = CreateJob(AIJobStatus.Processing);
        var before = Snapshot(job);

        var result = _sut.Retry(job, UtcNow, UtcNow.AddTicks(-1));

        Assert.Equal(AIAnalysisJobTransitionResult.InvalidInput, result);
        Assert.Equal(before, Snapshot(job));
    }

    [Fact]
    public void Retry_ClearsExecutionFieldsAndPreservesIdentity()
    {
        var job = CreateJob(AIJobStatus.Processing);
        var identity = CaptureIdentity(job);

        var result = _sut.Retry(job, UtcNow, UtcNow);

        Assert.Equal(AIAnalysisJobTransitionResult.Success, result);
        Assert.Equal(identity, CaptureIdentity(job));
        Assert.Null(job.StartedAt);
        Assert.Null(job.CompletedAt);
        Assert.Null(job.LeaseOwner);
        Assert.Null(job.LeaseUntil);
    }

    [Fact]
    public void RecoverExpiredLease_WhenLeaseIsStrictlyExpired_Succeeds()
    {
        var job = CreateJob(AIJobStatus.Processing);
        job.LeaseUntil = UtcNow.AddTicks(-1);

        var result = _sut.RecoverExpiredLease(job, UtcNow);

        Assert.Equal(AIAnalysisJobTransitionResult.Success, result);
        Assert.Equal(AIJobStatus.Pending, job.Status);
        Assert.Equal(UtcNow, job.AvailableAt);
        Assert.Null(job.StartedAt);
        Assert.Null(job.CompletedAt);
        Assert.Null(job.LeaseOwner);
        Assert.Null(job.LeaseUntil);
        Assert.Equal(UtcNow, job.UpdatedAt);
    }

    [Fact]
    public void RecoverExpiredLease_WhenLeaseEqualsNow_ReturnsNotAvailableWithoutMutation()
    {
        var job = CreateJob(AIJobStatus.Processing);
        job.LeaseUntil = UtcNow;
        var before = Snapshot(job);

        var result = _sut.RecoverExpiredLease(job, UtcNow);

        Assert.Equal(AIAnalysisJobTransitionResult.NotAvailable, result);
        Assert.Equal(before, Snapshot(job));
    }

    [Fact]
    public void RecoverExpiredLease_WhenLeaseIsInFuture_ReturnsNotAvailableWithoutMutation()
    {
        var job = CreateJob(AIJobStatus.Processing);
        job.LeaseUntil = UtcNow.AddTicks(1);
        var before = Snapshot(job);

        var result = _sut.RecoverExpiredLease(job, UtcNow);

        Assert.Equal(AIAnalysisJobTransitionResult.NotAvailable, result);
        Assert.Equal(before, Snapshot(job));
    }

    [Fact]
    public void RecoverExpiredLease_WhenLeaseIsNull_ReturnsInvalidLeaseWithoutMutation()
    {
        var job = CreateJob(AIJobStatus.Processing);
        job.LeaseUntil = null;
        var before = Snapshot(job);

        var result = _sut.RecoverExpiredLease(job, UtcNow);

        Assert.Equal(AIAnalysisJobTransitionResult.InvalidLease, result);
        Assert.Equal(before, Snapshot(job));
    }

    [Theory]
    [InlineData(AIJobStatus.Pending)]
    [InlineData(AIJobStatus.Completed)]
    [InlineData(AIJobStatus.FallbackCompleted)]
    [InlineData(AIJobStatus.FailedTerminal)]
    public void RecoverExpiredLease_FromNonProcessingState_IsRejectedWithoutMutation(AIJobStatus status)
    {
        var job = CreateJob(status);
        job.LeaseUntil = UtcNow.AddTicks(-1);
        var before = Snapshot(job);

        var result = _sut.RecoverExpiredLease(job, UtcNow);

        Assert.Equal(AIAnalysisJobTransitionResult.InvalidTransition, result);
        Assert.Equal(before, Snapshot(job));
    }

    [Theory]
    [InlineData((byte)0)]
    [InlineData((byte)1)]
    public void RecoverExpiredLease_PreservesRetryCount(byte retryCount)
    {
        var job = CreateJob(AIJobStatus.Processing, retryCount);
        job.LeaseUntil = UtcNow.AddTicks(-1);

        var result = _sut.RecoverExpiredLease(job, UtcNow);

        Assert.Equal(AIAnalysisJobTransitionResult.Success, result);
        Assert.Equal(retryCount, job.RetryCount);
    }

    [Fact]
    public void Complete_FromProcessing_AppliesCompletedSemantics()
    {
        var job = CreateJob(AIJobStatus.Processing);
        var startedAt = job.StartedAt;

        var result = _sut.Complete(job, UtcNow);

        Assert.Equal(AIAnalysisJobTransitionResult.Success, result);
        Assert.Equal(AIJobStatus.Completed, job.Status);
        Assert.Equal(startedAt, job.StartedAt);
        Assert.Equal(UtcNow, job.CompletedAt);
        Assert.Null(job.LeaseOwner);
        Assert.Null(job.LeaseUntil);
        Assert.Null(job.LastErrorCode);
        Assert.Null(job.LastErrorMessage);
        Assert.Equal(UtcNow, job.UpdatedAt);
    }

    [Fact]
    public void CompleteFallback_FromProcessing_AppliesFallbackSemanticsAndErrors()
    {
        var job = CreateJob(AIJobStatus.Processing);

        var result = _sut.CompleteFallback(job, UtcNow, "fallback", "used local score");

        Assert.Equal(AIAnalysisJobTransitionResult.Success, result);
        Assert.Equal(AIJobStatus.FallbackCompleted, job.Status);
        Assert.Equal(UtcNow, job.CompletedAt);
        Assert.Null(job.LeaseOwner);
        Assert.Null(job.LeaseUntil);
        Assert.Equal("fallback", job.LastErrorCode);
        Assert.Equal("used local score", job.LastErrorMessage);
        Assert.Equal(UtcNow, job.UpdatedAt);
    }

    [Fact]
    public void FailTerminal_FromProcessing_AppliesFailureSemanticsAndErrors()
    {
        var job = CreateJob(AIJobStatus.Processing);

        var result = _sut.FailTerminal(job, UtcNow, "terminal", "analysis failed");

        Assert.Equal(AIAnalysisJobTransitionResult.Success, result);
        Assert.Equal(AIJobStatus.FailedTerminal, job.Status);
        Assert.Equal(UtcNow, job.CompletedAt);
        Assert.Null(job.LeaseOwner);
        Assert.Null(job.LeaseUntil);
        Assert.Equal("terminal", job.LastErrorCode);
        Assert.Equal("analysis failed", job.LastErrorMessage);
        Assert.Equal(UtcNow, job.UpdatedAt);
    }

    [Theory]
    [InlineData(AIJobStatus.Completed)]
    [InlineData(AIJobStatus.FallbackCompleted)]
    [InlineData(AIJobStatus.FailedTerminal)]
    public void TerminalTransition_FromPending_IsRejectedWithoutMutation(AIJobStatus target)
    {
        var job = CreateJob(AIJobStatus.Pending);
        var before = Snapshot(job);

        var result = TransitionTo(job, target);

        Assert.Equal(AIAnalysisJobTransitionResult.InvalidTransition, result);
        Assert.Equal(before, Snapshot(job));
    }

    [Theory]
    [InlineData(AIJobStatus.Completed)]
    [InlineData(AIJobStatus.FallbackCompleted)]
    [InlineData(AIJobStatus.FailedTerminal)]
    public void TerminalState_RejectsEveryTransitionAndRemainsImmutable(AIJobStatus terminalStatus)
    {
        var job = CreateJob(terminalStatus);
        var before = Snapshot(job);

        Assert.Equal(
            AIAnalysisJobTransitionResult.InvalidTransition,
            _sut.Claim(job, UtcNow, "worker", UtcNow.AddMinutes(1)));
        Assert.Equal(
            AIAnalysisJobTransitionResult.InvalidTransition,
            _sut.Retry(job, UtcNow, UtcNow));
        Assert.Equal(
            AIAnalysisJobTransitionResult.InvalidTransition,
            _sut.RecoverExpiredLease(job, UtcNow));
        Assert.Equal(
            AIAnalysisJobTransitionResult.InvalidTransition,
            _sut.Complete(job, UtcNow));
        Assert.Equal(
            AIAnalysisJobTransitionResult.InvalidTransition,
            _sut.CompleteFallback(job, UtcNow));
        Assert.Equal(
            AIAnalysisJobTransitionResult.InvalidTransition,
            _sut.FailTerminal(job, UtcNow));
        Assert.Equal(before, Snapshot(job));
    }

    [Theory]
    [InlineData(AIJobStatus.Completed)]
    [InlineData(AIJobStatus.FallbackCompleted)]
    [InlineData(AIJobStatus.FailedTerminal)]
    public void TerminalTransition_PreservesIdentityRetryAndCreationFields(AIJobStatus target)
    {
        var job = CreateJob(AIJobStatus.Processing, retryCount: 1);
        var identity = CaptureIdentity(job);

        var result = TransitionTo(job, target);

        Assert.Equal(AIAnalysisJobTransitionResult.Success, result);
        Assert.Equal(identity, CaptureIdentity(job));
        Assert.Equal((byte)1, job.RetryCount);
    }

    [Fact]
    public void ErrorSanitization_TruncatesCodeToOneHundredCharacters()
    {
        var job = CreateJob(AIJobStatus.Processing);

        var result = _sut.FailTerminal(job, UtcNow, new string('c', 101), "message");

        Assert.Equal(AIAnalysisJobTransitionResult.Success, result);
        Assert.Equal(new string('c', 100), job.LastErrorCode);
    }

    [Fact]
    public void ErrorSanitization_TruncatesMessageToOneThousandCharacters()
    {
        var job = CreateJob(AIJobStatus.Processing);

        var result = _sut.CompleteFallback(job, UtcNow, "code", new string('m', 1001));

        Assert.Equal(AIAnalysisJobTransitionResult.Success, result);
        Assert.Equal(new string('m', 1000), job.LastErrorMessage);
    }

    [Fact]
    public void ErrorSanitization_RemovesControlsTrimsAndMapsBlankToNull()
    {
        var retryJob = CreateJob(AIJobStatus.Processing);
        var terminalJob = CreateJob(AIJobStatus.Processing);

        var retryResult = _sut.Retry(
            retryJob,
            UtcNow,
            UtcNow,
            " \rE\u0000R\nR\t ",
            "\r\n\t");
        var terminalResult = _sut.FailTerminal(
            terminalJob,
            UtcNow,
            "\u0001 CODE \u0002",
            " \tmessage\r\n ");

        Assert.Equal(AIAnalysisJobTransitionResult.Success, retryResult);
        Assert.Equal("ERR", retryJob.LastErrorCode);
        Assert.Null(retryJob.LastErrorMessage);
        Assert.Equal(AIAnalysisJobTransitionResult.Success, terminalResult);
        Assert.Equal("CODE", terminalJob.LastErrorCode);
        Assert.Equal("message", terminalJob.LastErrorMessage);
    }

    [Fact]
    public void InvalidTransition_PreservesEveryPublicScalarProperty()
    {
        var job = CreateJob(AIJobStatus.Completed, retryCount: 1);
        var before = Snapshot(job);

        var result = _sut.Retry(
            job,
            UtcNow.AddDays(1),
            UtcNow.AddDays(2),
            "replacement",
            "replacement");

        Assert.Equal(AIAnalysisJobTransitionResult.InvalidTransition, result);
        Assert.Equal(before, Snapshot(job));
    }

    [Fact]
    public void SameSnapshotAndInputs_ProduceTheSameResultAndSnapshot()
    {
        var first = CreateJob(AIJobStatus.Processing);
        var second = Clone(first);

        var firstResult = _sut.Retry(
            first,
            UtcNow,
            UtcNow.AddMinutes(3),
            " \rtransient\n ",
            " retry ");
        var secondResult = _sut.Retry(
            second,
            UtcNow,
            UtcNow.AddMinutes(3),
            " \rtransient\n ",
            " retry ");

        Assert.Equal(firstResult, secondResult);
        Assert.Equal(Snapshot(first), Snapshot(second));
    }

    public static IEnumerable<object[]> StatePairs()
    {
        foreach (var source in Enum.GetValues<AIJobStatus>())
        {
            foreach (var target in Enum.GetValues<AIJobStatus>())
            {
                yield return new object[] { source, target };
            }
        }
    }

    [Theory]
    [MemberData(nameof(StatePairs))]
    public void StateMatrix_AllowsOnlyLockedTransitions(AIJobStatus source, AIJobStatus target)
    {
        var job = CreateJob(source);
        var before = Snapshot(job);
        var expectedSuccess =
            source == AIJobStatus.Pending && target == AIJobStatus.Processing ||
            source == AIJobStatus.Processing && target is
                AIJobStatus.Pending or
                AIJobStatus.Completed or
                AIJobStatus.FallbackCompleted or
                AIJobStatus.FailedTerminal;

        var result = TransitionTo(job, target);

        if (expectedSuccess)
        {
            Assert.Equal(AIAnalysisJobTransitionResult.Success, result);
            Assert.Equal(target, job.Status);
        }
        else
        {
            Assert.Equal(AIAnalysisJobTransitionResult.InvalidTransition, result);
            Assert.Equal(before, Snapshot(job));
        }
    }

    [Fact]
    public void UnknownStatus_FailsClosedForEveryOperationWithoutMutation()
    {
        var job = CreateJob((AIJobStatus)int.MaxValue);
        var before = Snapshot(job);

        Assert.Equal(
            AIAnalysisJobTransitionResult.InvalidTransition,
            _sut.Claim(job, UtcNow, "worker", UtcNow.AddMinutes(1)));
        Assert.Equal(
            AIAnalysisJobTransitionResult.InvalidTransition,
            _sut.Retry(job, UtcNow, UtcNow));
        Assert.Equal(
            AIAnalysisJobTransitionResult.InvalidTransition,
            _sut.RecoverExpiredLease(job, UtcNow));
        Assert.Equal(
            AIAnalysisJobTransitionResult.InvalidTransition,
            _sut.Complete(job, UtcNow));
        Assert.Equal(
            AIAnalysisJobTransitionResult.InvalidTransition,
            _sut.CompleteFallback(job, UtcNow));
        Assert.Equal(
            AIAnalysisJobTransitionResult.InvalidTransition,
            _sut.FailTerminal(job, UtcNow));
        Assert.Equal(before, Snapshot(job));
    }

    [Fact]
    public void AddAssessmentAndReasoning_RegistersStateMachineAsSingleton()
    {
        var services = new ServiceCollection();

        services.AddAssessmentAndReasoning();

        var descriptor = Assert.Single(services, candidate =>
            candidate.ServiceType == typeof(IAIAnalysisJobStateMachine));
        Assert.Equal(ServiceLifetime.Singleton, descriptor.Lifetime);
        Assert.Equal(typeof(AIAnalysisJobStateMachine), descriptor.ImplementationType);
    }

    private AIAnalysisJobTransitionResult TransitionTo(AIAnalysisJob job, AIJobStatus target) =>
        target switch
        {
            AIJobStatus.Pending => _sut.Retry(job, UtcNow, UtcNow),
            AIJobStatus.Processing => _sut.Claim(
                job,
                UtcNow,
                "matrix-worker",
                UtcNow.AddMinutes(1)),
            AIJobStatus.Completed => _sut.Complete(job, UtcNow),
            AIJobStatus.FallbackCompleted => _sut.CompleteFallback(job, UtcNow),
            AIJobStatus.FailedTerminal => _sut.FailTerminal(job, UtcNow),
            _ => throw new ArgumentOutOfRangeException(nameof(target), target, null)
        };

    private static AIAnalysisJob CreateJob(
        AIJobStatus status,
        byte retryCount = 0) =>
        new()
        {
            AnalysisJobId = 7001,
            CenterId = Guid.Parse("11111111-1111-1111-1111-111111111111"),
            AttemptId = 8001,
            Status = status,
            RetryCount = retryCount,
            AvailableAt = UtcNow.AddMinutes(-10),
            StartedAt = UtcNow.AddMinutes(-5),
            CompletedAt = UtcNow.AddMinutes(-1),
            LeaseOwner = "original-worker",
            LeaseUntil = UtcNow.AddMinutes(5),
            LastErrorCode = "original-code",
            LastErrorMessage = "original-message",
            CorrelationId = "correlation-01",
            CreatedAt = UtcNow.AddHours(-1),
            CreatedBy = Guid.Parse("22222222-2222-2222-2222-222222222222"),
            UpdatedAt = UtcNow.AddMinutes(-3),
            RowVersion = 17
        };

    private static AIAnalysisJob Clone(AIAnalysisJob job) =>
        new()
        {
            AnalysisJobId = job.AnalysisJobId,
            CenterId = job.CenterId,
            AttemptId = job.AttemptId,
            Status = job.Status,
            RetryCount = job.RetryCount,
            AvailableAt = job.AvailableAt,
            StartedAt = job.StartedAt,
            CompletedAt = job.CompletedAt,
            LeaseOwner = job.LeaseOwner,
            LeaseUntil = job.LeaseUntil,
            LastErrorCode = job.LastErrorCode,
            LastErrorMessage = job.LastErrorMessage,
            CorrelationId = job.CorrelationId,
            CreatedAt = job.CreatedAt,
            CreatedBy = job.CreatedBy,
            UpdatedAt = job.UpdatedAt,
            RowVersion = job.RowVersion
        };

    private static JobSnapshot Snapshot(AIAnalysisJob job) =>
        new(
            job.AnalysisJobId,
            job.CenterId,
            job.AttemptId,
            job.Status,
            job.RetryCount,
            job.AvailableAt,
            job.StartedAt,
            job.CompletedAt,
            job.LeaseOwner,
            job.LeaseUntil,
            job.LastErrorCode,
            job.LastErrorMessage,
            job.CorrelationId,
            job.CreatedAt,
            job.CreatedBy,
            job.UpdatedAt,
            job.RowVersion);

    private static IdentitySnapshot CaptureIdentity(AIAnalysisJob job) =>
        new(
            job.AnalysisJobId,
            job.CenterId,
            job.AttemptId,
            job.CorrelationId,
            job.CreatedAt,
            job.CreatedBy,
            job.RowVersion);

    private sealed record JobSnapshot(
        ulong AnalysisJobId,
        Guid CenterId,
        ulong AttemptId,
        AIJobStatus Status,
        byte RetryCount,
        DateTime AvailableAt,
        DateTime? StartedAt,
        DateTime? CompletedAt,
        string? LeaseOwner,
        DateTime? LeaseUntil,
        string? LastErrorCode,
        string? LastErrorMessage,
        string CorrelationId,
        DateTime CreatedAt,
        Guid? CreatedBy,
        DateTime UpdatedAt,
        ulong RowVersion);

    private sealed record IdentitySnapshot(
        ulong AnalysisJobId,
        Guid CenterId,
        ulong AttemptId,
        string CorrelationId,
        DateTime CreatedAt,
        Guid? CreatedBy,
        ulong RowVersion);
}
