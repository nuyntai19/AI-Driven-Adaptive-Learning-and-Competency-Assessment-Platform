using System;
using System.Text.Json;
using EduTwin.BLL.AssessmentAndReasoning.Evidence;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.CurriculumAndQuestions;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Evidence;

public sealed class EvidenceConsistencyCheckerTests
{
    private readonly Guid _centerId = Guid.NewGuid();
    private readonly Guid _subjectId = Guid.NewGuid();
    private readonly EvidenceConsistencyChecker _checker = new();

    private (Attempt Attempt, Question Question, ReasoningAnalysis Analysis) CreateValidContext(
        bool? isCorrect = true,
        int reasoningQuality = 85,
        ErrorType errorType = ErrorType.None,
        int confidence = 90,
        string[]? rootCauses = null)
    {
        var now = DateTime.UtcNow;
        var attempt = new Attempt
        {
            AttemptId = 1001,
            CenterId = _centerId,
            QuestionId = 501,
            StudentId = Guid.NewGuid(),
            FinalAnswer = "42",
            ReasoningText = "Step 1: calculate x, Step 2: substitute to find 42",
            IsCorrect = isCorrect,
            TimeSpentSeconds = 45,
            Confidence = 80m,
            Skipped = false,
            ReasoningLanguage = "vi",
            Status = AttemptStatus.PendingAnalysis,
            CreatedAt = now,
            UpdatedAt = now
        };

        var question = new Question
        {
            QuestionId = 501,
            CenterId = _centerId,
            SubjectId = _subjectId,
            PrimaryTopicNodeId = 101,
            Difficulty = 3,
            QuestionText = "Calculate...",
            CorrectAnswer = "42",
            Solution = "Detailed solution",
            LanguageCode = "vi",
            ReasoningRequired = true,
            Status = QuestionStatus.Active,
            CreatedAt = now,
            UpdatedAt = now
        };

        var rootCausesJson = JsonSerializer.SerializeToDocument(rootCauses ?? Array.Empty<string>());
        var missingStepsJson = JsonSerializer.SerializeToDocument(Array.Empty<string>());

        var analysis = new ReasoningAnalysis
        {
            AnalysisId = 2001,
            CenterId = _centerId,
            AttemptId = 1001,
            SchemaVersion = "1.0",
            ReasoningQuality = reasoningQuality,
            ErrorType = errorType,
            MissingSteps = missingStepsJson,
            RootCauseNodeIds = rootCausesJson,
            AnalysisConfidence = confidence,
            Feedback = "Well explained approach.",
            IsFallback = false,
            NeedsTeacherReview = false,
            CreatedAt = now,
            UpdatedAt = now
        };

        return (attempt, question, analysis);
    }

    [Fact]
    public void Evaluate_RuleFallback_ReturnsFallbackDefaults()
    {
        var (attempt, question, analysis) = CreateValidContext();
        analysis.IsFallback = true;
        analysis.ReasoningQuality = null;

        var result = _checker.Evaluate(attempt, question, analysis);

        Assert.True(result.StructuralValidationPassed);
        Assert.True(result.SemanticValidationPassed);
        Assert.False(result.HasContradiction);
        Assert.False(result.HasAnomaly);
        Assert.False(result.HasRequiredEvidence);
        Assert.Contains(EvidenceReasonCodes.SourceRuleFallback, result.ReasonCodes);
    }

    [Fact]
    public void Evaluate_IncorrectAttempt_AIClaimsNoError_DetectsContradiction()
    {
        var (attempt, question, analysis) = CreateValidContext(
            isCorrect: false,
            reasoningQuality: 60,
            errorType: ErrorType.None);

        var result = _checker.Evaluate(attempt, question, analysis);

        Assert.True(result.HasContradiction);
        Assert.Contains(EvidenceConsistencyReasonCodes.ContradictionIncorrectAttemptNoError, result.ReasonCodes);
    }

    [Fact]
    public void Evaluate_IncorrectAttempt_WithHighReasoningQuality_DoesNotFalsePositiveContradiction()
    {
        var (attempt, question, analysis) = CreateValidContext(
            isCorrect: false,
            reasoningQuality: 85,
            errorType: ErrorType.Skill);

        var result = _checker.Evaluate(attempt, question, analysis);

        Assert.False(result.HasContradiction);
    }

    [Fact]
    public void Evaluate_CorrectAttempt_WithLowReasoningQuality_DoesNotFalsePositiveContradiction()
    {
        var (attempt, question, analysis) = CreateValidContext(
            isCorrect: true,
            reasoningQuality: 15,
            errorType: ErrorType.Knowledge);

        var result = _checker.Evaluate(attempt, question, analysis);

        Assert.False(result.HasContradiction);
    }

    [Fact]
    public void Evaluate_CorrectAttempt_WithMinorDefectOnTopic_DoesNotFalsePositiveContradiction()
    {
        var (attempt, question, analysis) = CreateValidContext(
            isCorrect: true,
            reasoningQuality: 84,
            errorType: ErrorType.Reasoning,
            rootCauses: ["101"]); // 101 is question.PrimaryTopicNodeId

        var result = _checker.Evaluate(attempt, question, analysis);

        Assert.False(result.HasContradiction);
    }

