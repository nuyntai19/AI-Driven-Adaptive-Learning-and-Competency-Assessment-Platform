using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.Organization;

namespace EduTwin.BLL.Organization;

public interface IListTeacherStudentNotesUseCase
{
    Task<ListTeacherStudentNotesResult> ExecuteAsync(TeacherStudentNoteListQuery query, CancellationToken cancellationToken);
    Task<ListTeacherStudentNotesResult> GetMyNotesAsync(CancellationToken cancellationToken);
}
