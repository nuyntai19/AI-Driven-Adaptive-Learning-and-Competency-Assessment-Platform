using System;
using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.BLL.AssessmentAndReasoning.PreliminaryGrading;

public class PreliminaryGraderFactory
{
    private readonly MultipleChoiceGrader _multipleChoiceGrader;
    private readonly ShortAnswerGrader _shortAnswerGrader;
    private readonly EssayGrader _essayGrader;

    public PreliminaryGraderFactory(
        MultipleChoiceGrader multipleChoiceGrader,
        ShortAnswerGrader shortAnswerGrader,
        EssayGrader essayGrader)
    {
        _multipleChoiceGrader = multipleChoiceGrader;
        _shortAnswerGrader = shortAnswerGrader;
        _essayGrader = essayGrader;
    }

    public IQuestionGrader GetGrader(QuestionType type)
    {
        return type switch
        {
            QuestionType.MultipleChoice => _multipleChoiceGrader,
            QuestionType.ShortAnswer => _shortAnswerGrader,
            QuestionType.Essay => _essayGrader,
            _ => throw new ArgumentOutOfRangeException(nameof(type), $"Unsupported QuestionType: {type}")
        };
    }
}
