using System;
using System.Threading;
using System.Threading.Tasks;

namespace EduTwin.BLL.DigitalTwin;

public interface IListStudentSubjectGoalsUseCase
{
    Task<ListStudentSubjectGoalsResult> ExecuteAsync(Guid studentId, CancellationToken cancellationToken = default);
}
