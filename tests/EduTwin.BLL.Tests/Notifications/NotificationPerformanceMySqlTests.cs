using System.Data.Common;
using System.Diagnostics;
using System.Text.Json;
using EduTwin.BLL.Notifications;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.Assignments;
using EduTwin.DAL.AssessmentAndReasoning;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace EduTwin.BLL.Tests.Notifications;

public sealed partial class NotificationMySqlTests
{
    [NotificationMySqlFact]
    public async Task BusinessPerformance_ConcurrentFanout_QueryCostAndDeliveryLag()
    {
        await using var f = await Fixture.Create(UserRole.Teacher, UserRole.Student);
        await using var setup = f.Context();
        var recipients = await setup.Users.Select(u => u.UserId).ToArrayAsync();
        var events = new List<Guid>();
        var created = new Dictionary<Guid, DateTime>();
        for (var i = 0; i < 100; i++)
        {
            var id = await new NotificationOutbox(setup, f.Tenant, TimeProvider.System)
                .StageNoticeAsync("isolated-fanout:" + i, "Isolated fanout", "Synthetic evidence only", recipients);
            events.Add(id); created[id] = DateTime.UtcNow;
        }
        await setup.SaveChangesAsync();
        var durations = new System.Collections.Concurrent.ConcurrentBag<double>();
        var commands = new System.Collections.Concurrent.ConcurrentBag<int>();
        // Stress lease/fanout, not a claim that the fair five-second production scheduler is bypassed.
        await Parallel.ForEachAsync(events, new ParallelOptions { MaxDegreeOfParallelism = 8 }, async (id, ct) =>
        {
            var counter = new CommandCounter(); await using var db = f.Context(counter: counter);
            var watch = Stopwatch.StartNew(); Assert.True(await new NotificationDispatcher(db, TimeProvider.System).DispatchAsync(id, ct));
            watch.Stop(); durations.Add(watch.Elapsed.TotalMilliseconds); commands.Add(counter.Count);
        });
        await using var proof = f.Context();
        var delivered = await proof.NotificationDeliveries.AsNoTracking().ToArrayAsync();
        Assert.Equal(100 * recipients.Length, delivered.Length);
        Assert.Equal(delivered.Length, delivered.Select(d => (d.EventId, d.UserId)).Distinct().Count());
        Assert.Equal(100, await proof.NotificationDispatches.CountAsync(d => d.State == "Completed"));
        var lag = delivered.Select(d => Math.Max(0, (d.DeliveredAt - created[d.EventId]).TotalMilliseconds)).Order().ToArray();
        static double P(double[] values, double fraction) => values[(int)Math.Ceiling(values.Length * fraction) - 1];
        var elapsed = durations.Order().ToArray();
        output.WriteLine("NOTIFICATION_FANOUT_BENCHMARK " + JsonSerializer.Serialize(new {
            events = events.Count, recipientsPerEvent = recipients.Length, concurrency = 8,
            dispatchP50Ms = P(elapsed, .5), dispatchP95Ms = P(elapsed, .95),
            harnessDeliveryLagP50Ms = P(lag, .5), harnessDeliveryLagP95Ms = P(lag, .95),
            commandsPerEvent = commands.Average(), geminiRequests = 0,
            productionScheduler = "one event per tenant per five-second cycle; harness measures dispatch only" }));
    }
    [NotificationMySqlFact]
    public async Task BusinessPerformance_50And100Questions_TwoStudents_SubmitAndAiCommit()
    {
        await using var f = await Fixture.Create(UserRole.Teacher, UserRole.Student); var data = await Academic(f, 100);
        var student2 = Guid.NewGuid();
        await using (var setup = f.Context())
        {
            var now = DateTime.UtcNow;
            setup.Users.Add(new() { CenterId = f.CenterId, UserId = student2, Username = "second_student", DisplayName = "Isolated second student", RoleName = UserRole.Student,
                Status = UserStatus.Active, AuthVersion = 1, PasswordHash = "not-a-password", CreatedAt = now, UpdatedAt = now });
            setup.Students.Add(new() { CenterId = f.CenterId, StudentId = student2, FullName = "Isolated second student", GradeLevel = 10, CreatedAt = now, UpdatedAt = now });
            await setup.SaveChangesAsync();
        }
        ulong nextProgressId = 1; // Production allocates this non-generated key; isolated A/B rows must also be unique.
        foreach (var count in new[] { 50, 100 })
        {
            var baselineSubmit = new List<double>(); var newSubmit = new List<double>(); var baselineAi = new List<double>(); var newAi = new List<double>();
            var queryBaseline = new List<int>(); var queryNew = new List<int>();
            // Interleaved A/B, two warm-up pairs then twenty measured pairs; same database, questions and users.
            for (var sample = -2; sample < 20; sample++)
            foreach (var observe in sample % 2 == 0 ? new[] { false, true } : new[] { true, false })
            {
                var assignmentId = Guid.NewGuid();
                await using (var setup = f.Context())
                {
                    var now = DateTime.UtcNow;
                    setup.Assignments.Add(new() { CenterId = f.CenterId, AssignmentId = assignmentId, ClassId = data.ClassId, CreatedByTeacherId = f.UserId,
                        Title = "Isolated benchmark", Status = AssignmentStatus.Published, CreatedAt = now, UpdatedAt = now });
                    for (var i = 1; i <= count; i++) setup.AssignmentQuestions.Add(new() { CenterId = f.CenterId, AssignmentId = assignmentId, QuestionId = (ulong)i, OrderIndex = (uint)i, Points = 10, CreatedAt = now });
                    foreach (var student in new[] { data.StudentId, student2 })
                    {
                        setup.AssignmentTargets.Add(new() { CenterId = f.CenterId, AssignmentId = assignmentId, StudentId = student, CreatedAt = now });
                        setup.StudentAssignmentProgresses.Add(new() { ProgressId = nextProgressId++, CenterId = f.CenterId, AssignmentId = assignmentId, StudentId = student, Status = ProgressStatus.NotStarted, CreatedAt = now, UpdatedAt = now });
                    }
                    await setup.SaveChangesAsync();
                }
                var counter = new CommandCounter(); await using var db = f.Context(observeBusiness: observe, counter: counter);
                var progresses = await db.StudentAssignmentProgresses.Where(p => p.AssignmentId == assignmentId).ToArrayAsync();
                foreach (var p in progresses)
                {
                    p.Status = ProgressStatus.Completed; p.CompletedAt = DateTime.UtcNow;
                    for (var i = 1; i <= count; i++)
                    {
                        var attempt = AttemptFor(f, data with { AssignmentId = assignmentId, StudentId = p.StudentId }, (ulong)i, AttemptStatus.PendingAnalysis);
                        db.Attempts.Add(attempt);
                    }
                }
                counter.Count = 0; var watch = Stopwatch.StartNew(); await db.SaveChangesAsync(); watch.Stop();
                if (sample >= 0) { (observe ? newSubmit : baselineSubmit).Add(watch.Elapsed.TotalMilliseconds); (observe ? queryNew : queryBaseline).Add(counter.Count); }
                // Prepare AI jobs outside the measured commit. No provider calls; measure persistence only.
                var attempts = await db.Attempts.Where(a => a.AssignmentId == assignmentId).ToArrayAsync();
                db.AIAnalysisJobs.Add(new() { CenterId = f.CenterId, AttemptId = attempts[0].AttemptId, Status = AIJobStatus.Pending,
                    CorrelationId = Guid.NewGuid().ToString("N"), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow, AvailableAt = DateTime.UtcNow });
                await db.SaveChangesAsync(); var job = await db.AIAnalysisJobs.SingleAsync(j => j.AttemptId == attempts[0].AttemptId);
                job.Status = AIJobStatus.FailedTerminal; job.CompletedAt = DateTime.UtcNow; job.UpdatedAt = DateTime.UtcNow; attempts[0].Status = AttemptStatus.AnalysisFailed;
                watch.Restart(); await db.SaveChangesAsync(); watch.Stop(); if (sample >= 0) (observe ? newAi : baselineAi).Add(watch.Elapsed.TotalMilliseconds);
            }
            static double Percentile(List<double> values, double fraction) => values.Order().ElementAt((int)Math.Ceiling(values.Count * fraction) - 1);
            var beforeSubmit = Percentile(baselineSubmit, .95); var afterSubmit = Percentile(newSubmit, .95); var beforeAi = Percentile(baselineAi, .95); var afterAi = Percentile(newAi, .95);
            output.WriteLine("NOTIFICATION_BENCHMARK " + JsonSerializer.Serialize(new { questionsPerStudent = count, students = 2, samples = 20,
                submitBaselineP50 = Percentile(baselineSubmit, .5), submitBaselineP95 = beforeSubmit, submitNewP50 = Percentile(newSubmit, .5), submitNewP95 = afterSubmit,
                aiCommitBaselineP50 = Percentile(baselineAi, .5), aiCommitBaselineP95 = beforeAi, aiCommitNewP50 = Percentile(newAi, .5), aiCommitNewP95 = afterAi,
                submitCommandsBefore = queryBaseline.Average(), submitCommandsAfter = queryNew.Average(), geminiRequests = 0 }));
            Assert.True(afterSubmit <= beforeSubmit + Math.Max(beforeSubmit * .1, 100), $"Submission p95 regression: {beforeSubmit:F2} → {afterSubmit:F2}ms");
            Assert.True(afterAi <= beforeAi + Math.Max(beforeAi * .1, 100), $"AI commit p95 regression: {beforeAi:F2} → {afterAi:F2}ms");
        }
    }
    private sealed class CommandCounter : DbCommandInterceptor
    {
        public int Count;
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData data, InterceptionResult<DbDataReader> result, CancellationToken ct = default)
        { Count++; return ValueTask.FromResult(result); }
        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData data, InterceptionResult<int> result, CancellationToken ct = default)
        { Count++; return ValueTask.FromResult(result); }
        public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand command, CommandEventData data, InterceptionResult<object> result, CancellationToken ct = default)
        { Count++; return ValueTask.FromResult(result); }
    }
}
