using System;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.DAL.DigitalTwin;

namespace EduTwin.BLL.DigitalTwin;

public interface IStudentGoalRiskUpdater
{
    Task<StudentSubjectGoal?> UpdateAsync(
        Guid centerId,
        Guid studentId,
        Guid subjectId,
        DateTime utcNow,
        CancellationToken cancellationToken);
}
