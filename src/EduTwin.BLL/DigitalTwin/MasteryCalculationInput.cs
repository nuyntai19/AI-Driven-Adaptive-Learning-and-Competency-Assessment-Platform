namespace EduTwin.BLL.DigitalTwin;

public sealed record MasteryCalculationInput(
    decimal CurrentMastery,
    decimal? ReasoningQuality,
    decimal ReasoningWeight,
    bool IsCorrect,
    decimal TimeQuality,
    decimal? ConfidenceCalibration,
    byte Difficulty);