    [Fact]
    public void Evaluate_NoError_AIReportsRootCauses_DetectsContradiction()
    {
        var (attempt, question, analysis) = CreateValidContext(
            isCorrect: true,
            reasoningQuality: 95,
            errorType: ErrorType.None,
            rootCauses: ["105"]);

        var result = _checker.Evaluate(attempt, question, analysis);

        Assert.True(result.HasContradiction);
        Assert.Contains(EvidenceConsistencyReasonCodes.ContradictionNoErrorWithRootCauses, result.ReasonCodes);
    }

    [Fact]
    public void Evaluate_MetadataMismatch_DetectsContradiction()
    {
        var (attempt, question, analysis) = CreateValidContext();
        analysis.AttemptId = 99999; // Mismatched

        var result = _checker.Evaluate(attempt, question, analysis);

        Assert.True(result.HasContradiction);
        Assert.Contains(EvidenceConsistencyReasonCodes.ContradictionMetadataMismatch, result.ReasonCodes);
    }

    [Fact]
    public void Evaluate_ZeroTimeSpent_AIReportsHighReasoning_DetectsAnomaly()
    {
        var (attempt, question, analysis) = CreateValidContext(
            isCorrect: true,
            reasoningQuality: 75);
        attempt.TimeSpentSeconds = 0;

        var result = _checker.Evaluate(attempt, question, analysis);

        Assert.True(result.HasAnomaly);
        Assert.Contains(EvidenceConsistencyReasonCodes.AnomalyZeroTimeHighReasoning, result.ReasonCodes);
    }

    [Fact]
    public void Evaluate_UnrealisticCompletionTimeForHighDifficulty_DetectsAnomaly()
    {
        var (attempt, question, analysis) = CreateValidContext(
            isCorrect: true,
            reasoningQuality: 90);
        attempt.TimeSpentSeconds = 2;
        question.Difficulty = 5;

        var result = _checker.Evaluate(attempt, question, analysis);

        Assert.True(result.HasAnomaly);
        Assert.Contains(EvidenceConsistencyReasonCodes.AnomalyUnrealisticCompletionTime, result.ReasonCodes);
    }

    [Fact]
    public void Evaluate_MissingReasoningTextWhenRequired_DetectsAnomaly()
    {
        var (attempt, question, analysis) = CreateValidContext(
            isCorrect: true,
            reasoningQuality: 60);
        question.ReasoningRequired = true;
        attempt.ReasoningText = "";

        var result = _checker.Evaluate(attempt, question, analysis);

        Assert.True(result.HasAnomaly);
        Assert.Contains(EvidenceConsistencyReasonCodes.AnomalyMissingReasoningTextHighQuality, result.ReasonCodes);
    }

    [Fact]
    public void Evaluate_RootCauseNotAllowed_DetectsSemanticInvalid()
    {
        var (attempt, question, analysis) = CreateValidContext(
            isCorrect: false,
            reasoningQuality: 40,
            errorType: ErrorType.Knowledge,
            rootCauses: ["999"]);

        var allowedNodes = new ulong[] { 101, 102, 103 };
        var result = _checker.Evaluate(attempt, question, analysis, allowedNodes);

        Assert.False(result.SemanticValidationPassed);
        Assert.Contains(EvidenceConsistencyReasonCodes.SemanticInvalidRootCauseMismatch, result.ReasonCodes);
    }

    [Fact]
    public void Evaluate_RootCauseInvalidFormat_DetectsSemanticInvalid()
    {
        var (attempt, question, analysis) = CreateValidContext(
            isCorrect: false,
            reasoningQuality: 40,
            errorType: ErrorType.Knowledge,
            rootCauses: ["abc"]);

        var result = _checker.Evaluate(attempt, question, analysis);

        Assert.False(result.SemanticValidationPassed);
        Assert.Contains(EvidenceConsistencyReasonCodes.SemanticInvalidRootCauseFormat, result.ReasonCodes);
    }

    [Fact]
    public void Evaluate_ReasoningLanguageDifferentFromQuestionLanguage_RemainsSemanticallyValid()
    {
        var (attempt, question, analysis) = CreateValidContext();
        attempt.ReasoningLanguage = "en";
        question.LanguageCode = "vi";

        var result = _checker.Evaluate(attempt, question, analysis);

        Assert.True(result.SemanticValidationPassed);
    }

    [Fact]
    public void Evaluate_ConsistentValidSubmission_PassesAllChecks()
    {
        var (attempt, question, analysis) = CreateValidContext(
            isCorrect: true,
            reasoningQuality: 85,
            errorType: ErrorType.None,
            confidence: 92);

        var allowedNodes = new ulong[] { 101, 102 };
        var result = _checker.Evaluate(attempt, question, analysis, allowedNodes);

        Assert.True(result.StructuralValidationPassed);
        Assert.True(result.SemanticValidationPassed);
        Assert.False(result.HasContradiction);
        Assert.False(result.HasAnomaly);
        Assert.True(result.HasRequiredEvidence);
        Assert.Empty(result.ReasonCodes);
    }
}
