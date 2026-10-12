using System.Data;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EduTwin.BLL.KnowledgeGraph;

internal static class GraphMutationTransaction
{
    // Serialize authoring per subject. Optimistic edge versions alone cannot prevent
    // two different inserts (A -> B and B -> A) both passing the DAG check.
    public static async Task<IDbContextTransaction?> BeginAsync(EduTwinDbContext db, Guid center, Guid subject, CancellationToken ct)
    {
        if (!db.Database.IsRelational()) return null;
        var tx = await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct);
        try
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"SELECT subject_id FROM subjects WHERE center_id={center.ToString("D")} AND subject_id={subject.ToString("D")} FOR UPDATE", ct);
            return tx;
        }
        catch { await tx.DisposeAsync(); throw; }
    }
}
