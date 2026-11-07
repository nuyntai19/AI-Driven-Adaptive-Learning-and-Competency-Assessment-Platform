using System.Text.Json;
using System.Text.Json.Serialization;
using EduTwin.BLL.CurriculumAndQuestions;
using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.PromptEvaluation;

// These are authored UI values only. Do not add request IDs, preliminary grades,
// canonical answers, repair hints, verified evidence, policy/configuration or secrets.
public sealed record UserInput
{
    public required TeacherInput Teacher { get; init; }
    public required StudentInput Student { get; init; }
}

public sealed record TeacherInput
{
    public required string QuestionText { get; init; }
    public required QuestionType QuestionType { get; init; }
    public required QuestionAnswerEvaluationMode AnswerEvaluationMode { get; init; }
    public required string CorrectAnswer { get; init; }
    public required string Solution { get; init; }
    public string? ExpectedReasoning { get; init; }
    public string ContentLanguage { get; init; } = "vi";
    public decimal MaxScore { get; init; } = 10m;
    public required IReadOnlyList<string> KnowledgeTopics { get; init; }
    public IReadOnlyList<OptionInput> Options { get; init; } = [];
    public AuthoredCriteria GradingCriteria { get; init; } = new();
    public PngInput? ProblemImage { get; init; }
}

public sealed record OptionInput
{
    public required string Label { get; init; }
    public required string Text { get; init; }
    public required bool IsCorrect { get; init; }
}

public sealed record AuthoredCriteria
{
    public IReadOnlyList<string> RequiredIdeas { get; init; } = [];
    public IReadOnlyList<string> CommonErrors { get; init; } = [];
    public string ScoringNotes { get; init; } = "";
    public IReadOnlyList<AuthoredCriterion> Criteria { get; init; } = [];
}

