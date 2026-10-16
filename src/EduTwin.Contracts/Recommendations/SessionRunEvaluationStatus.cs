namespace EduTwin.Contracts.Recommendations;

public enum SessionRunEvaluationStatus
{
    NotStarted,
    Queued,
    Processing,
    AwaitingTeacher,
    Completed,
    Failed
}
