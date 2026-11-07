using System;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.Organization;

namespace EduTwin.BLL.Organization;

public interface IUpdateTeacherStudentNoteUseCase
{
    Task<TeacherStudentNoteResult> ExecuteAsync(Guid noteId, UpdateTeacherStudentNoteRequest request, CancellationToken cancellationToken);
}
