using System;
using System.Threading;
using System.Threading.Tasks;

namespace EduTwin.BLL.Dashboards;

public interface IGetStudentDashboardUseCase
{
    Task<StudentDashboardResult> ExecuteAsync(Guid subjectId, CancellationToken cancellationToken);
}
