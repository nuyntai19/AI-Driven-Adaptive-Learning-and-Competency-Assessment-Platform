using System;
using System.Threading;
using System.Threading.Tasks;

namespace EduTwin.BLL.Organization;

public interface IGetTeacherStudentNoteUseCase
{
    Task<TeacherStudentNoteResult> ExecuteAsync(Guid noteId, CancellationToken cancellationToken);
    Task<TeacherStudentNoteResult> GetForStudentAsync(Guid studentId, CancellationToken cancellationToken);
}
