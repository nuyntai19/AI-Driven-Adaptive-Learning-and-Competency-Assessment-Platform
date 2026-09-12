using System.Collections.Generic;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.DAL.CurriculumAndQuestions;

namespace EduTwin.BLL.AssessmentAndReasoning.PreliminaryGrading;

public interface IQuestionGrader
{
    /// <summary>
    /// Grades the student's answer deterministically.
    /// </summary>
    /// <param name="studentAnswer">The answer provided by the student.</param>
    /// <param name="correctAnswer">The exact or canonical correct answer defined by the teacher.</param>
    /// <param name="maxScore">The maximum score possible for this question.</param>
    /// <param name="criteria">The grading criteria defining required ideas or common errors.</param>
    /// <param name="options">Optional: list of multiple choice options if applicable.</param>
    /// <returns>A PreliminaryGradingResult indicating if it's correct, the score, and feedback.</returns>
    PreliminaryGradingResult Grade(
        string? studentAnswer, 
        string? correctAnswer, 
        decimal maxScore, 
        GradingCriteria criteria, 
        IEnumerable<QuestionOption>? options = null)
    {
        return Grade(studentAnswer, correctAnswer, new QuestionGradingContext
        {
            EvaluationMode = QuestionAnswerEvaluationMode.TextExact,
            MaxScore = maxScore,
            Criteria = criteria,
            Options = options
        });
    }

    PreliminaryGradingResult Grade(
        string? studentAnswer,
        string? correctAnswer,
        QuestionGradingContext context);
}
