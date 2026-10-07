using System.Collections.Generic;
using System.Text.Json;
using EduTwin.Contracts.Assignments;

namespace EduTwin.BLL.Assignments;

public static class DraftAnswersHelper
{
    public static (List<AssignmentDraftAnswerItemDto> Answers, int Version) ParseDraft(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return (new List<AssignmentDraftAnswerItemDto>(), 0);
        }

        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind == JsonValueKind.Array)
            {
                var list = JsonSerializer.Deserialize<List<AssignmentDraftAnswerItemDto>>(json)
                    ?? new List<AssignmentDraftAnswerItemDto>();
                return (list, 0);
            }

            if (doc.RootElement.ValueKind == JsonValueKind.Object)
            {
                int version = 0;
                if (doc.RootElement.TryGetProperty("version", out var v) && v.TryGetInt32(out var vInt))
                {
                    version = vInt;
                }

                List<AssignmentDraftAnswerItemDto> list = new();
                if (doc.RootElement.TryGetProperty("answers", out var a))
                {
                    list = JsonSerializer.Deserialize<List<AssignmentDraftAnswerItemDto>>(a.GetRawText())
                        ?? new List<AssignmentDraftAnswerItemDto>();
                }

                return (list, version);
            }
        }
        catch
        {
            // fallback for invalid json
        }

        return (new List<AssignmentDraftAnswerItemDto>(), 0);
    }

    public static string SerializeDraft(int version, List<AssignmentDraftAnswerItemDto> answers)
    {
        var container = new
        {
            version,
            answers
        };
        return JsonSerializer.Serialize(container);
    }

    public static string SerializeDraft(List<AssignmentDraftAnswerItemDto> answers, int version = 0)
        => SerializeDraft(version, answers);
}
