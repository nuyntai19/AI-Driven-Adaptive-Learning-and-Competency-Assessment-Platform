using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.CurriculumAndQuestions;

public sealed class GetQuestionImageUseCase(EduTwinDbContext db, ITenantContext tenant)
{
    public async Task<byte[]?> ExecuteAsync(ulong questionId, CancellationToken token)
    {
        if (!tenant.IsResolved || tenant.CenterId is not { } center || center == Guid.Empty ||
            tenant.UserId is not { } actor || actor == Guid.Empty || questionId == 0) return null;
        var question = await db.Questions.AsNoTracking().SingleOrDefaultAsync(q => q.CenterId == center && q.QuestionId == questionId && q.HasImage, token);
        if (question is null) return null;
        bool allowed;
        if (tenant.Role == nameof(UserRole.Teacher))
            allowed = question.CreatedByTeacherId == actor || (question.Visibility == MaterialVisibility.Shared && question.Status == QuestionStatus.Active)
                || await db.AssignmentQuestions.AnyAsync(aq => aq.CenterId == center && aq.QuestionId == questionId && aq.Assignment != null &&
                    aq.Assignment.Class != null && aq.Assignment.Class.TeacherId == actor, token);
        else if (tenant.Role == nameof(UserRole.CenterManager)) allowed = true; // center-filtered management scope
        else if (tenant.Role == nameof(UserRole.Student))
        {
            // A private question's image is readable only when this student was
            // assigned it, attempted it, or it is on their active learning path.
            allowed = await db.Students.AnyAsync(s => s.CenterId == center && s.StudentId == actor, token) &&
                (await db.Attempts.AnyAsync(a => a.CenterId == center && a.StudentId == actor && a.QuestionId == questionId, token) ||
                 await db.StudentAssignmentProgresses.AnyAsync(p => p.CenterId == center && p.StudentId == actor && p.Assignment != null &&
                     (p.Assignment.Status == AssignmentStatus.Published || p.Assignment.Status == AssignmentStatus.Closed) &&
                     db.AssignmentQuestions.Any(aq => aq.CenterId == center && aq.AssignmentId == p.AssignmentId && aq.QuestionId == questionId), token) ||
                 await db.LearningPathItems.AnyAsync(i => i.CenterId == center && i.RecommendedQuestionId == questionId && !i.IsDeleted &&
                     i.LearningPath != null && i.LearningPath.StudentId == actor && !i.LearningPath.IsDeleted, token));
        }
        else allowed = false;
        if (!allowed) return null; // do not disclose existence outside scope
        return await db.QuestionImages.AsNoTracking().Where(i => i.CenterId == center && i.QuestionId == questionId).Select(i => i.Data).SingleOrDefaultAsync(token);
    }
}
