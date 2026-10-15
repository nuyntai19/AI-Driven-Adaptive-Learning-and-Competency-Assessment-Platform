using System;

namespace EduTwin.Contracts.Recommendations;

public class StartSessionRequest
{
    public Guid SubjectId { get; set; }
    public Guid? ClassId { get; set; }
    public bool History { get; set; }
}
