namespace EduTwin.Contracts.CurriculumAndQuestions;

/// <summary>
/// Defines how a question's answer is evaluated deterministically.
/// Enforced by strict matrix with QuestionType:
/// - MultipleChoice: Strictly TextExact
/// - Essay: Strictly Manual
/// - ShortAnswer: TextExact, NumericRational, Coordinate2D, or Manual
/// </summary>
public enum QuestionAnswerEvaluationMode
{
    TextExact = 1,
    NumericRational = 2,
    Manual = 3,
    Coordinate2D = 4
}
