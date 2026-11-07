using System;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.Organization;

namespace EduTwin.BLL.Organization;

public interface IUpsertTeacherStudentNoteUseCase
{
    Task<TeacherStudentNoteResult> ExecuteAsync(Guid studentId, UpsertTeacherStudentNoteRequest request, CancellationToken cancellationToken);
}
