namespace EduTwin.BLL.AssessmentAndReasoning.AI;

// An unavailable coordination store is an operational failure, never evidence about a student's work.
public sealed class AIAnalysisInfrastructureException() : Exception("AI processing infrastructure is temporarily unavailable.");
