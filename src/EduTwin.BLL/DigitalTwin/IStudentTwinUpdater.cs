using System;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.DAL.DigitalTwin;

namespace EduTwin.BLL.DigitalTwin;

public interface IStudentTwinUpdater
{
    Task<StudentTwin> UpdateAsync(
        Guid centerId,
        Guid studentId,
        DateTime utcNow,
        CancellationToken cancellationToken);
}
