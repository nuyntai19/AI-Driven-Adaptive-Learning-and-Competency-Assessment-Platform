using EduTwin.API.AssessmentAndReasoning.AI;
using EduTwin.BLL.Recommendations.Services;
using Google.GenAI.Types;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace EduTwin.BLL.Tests.Recommendations;

public class LearningTutorGeneratorTests
{
    private sealed class Client(string response) : IGeminiGenerateContentClient
    {
        public string Prompt="";
        public Task<GeminiGenerateContentResult> GenerateContentAsync(string model,string prompt,GenerateContentConfig config,CancellationToken token)
        { Prompt=prompt;Assert.Equal("learning_path",EduTwin.BLL.Platform.Operations.AiUsageStage.Current);return Task.FromResult(new GeminiGenerateContentResult(response,null,null,null)); }
    }
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task TutorAdapter_UsesConfiguredClientAndVietnamesePrompt_AndRejectsMalformedOutput(bool valid)
    {
        var client=new Client(valid ? """{"comment":"Em cần sửa cách dùng đại từ.","aiSolution":"Who chỉ người, which chỉ vật trong ví dụ này.","understanding":"Misunderstood","correction":"Đối chiếu danh từ đứng trước."}""" : "not-json");
        var generator=new LearningTutorFeedbackGenerator(client,null!,Options.Create(new GeminiOptions{ApiKey="unit-test-only",Model="unit-test-model"}),Options.Create(new AIGradingOptions()),NullLogger<LearningTutorFeedbackGenerator>.Instance,TimeProvider.System);
        var result=await generator.GenerateAsync(new LearningTutorContext("Study","Mệnh đề quan hệ","Bài học","Which chỉ vật","Who chỉ vật",null,null),CancellationToken.None);
        Assert.Contains("tiếng Việt",client.Prompt);
        Assert.Equal(valid?"Generated":"Unavailable",result.Status);
        if(valid){Assert.Equal("Gemini",result.Provider);Assert.Equal("Misunderstood",result.Understanding);Assert.NotEmpty(result.AiSolution);}
        else Assert.Empty(result.AiSolution);
    }
}
