using EduTwin.BLL.AssessmentAndReasoning.Attachments;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Attachments;

public sealed class AttemptAttachmentNonceQueryTests
{
    private static readonly Guid CenterId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private const string NonceA = "11111111111111111111111111111111";
    private const string NonceB = "22222222222222222222222222222222";

    private static ITenantIdAccessor Tenant()
    {
        var tenant = new Mock<ITenantIdAccessor>();
        tenant.SetupGet(x => x.CenterId).Returns(CenterId);
        return tenant.Object;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void VerifiedNonces_CompileWithActualMySqlProvider_WithoutCollectionParameter(int count)
    {
        // SQL compilation only: no connection or mutation of the user's database.
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseMySQL("Server=127.0.0.1;Database=unused_sql_compilation;User=test;Password=test")
            .Options;
        using var db = new EduTwinDbContext(options, Tenant());
        var nonces = new[] { NonceA, NonceB }.Take(count);
        var sql = AttemptAttachmentNonceQuery.ForUploadNonces(db.AttemptAttachments, CenterId, nonces)
            .Select(a => a.AttachmentId).ToQueryString();

        Assert.Contains("upload_nonce", sql);
        Assert.Contains("center_id", sql);
        Assert.Contains(NonceA, sql);
        if (count == 2) Assert.Contains(NonceB, sql);
        Assert.DoesNotContain("@nonceList", sql);
    }

    [Fact]
    public async Task NonceMatch_IsTenantScoped_AndDoesNotMatchUnrelatedNonce()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new EduTwinDbContext(options, Tenant());
        db.AttemptAttachments.AddRange(
            Attachment(1, CenterId, NonceA),
            Attachment(2, Guid.NewGuid(), NonceB));
        await db.SaveChangesAsync();

        Assert.True(await AttemptAttachmentNonceQuery.ForUploadNonces(db.AttemptAttachments, CenterId,
            new[] { NonceA, NonceA }).AnyAsync());
        Assert.False(await AttemptAttachmentNonceQuery.ForUploadNonces(db.AttemptAttachments, CenterId,
            new[] { NonceB }).AnyAsync());
        Assert.False(await AttemptAttachmentNonceQuery.ForUploadNonces(db.AttemptAttachments, CenterId,
            Array.Empty<string>()).AnyAsync());
    }

    private static AttemptAttachment Attachment(ulong id, Guid center, string nonce) => new()
    {
        AttachmentId = id, AttemptId = id, CenterId = center, UploadNonce = nonce,
        FileName = "scratchpad.png", ContentType = "image/png", StorageKey = $"test-{id}",
        FileSizeBytes = 10, CreatedAt = DateTime.UtcNow
    };
}
