using System.Text.Json;
using System.Text.Json.Nodes;
using EduTwin.BLL.Recommendations;
using Google.GenAI.Types;
using Microsoft.Extensions.Options;

namespace EduTwin.API.AssessmentAndReasoning.AI;

public sealed class GeminiLearningPathPlanEnricher : ILearningPathPlanEnricher
{
    private readonly IGeminiGenerateContentClient _client;
    private readonly GeminiOptions _options;

    public GeminiLearningPathPlanEnricher(IGeminiGenerateContentClient client, IOptions<GeminiOptions> options)
    {
        _client = client;
        _options = options.Value;
    }

    public async Task<LearningPathEnrichmentResult?> EnrichAsync(LearningPathEnrichmentRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.Model) || _options.GetAllApiKeys().Count == 0) return null;
        var allowedIds = request.Sessions.Select(s => s.SessionId).ToHashSet(StringComparer.Ordinal);
        var prompt = "Bạn là biên tập viên kế hoạch học tập EduTwin. Chỉ diễn đạt rõ hơn dữ liệu JSON đã cho. " +
                     "Không tạo topic, question ID, mastery, prerequisite, session ID hay thời lượng mới. " +
                     "Giữ nguyên sessionId; viết tiếng Việt thân thiện, hoạt động online/offline cụ thể. Trả đúng JSON schema.\nINPUT:\n" +
                     JsonSerializer.Serialize(request, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        var result = await _client.GenerateContentAsync(_options.Model, prompt, CreateConfig(), cancellationToken);
        var parsed = JsonSerializer.Deserialize<ProviderResult>(result.ResponseText, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        if (parsed is null || parsed.Sessions.Any(s => !allowedIds.Contains(s.SessionId))) return null;
        return new LearningPathEnrichmentResult(parsed.Summary,
            parsed.Sessions.Select(s => new LearningPathEnrichmentSessionOutput(s.SessionId, s.Objective, s.OnlineActivities, s.OfflineActivities)).ToList());
    }

    private static GenerateContentConfig CreateConfig()
    {
        var stringArray = new JsonObject { ["type"] = "array", ["items"] = new JsonObject { ["type"] = "string" } };
        var session = new JsonObject
        {
            ["type"] = "object", ["additionalProperties"] = false,
            ["properties"] = new JsonObject
            {
                ["sessionId"] = new JsonObject { ["type"] = "string" },
                ["objective"] = new JsonObject { ["type"] = "string" },
                ["onlineActivities"] = stringArray.DeepClone(),
                ["offlineActivities"] = stringArray.DeepClone()
            },
            ["required"] = new JsonArray(JsonValue.Create("sessionId"), JsonValue.Create("objective"), JsonValue.Create("onlineActivities"), JsonValue.Create("offlineActivities"))
        };
        return new GenerateContentConfig
        {
            ResponseMimeType = "application/json", CandidateCount = 1, Temperature = 0,
            ResponseJsonSchema = new JsonObject
            {
                ["type"] = "object", ["additionalProperties"] = false,
                ["properties"] = new JsonObject
                {
                    ["summary"] = new JsonObject { ["type"] = "string" },
                    ["sessions"] = new JsonObject { ["type"] = "array", ["items"] = session }
                },
                ["required"] = new JsonArray(JsonValue.Create("summary"), JsonValue.Create("sessions"))
            }
        };
    }

    private sealed class ProviderResult
    {
        public string Summary { get; set; } = string.Empty;
        public List<ProviderSession> Sessions { get; set; } = new();
    }

    private sealed class ProviderSession
    {
        public string SessionId { get; set; } = string.Empty;
        public string Objective { get; set; } = string.Empty;
        public List<string> OnlineActivities { get; set; } = new();
        public List<string> OfflineActivities { get; set; } = new();
    }
}
