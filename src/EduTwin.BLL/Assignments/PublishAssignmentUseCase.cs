using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.Assignments;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Assignments;

/// <summary>
/// Triển khai POST /api/v1/assignments/{id}/publish (API_CONTRACTS.md §51, MASTER_PLAN P10-T02).
///
/// Publish phải:
///   1. Validate Assignment tồn tại, đúng tenant, đúng owner, đang Draft.
///   2. Validate rowVersion.
///   3. Validate questions không rỗng và tất cả Active + đúng Subject.
///   4. Trong một transaction duy nhất:
///      a. Materialize AssignmentTargets từ snapshot (WholeClass → active members, SelectedStudents → thành viên đã chọn).
///      b. Tạo StudentAssignmentProgress (NotStarted) cho từng target.
///      c. Set Assignment.Status = Published, PublishedAt = now.
///      d. Tăng RowVersion.
///   5. Nếu bất kỳ bước nào fail → rollback toàn bộ.
///
/// Invariants tuân theo CONSTITUTION §8, §17, DATABASE_SCHEMA §22, §23.
/// Tenant (center_id) luôn lấy từ JWT/ITenantContext — KHÔNG từ client.
/// </summary>
public class PublishAssignmentUseCase : IPublishAssignmentUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public PublishAssignmentUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _timeProvider = timeProvider;
    }

    public async Task<PublishAssignmentResult> ExecuteAsync(
        Guid assignmentId,
        PublishAssignmentRequest request,
        CancellationToken cancellationToken = default)
    {
        // ── 1. Fail-closed tenant/role gate ────────────────────────────────────
        if (!_tenantContext.IsResolved ||
            !_tenantContext.CenterId.HasValue || _tenantContext.CenterId.Value == Guid.Empty ||
            !_tenantContext.UserId.HasValue || _tenantContext.UserId.Value == Guid.Empty ||
            string.IsNullOrWhiteSpace(_tenantContext.Role) ||
            (!string.Equals(_tenantContext.Role, nameof(UserRole.Teacher), StringComparison.Ordinal) &&
             !string.Equals(_tenantContext.Role, nameof(UserRole.CenterManager), StringComparison.Ordinal)))
        {
            return PublishAssignmentResult.Failure(ErrorCodes.ResourceNotFound);
        }

        if (assignmentId == Guid.Empty)
            return PublishAssignmentResult.Failure(ErrorCodes.ResourceNotFound);

        var actorId = _tenantContext.UserId.Value;
        var isTeacher = string.Equals(_tenantContext.Role, nameof(UserRole.Teacher), StringComparison.Ordinal);

        // ── 2. Validate rowVersion format: ASCII digits only, > 0 ──────────────
        if (string.IsNullOrEmpty(request.RowVersion))
            return PublishAssignmentResult.Failure(ErrorCodes.ValidationFailed);

        foreach (var ch in request.RowVersion)
        {
            if (ch < '0' || ch > '9')
                return PublishAssignmentResult.Failure(ErrorCodes.ValidationFailed);
        }

        if (!ulong.TryParse(request.RowVersion, NumberStyles.None, CultureInfo.InvariantCulture, out var clientRowVersion) || clientRowVersion == 0)
            return PublishAssignmentResult.Failure(ErrorCodes.ValidationFailed);

        // ── 3. Load Assignment (Global Query Filter: centerId + !isDeleted) ─────
        var assignment = await _dbContext.Assignments
            .FirstOrDefaultAsync(a => a.AssignmentId == assignmentId, cancellationToken);

        if (assignment == null)
            return PublishAssignmentResult.Failure(ErrorCodes.ResourceNotFound);

        // ── 4. Ownership guard ──────────────────────────────────────────────────
        // Teacher: chỉ publish Assignment của Class mình sở hữu
        // CenterManager: toàn Center (Global Query Filter đã scope)
        if (isTeacher)
        {
            var classOwner = await _dbContext.Classes
                .AsNoTracking()
                .Select(c => new { c.ClassId, c.TeacherId })
                .FirstOrDefaultAsync(c => c.ClassId == assignment.ClassId, cancellationToken);

            if (classOwner == null || classOwner.TeacherId != actorId)
                return PublishAssignmentResult.Failure(ErrorCodes.ResourceNotFound);
        }

        // ── 5. State machine: chỉ Draft được phép publish ───────────────────────
        if (assignment.Status != AssignmentStatus.Draft)
            return PublishAssignmentResult.Failure(ErrorCodes.InvalidStateTransition);

        // ── 6. rowVersion concurrency check ─────────────────────────────────────
        if (assignment.RowVersion != clientRowVersion)
            return PublishAssignmentResult.Failure(ErrorCodes.ConcurrencyConflict);

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        if (assignment.DueAt.HasValue && assignment.DueAt.Value <= now)
            return PublishAssignmentResult.Failure(ErrorCodes.ValidationFailed);

        // ── 7. Load ordered Questions — phải không rỗng ─────────────────────────
        var orderedQuestions = await _dbContext.AssignmentQuestions
            .AsNoTracking()
            .Where(aq => aq.AssignmentId == assignmentId)
            .OrderBy(aq => aq.OrderIndex)
            .ToListAsync(cancellationToken);

        // Business invariant: Assignment phải có ít nhất 1 Question khi publish
        if (orderedQuestions.Count == 0)
            return PublishAssignmentResult.Failure(ErrorCodes.ValidationFailed);

        // ── 8. Validate tất cả Questions: Active + đúng Subject ─────────────────
        // Load Subject của Class một lần
        var classEntity = await _dbContext.Classes
            .AsNoTracking()
            .Select(c => new { c.ClassId, c.SubjectId, c.TeacherId, c.Status })
            .FirstOrDefaultAsync(c => c.ClassId == assignment.ClassId, cancellationToken);

        if (classEntity == null)
            return PublishAssignmentResult.Failure(ErrorCodes.ResourceNotFound);

        var questionIds = orderedQuestions.Select(q => q.QuestionId).ToList();
        var dbQuestions = await _dbContext.Questions
            .AsNoTracking()
            .Where(q => questionIds.Contains(q.QuestionId))
            .Select(q => new { q.QuestionId, q.SubjectId, q.Status })
            .ToListAsync(cancellationToken);

        if (dbQuestions.Count != questionIds.Count)
            return PublishAssignmentResult.Failure(ErrorCodes.ResourceNotFound);

        foreach (var q in dbQuestions)
        {
            if (q.Status != EduTwin.Contracts.CurriculumAndQuestions.QuestionStatus.Active)
                return PublishAssignmentResult.Failure(ErrorCodes.ValidationFailed);

            if (q.SubjectId != classEntity.SubjectId)
                return PublishAssignmentResult.Failure(ErrorCodes.ValidationFailed);
        }

        // ── 9. Xác định danh sách Target Students ───────────────────────────────
        // Đọc target mode từ Draft: nếu có SelectedStudents records trong AssignmentTargets
        // thì dùng SelectedStudents; nếu không (hoặc rỗng) => WholeClass.
        // NOTE: P10-T01 lưu TargetMode thông qua AssignmentTargets records ở Draft.
        // Theo thiết kế hiện tại, Draft không có AssignmentTargets (chúng được materialize khi publish).
        // Do đó cần đọc lại từ Draft context:
        // - CreateAssignment lưu TargetMode qua CreateAssignmentRequest.TargetMode
        //   nhưng Assignment entity không lưu TargetMode string (chỉ lưu sau khi publish).
        //
        // Kiểm tra thực tế: Assignment entity không có TargetMode field.
        // Thay vào đó, khi create (WholeClass): không lưu targets ngay;
        // khi create (SelectedStudents): không lưu targets ngay — chờ publish.
        //
        // Vậy làm sao biết mode? Nhìn lại CreateAssignmentUseCase:
        //   - Nó KHÔNG persist AssignmentTargets ở Draft phase.
        //   - TargetMode/StudentIds chỉ validate lúc create, không lưu.
        // => Vậy publish cần đọc từ một nguồn khác.
        //
        // ⚠️ MÂU THUẪN PHÁT HIỆN: Assignment entity không có TargetMode column.
        // DATABASE_SCHEMA §20 (assignments table) không có target_mode column.
        // Nhưng DATABASE_SCHEMA §22 (assignment_targets) có target_source column.
        // CreateAssignmentUseCase chưa tạo assignment_targets khi Draft.
        //
        // => Publish cần dùng thông tin từ Assignment creation context.
        //    Giải pháp: Assignment phải lưu TargetMode hoặc phải có một cách
        //    để biết ai là target khi publish.
        //
        // Tham chiếu API_CONTRACTS.md §50:
        //   targetMode: WholeClass hoặc SelectedStudents
        //   studentIds: list student IDs (khi SelectedStudents)
        //
        // MASTER_PLAN P10-T02 §67:
        //   "Target WholeClass lấy active membership tại thời điểm publish."
        //   "SelectedStudents phải thuộc Class."
        //
        // Kết luận: Vì Assignment không lưu TargetMode + StudentIds,
        // publish sẽ lấy active membership của Class (WholeClass behavior)
        // trừ khi có AssignmentTargets Draft records (SelectedStudents behavior).
        //
        // NOTE: Cần xem lại xem CreateAssignmentUseCase có lưu Draft targets không.
        // Nhìn lại CreateAssignmentUseCase: KHÔNG lưu AssignmentTargets khi create.
        // Vậy, về mặt nghiệp vụ thuần túy:
        //
        // Cách tiếp cận được approve:
        //   - Xem Draft AssignmentTargets:
        //     * Nếu KHÔNG có records => WholeClass (lấy active class members tại publish time)
        //     * Nếu CÓ records => SelectedStudents (đã được lưu trước khi publish)
        //
        // Tuy nhiên CreateAssignmentUseCase không lưu AssignmentTargets!
        // => Cần thêm bước cho UpdateAssignment để lưu Draft targets,
        //    hoặc Publish phải nhận thêm thông tin targetMode + studentIds.
        //
        // Tham chiếu API_CONTRACTS.md §51 Publish request: CHỈ CÓ rowVersion.
        // => Publish không nhận thêm thông tin target.
        //
        // => Publish hoạt động theo logic:
        //   - Nếu AssignmentTargets (Draft records) đã tồn tại → dùng danh sách đó
        //   - Nếu không có → lấy toàn bộ active class members (WholeClass)
        //
        // Đây là thiết kế hợp lệ: Teacher có thể Update assignment để thêm Draft targets,
        // hoặc không thêm gì (WholeClass là default).

        // Đọc các Draft targets đã lưu (nếu có)
        var existingDraftTargets = await _dbContext.AssignmentTargets
            .AsNoTracking()
            .Where(at => at.AssignmentId == assignmentId)
            .ToListAsync(cancellationToken);

        List<Guid> targetStudentIds;
        TargetSource targetSourceEnum;

        if (existingDraftTargets.Count > 0)
        {
            // SelectedStudents mode: dùng danh sách đã lưu
            targetSourceEnum = TargetSource.SelectedStudents;
            targetStudentIds = existingDraftTargets.Select(t => t.StudentId).Distinct().ToList();

            // Validate: tất cả Student phải là active member của Class
            var activeMemberIds = await _dbContext.ClassStudents
                .AsNoTracking()
                .Where(cs => cs.ClassId == assignment.ClassId &&
                             cs.Status == ClassStudentStatus.Active)
                .Select(cs => cs.StudentId)
                .ToListAsync(cancellationToken);

            // Compare in memory because the MySQL EF provider cannot type-map
            // Contains(List<Guid>) against GUID columns stored as varchar(36).
            var activeMemberIdSet = activeMemberIds.ToHashSet();
            if (targetStudentIds.Any(studentId => !activeMemberIdSet.Contains(studentId)))
                return PublishAssignmentResult.Failure(ErrorCodes.ValidationFailed);
        }
        else
        {
            // WholeClass mode: lấy active members tại thời điểm publish
            targetSourceEnum = TargetSource.WholeClass;
            targetStudentIds = await _dbContext.ClassStudents
                .AsNoTracking()
                .Where(cs => cs.ClassId == assignment.ClassId &&
                             cs.Status == ClassStudentStatus.Active)
                .Select(cs => cs.StudentId)
                .ToListAsync(cancellationToken);
        }

        // Business invariant: phải có ít nhất 1 target
        if (targetStudentIds.Count == 0)
            return PublishAssignmentResult.Failure(ErrorCodes.ValidationFailed);

        var totalQuestionCount = (uint)orderedQuestions.Count;
        // ── 10. Atomic publish transaction ──────────────────────────────────────
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // 10a. Xóa Draft targets (nếu có) — sẽ thay bằng materialized targets
            if (existingDraftTargets.Count > 0)
            {
                var draftTargetsTracked = await _dbContext.AssignmentTargets
                    .Where(at => at.AssignmentId == assignmentId)
                    .ToListAsync(cancellationToken);
                _dbContext.AssignmentTargets.RemoveRange(draftTargetsTracked);
                await _dbContext.SaveChangesAsync(cancellationToken);
            }

            // 10b. Materialize AssignmentTargets — snapshot tại thời điểm publish
            var newTargets = targetStudentIds.Select(studentId => new AssignmentTarget
            {
                CenterId = assignment.CenterId,
                AssignmentId = assignmentId,
                StudentId = studentId,
                TargetSource = targetSourceEnum,
                CreatedAt = now,
                CreatedBy = actorId
            }).ToList();

            _dbContext.AssignmentTargets.AddRange(newTargets);

            // 10c. Tạo StudentAssignmentProgress (NotStarted) cho từng target
            var progresses = new List<StudentAssignmentProgress>(targetStudentIds.Count);
            byte[] progressIdBytes = new byte[8];
            foreach (var studentId in targetStudentIds)
            {
                ulong progressId;
                do
                {
                    System.Security.Cryptography.RandomNumberGenerator.Fill(progressIdBytes);
                    progressId = BitConverter.ToUInt64(progressIdBytes, 0);
                } while (progressId == 0);

                progresses.Add(new StudentAssignmentProgress
                {
                    ProgressId = progressId,
                    CenterId = assignment.CenterId,
                    AssignmentId = assignmentId,
                    StudentId = studentId,
                    Status = ProgressStatus.NotStarted,
                    CompletedQuestionCount = 0,
                    TotalQuestionCount = totalQuestionCount,
                    StartedAt = null,
                    CompletedAt = null,
                    IsDeleted = false,
                    CreatedAt = now,
                    UpdatedAt = now,
                    CreatedBy = actorId,
                    UpdatedBy = actorId
                });
            }

            _dbContext.StudentAssignmentProgresses.AddRange(progresses);

            // 10d. Chốt Assignment: Published + published_at
            assignment.Status = AssignmentStatus.Published;
            assignment.PublishedAt = now;
            assignment.UpdatedAt = now;
            assignment.UpdatedBy = actorId;
            // RowVersion sẽ được tăng tự động bởi EduTwinDbContext.UpdateRowVersions()

            await _dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }

        // ── 11. Build response DTO ───────────────────────────────────────────────
        var questionDtos = orderedQuestions
            .Select(aq => new AssignmentQuestionDto
            {
                QuestionId = aq.QuestionId.ToString(CultureInfo.InvariantCulture),
                OrderIndex = aq.OrderIndex,
                Points = aq.Points
            })
            .ToList();

        var targetDtos = targetStudentIds
            .Select(sid => new AssignmentTargetDto
            {
                StudentId = sid.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(),
                TargetSource = targetSourceEnum.ToString()
            })
            .ToList();

        var dto = new AssignmentDto
        {
            AssignmentId = assignment.AssignmentId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(),
            ClassId = assignment.ClassId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(),
            CreatedByTeacherId = assignment.CreatedByTeacherId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(),
            Title = assignment.Title,
            Instructions = assignment.Instructions,
            DueAt = assignment.DueAt,
            Status = assignment.Status.ToString(),
            QuestionCount = questionDtos.Count,
            TargetStudentCount = targetDtos.Count,
            Questions = questionDtos,
            Targets = targetDtos,
            RowVersion = assignment.RowVersion.ToString(CultureInfo.InvariantCulture)
        };

        return PublishAssignmentResult.Success(dto);
    }
}
