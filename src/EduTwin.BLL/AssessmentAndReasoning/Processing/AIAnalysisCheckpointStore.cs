using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EduTwin.BLL.AssessmentAndReasoning.AI;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.AssessmentAndReasoning.Processing;

public interface IAIAnalysisCheckpointStore
{
    Task<AnalyzeReasoningResponse?> ReadAsync(Guid centerId, ulong attemptId, string fingerprint, CancellationToken token);
    Task<bool> SaveAsync(AIAnalysisJob job, string workerId, string fingerprint, AnalyzeReasoningResponse response, DateTime now, CancellationToken token);
}

public sealed class AIAnalysisCheckpointStore(EduTwinDbContext db, TimeProvider clock) : IAIAnalysisCheckpointStore
{
    public static string Fingerprint(AnalyzeReasoningRequest request, string profile, ulong questionVersion)
    {
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        hash.AppendData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { Profile = profile, QuestionVersion = questionVersion, Request = request })));
        foreach (var image in request.StudentSubmission.ImageParts)
        {
            hash.AppendData(Encoding.UTF8.GetBytes(image.MimeType));
            hash.AppendData(SHA256.HashData(image.Data));
        }
        return Convert.ToHexString(hash.GetHashAndReset());
    }

    public async Task<AnalyzeReasoningResponse?> ReadAsync(Guid centerId, ulong attemptId, string fingerprint, CancellationToken token)
    {
        var checkpoint = await db.AIAnalysisCheckpoints.AsNoTracking().SingleOrDefaultAsync(x =>
            x.CenterId == centerId && x.AttemptId == attemptId && x.RequestFingerprint == fingerprint, token);
        if (checkpoint is null) return null;
        try { return JsonSerializer.Deserialize<AnalyzeReasoningResponse>(checkpoint.ResponseJson); }
        catch (JsonException) { return null; } // Rebuild a corrupt checkpoint, never expose raw JSON diagnostics.
    }

    public async Task<bool> SaveAsync(AIAnalysisJob job, string workerId, string fingerprint, AnalyzeReasoningResponse response, DateTime now, CancellationToken token)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(token);
        // Fence the write against lease replacement, not just a check before an unprotected upsert.
        if (db.Database.IsRelational())
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT analysis_job_id FROM ai_analysis_jobs WHERE center_id = {job.CenterId} AND analysis_job_id = {job.AnalysisJobId} FOR UPDATE", token);
        now = clock.GetUtcNow().UtcDateTime;
        if (!await db.AIAnalysisJobs.AsNoTracking().AnyAsync(x => x.CenterId == job.CenterId &&
            x.AnalysisJobId == job.AnalysisJobId && x.RowVersion == job.RowVersion &&
            x.Status == EduTwin.Contracts.AssessmentAndReasoning.AIJobStatus.Processing &&
            x.LeaseOwner == workerId && x.LeaseUntil > now, token)) return false;
        var row = await db.AIAnalysisCheckpoints.SingleOrDefaultAsync(x => x.CenterId == job.CenterId && x.AttemptId == job.AttemptId, token);
        if (row is null)
        {
            row = new AIAnalysisCheckpoint { CenterId = job.CenterId, AttemptId = job.AttemptId };
            db.AIAnalysisCheckpoints.Add(row);
        }
        row.RequestFingerprint = fingerprint;
        row.ResponseJson = JsonSerializer.Serialize(response);
        row.CreatedAt = now;
        await db.SaveChangesAsync(token);
        await transaction.CommitAsync(token);
        db.ChangeTracker.Clear();
        return true;
    }
}
