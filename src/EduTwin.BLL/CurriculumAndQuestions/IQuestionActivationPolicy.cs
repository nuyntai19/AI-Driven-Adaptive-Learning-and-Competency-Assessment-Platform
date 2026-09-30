using System.Collections.Generic;
using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.BLL.CurriculumAndQuestions;

public readonly record struct QuestionOptionValidationItem(string OptionLabel, string OptionText, bool IsCorrect);

public interface IQuestionActivationPolicy
{
    bool Validate(
        QuestionType questionType,
        QuestionAnswerEvaluationMode evaluationMode,
        string? correctAnswer,
        IReadOnlyList<QuestionOptionValidationItem>? options,
        out string? validationError);

    bool ValidateCompleteQuestion(
        QuestionType questionType,
        QuestionAnswerEvaluationMode evaluationMode,
        byte difficulty,
        string? questionText,
        string? correctAnswer,
        string? solution,
        decimal maxScore,
        uint estimatedTimeSeconds,
        IReadOnlyList<QuestionOptionValidationItem>? options,
        out string? validationError);
}
