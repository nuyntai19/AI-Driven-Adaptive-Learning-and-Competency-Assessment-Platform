using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using EduTwin.BLL.AssessmentAndReasoning.AI;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.CurriculumAndQuestions;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.AI;

public sealed class AIAnalysisContractTests
{
    [Fact]
    public void SchemaVersion_IsExactlyAiAnalysisV1()
    {
        Assert.Equal("ai-analysis-v1", AIAnalysisContract.SchemaVersion);
    }

    [Fact]
    public void Request_PublicShape_IsExactAtEveryLevel()
    {
        AssertExactProperties(
            typeof(AnalyzeReasoningRequest),
            ("SchemaVersion", typeof(string), NullabilityState.NotNull, true),
            ("Language", typeof(string), NullabilityState.NotNull, true),
            ("Question", typeof(AnalyzeReasoningQuestion), NullabilityState.NotNull, true),
            ("StudentSubmission", typeof(AnalyzeReasoningStudentSubmission), NullabilityState.NotNull, true),
            ("AllowedKnowledgeNodes", typeof(IReadOnlyList<AnalyzeReasoningAllowedKnowledgeNode>), NullabilityState.NotNull, true));
        AssertExactProperties(
            typeof(AnalyzeReasoningQuestion),
            ("QuestionType", typeof(QuestionType), NullabilityState.NotNull, false),
            ("QuestionText", typeof(string), NullabilityState.NotNull, true),
            ("CorrectAnswer", typeof(string), NullabilityState.NotNull, true),
            ("Solution", typeof(string), NullabilityState.NotNull, true),
            ("ExpectedReasoning", typeof(string), NullabilityState.Nullable, false),
            ("GradingCriteria", typeof(AnalyzeReasoningGradingCriteria), NullabilityState.NotNull, true));
        AssertExactProperties(
            typeof(AnalyzeReasoningGradingCriteria),
            ("SchemaVersion", typeof(string), NullabilityState.NotNull, true),
            ("RequiredIdeas", typeof(IReadOnlyList<string>), NullabilityState.NotNull, true),
            ("CommonErrors", typeof(IReadOnlyList<string>), NullabilityState.NotNull, true),
            ("ScoringNotes", typeof(string), NullabilityState.NotNull, true));
        AssertExactProperties(
            typeof(AnalyzeReasoningStudentSubmission),
            ("FinalAnswer", typeof(string), NullabilityState.NotNull, true),
            ("ReasoningText", typeof(string), NullabilityState.Nullable, false),
            ("TimeSpentSeconds", typeof(uint), NullabilityState.NotNull, false),
            ("Confidence", typeof(decimal), NullabilityState.NotNull, false),
            ("AnswerChanges", typeof(uint), NullabilityState.NotNull, false));
        AssertExactProperties(
            typeof(AnalyzeReasoningAllowedKnowledgeNode),
            ("NodeId", typeof(string), NullabilityState.NotNull, true),
            ("NodeName", typeof(string), NullabilityState.NotNull, true));
    }

    [Fact]
    public void Response_PublicShape_IsExact()
    {
        AssertExactProperties(
            typeof(AnalyzeReasoningResponse),
            ("SchemaVersion", typeof(string), NullabilityState.NotNull, true),
            ("Language", typeof(string), NullabilityState.NotNull, true),
            ("MethodDetected", typeof(string), NullabilityState.Nullable, false),
            ("ReasoningQuality", typeof(int), NullabilityState.NotNull, false),
            ("ErrorType", typeof(ErrorType), NullabilityState.NotNull, false),
            ("Misconception", typeof(string), NullabilityState.Nullable, false),
            ("MissingSteps", typeof(IReadOnlyList<string>), NullabilityState.NotNull, true),
            ("RootCauseNodeIds", typeof(IReadOnlyList<string>), NullabilityState.NotNull, true),
            ("Confidence", typeof(int), NullabilityState.NotNull, false),
            ("Feedback", typeof(string), NullabilityState.NotNull, true));
    }

