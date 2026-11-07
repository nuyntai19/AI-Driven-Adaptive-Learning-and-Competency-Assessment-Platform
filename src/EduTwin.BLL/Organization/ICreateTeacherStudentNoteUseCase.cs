using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.Organization;

namespace EduTwin.BLL.Organization;

public interface ICreateTeacherStudentNoteUseCase
{
    Task<TeacherStudentNoteResult> ExecuteAsync(CreateTeacherStudentNoteRequest request, CancellationToken cancellationToken);
}
