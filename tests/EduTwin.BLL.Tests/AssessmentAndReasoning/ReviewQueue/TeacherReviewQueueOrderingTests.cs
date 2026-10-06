using EduTwin.BLL.AssessmentAndReasoning.ReviewQueue;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.ReviewQueue;

public sealed class TeacherReviewQueueOrderingTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void OrderBeforePaginationCompilesWithActualMySqlProvider(bool studentDetail)
    {
        // SQL compilation only; no connection or changes to the user's database.
        var centerId = Guid.NewGuid();
        var tenant = new TenantContext();
        tenant.Initialize(centerId, Guid.NewGuid(), "Teacher", 1);
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseMySQL("Server=127.0.0.1;Database=unused_sql_compilation;User=test;Password=test").Options;
        using var db = new EduTwinDbContext(options, tenant);
        var sql = TeacherReviewQueueOrdering.Apply(db.EvidenceAssessments.AsNoTracking(), db, centerId, studentDetail)
            .Skip(1).Take(20).ToQueryString();
        Assert.Contains("ORDER BY", sql);
        Assert.Contains("LIMIT", sql);
        Assert.Contains(studentDetail ? "order_index" : "evaluated_at", sql);
        Assert.Contains("center_id", sql);
    }
}
