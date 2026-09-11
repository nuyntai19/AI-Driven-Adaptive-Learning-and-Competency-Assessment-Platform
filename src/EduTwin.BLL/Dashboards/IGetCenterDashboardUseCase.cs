using System;
using System.Threading;
using System.Threading.Tasks;

namespace EduTwin.BLL.Dashboards;

public interface IGetCenterDashboardUseCase
{
    Task<CenterDashboardResult> ExecuteAsync(Guid? subjectId, decimal riskThreshold, CancellationToken cancellationToken);
}
