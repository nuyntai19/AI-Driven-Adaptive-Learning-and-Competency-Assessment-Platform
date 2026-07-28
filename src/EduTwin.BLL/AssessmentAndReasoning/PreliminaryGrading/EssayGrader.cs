using System.Collections.Generic;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.DAL.CurriculumAndQuestions;

namespace EduTwin.BLL.AssessmentAndReasoning.PreliminaryGrading;

public class EssayGrader : IQuestionGrader
{
    public PreliminaryGradingResult Grade(
        string? studentAnswer, 
        string? correctAnswer, 
        decimal maxScore, 
        GradingCriteria criteria, 
        IEnumerable<QuestionOption>? options = null)
    {
        // Essay questions require manual or AI review (Phase P12).
        // Deterministic preliminary grader always returns null for IsCorrect and 0 for Score.
        return new PreliminaryGradingResult
        {
            IsCorrect = null,
            Score = 0m,
            Feedback = "Requires AI or manual teacher review."
        };
    }
}
