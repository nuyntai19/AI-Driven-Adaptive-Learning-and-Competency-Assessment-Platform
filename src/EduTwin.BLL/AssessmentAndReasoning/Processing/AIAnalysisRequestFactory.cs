using System.Globalization;
using EduTwin.BLL.AssessmentAndReasoning.AI;
using EduTwin.BLL.AssessmentAndReasoning.PreliminaryGrading;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.KnowledgeGraph;

namespace EduTwin.BLL.AssessmentAndReasoning.Processing;

public sealed class AIAnalysisRequestFactory : IAIAnalysisRequestFactory
{
    private static readonly HashSet<string> SupportedLanguages =
        new(["vi", "en"], StringComparer.Ordinal);

    private readonly IMathAnswerNormalizer _mathNormalizer;
    private readonly ICoordinateAnswerNormalizer _coordinateNormalizer;

    public AIAnalysisRequestFactory(
        IMathAnswerNormalizer? mathNormalizer = null,
        ICoordinateAnswerNormalizer? coordinateNormalizer = null)
    {
        _mathNormalizer = mathNormalizer ?? new MathAnswerNormalizer();
        _coordinateNormalizer = coordinateNormalizer ?? new CoordinateAnswerNormalizer(_mathNormalizer);
    }

    public AnalyzeReasoningRequest Create(
        Attempt attempt,
        Question question,
        IReadOnlyList<KnowledgeNode> allowedKnowledgeNodes,
        IReadOnlyList<AnalyzeReasoningImagePart>? imageParts = null)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(allowedKnowledgeNodes);

        Validate(attempt, question, allowedKnowledgeNodes);

        var canonicalCorrect = ResolveCanonicalAnswer(question.AnswerEvaluationMode, question.CorrectAnswer);
        var canonicalFinal = ResolveCanonicalAnswer(question.AnswerEvaluationMode, attempt.FinalAnswer);

        return new AnalyzeReasoningRequest
        {
            SchemaVersion = AIAnalysisContract.SchemaVersion,
            Language = attempt.ReasoningLanguage,
            Question = new AnalyzeReasoningQuestion
            {
                QuestionType = question.QuestionType,
                AnswerEvaluationMode = question.AnswerEvaluationMode,
                QuestionText = question.QuestionText,
                CorrectAnswer = question.CorrectAnswer,
                CanonicalCorrectAnswer = canonicalCorrect,
                Solution = question.Solution,
                ExpectedReasoning = question.ExpectedReasoning,
                GradingCriteria = new AnalyzeReasoningGradingCriteria
                {
                    SchemaVersion = question.GradingCriteria.SchemaVersion,
                    RequiredIdeas = question.GradingCriteria.RequiredIdeas.ToArray(),
                    CommonErrors = question.GradingCriteria.CommonErrors.ToArray(),
                    ScoringNotes = question.GradingCriteria.ScoringNotes
                }
            },
            StudentSubmission = new AnalyzeReasoningStudentSubmission
            {
                FinalAnswer = attempt.FinalAnswer,
                AnswerDisplayLatex = attempt.AnswerDisplayLatex,
                CanonicalFinalAnswer = canonicalFinal,
                PreliminaryIsCorrect = attempt.IsCorrect,
                ReasoningText = attempt.ReasoningText,
                TimeSpentSeconds = attempt.TimeSpentSeconds,
                Confidence = attempt.Confidence,
                AnswerChanges = attempt.AnswerChanges,
                ImageParts = imageParts ?? []
            },
            AllowedKnowledgeNodes = allowedKnowledgeNodes
                .Select(node => new AnalyzeReasoningAllowedKnowledgeNode
                {
                    NodeId = node.NodeId.ToString(CultureInfo.InvariantCulture),
                    NodeName = node.NodeName
                })
                .ToArray()
        };
    }

    private string? ResolveCanonicalAnswer(QuestionAnswerEvaluationMode mode, string? rawAnswer)
    {
        if (string.IsNullOrWhiteSpace(rawAnswer)) return null;

        return mode switch
        {
            QuestionAnswerEvaluationMode.MathEquivalent when new BoundedMathAnswerNormalizer().TryNormalize(rawAnswer, out var mathematical) => mathematical,
            QuestionAnswerEvaluationMode.NumericRational when _mathNormalizer.TryNormalize(rawAnswer, out var rational) && rational.HasValue =>
                rational.Value.ToString(),
            QuestionAnswerEvaluationMode.Coordinate2D when _coordinateNormalizer.TryNormalize(rawAnswer, out var coordinate) && coordinate.HasValue =>
                $"({coordinate.Value.X}; {coordinate.Value.Y})",
            _ => rawAnswer.Trim()
        };
    }

    private static void Validate(
        Attempt attempt,
        Question question,
        IReadOnlyList<KnowledgeNode> allowedKnowledgeNodes)
    {
        if (attempt.AttemptId == 0
            || attempt.CenterId == Guid.Empty
            || attempt.QuestionId == 0
            || question.QuestionId == 0
            || question.CenterId == Guid.Empty
            || question.SubjectId == Guid.Empty
            || question.PrimaryTopicNodeId == 0
            || attempt.QuestionId != question.QuestionId
            || attempt.CenterId != question.CenterId
            || !SupportedLanguages.Contains(attempt.ReasoningLanguage)
            || (!attempt.Skipped && string.IsNullOrWhiteSpace(attempt.FinalAnswer))
            || string.IsNullOrWhiteSpace(question.QuestionText)
            || string.IsNullOrWhiteSpace(question.CorrectAnswer)
            || string.IsNullOrWhiteSpace(question.Solution)
            || question.GradingCriteria is null
            || string.IsNullOrWhiteSpace(question.GradingCriteria.SchemaVersion)
            || question.GradingCriteria.RequiredIdeas is null
            || question.GradingCriteria.CommonErrors is null
            || question.GradingCriteria.ScoringNotes is null
            || allowedKnowledgeNodes.Count == 0)
        {
            throw new ArgumentException("AI analysis request context is invalid.");
        }

        var seenNodeIds = new HashSet<ulong>();
        ulong previousNodeId = 0;
        foreach (var node in allowedKnowledgeNodes)
        {
            if (node is null
                || node.NodeId == 0
                || node.CenterId != question.CenterId
                || node.SubjectId != question.SubjectId
                || !node.IsActive
                || node.IsDeleted
                || string.IsNullOrWhiteSpace(node.NodeName)
                || !seenNodeIds.Add(node.NodeId)
                || node.NodeId <= previousNodeId)
            {
                throw new ArgumentException("Allowed knowledge-node context is invalid.");
            }

            previousNodeId = node.NodeId;
        }

        if (!seenNodeIds.Contains(question.PrimaryTopicNodeId))
        {
            throw new ArgumentException("The primary topic is not in the allowed node set.");
        }
    }
}
