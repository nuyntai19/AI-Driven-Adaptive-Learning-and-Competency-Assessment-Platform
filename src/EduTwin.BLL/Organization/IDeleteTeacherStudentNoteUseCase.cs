using System;
using System.Threading;
using System.Threading.Tasks;

namespace EduTwin.BLL.Organization;

public interface IDeleteTeacherStudentNoteUseCase
{
    Task<DeleteTeacherStudentNoteResult> ExecuteAsync(Guid noteId, string? rowVersion, CancellationToken cancellationToken);
}
