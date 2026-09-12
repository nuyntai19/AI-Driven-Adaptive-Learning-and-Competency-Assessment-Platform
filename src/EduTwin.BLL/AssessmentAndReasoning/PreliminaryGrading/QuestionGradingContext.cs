using System.Collections.Generic;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.DAL.CurriculumAndQuestions;

namespace EduTwin.BLL.AssessmentAndReasoning.PreliminaryGrading;

public class QuestionGradingContext
{
    public QuestionAnswerEvaluationMode EvaluationMode { get; init; } = QuestionAnswerEvaluationMode.TextExact;
    public decimal MaxScore { get; init; } = 1.0m;
    public GradingCriteria Criteria { get; init; } = new();
    public IEnumerable<QuestionOption>? Options { get; init; }
}