    [Fact]
    public void Request_SerializesToExactSection66LogicalJson()
    {
        using var document = SerializeToDocument(CreateVietnameseRequest());
        var root = document.RootElement;

        AssertObjectProperties(root, "schemaVersion", "language", "question", "studentSubmission", "allowedKnowledgeNodes");
        Assert.Equal("ai-analysis-v1", root.GetProperty("schemaVersion").GetString());
        Assert.Equal("vi", root.GetProperty("language").GetString());

        var question = root.GetProperty("question");
        AssertObjectProperties(
            question,
            "questionType",
            "questionText",
            "correctAnswer",
            "solution",
            "expectedReasoning",
            "gradingCriteria");
        Assert.Equal("MultipleChoice", question.GetProperty("questionType").GetString());
        Assert.Equal("Giải phương trình ...", question.GetProperty("questionText").GetString());
        Assert.Equal("B", question.GetProperty("correctAnswer").GetString());
        Assert.Equal("Lời giải chuẩn", question.GetProperty("solution").GetString());
        Assert.Equal("Các ý mong đợi", question.GetProperty("expectedReasoning").GetString());

        var gradingCriteria = question.GetProperty("gradingCriteria");
        AssertObjectProperties(gradingCriteria, "schemaVersion", "requiredIdeas", "commonErrors", "scoringNotes");
        Assert.Equal("1.0", gradingCriteria.GetProperty("schemaVersion").GetString());
        Assert.Equal("Xác định điều kiện", gradingCriteria.GetProperty("requiredIdeas")[0].GetString());
        Assert.Equal("Quên điều kiện", gradingCriteria.GetProperty("commonErrors")[0].GetString());
        Assert.Equal("Ưu tiên reasoning", gradingCriteria.GetProperty("scoringNotes").GetString());

        var submission = root.GetProperty("studentSubmission");
        AssertObjectProperties(
            submission,
            "finalAnswer",
            "reasoningText",
            "timeSpentSeconds",
            "confidence",
            "answerChanges");
        Assert.Equal("B", submission.GetProperty("finalAnswer").GetString());
        Assert.Equal("Em đặt điều kiện...", submission.GetProperty("reasoningText").GetString());
        Assert.Equal(165u, submission.GetProperty("timeSpentSeconds").GetUInt32());
        Assert.Equal(80m, submission.GetProperty("confidence").GetDecimal());
        Assert.Equal(1u, submission.GetProperty("answerChanges").GetUInt32());

        var allowedNode = Assert.Single(root.GetProperty("allowedKnowledgeNodes").EnumerateArray());
        AssertObjectProperties(allowedNode, "nodeId", "nodeName");
        Assert.Equal("101", allowedNode.GetProperty("nodeId").GetString());
        Assert.Equal("Mũ và Logarit", allowedNode.GetProperty("nodeName").GetString());
    }

