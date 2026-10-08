using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EduTwin.API.AssessmentAndReasoning.AI;
using EduTwin.BLL.AssessmentAndReasoning.AI;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Persistence;
using Google.GenAI.Types;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.AI;

public sealed class GeminiAdmissionRegressionTests
{
    private static readonly DateTime Now = new(2026, 10, 7, 10, 0, 0, DateTimeKind.Utc);
    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => new(Now); }

    private static ServiceProvider CreateServices(IInterceptor? interceptor = null)
    {
        var store = new InMemoryDatabaseRoot(); var name = Guid.NewGuid().ToString();
        var services = new ServiceCollection();
        services.AddScoped(_ =>
        {
            var builder = new DbContextOptionsBuilder<EduTwinDbContext>().UseInMemoryDatabase(name, store)
                .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning));
            if (interceptor is not null) builder.AddInterceptors(interceptor);
            return new EduTwinDbContext(builder.Options, new TenantContext());
        });
        return services.BuildServiceProvider();
    }

    private static GeminiQuotaCoordinator Coordinator(ServiceProvider services) => new(services.GetRequiredService<IServiceScopeFactory>(), new Clock());
    private static GeminiQuotaPoolOptions Pool(int index) => new() { ProjectId = $"fake-project-{index}", KeyIndexes = [index], MaxConcurrentRequests = 1 };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClientDeadlineBoundsEveryCallerAndPreservesCallerCancellation(bool callerCancels)
    {
        using var services = CreateServices(new BlockReservationUntilCancelled());
        using var caller = new CancellationTokenSource();
        var options = new GeminiOptions { ApiKey = "fake", Model = "synthetic", Timeout = TimeSpan.FromMilliseconds(50) };
        using var client = new GoogleGenAIGenerateContentClient(Options.Create(options), quota: Coordinator(services));
        if (callerCancels) caller.Cancel();
        if (callerCancels)
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => client.GenerateContentAsync("synthetic", "synthetic", new(), caller.Token));
        else
        {
            var error = await Assert.ThrowsAsync<GeminiAdapterException>(() => client.GenerateContentAsync("synthetic", "synthetic", new(), caller.Token));
            Assert.Equal("AI_PROVIDER_TIMEOUT", error.ErrorCode);
        }
    }

    private sealed class BlockReservationUntilCancelled : SaveChangesInterceptor
    {
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return result;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task QuotaWaitSelectsEarliestPoolRegardlessOfRotationAndDoesNotMislabelMixedWaits(bool allDaily)
    {
        using var services = CreateServices();
        var options = new GeminiOptions { ListKey = "[\"fake-slow\",\"fake-fast\"]", Model = "audit-model", QuotaPools = [Pool(0), Pool(1)] };
        await using (var scope = services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EduTwinDbContext>();
            foreach (var pool in options.QuotaPools)
                db.AIProviderQuotaStates.Add(new AIProviderQuotaState {
                    PoolId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(pool.ProjectId + "\n" + options.Model))),
                    StateJson = JsonSerializer.Serialize(new GeminiQuotaLedger {
                        CooldownUntil = Now.AddSeconds(pool.KeyIndexes[0] == 0 ? 18000 : 10),
                        CooldownReason = allDaily || pool.KeyIndexes[0] == 1 ? "AI_PROVIDER_DAILY_QUOTA_WAIT" : "AI_PROVIDER_CAPACITY_WAIT"
                    })
                });
            await db.SaveChangesAsync();
        }
        using var client = new GoogleGenAIGenerateContentClient(Options.Create(options), quota: Coordinator(services), timeProvider: new Clock());
        for (var rotation = 0; rotation < 2; rotation++)
        {
            // Admission blocks before creating an SDK client: no network or real quota.
            var error = await Assert.ThrowsAsync<AIAnalysisDeferredException>(() => client.GenerateContentAsync(options.Model, "synthetic", new(), default));
            Assert.Equal(TimeSpan.FromSeconds(10), error.RetryAfter);
            Assert.Equal(allDaily ? "AI_PROVIDER_DAILY_QUOTA_WAIT" : "AI_PROVIDER_CAPACITY_WAIT", error.ErrorCode);
        }
        await using var verify = services.CreateAsyncScope();
        var global = await verify.ServiceProvider.GetRequiredService<EduTwinDbContext>().AIProviderQuotaStates.SingleAsync(x => x.PoolId == GeminiQuotaCoordinator.GlobalCapacityPoolId);
        Assert.Empty(JsonSerializer.Deserialize<GeminiConcurrencyLedger>(global.StateJson)!.Entries);
    }

    [Fact]
    public async Task GlobalLimitIsSharedAcrossPoolsModelsAndCoordinatorInstances_AndReleasedOnFailure()
    {
        using var services = CreateServices(); var first = Coordinator(services); var second = Coordinator(services);
        var a = await first.AcquireAsync(Pool(0), "batch-model", 10, TimeSpan.FromSeconds(30), default, 2);
        var b = await second.AcquireAsync(Pool(1), "repair-model", 10, TimeSpan.FromSeconds(30), default, 2);
        var error = await Assert.ThrowsAsync<AIAnalysisDeferredException>(() =>
            second.AcquireAsync(Pool(2), "enrichment-model", 10, TimeSpan.FromSeconds(30), default, 2));
        Assert.True(error.BlocksAllPools);
        using var client = new GoogleGenAIGenerateContentClient(Options.Create(new GeminiOptions {
            ListKey = "[\"fake-0\",\"fake-1\",\"fake-2\"]", Model = "audit-model", MaxConcurrentRequests = 2,
            QuotaPools = [Pool(0), Pool(1), Pool(2)]
        }), quota: second, timeProvider: new Clock());
        var clientError = await Assert.ThrowsAsync<AIAnalysisDeferredException>(() => client.GenerateContentAsync("audit-model", "synthetic", new(), default));
        Assert.True(clientError.BlocksAllPools); // Confirms the real client passes its global configuration.
        await first.CompleteAsync(a, null, true, true, default, TimeSpan.FromMinutes(5));
        var c = await second.AcquireAsync(Pool(2), "enrichment-model", 10, TimeSpan.FromSeconds(30), default, 2);
        await second.CompleteAsync(b, 10, false, false, default);
        await second.CompleteAsync(c, 10, false, false, default);
        await using var verify = services.CreateAsyncScope();
        var rows = await verify.ServiceProvider.GetRequiredService<EduTwinDbContext>().AIProviderQuotaStates.ToListAsync();
        Assert.Empty(JsonSerializer.Deserialize<GeminiConcurrencyLedger>(rows.Single(x => x.PoolId == GeminiQuotaCoordinator.GlobalCapacityPoolId).StateJson)!.Entries);
        // Rejected admissions never consumed quota, even though the provider operation had another model.
        Assert.Equal(3, rows.Where(x => x.PoolId != GeminiQuotaCoordinator.GlobalCapacityPoolId)
            .Sum(x => JsonSerializer.Deserialize<GeminiQuotaLedger>(x.StateJson)!.DayRequestCount));
    }

    [Fact]
    public void GlobalSlotsExpireAfterCrash_AndReleaseIsIdempotent()
    {
        var ledger = new GeminiConcurrencyLedger(); var a = Guid.NewGuid(); var b = Guid.NewGuid();
        Assert.Null(ledger.TryReserve(2, TimeSpan.FromSeconds(30), a, Now));
        Assert.Null(ledger.TryReserve(2, TimeSpan.FromSeconds(30), b, Now));
        Assert.NotNull(ledger.TryReserve(2, TimeSpan.FromSeconds(30), Guid.NewGuid(), Now));
        ledger = JsonSerializer.Deserialize<GeminiConcurrencyLedger>(JsonSerializer.Serialize(ledger))!;
        Assert.Null(ledger.TryReserve(2, TimeSpan.FromSeconds(30), Guid.NewGuid(), Now.AddSeconds(46)));
        ledger.Complete(a); ledger.Complete(a);
        Assert.Single(ledger.Entries);
    }
}
