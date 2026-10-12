using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.CurriculumAndQuestions;

public sealed record CurriculumApplicationResult(bool IsSuccess, string? ErrorCode, string? Message,
    string? RowVersion = null, List<CurriculumApplicationDto>? Data = null);

public sealed class CurriculumApplicationUseCase(EduTwinDbContext db, ITenantContext tenant, TimeProvider clock)
{
    public async Task<List<AcademicDependencyGuards.ActiveClassUsage>?> UsageAsync(Guid curriculumId, CancellationToken ct)
    {
        if (!CurriculumGuards.TryResolveActor(tenant, out var center, out var actor, out var teacher) || !teacher) return null;
        var curriculum = await db.Curriculums.AsNoTracking().SingleOrDefaultAsync(c => c.CenterId == center && c.CurriculumId == curriculumId, ct);
        if (curriculum is null || !CurriculumGuards.CanAccess(curriculum, actor, teacher)) return null;
        return await AcademicDependencyGuards.CurriculumUsageAsync(db, center, curriculumId, ct);
    }

    public async Task<CurriculumApplicationResult> ReadAsync(Guid curriculumId, CancellationToken ct)
    {
        if (!CurriculumGuards.TryResolveActor(tenant, out var center, out var actor, out var teacher) || !teacher)
            return new(false, ErrorCodes.ResourceNotFound, null);
        var curriculum = await db.Curriculums.SingleOrDefaultAsync(c => c.CenterId == center && c.CurriculumId == curriculumId, ct);
        if (curriculum is null || !CurriculumGuards.CanRead(curriculum, actor, teacher)) return new(false, ErrorCodes.ResourceNotFound, null);
        var rows = await db.ClassCurriculumApplications.AsNoTracking()
            .Where(a => a.CenterId == center && a.CurriculumId == curriculumId && a.Class.TeacherId == actor)
            .OrderByDescending(a => a.StartedAt).ThenBy(a => a.ApplicationId)
            .Select(a => new CurriculumApplicationDto { ApplicationId = a.ApplicationId, ClassId = a.ClassId,
                ClassName = a.Class.ClassName, PausedByClass = a.Class.Status != ClassStatus.Active, ApplicationRole = a.ApplicationRole, StartedAt = a.StartedAt,
                EndedAt = a.EndedAt, AssignedBy = a.AssignedBy, ChangeReason = a.ChangeReason, EndReason = a.EndReason,
                GradeMismatchReason = a.GradeMismatchReason }).ToListAsync(ct);
        return new(true, null, null, curriculum.RowVersion.ToString(), rows);
    }

