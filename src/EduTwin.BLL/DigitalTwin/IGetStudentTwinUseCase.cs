using System;
using System.Threading;
using System.Threading.Tasks;

namespace EduTwin.BLL.DigitalTwin;

public interface IGetStudentTwinUseCase
{
    Task<StudentTwinResult> ExecuteAsync(Guid subjectId, CancellationToken cancellationToken);
}
