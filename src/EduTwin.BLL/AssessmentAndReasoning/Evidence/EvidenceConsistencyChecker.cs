using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.CurriculumAndQuestions;

namespace EduTwin.BLL.AssessmentAndReasoning.Evidence;

public sealed class EvidenceConsistencyChecker : IEvidenceConsistencyChecker
{
    public EvidenceConsistencyResult Evaluate(
        Attempt attempt,
        Question question,
        ReasoningAnalysis analysis,
        IReadOnlyCollection<ulong>? allowedNodeIds = null)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        ArgumentNullException.ThrowIfNull(question);
        ArgumentNullException.ThrowIfNull(analysis);

        var reasons = new List<string>();

        // 1. Fallback short-circuit
        if (analysis.IsFallback)
        {
            return new EvidenceConsistencyResult(
                SemanticValidationPassed: true,
                HasContradiction: false,
                HasAnomaly: false,
                HasRequiredEvidence: false,
                ReasonCodes: [EvidenceReasonCodes.SourceRuleFallback],
                StructuralValidationPassed: true);
        }

        // 2. Structural checks
        var structuralPassed = true;
        if (string.IsNullOrWhiteSpace(analysis.Feedback)
            || analysis.ReasoningQuality is < 0 or > 100
            || analysis.AnalysisConfidence is < 0 or > 100)
        {
            structuralPassed = false;
            reasons.Add(EvidenceReasonCodes.StructuralValidationFailed);
        }

        // 3. Parse RootCauseNodeIds
        var rootCauseIds = new List<ulong>();
        var rootCauseFormatValid = true;
        if (analysis.RootCauseNodeIds is not null && analysis.RootCauseNodeIds.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var element in analysis.RootCauseNodeIds.RootElement.EnumerateArray())
            {
                if (element.ValueKind == JsonValueKind.String)
                {
                    var str = element.GetString();
                    if (ulong.TryParse(str, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) && parsed > 0)
                    {
                        rootCauseIds.Add(parsed);
                    }
                    else
                    {
                        rootCauseFormatValid = false;
                    }
                }
                else if (element.ValueKind == JsonValueKind.Number && element.TryGetUInt64(out var num) && num > 0)
                {
                    rootCauseIds.Add(num);
                }
                else
                {
                    rootCauseFormatValid = false;
                }
            }
        }

        // 4. Contradiction checks
        var hasContradiction = false;

        // Contradiction 1: Student is incorrect, but AI says ErrorType.None
        if (attempt.IsCorrect == false && analysis.ErrorType == ErrorType.None)
        {
            hasContradiction = true;
            reasons.Add(EvidenceConsistencyReasonCodes.ContradictionIncorrectAttemptNoError);
        }

        // Contradiction 2: Student is incorrect, but AI claims high reasoning quality (>= 80)
        if (attempt.IsCorrect == false && analysis.ReasoningQuality >= 80m)
        {
            hasContradiction = true;
            reasons.Add(EvidenceConsistencyReasonCodes.ContradictionIncorrectAttemptHighQuality);
        }

        // Contradiction 3: Student is correct, but AI reports severe error with abysmal reasoning quality (< 20)
        if (attempt.IsCorrect == true && analysis.ErrorType != ErrorType.None && analysis.ReasoningQuality < 20m)
        {
            hasContradiction = true;
            reasons.Add(EvidenceConsistencyReasonCodes.ContradictionCorrectAttemptFatalError);
        }

        // Contradiction 4: ErrorType.None, but AI returns root cause nodes
        if (analysis.ErrorType == ErrorType.None && rootCauseIds.Count > 0)
        {
            hasContradiction = true;
            reasons.Add(EvidenceConsistencyReasonCodes.ContradictionNoErrorWithRootCauses);
        }

        // Contradiction 5: Metadata mismatch (attempt/center/question mismatch)
        if (analysis.AttemptId != attempt.AttemptId || analysis.CenterId != attempt.CenterId)
        {
            hasContradiction = true;
            reasons.Add(EvidenceConsistencyReasonCodes.ContradictionMetadataMismatch);
        }
        if (attempt.QuestionId != question.QuestionId || attempt.CenterId != question.CenterId)
        {
            hasContradiction = true;
            reasons.Add(EvidenceConsistencyReasonCodes.ContradictionQuestionAttemptMismatch);
        }

        // 5. Anomaly checks
        var hasAnomaly = false;

        // Anomaly 1: Zero time spent with high reasoning quality (>= 70)
        if (!attempt.Skipped && attempt.TimeSpentSeconds == 0 && analysis.ReasoningQuality >= 70m)
        {
            hasAnomaly = true;
            reasons.Add(EvidenceConsistencyReasonCodes.AnomalyZeroTimeHighReasoning);
        }

        // Anomaly 2: Unrealistic completion time for high-difficulty questions (under 3s for difficulty >= 4 with quality >= 80)
        if (!attempt.Skipped && attempt.TimeSpentSeconds > 0 && attempt.TimeSpentSeconds < 3 && question.Difficulty >= 4 && analysis.ReasoningQuality >= 80m)
        {
            hasAnomaly = true;
            reasons.Add(EvidenceConsistencyReasonCodes.AnomalyUnrealisticCompletionTime);
        }

        // Anomaly 3: Reasoning required, but student provided empty reasoning text while AI claims substantial quality (> 50)
        if (question.ReasoningRequired && !attempt.Skipped && string.IsNullOrWhiteSpace(attempt.ReasoningText) && analysis.ReasoningQuality > 50m)
        {
            hasAnomaly = true;
            reasons.Add(EvidenceConsistencyReasonCodes.AnomalyMissingReasoningTextHighQuality);
        }

        // 6. Semantic Validation checks
        var semanticPassed = true;

        if (!rootCauseFormatValid)
        {
            semanticPassed = false;
            reasons.Add(EvidenceConsistencyReasonCodes.SemanticInvalidRootCauseFormat);
        }

        if (allowedNodeIds is not null && allowedNodeIds.Count > 0)
        {
            foreach (var rootCauseId in rootCauseIds)
            {
                if (!allowedNodeIds.Contains(rootCauseId))
                {
                    semanticPassed = false;
                    reasons.Add(EvidenceConsistencyReasonCodes.SemanticInvalidRootCauseMismatch);
                    break;
                }
            }
        }

        if (!string.IsNullOrWhiteSpace(attempt.ReasoningLanguage)
            && !string.IsNullOrWhiteSpace(question.LanguageCode)
            && !string.Equals(attempt.ReasoningLanguage, question.LanguageCode, StringComparison.OrdinalIgnoreCase))
        {
            semanticPassed = false;
            reasons.Add(EvidenceConsistencyReasonCodes.SemanticInvalidLanguageMismatch);
        }

        // 7. Required Evidence check
        var hasRequiredEvidence = analysis.ReasoningQuality.HasValue
            && analysis.AnalysisConfidence.HasValue
            && !string.IsNullOrWhiteSpace(analysis.Feedback)
            && !analysis.IsFallback;

        if (!hasRequiredEvidence)
        {
            reasons.Add(EvidenceReasonCodes.RequiredEvidenceMissing);
        }

        return new EvidenceConsistencyResult(
            SemanticValidationPassed: semanticPassed,
            HasContradiction: hasContradiction,
            HasAnomaly: hasAnomaly,
            HasRequiredEvidence: hasRequiredEvidence,
            ReasonCodes: reasons,
            StructuralValidationPassed: structuralPassed);
    }
}
