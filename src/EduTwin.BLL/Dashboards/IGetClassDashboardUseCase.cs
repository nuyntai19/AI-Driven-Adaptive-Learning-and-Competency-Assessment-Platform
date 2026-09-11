using System;
using System.Threading;
using System.Threading.Tasks;

namespace EduTwin.BLL.Dashboards;

public interface IGetClassDashboardUseCase
{
    Task<ClassDashboardResult> ExecuteAsync(Guid classId, decimal riskThreshold, CancellationToken cancellationToken);
}