    public async Task<CurriculumApplicationResult> ApplyAsync(Guid curriculumId, ApplyCurriculumRequest request, CancellationToken ct)
    {
        if (!CurriculumGuards.TryResolveActor(tenant, out var center, out var actor, out var teacher) || !teacher)
            return new(false, ErrorCodes.ResourceNotFound, null);
        if (request.ClassIds is null || request.ClassIds.Count > 100 || request.ClassIds.Any(id => id == Guid.Empty) ||
            request.ClassIds.Distinct().Count() != request.ClassIds.Count || request.ApplicationRole is not ("Primary" or "Supplemental") ||
            request.ChangeReason?.Length > 500 || request.GradeMismatchReason?.Length > 500 ||
            !CurriculumGuards.TryParseRowVersion(request.RowVersion, out var version))
            return new(false, ErrorCodes.ValidationFailed, "Kiểm tra lớp, vai trò áp dụng, phiên bản và lý do (tối đa 500 ký tự).");
        var curriculum = await db.Curriculums.SingleOrDefaultAsync(c => c.CenterId == center && c.CurriculumId == curriculumId, ct);
        if (curriculum is null || !CurriculumGuards.CanRead(curriculum, actor, teacher)) return new(false, ErrorCodes.ResourceNotFound, null);
        if (curriculum.RowVersion != version) return new(false, ErrorCodes.ConcurrencyConflict, "Giáo trình đã thay đổi. Hãy tải lại.");
        if (curriculum.ReviewStatus != ReviewStatus.Published) return new(false, ErrorCodes.InvalidStateTransition, "Chỉ áp dụng giáo trình đã xuất bản.");
        // Read only this teacher's classes. Never trust IDs or UI filtering.
        var classes = await db.Classes.Where(c => c.CenterId == center && c.TeacherId == actor && c.SubjectId == curriculum.SubjectId && !c.IsDeleted).ToListAsync(ct);
        var selected = classes.Where(c => c.Status == ClassStatus.Active && c.LearningScope == ClassLearningScope.Current && request.ClassIds.Contains(c.ClassId)).ToList();
        if (selected.Count != request.ClassIds.Count) return new(false, ErrorCodes.ResourceNotFound, "Chỉ được áp dụng cho lớp đang học do bạn phụ trách, cùng môn.");
        var current = await db.ClassCurriculumApplications.Where(a => a.CenterId == center && a.EndedAt == null &&
            a.Class.TeacherId == actor && a.Class.LearningScope == ClassLearningScope.Current && a.SubjectId == curriculum.SubjectId).ToListAsync(ct);
        var toEnd = current.Where(a => (a.CurriculumId == curriculumId &&
            ((a.ApplicationRole == request.ApplicationRole && !request.ClassIds.Contains(a.ClassId)) ||
             (request.ClassIds.Contains(a.ClassId) && a.ApplicationRole != request.ApplicationRole))) ||
            (request.ApplicationRole == "Primary" && request.ClassIds.Contains(a.ClassId) && a.ApplicationRole == "Primary" && a.CurriculumId != curriculumId)).ToList();
        if (toEnd.Count > 0 && string.IsNullOrWhiteSpace(request.ChangeReason))
            return new(false, ErrorCodes.ValidationFailed, "Thay hoặc ngừng áp dụng giáo trình cần nhập lý do để lưu lịch sử.");
        var now = clock.GetUtcNow().UtcDateTime;
        var toAdd = selected.Where(c => !current.Any(a => a.EndedAt == null && a.ClassId == c.ClassId &&
            a.CurriculumId == curriculumId && a.ApplicationRole == request.ApplicationRole)).ToList();
        if (toEnd.Count == 0 && toAdd.Count == 0) return await ReadAsync(curriculumId, ct);
        if (toAdd.Any(c => !c.GradeLevel.HasValue || !curriculum.GradeLevel.HasValue || c.GradeLevel != curriculum.GradeLevel) && string.IsNullOrWhiteSpace(request.GradeMismatchReason))
            return new(false, ErrorCodes.ValidationFailed, "Giáo trình khác khối/chưa phân khối cần lý do ngoại lệ rõ ràng.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        try
        {
            foreach (var application in toEnd) { application.EndedAt = now; application.EndedBy = actor; application.EndReason = request.ChangeReason!.Trim(); }
            // Release generated unique keys before inserting replacements, inside one transaction.
            await db.SaveChangesAsync(ct);
            foreach (var cls in toAdd)
            {
                db.ClassCurriculumApplications.Add(NewApplication(curriculum, cls, actor, now, request.ApplicationRole,
                    request.ChangeReason, request.GradeMismatchReason));
                cls.UpdatedAt = now; cls.UpdatedBy = actor;
            }
            foreach (var classId in toEnd.Select(a => a.ClassId).Distinct())
            {
                var cls = classes.FirstOrDefault(c => c.ClassId == classId);
                if (cls is not null) { cls.UpdatedAt = now; cls.UpdatedBy = actor; }
            }
            curriculum.UpdatedAt = now; curriculum.UpdatedBy = actor;
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
        catch (DbUpdateException ex) when (ex is DbUpdateConcurrencyException || IsGuardConflict(ex))
        {
            await transaction.RollbackAsync(ct); db.ChangeTracker.Clear();
            return new(false, ErrorCodes.ConcurrencyConflict, "Lớp hoặc giáo trình đã thay đổi; vui lòng tải lại. Mỗi lớp chỉ có một giáo trình chính đang áp dụng.");
        }
        return await ReadAsync(curriculumId, ct);
    }

    private static bool IsGuardConflict(Exception error)
    {
        for (Exception? current = error; current is not null; current = current.InnerException)
            if (current is MySql.Data.MySqlClient.MySqlException { Number: 1062 or 1644 }) return true;
        return false;
    }

    internal static ClassCurriculumApplication NewApplication(Curriculum curriculum, Class cls, Guid actor, DateTime now,
        string role = "Primary", string? changeReason = null, string? exceptionReason = null)
    {
        var mismatch = !cls.GradeLevel.HasValue || !curriculum.GradeLevel.HasValue || cls.GradeLevel != curriculum.GradeLevel;
        return new() { ApplicationId = Guid.NewGuid(), CenterId = cls.CenterId, ClassId = cls.ClassId,
            CurriculumId = curriculum.CurriculumId, SubjectId = cls.SubjectId, ApplicationRole = role,
            StartedAt = now, AssignedBy = actor, ChangeReason = changeReason?.Trim(),
            ClassGradeAtStart = cls.GradeLevel, CurriculumGradeAtStart = curriculum.GradeLevel,
            IsGradeException = mismatch, GradeMismatchReason = mismatch ? exceptionReason?.Trim() : null,
            ExceptionApprovedBy = mismatch ? actor : null, ExceptionApprovedAt = mismatch ? now : null };
    }
}
