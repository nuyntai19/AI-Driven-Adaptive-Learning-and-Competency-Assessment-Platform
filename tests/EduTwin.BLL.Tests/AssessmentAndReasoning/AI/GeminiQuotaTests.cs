using System.Text.Json;
using EduTwin.API.AssessmentAndReasoning.AI;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.AI;

public sealed class GeminiQuotaTests
{
    [Fact]
    public void GenerateContentDailyQuotaStopsUntilPacificReset()
    {
        var failure = GeminiQuotaFailureClassifier.Classify("quotaId: GenerateRequestsPerDayPerProjectPerModel-FreeTier retryDelay: \"34s\"");
        Assert.True(failure.Daily); Assert.Equal(TimeSpan.FromSeconds(34), failure.RetryAfter);
        var ledger = new GeminiQuotaLedger(); var id = Guid.NewGuid();
        ledger.TryReserve(Pool(), 10, TimeSpan.FromSeconds(30), id, Now);
        ledger.Complete(id, null, true, true, Now, failure.RetryAfter, failure.Daily);
        Assert.True(ledger.CooldownUntil > Now.AddHours(1));
        Assert.Null(ledger.TryReserve(Pool(), 10, TimeSpan.FromSeconds(30), Guid.NewGuid(), Now.AddDays(1)));
    }

    [Fact]
    public void MinuteQuotaIsNotMisclassifiedAsDailyAndHonorsRetryDelay()
    {
        var failure = GeminiQuotaFailureClassifier.Classify("GenerateRequestsPerMinutePerProjectPerModel-FreeTier retryDelay: \"120s\"");
        Assert.False(failure.Daily);
        var ledger = new GeminiQuotaLedger(); var id = Guid.NewGuid();
        ledger.TryReserve(Pool(), 10, TimeSpan.FromSeconds(30), id, Now);
        ledger.Complete(id, null, true, true, Now, failure.RetryAfter);
        Assert.Equal(Now.AddSeconds(120), ledger.CooldownUntil);
    }

    [Fact]
    public void RollingDailyTokenLimitRemainsAfterMinuteWindowAndRestart()
    {
        var pool = Pool(); pool.RollingDailyWindow = true; pool.TokensPerDay = 100;
        var ledger = new GeminiQuotaLedger(); var id = Guid.NewGuid();
        ledger.TryReserve(pool, 90, TimeSpan.FromSeconds(30), id, Now);
        ledger.Complete(id, 80, false, false, Now);
        ledger = JsonSerializer.Deserialize<GeminiQuotaLedger>(JsonSerializer.Serialize(ledger))!;
        Assert.NotNull(ledger.TryReserve(pool, 30, TimeSpan.FromSeconds(30), Guid.NewGuid(), Now.AddHours(1)));
        Assert.Null(ledger.TryReserve(pool, 30, TimeSpan.FromSeconds(30), Guid.NewGuid(), Now.AddDays(1).AddSeconds(1)));
    }
    private static readonly DateTime Now = new(2026, 10, 7, 10, 0, 0, DateTimeKind.Utc);
    private static GeminiQuotaPoolOptions Pool(int concurrent = 2, int rpm = 0, long tpm = 0, int daily = 0) =>
        new() { ProjectId = "verified-test-project", KeyIndexes = [0], MaxConcurrentRequests = concurrent,
            RequestsPerMinute = rpm, InputTokensPerMinute = tpm, RequestsPerDay = daily };

    [Fact]
    public void SharedPoolLimitsActiveCalls_AndRecoversExpiredReservation()
    {
        var ledger = new GeminiQuotaLedger(); var pool = Pool();
        Assert.Null(ledger.TryReserve(pool, 10, TimeSpan.FromSeconds(30), Guid.NewGuid(), Now));
        Assert.Null(ledger.TryReserve(pool, 10, TimeSpan.FromSeconds(30), Guid.NewGuid(), Now));
        Assert.NotNull(ledger.TryReserve(pool, 10, TimeSpan.FromSeconds(30), Guid.NewGuid(), Now));
        Assert.Null(ledger.TryReserve(pool, 10, TimeSpan.FromSeconds(30), Guid.NewGuid(), Now.AddSeconds(46)));
    }

