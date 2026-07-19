using System.Threading.Tasks;
using EduTwin.Contracts.Organization;

namespace EduTwin.BLL.Organization;

public interface IUpdateStudentUseCase
{
    Task<UpdateStudentResult> ExecuteAsync(System.Guid studentId, UpdateStudentRequest request);
}