    [Fact]
    public void Response_SerializesToExactSection67LogicalJson()
    {
        using var document = SerializeToDocument(CreateVietnameseResponse());
        var root = document.RootElement;

        AssertObjectProperties(
            root,
            "schemaVersion",
            "language",
            "methodDetected",
            "reasoningQuality",
            "errorType",
            "misconception",
            "missingSteps",
            "rootCauseNodeIds",
            "confidence",
            "feedback");
        Assert.Equal("ai-analysis-v1", root.GetProperty("schemaVersion").GetString());
        Assert.Equal("vi", root.GetProperty("language").GetString());
        Assert.Equal("Đưa hai vế về cùng cơ số", root.GetProperty("methodDetected").GetString());
        Assert.Equal(72, root.GetProperty("reasoningQuality").GetInt32());
        Assert.Equal("Reasoning", root.GetProperty("errorType").GetString());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("misconception").ValueKind);
        Assert.Equal("Chưa đối chiếu điều kiện", root.GetProperty("missingSteps")[0].GetString());
        Assert.Equal("101", root.GetProperty("rootCauseNodeIds")[0].GetString());
        Assert.Equal(85, root.GetProperty("confidence").GetInt32());
        Assert.Equal(
            "Em đã chọn đúng phương pháp nhưng cần đối chiếu điều kiện.",
            root.GetProperty("feedback").GetString());
    }

    [Fact]
    public void Request_NullableFields_PreserveNullSemantics()
    {
        var request = CreateVietnameseRequest(expectedReasoning: null, reasoningText: null);

        using var document = SerializeToDocument(request);

        Assert.Equal(
            JsonValueKind.Null,
            document.RootElement.GetProperty("question").GetProperty("expectedReasoning").ValueKind);
        Assert.Equal(
            JsonValueKind.Null,
            document.RootElement.GetProperty("studentSubmission").GetProperty("reasoningText").ValueKind);
    }

    [Fact]
    public void Response_NullableFields_PreserveNullSemantics()
    {
        var response = CreateVietnameseResponse() with
        {
            MethodDetected = null,
            Misconception = null
        };

        using var document = SerializeToDocument(response);

        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("methodDetected").ValueKind);
        Assert.Equal(JsonValueKind.Null, document.RootElement.GetProperty("misconception").ValueKind);
    }

    [Fact]
    public void RootCauseAndAllowedNodeIds_SerializeAsStrings()
    {
        using var requestDocument = SerializeToDocument(CreateVietnameseRequest());
        using var responseDocument = SerializeToDocument(CreateVietnameseResponse());

        Assert.Equal(
            JsonValueKind.String,
            requestDocument.RootElement.GetProperty("allowedKnowledgeNodes")[0].GetProperty("nodeId").ValueKind);
        Assert.Equal(
            JsonValueKind.String,
            responseDocument.RootElement.GetProperty("rootCauseNodeIds")[0].ValueKind);
    }

    [Fact]
    public void EnumFields_SerializeAsCaseSensitiveNamesAndRejectNumericWriteConfiguration()
    {
        var options = CreateSerializerOptions();
        using var requestDocument = JsonDocument.Parse(JsonSerializer.Serialize(CreateVietnameseRequest(), options));
        using var responseDocument = JsonDocument.Parse(JsonSerializer.Serialize(CreateVietnameseResponse(), options));

        Assert.Equal(
            "MultipleChoice",
            requestDocument.RootElement.GetProperty("question").GetProperty("questionType").GetString());
        Assert.Equal("Reasoning", responseDocument.RootElement.GetProperty("errorType").GetString());
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize((QuestionType)int.MaxValue, options));
        Assert.Throws<JsonException>(() => JsonSerializer.Serialize((ErrorType)int.MaxValue, options));
    }

    [Fact]
    public void ContractGraph_ContainsNoDalEntityOrMutableListProperty()
    {
        var contractTypes = GetContractGraphTypes(typeof(AnalyzeReasoningRequest), typeof(AnalyzeReasoningResponse));
        var properties = contractTypes.SelectMany(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public));

        Assert.DoesNotContain(contractTypes, type =>
            type.FullName?.Contains("EduTwin.DAL", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(properties, property =>
            property.PropertyType.IsGenericType
            && property.PropertyType.GetGenericTypeDefinition() == typeof(List<>));
    }

    [Fact]
    public void ContractGraph_ContainsNoForbiddenIdentitySecretOrJobInternalFields()
    {
        var forbiddenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "CenterId", "CenterName", "StudentId", "AttemptId", "AssignmentId", "QuestionId",
            "TeacherId", "ClassId", "Username", "Email", "Password", "PasswordHash", "Token",
            "Jwt", "RefreshToken", "JobId", "AnalysisJobId", "CorrelationId", "LeaseOwner",
            "LeaseUntil", "RetryCount", "Provider", "Model", "ModelName", "ApiKey",
            "RawRequest", "RawResponse", "RawPayload", "Profile", "Skipped"
        };
        var propertyNames = GetContractGraphTypes(typeof(AnalyzeReasoningRequest), typeof(AnalyzeReasoningResponse))
            .SelectMany(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            .Select(property => property.Name)
            .ToArray();

        Assert.DoesNotContain(propertyNames, forbiddenNames.Contains);
    }

    [Fact]
    public void Collections_AreNonNullInValidFixtureAndExposeReadOnlyInterfaces()
    {
        var request = CreateVietnameseRequest();
        var response = CreateVietnameseResponse();

        Assert.NotNull(request.AllowedKnowledgeNodes);
        Assert.NotNull(request.Question.GradingCriteria.RequiredIdeas);
        Assert.NotNull(request.Question.GradingCriteria.CommonErrors);
        Assert.NotNull(response.MissingSteps);
        Assert.NotNull(response.RootCauseNodeIds);
        Assert.Equal(
            typeof(IReadOnlyList<AnalyzeReasoningAllowedKnowledgeNode>),
            typeof(AnalyzeReasoningRequest).GetProperty("AllowedKnowledgeNodes")!.PropertyType);
        Assert.Equal(
            typeof(IReadOnlyList<string>),
            typeof(AnalyzeReasoningGradingCriteria).GetProperty("RequiredIdeas")!.PropertyType);
        Assert.Equal(
            typeof(IReadOnlyList<string>),
            typeof(AnalyzeReasoningGradingCriteria).GetProperty("CommonErrors")!.PropertyType);
        Assert.Equal(
            typeof(IReadOnlyList<string>),
            typeof(AnalyzeReasoningResponse).GetProperty("MissingSteps")!.PropertyType);
        Assert.Equal(
            typeof(IReadOnlyList<string>),
            typeof(AnalyzeReasoningResponse).GetProperty("RootCauseNodeIds")!.PropertyType);
    }

    [Fact]
    public void RoundTrip_PreservesVietnameseUnicode()
    {
        var request = CreateVietnameseRequest();
        var json = JsonSerializer.Serialize(request, CreateSerializerOptions());

        var roundTrip = JsonSerializer.Deserialize<AnalyzeReasoningRequest>(json, CreateSerializerOptions());

        Assert.NotNull(roundTrip);
        Assert.Equal("Lời giải chuẩn", roundTrip.Question.Solution);
        Assert.Equal("Xác định điều kiện", Assert.Single(roundTrip.Question.GradingCriteria.RequiredIdeas));
        Assert.Equal("Mũ và Logarit", Assert.Single(roundTrip.AllowedKnowledgeNodes).NodeName);
        Assert.Equal("Em đặt điều kiện...", roundTrip.StudentSubmission.ReasoningText);
    }

    [Fact]
    public void EnglishFixture_RoundTripPreservesLanguageAndFeedback()
    {
        var response = CreateVietnameseResponse() with
        {
            Language = "en",
            Feedback = "Your method is correct, but verify the final condition."
        };
        var json = JsonSerializer.Serialize(response, CreateSerializerOptions());

        var roundTrip = JsonSerializer.Deserialize<AnalyzeReasoningResponse>(json, CreateSerializerOptions());

        Assert.NotNull(roundTrip);
        Assert.Equal("en", roundTrip.Language);
        Assert.Equal("Your method is correct, but verify the final condition.", roundTrip.Feedback);
    }

    private static AnalyzeReasoningRequest CreateVietnameseRequest(
        string? expectedReasoning = "Các ý mong đợi",
        string? reasoningText = "Em đặt điều kiện...") =>
        new()
        {
            SchemaVersion = AIAnalysisContract.SchemaVersion,
            Language = "vi",
            Question = new AnalyzeReasoningQuestion
            {
                QuestionType = QuestionType.MultipleChoice,
                QuestionText = "Giải phương trình ...",
                CorrectAnswer = "B",
                Solution = "Lời giải chuẩn",
                ExpectedReasoning = expectedReasoning,
                GradingCriteria = new AnalyzeReasoningGradingCriteria
                {
                    SchemaVersion = "1.0",
                    RequiredIdeas = ["Xác định điều kiện"],
                    CommonErrors = ["Quên điều kiện"],
                    ScoringNotes = "Ưu tiên reasoning"
                }
            },
            StudentSubmission = new AnalyzeReasoningStudentSubmission
            {
                FinalAnswer = "B",
                ReasoningText = reasoningText,
                TimeSpentSeconds = 165,
                Confidence = 80m,
                AnswerChanges = 1
            },
            AllowedKnowledgeNodes =
            [
                new AnalyzeReasoningAllowedKnowledgeNode
                {
                    NodeId = "101",
                    NodeName = "Mũ và Logarit"
                }
            ]
        };

    private static AnalyzeReasoningResponse CreateVietnameseResponse() =>
        new()
        {
            SchemaVersion = AIAnalysisContract.SchemaVersion,
            Language = "vi",
            MethodDetected = "Đưa hai vế về cùng cơ số",
            ReasoningQuality = 72,
            ErrorType = ErrorType.Reasoning,
            Misconception = null,
            MissingSteps = ["Chưa đối chiếu điều kiện"],
            RootCauseNodeIds = ["101"],
            Confidence = 85,
            Feedback = "Em đã chọn đúng phương pháp nhưng cần đối chiếu điều kiện."
        };

    private static JsonDocument SerializeToDocument<T>(T value) =>
        JsonDocument.Parse(JsonSerializer.Serialize(value, CreateSerializerOptions()));

    private static JsonSerializerOptions CreateSerializerOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter(namingPolicy: null, allowIntegerValues: false));
        return options;
    }

    private static void AssertObjectProperties(JsonElement element, params string[] expectedNames)
    {
        Assert.Equal(JsonValueKind.Object, element.ValueKind);
        Assert.Equal(expectedNames, element.EnumerateObject().Select(property => property.Name).ToArray());
    }

    private static void AssertExactProperties(
        Type type,
        params (string Name, Type Type, NullabilityState Nullability, bool Required)[] expected)
    {
        var actual = type.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .ToDictionary(property => property.Name, StringComparer.Ordinal);
        var nullabilityContext = new NullabilityInfoContext();

        Assert.Equal(expected.Length, actual.Count);
        foreach (var expectedProperty in expected)
        {
            var property = Assert.Contains(expectedProperty.Name, actual);
            Assert.Equal(expectedProperty.Type, property.PropertyType);
            Assert.Equal(expectedProperty.Nullability, nullabilityContext.Create(property).ReadState);
            Assert.Equal(
                expectedProperty.Required,
                property.IsDefined(typeof(RequiredMemberAttribute), inherit: false));
        }
    }

    private static IReadOnlySet<Type> GetContractGraphTypes(params Type[] roots)
    {
        var visited = new HashSet<Type>();
        var pending = new Stack<Type>(roots);

        while (pending.TryPop(out var current))
        {
            if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(IReadOnlyList<>))
            {
                pending.Push(current.GetGenericArguments()[0]);
                continue;
            }

            if (current == typeof(string)
                || current.IsPrimitive
                || current.IsEnum
                || current == typeof(decimal)
                || current.Namespace != typeof(AnalyzeReasoningRequest).Namespace
                || !visited.Add(current))
            {
                continue;
            }

            foreach (var property in current.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                pending.Push(property.PropertyType);
            }
        }

        return visited;
    }
}