public sealed record AuthoredCriterion
{
    public required string CriterionId { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public required decimal MaxScore { get; init; }
    public IReadOnlyList<string> VisualRequirements { get; init; } = [];
}

public sealed record StudentInput
{
    public required string FinalAnswer { get; init; }
    public string? AnswerDisplayLatex { get; init; }
    public string? ReasoningText { get; init; }
    public uint TimeSpentSeconds { get; init; } = 60;
    public decimal? Confidence { get; init; }
    public uint AnswerChanges { get; init; }
    public PngInput? ScratchpadImage { get; init; }
}

public sealed record PngInput
{
    public required string DataBase64 { get; init; }
}

public static class FixtureReader
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        PropertyNameCaseInsensitive = false,
        Converters = { new JsonStringEnumConverter(allowIntegerValues: false) }
    };

    public static UserInput Read(string text)
    {
        using var document = JsonDocument.Parse(text, new() { MaxDepth = 32 });
        NoDuplicates(document.RootElement);
        var input = JsonSerializer.Deserialize<UserInput>(text, Json) ?? throw new InvalidDataException();
        Validate(input);
        return input;
    }

    public static void NoDuplicates(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!names.Add(property.Name)) throw new InvalidDataException("Duplicate JSON field.");
                NoDuplicates(property.Value);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
            foreach (var item in element.EnumerateArray()) NoDuplicates(item);
    }

    private static void Text(string? value, int max, bool required = false)
    {
        if ((required && string.IsNullOrWhiteSpace(value)) || value?.Length > max)
            throw new InvalidDataException("Authored text is missing or exceeds its limit.");
    }

    public static byte[] Image(PngInput? image)
    {
        if (image is null) return [];
        if (image.DataBase64 is null || image.DataBase64.Length > (QuestionImageContent.MaxBytes + 2) / 3 * 4)
            throw new InvalidDataException("Invalid or oversized PNG.");
        byte[] data;
        try { data = Convert.FromBase64String(image.DataBase64); }
        catch (FormatException) { throw new InvalidDataException("Invalid PNG encoding."); }
        // Use the application's CRC/dimension/decompression checks, not extension checks.
        // This harness uses the stricter problem-image 2 MiB cap for BOTH image roles.
        if (!QuestionImageContent.TryValidate(data)) throw new InvalidDataException("Invalid PNG content.");
        return data;
    }

    public static void Validate(UserInput input)
    {
        if (input.Teacher is null || input.Student is null) throw new InvalidDataException();
        var t = input.Teacher; var s = input.Student;
        Text(t.QuestionText, 20000, true); Text(t.CorrectAnswer, 2048, true);
        Text(t.Solution, 20000, true); Text(t.ExpectedReasoning, 20000);
        Text(s.FinalAnswer, 2048, true); Text(s.AnswerDisplayLatex, 2048); Text(s.ReasoningText, 10000);
        if (!Enum.IsDefined(t.QuestionType) || !Enum.IsDefined(t.AnswerEvaluationMode) ||
            t.ContentLanguage is not ("vi" or "en") || !QuestionScoreLimits.IsValid(t.MaxScore) ||
            s.Confidence is < 0 or > 100 || s.TimeSpentSeconds > 86400 || s.AnswerChanges > 10000 ||
            t.KnowledgeTopics is null || t.KnowledgeTopics.Count is < 1 or > 100 ||
            t.Options is null || t.Options.Count > 20 || t.GradingCriteria is null)
            throw new InvalidDataException("Unsupported authored parameter.");
        foreach (var topic in t.KnowledgeTopics) Text(topic, 250, true);
        if (t.KnowledgeTopics.Distinct(StringComparer.Ordinal).Count() != t.KnowledgeTopics.Count)
            throw new InvalidDataException("Duplicate topic.");
        foreach (var option in t.Options)
        {
            if (option is null) throw new InvalidDataException();
            Text(option.Label, 10, true); Text(option.Text, 4000, true);
        }
        if (t.Options.Select(o => o.Label).Distinct(StringComparer.OrdinalIgnoreCase).Count() != t.Options.Count)
            throw new InvalidDataException("Duplicate option label.");
        var options = t.Options.Select(o => new QuestionOptionValidationItem(o.Label, o.Text, o.IsCorrect)).ToList();
        if (!new QuestionActivationPolicy().Validate(t.QuestionType, t.AnswerEvaluationMode, t.CorrectAnswer, options, out _))
            throw new InvalidDataException("Question type/evaluation mode/reference is invalid.");
        if (t.QuestionType == QuestionType.MultipleChoice &&
            (!t.Options.Any(o => o.IsCorrect && o.Label == t.CorrectAnswer) || !t.Options.Any(o => o.Label == s.FinalAnswer)))
            throw new InvalidDataException("Multiple-choice answers must use an authored option label.");
        if (t.QuestionType != QuestionType.MultipleChoice && t.Options.Count != 0)
            throw new InvalidDataException("Options belong to multiple-choice questions only.");
        var criteria = t.GradingCriteria;
        if (criteria.RequiredIdeas is null || criteria.CommonErrors is null || criteria.Criteria is null || criteria.ScoringNotes is null ||
            criteria.RequiredIdeas.Count > 100 || criteria.CommonErrors.Count > 100 || criteria.Criteria.Count > 20)
            throw new InvalidDataException();
        Text(criteria.ScoringNotes, 10000);
        foreach (var idea in criteria.RequiredIdeas.Concat(criteria.CommonErrors)) Text(idea, 4000, true);
        foreach (var criterion in criteria.Criteria)
        {
            if (criterion is null || criterion.MaxScore <= 0 || criterion.VisualRequirements is null || criterion.VisualRequirements.Count > 12)
                throw new InvalidDataException();
            Text(criterion.CriterionId, 64, true); Text(criterion.Title, 200, true); Text(criterion.Description, 2000, true);
            foreach (var visual in criterion.VisualRequirements) Text(visual, 500, true);
        }
        if (criteria.Criteria.Select(c => c.CriterionId).Distinct().Count() != criteria.Criteria.Count ||
            criteria.Criteria.Count > 0 && criteria.Criteria.Sum(c => c.MaxScore) != t.MaxScore)
            throw new InvalidDataException("Rubric IDs/scores are invalid.");
        if (GradingCriteriaValidator.Validate(RequestBridge.Criteria(criteria), t.MaxScore).Count != 0)
            throw new InvalidDataException("The application rejected the authored rubric.");
        _ = Image(t.ProblemImage); _ = Image(s.ScratchpadImage);
    }
}
