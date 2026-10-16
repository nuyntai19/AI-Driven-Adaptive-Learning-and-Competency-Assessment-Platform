using System;

namespace EduTwin.Contracts.Recommendations;

public class EvaluateSessionRequest
{
    public ulong? SessionRunId { get; set; }
    public Guid? SubjectId { get; set; }
    public Guid? ClassId { get; set; }
    public bool History { get; set; }
}
