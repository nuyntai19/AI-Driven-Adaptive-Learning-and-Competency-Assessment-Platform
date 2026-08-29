namespace EduTwin.BLL.DigitalTwin;

public sealed record MasteryCalculationInput(
    decimal CurrentMastery,
    decimal? ReasoningQuality,
    bool IsCorrect,
    decimal TimeQuality,
    decimal? ConfidenceCalibration,
    byte Difficulty);
