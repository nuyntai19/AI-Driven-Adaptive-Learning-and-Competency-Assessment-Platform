using System;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.Organization;

namespace EduTwin.BLL.DigitalTwin;

public interface IUpsertStudentSubjectGoalUseCase
{
    Task<UpsertStudentSubjectGoalResult> ExecuteAsync(Guid studentId, Guid subjectId, UpsertStudentSubjectGoalRequest request, CancellationToken cancellationToken);
}
