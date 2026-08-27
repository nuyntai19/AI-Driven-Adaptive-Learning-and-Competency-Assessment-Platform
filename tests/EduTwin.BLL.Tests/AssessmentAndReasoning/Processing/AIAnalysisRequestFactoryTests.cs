using System.Text.Json;
using EduTwin.BLL.AssessmentAndReasoning.AI;
using EduTwin.BLL.AssessmentAndReasoning.Processing;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.KnowledgeGraph;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Processing;

public sealed class AIAnalysisRequestFactoryTests
{
    [Theory]
    [InlineData("vi")]
    [InlineData("en")]
    public void Create_MapsExactMinimalRequestWithoutMutatingInputs(string language)
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var attempt = CreateAttempt(centerId, language);
        var question = CreateQuestion(centerId, subjectId, language);
        var nodes = new[]
        {
            CreateNode(2, centerId, subjectId, "Second"),
            CreateNode(9, centerId, subjectId, "Primary")
        };
        var requiredIdeas = question.GradingCriteria.RequiredIdeas.ToArray();
        var commonErrors = question.GradingCriteria.CommonErrors.ToArray();
        var originalNodeNames = nodes.Select(node => node.NodeName).ToArray();

        var request = new AIAnalysisRequestFactory().Create(attempt, question, nodes);

        Assert.Equal(AIAnalysisContract.SchemaVersion, request.SchemaVersion);
        Assert.Equal(language, request.Language);
        Assert.Equal(question.QuestionType, request.Question.QuestionType);
        Assert.Equal(question.QuestionText, request.Question.QuestionText);
        Assert.Equal(question.CorrectAnswer, request.Question.CorrectAnswer);
        Assert.Equal(question.Solution, request.Question.Solution);
        Assert.Equal(question.ExpectedReasoning, request.Question.ExpectedReasoning);
        Assert.Equal(question.GradingCriteria.SchemaVersion, request.Question.GradingCriteria.SchemaVersion);
        Assert.Equal(requiredIdeas, request.Question.GradingCriteria.RequiredIdeas);
        Assert.Equal(commonErrors, request.Question.GradingCriteria.CommonErrors);
        Assert.Equal(question.GradingCriteria.ScoringNotes, request.Question.GradingCriteria.ScoringNotes);
        Assert.Equal(attempt.FinalAnswer, request.StudentSubmission.FinalAnswer);
        Assert.Equal(attempt.ReasoningText, request.StudentSubmission.ReasoningText);
        Assert.Equal(attempt.TimeSpentSeconds, request.StudentSubmission.TimeSpentSeconds);
        Assert.Equal(attempt.Confidence, request.StudentSubmission.Confidence);
        Assert.Equal(attempt.AnswerChanges, request.StudentSubmission.AnswerChanges);
        Assert.Equal(["2", "9"], request.AllowedKnowledgeNodes.Select(node => node.NodeId));
        Assert.Equal(["Second", "Primary"], request.AllowedKnowledgeNodes.Select(node => node.NodeName));

        var serialized = JsonSerializer.Serialize(request);
        Assert.DoesNotContain(centerId.ToString(), serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(attempt.StudentId.ToString(), serialized, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(attempt.AttemptId.ToString(), serialized, StringComparison.Ordinal);
        Assert.DoesNotContain(attempt.ClientSubmissionId.ToString(), serialized, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(requiredIdeas, question.GradingCriteria.RequiredIdeas);
        Assert.Equal(commonErrors, question.GradingCriteria.CommonErrors);
        Assert.Equal(originalNodeNames, nodes.Select(node => node.NodeName));
    }

    [Fact]
    public void Create_CopiesAuthoredCollectionsInsteadOfAliasingThem()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var attempt = CreateAttempt(centerId, "vi");
        var question = CreateQuestion(centerId, subjectId, "vi");
        var nodes = new[] { CreateNode(9, centerId, subjectId, "Primary") };

        var request = new AIAnalysisRequestFactory().Create(attempt, question, nodes);
        question.GradingCriteria.RequiredIdeas[0] = "changed";
        nodes[0].NodeName = "changed";

        Assert.Equal("idea-a", request.Question.GradingCriteria.RequiredIdeas[0]);
        Assert.Equal("Primary", request.AllowedKnowledgeNodes[0].NodeName);
    }

    [Fact]
    public void Create_SkippedEmptyAnswerAndEmptyScoringNotes_MapsDomainValidValues()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var attempt = CreateAttempt(centerId, "vi");
        attempt.FinalAnswer = string.Empty;
        attempt.ReasoningText = null;
        attempt.Skipped = true;
        var question = CreateQuestion(centerId, subjectId, "vi");
        question.GradingCriteria.ScoringNotes = string.Empty;

        var request = new AIAnalysisRequestFactory().Create(
            attempt,
            question,
            [CreateNode(9, centerId, subjectId, "Primary")]);

        Assert.Equal(string.Empty, request.StudentSubmission.FinalAnswer);
        Assert.Null(request.StudentSubmission.ReasoningText);
        Assert.Equal(string.Empty, request.Question.GradingCriteria.ScoringNotes);
    }

    [Fact]
    public void Create_InvalidRequiredContext_ThrowsDeterministically()
    {
        var centerId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var attempt = CreateAttempt(centerId, "vi");
        var question = CreateQuestion(centerId, subjectId, "vi");
        question.PrimaryTopicNodeId = 77;

        var exception = Assert.Throws<ArgumentException>(() =>
            new AIAnalysisRequestFactory().Create(
                attempt,
                question,
                [CreateNode(9, centerId, subjectId, "Mapped")])) ;

        Assert.Equal("The primary topic is not in the allowed node set.", exception.Message);
    }

    private static Attempt CreateAttempt(Guid centerId, string language) => new()
    {
        AttemptId = 41,
        CenterId = centerId,
        StudentId = Guid.NewGuid(),
        QuestionId = 7,
        AssignmentId = Guid.NewGuid(),
        FinalAnswer = "student answer",
        ReasoningText = "student reasoning",
        TimeSpentSeconds = 37,
        Confidence = 82.5m,
        AnswerChanges = 2,
        ReasoningLanguage = language,
        ClientSubmissionId = Guid.NewGuid()
    };

    private static Question CreateQuestion(Guid centerId, Guid subjectId, string language) => new()
    {
        QuestionId = 7,
        CenterId = centerId,
        SubjectId = subjectId,
        PrimaryTopicNodeId = 9,
        QuestionType = QuestionType.Essay,
        QuestionText = "question text",
        CorrectAnswer = "correct answer",
        Solution = "worked solution",
        ExpectedReasoning = "expected reasoning",
        LanguageCode = language,
        GradingCriteria = new GradingCriteria
        {
            SchemaVersion = "1.2",
            RequiredIdeas = ["idea-a", "idea-b"],
            CommonErrors = ["error-a", "error-b"],
            ScoringNotes = "scoring notes"
        }
    };

    private static KnowledgeNode CreateNode(
        ulong nodeId,
        Guid centerId,
        Guid subjectId,
        string name) => new()
    {
        NodeId = nodeId,
        CenterId = centerId,
        SubjectId = subjectId,
        NodeType = NodeType.Topic,
        NodeCode = $"N-{nodeId}",
        NodeName = name,
        IsActive = true
    };
}