    [Fact]
    public void CompletedCallsStillCountAgainstRollingRequestAndTokenLimits()
    {
        var ledger = new GeminiQuotaLedger(); var id = Guid.NewGuid(); var pool = Pool(rpm: 1, tpm: 100);
        Assert.Null(ledger.TryReserve(pool, 60, TimeSpan.FromSeconds(30), id, Now));
        ledger.Complete(id, 80, false, false, Now.AddSeconds(1));
        Assert.Equal(80, ledger.Entries.Single().InputTokens);
        Assert.Equal(TimeSpan.FromSeconds(59), ledger.TryReserve(pool, 10, TimeSpan.FromSeconds(30), Guid.NewGuid(), Now.AddSeconds(1)));
        Assert.Null(ledger.TryReserve(pool, 10, TimeSpan.FromSeconds(30), Guid.NewGuid(), Now.AddSeconds(61)));
    }

    [Fact]
    public void TokenBudgetBlocksEvenWhenRequestAndConcurrencySlotsRemain()
    {
        var ledger = new GeminiQuotaLedger(); var pool = Pool(concurrent: 8, tpm: 100);
        Assert.Null(ledger.TryReserve(pool, 70, TimeSpan.FromSeconds(30), Guid.NewGuid(), Now));
        Assert.NotNull(ledger.TryReserve(pool, 40, TimeSpan.FromSeconds(30), Guid.NewGuid(), Now));
        Assert.Throws<GeminiAdapterException>(() => ledger.TryReserve(pool, 101, TimeSpan.FromSeconds(30), Guid.NewGuid(), Now));
    }

    [Fact]
    public void CooldownSurvivesRestart_AndAnotherInflightSuccess()
    {
        var ledger = new GeminiQuotaLedger(); var first = Guid.NewGuid(); var second = Guid.NewGuid(); var pool = Pool();
        ledger.TryReserve(pool, 10, TimeSpan.FromSeconds(30), first, Now);
        ledger.TryReserve(pool, 10, TimeSpan.FromSeconds(30), second, Now);
        ledger.Complete(first, null, true, true, Now);
        var until = ledger.CooldownUntil;
        ledger.Complete(second, 10, false, false, Now);
        ledger = JsonSerializer.Deserialize<GeminiQuotaLedger>(JsonSerializer.Serialize(ledger))!;
        Assert.Equal(until, ledger.CooldownUntil);
        Assert.NotNull(ledger.TryReserve(pool, 10, TimeSpan.FromSeconds(30), Guid.NewGuid(), Now.AddSeconds(30)));
        Assert.Null(ledger.TryReserve(pool, 10, TimeSpan.FromSeconds(30), Guid.NewGuid(), Now.AddSeconds(62)));
    }

    [Fact]
    public void DailyBudgetUsesPacificDay_AndResetsAtNextDay()
    {
        var ledger = new GeminiQuotaLedger(); var id = Guid.NewGuid(); var pool = Pool(daily: 1);
        ledger.TryReserve(pool, 10, TimeSpan.FromSeconds(30), id, Now);
        ledger.Complete(id, 10, false, false, Now);
        Assert.NotNull(ledger.TryReserve(pool, 10, TimeSpan.FromSeconds(30), Guid.NewGuid(), Now.AddHours(3)));
        Assert.Null(ledger.TryReserve(pool, 10, TimeSpan.FromSeconds(30), Guid.NewGuid(), Now.AddDays(1)));
        Assert.Equal(1, ledger.DayRequestCount);
    }

    [Fact]
    public void UnverifiedKeysShareOneConservativePool()
    {
        var options = new GeminiOptions { ListKey = "[\"fake-1\",\"fake-2\",\"fake-3\"]", Model = "test" };
        options.Validate();
        var pool = Assert.Single(options.GetQuotaPools(3));
        Assert.Equal([0, 1, 2], pool.KeyIndexes); Assert.Equal(2, pool.MaxConcurrentRequests);
        Assert.Equal(0, pool.RequestsPerMinute);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("[null]")]
    [InlineData("[{\"projectId\":\"p\",\"keyIndexes\":[0,0]}]")]
    [InlineData("[{\"projectId\":\"p\",\"keyIndexes\":[5]}]")]
    [InlineData("[{\"projectId\":\"p\",\"keyIndexes\":null}]")]
    public void MalformedPoolsFailWithSanitizedConfigurationError(string json)
    {
        var options = new GeminiOptions { ListKey = "[\"fake\"]", Model = "test" };
        var exception = Assert.Throws<GeminiAdapterException>(() => { options.LoadQuotaPoolsJson(json); options.Validate(); });
        Assert.Equal("AI_PROVIDER_CONFIGURATION_INVALID", exception.ErrorCode);
        Assert.DoesNotContain(json, exception.Message);
    }
}
