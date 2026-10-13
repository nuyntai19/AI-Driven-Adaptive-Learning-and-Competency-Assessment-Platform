using System.Security.Cryptography;
using EduTwin.BLL.AssessmentAndReasoning.AI;
using EduTwin.BLL.AssessmentAndReasoning.Processing;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.DAL.CurriculumAndQuestions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Processing;

public sealed partial class AIAnalysisJobProcessorTests
{
    [Fact]
    public async Task ExecuteAsync_ProblemImage_IsLoadedAndSentAsQuestionContext()
    {
        var store = new InMemoryDatabaseRoot(); var databaseName = Guid.NewGuid().ToString(); var centerId = Guid.NewGuid();
        await SeedAsync(store, databaseName, centerId, retryCount: 0);
        var tenant = new TenantContext(); using var scope = tenant.BeginScope(centerId);
        await using var context = CreateContext(store, databaseName, tenant);
        var q = await context.Questions.SingleAsync(); q.HasImage = true;
        var bytes = CurriculumAndQuestions.QuestionImageFixture.Bytes;
        context.QuestionImages.Add(new QuestionImage { CenterId = centerId, QuestionId = q.QuestionId, Data = bytes,
            Sha256 = Convert.ToHexString(SHA256.HashData(bytes)), CreatedAt = UtcNow });
        await context.SaveChangesAsync();
        var ai = new RecordingAIService((request, _) =>
        {
            Assert.Equal(bytes, Assert.Single(request.Question.ImageParts).Data);
            Assert.Empty(request.StudentSubmission.ImageParts);
            Assert.Equal(1, request.Question.ImageCount);
            return Task.FromResult(ValidResponse("vi"));
        });
        var result = await CreateSut(context, tenant, UtcNow, ai).ExecuteAsync(1, "worker-current", CancellationToken.None);
        Assert.Equal(AIAnalysisJobProcessingOutcome.Completed, result.Outcome); Assert.Equal(1, ai.CallCount);
    }
}
