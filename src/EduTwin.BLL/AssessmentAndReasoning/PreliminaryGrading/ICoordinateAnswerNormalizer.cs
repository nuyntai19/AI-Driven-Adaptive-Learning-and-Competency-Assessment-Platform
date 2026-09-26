namespace EduTwin.BLL.AssessmentAndReasoning.PreliminaryGrading;

public interface ICoordinateAnswerNormalizer
{
    bool TryNormalize(string? rawAnswer, out Coordinate2DValue? normalized);
}
