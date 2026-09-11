using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Recommendations;

public static class StudentLockHelper
{
    public static async Task AcquireStudentLockAsync(
        EduTwinDbContext dbContext,
        Guid centerId,
        Guid studentId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dbContext);

        if (dbContext.Database.IsRelational())
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT student_id FROM students WHERE center_id = {centerId} AND student_id = {studentId} FOR UPDATE;",
                cancellationToken);
        }
    }
}
