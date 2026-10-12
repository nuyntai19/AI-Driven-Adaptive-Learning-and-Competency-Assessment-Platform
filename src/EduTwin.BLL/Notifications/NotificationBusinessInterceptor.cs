using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.Assignments;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Notifications;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;

namespace EduTwin.BLL.Notifications;

// All inserts are staged BEFORE the caller's SaveChanges, in the same EF transaction.
// No recursive SaveChanges, network or AI call. Only a missing transaction is owned across SaveChanges.
public sealed class NotificationBusinessInterceptor(ITenantContext tenant, TimeProvider clock) : SaveChangesInterceptor
{
    private IDbContextTransaction? ownedTransaction;
    private bool Observed(EduTwinDbContext db) => db.ChangeTracker.Entries().Any(e => e.State is EntityState.Added or EntityState.Modified &&
        (tenant.UserId != null && e.Entity is Assignment or StudentAssignmentProgress or StudentReviewRequest or Class or ClassStudent or ClassCurriculumApplication or Curriculum or User or AssignmentQuestion or AuthorizationAuditLog ||
         e.Entity is AIAnalysisJob { Status: AIJobStatus.Completed or AIJobStatus.FallbackCompleted or AIJobStatus.FailedTerminal }));
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is EduTwinDbContext db && tenant.IsResolved && tenant.CenterId == db.CurrentTenantId &&
            Observed(db))
            throw new InvalidOperationException("Business notification transitions require SaveChangesAsync.");
        return result;
    }
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is EduTwinDbContext db && tenant.IsResolved && tenant.CenterId == db.CurrentTenantId)
        {
            // Keep an existing caller-owned transaction intact. The provider's implicit batch transaction
            // is insufficient when the observer adds commands; own a transaction only when none exists.
            if ((Observed(db) || db.ChangeTracker.Entries<NotificationEvent>().Any(e => e.State == EntityState.Added)) &&
                db.Database.IsRelational() && db.Database.CurrentTransaction == null)
                ownedTransaction = await db.Database.BeginTransactionAsync(cancellationToken);
            try { await new NotificationBusinessStager(db, tenant, clock).StageAsync(cancellationToken); }
            catch { await FinishOwnedAsync(false, CancellationToken.None); throw; }
        }
        return result;
    }
    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    { await FinishOwnedAsync(true, cancellationToken); return result; }
    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
        => FinishOwnedAsync(false, CancellationToken.None);
    public override Task SaveChangesCanceledAsync(DbContextEventData eventData, CancellationToken cancellationToken = default)
        => FinishOwnedAsync(false, CancellationToken.None);
    private async Task FinishOwnedAsync(bool commit, CancellationToken ct)
    {
        var transaction = ownedTransaction; ownedTransaction = null;
        if (transaction == null) return;
        try { if (commit) await transaction.CommitAsync(ct); else await transaction.RollbackAsync(ct); }
        finally { await transaction.DisposeAsync(); }
    }
}

internal sealed class NotificationBusinessStager(EduTwinDbContext db, ITenantContext tenant, TimeProvider clock)
{
    private Guid Center => tenant.CenterId!.Value;
    private bool Human => tenant.UserId is { } id && id != Guid.Empty;
    private static bool Changed<T>(EntityEntry<T> entry, string property) where T : class => entry.State == EntityState.Modified &&
        !Equals(entry.Property(property).OriginalValue, entry.Property(property).CurrentValue);
    private static string Short(string text) => text.Length <= 100 ? text : text[..97] + "…";
    private async Task<Guid[]> Members(Guid classId, CancellationToken ct) => await db.ClassStudents.AsNoTracking()
        .Where(m => m.ClassId == classId && m.Status == ClassStudentStatus.Active && m.RemovedAt == null).Select(m => m.StudentId).ToArrayAsync(ct);
    private async Task<Guid[]> Managers(CancellationToken ct) => await db.Users.AsNoTracking()
        .Where(u => u.Status == UserStatus.Active && u.RoleName == (tenant.Role == nameof(UserRole.PlatformAdmin) ? UserRole.PlatformAdmin : UserRole.CenterManager)).Select(u => u.UserId).ToArrayAsync(ct);
    private async Task<Class?> Class(Guid classId, CancellationToken ct) => db.Classes.Local.FirstOrDefault(c => c.ClassId == classId)
        ?? await db.Classes.AsNoTracking().SingleOrDefaultAsync(c => c.ClassId == classId, ct);
    private async Task<Assignment?> Assignment(Guid id, CancellationToken ct) => db.Assignments.Local.FirstOrDefault(a => a.AssignmentId == id)
        ?? await db.Assignments.AsNoTracking().SingleOrDefaultAsync(a => a.AssignmentId == id, ct);
    private async Task<Guid[]> Targets(Guid id, CancellationToken ct)
    {
        var saved = await db.AssignmentTargets.AsNoTracking().Where(t => t.AssignmentId == id).Select(t => t.StudentId).ToArrayAsync(ct);
        var deleted = db.ChangeTracker.Entries<AssignmentTarget>().Where(e => e.State == EntityState.Deleted && e.Entity.AssignmentId == id).Select(e => e.Entity.StudentId).ToHashSet();
        return saved.Where(x => !deleted.Contains(x)).Concat(db.ChangeTracker.Entries<AssignmentTarget>()
            .Where(e => e.State == EntityState.Added && e.Entity.AssignmentId == id).Select(e => e.Entity.StudentId)).Distinct().ToArray();
    }
    public async Task StageAsync(CancellationToken ct)
    {
        // Snapshot the entries before adding events so recursion/iterator changes cannot create duplicate observers.
        var entries = db.ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified).ToArray();
        foreach (var raw in entries)
        {
            if (raw.Entity is AIAnalysisJob job && raw.State == EntityState.Modified &&
                !Equals(raw.Property(nameof(job.Status)).OriginalValue, job.Status) &&
                job.Status is AIJobStatus.Completed or AIJobStatus.FallbackCompleted or AIJobStatus.FailedTerminal)
            {
                var attempt = db.Attempts.Local.FirstOrDefault(a => a.AttemptId == job.AttemptId)
                    ?? await db.Attempts.AsNoTracking().SingleOrDefaultAsync(a => a.AttemptId == job.AttemptId, ct);
                if (attempt?.AssignmentId is { } assignmentId)
                    await Notice($"ai-terminal:{job.AnalysisJobId}:{attempt.ManualRetryCount}", "AICompletionMarker", "Kiểm tra phân tích toàn bài", "Marker nội bộ, không hiển thị inbox.",
                        [attempt.StudentId], new("AICompletionMarker", AssignmentId: assignmentId, StudentId: attempt.StudentId,
                            AttemptId: attempt.AttemptId, Version: attempt.ManualRetryCount.ToString()), ct);
                continue;
            }
            if (!Human) continue; // No seed/migration human attribution or retrospective notifications.
            switch (raw.Entity)
            {
                case Assignment a when a.CenterId == Center:
                    var ae = db.Entry(a);
                    if (ae.State == EntityState.Added)
                        await Notice($"assignment-created:{a.AssignmentId}", "AssignmentCreated", "Giáo viên tạo bài tập", "Một bài tập mới đã được tạo; đây là metadata quản trị, không cấp quyền mở học liệu.", await Managers(ct), new("Administrative"), ct);
                    var publish = Changed(ae, nameof(a.Status)) && a.Status == AssignmentStatus.Published;
                    var close = Changed(ae, nameof(a.Status)) && a.Status == AssignmentStatus.Closed;
                    var update = ae.State == EntityState.Modified && a.Status == AssignmentStatus.Published &&
                        (Changed(ae, nameof(a.DueAt)) || Changed(ae, nameof(a.Instructions)));
                    if (publish || close || update)
                    {
                        var kind = publish ? "AssignmentPublished" : close ? "AssignmentClosed" : "AssignmentUpdated";
                        await Notice($"{kind}:{a.AssignmentId}:{a.RowVersion}", kind, (publish ? "Bài tập mới: " : close ? "Đã đóng bài: " : "Cập nhật bài: ") + Short(a.Title),
                            publish ? "Bạn được giao bài tập này. Xem hướng dẫn và hạn nộp trong bài." : close ? "Bài tập đã đóng; bài làm đã nộp được giữ nguyên." : "Hạn nộp hoặc hướng dẫn đã thay đổi. Xem thông tin hiện tại trong bài.",
                            await Targets(a.AssignmentId, ct), new(kind, a.ClassId, a.AssignmentId, Version: a.RowVersion.ToString()), ct);
                    }
                    break;
                case StudentAssignmentProgress p when p.CenterId == Center:
                    var pe = db.Entry(p);
                    var submittedNow = Changed(pe, nameof(p.Status)) && p.Status == ProgressStatus.Completed && pe.Property(x => x.CompletedAt).OriginalValue == null && p.CompletedAt != null &&
                        entries.Any(e => e.State == EntityState.Added && e.Entity is Attempt submitted && submitted.AssignmentId == p.AssignmentId && submitted.StudentId == p.StudentId);
                    var finalNow = Changed(pe, nameof(p.FinalReviewVersion)) && (Changed(pe, nameof(p.TeacherFinalReviewStatus)) || p.FinalReviewedAt != null);
                    if (!submittedNow && !finalNow) break;
                    var pa = await Assignment(p.AssignmentId, ct); if (pa is null) break;
                    var pc = await Class(pa.ClassId, ct); if (pc is null) break;
                    if (submittedNow)
                        await Notice($"submitted:{p.AssignmentId}:{p.StudentId}:{p.ProgressId}", "AssignmentSubmitted", "Đã nhận bài nộp: " + Short(pa.Title),
                            "Học sinh đã nộp toàn bài, gồm cả câu bỏ qua nếu có. AI có thể vẫn đang xử lý; chưa phải kết quả đã chốt.", [pc.TeacherId],
                            new("TeacherWork", pc.ClassId, pa.AssignmentId, p.StudentId), ct);
                    if (finalNow)
                    {
                        var approved = p.TeacherFinalReviewStatus == TeacherFinalReviewStatus.Approved;
                        await Notice($"final:{p.AssignmentId}:{p.StudentId}:{p.FinalReviewVersion}", approved ? "AssignmentResultApproved" : "AssignmentResultReopened",
                            approved ? "Kết quả toàn bài đã chốt" : "Kết quả toàn bài đang được rà soát",
                            $"Bài {Short(pa.Title)} · phiên bản kết quả {p.FinalReviewVersion}. " + (approved ? "Đây là kết quả đã được giáo viên chốt; điểm bài dùng thang 10, không phải chỉ số lập luận /100." : "Bài nộp gốc vẫn được giữ. Mở lại kết quả không yêu cầu bạn làm lại bài."),
                            [p.StudentId], new("HistoricalAssignment", pc.ClassId, pa.AssignmentId, p.StudentId, Version: p.FinalReviewVersion.ToString()), ct);
                    }
                    break;
                case StudentReviewRequest r when r.CenterId == Center:
                    var re = db.Entry(r);
                    var pending = re.State == EntityState.Added && r.Status == StudentReviewRequestStatus.Pending;
                    var resolved = Changed(re, nameof(r.Status)) && r.Status != StudentReviewRequestStatus.Pending;
                    if (!pending && !resolved) break;
                    var ra = db.Attempts.Local.FirstOrDefault(a => a.AttemptId == r.AttemptId)
                        ?? await db.Attempts.AsNoTracking().SingleOrDefaultAsync(a => a.AttemptId == r.AttemptId, ct);
                    var ras = ra?.AssignmentId is { } rid ? await Assignment(rid, ct) : null;
                    var rc = ras is null ? null : await Class(ras.ClassId, ct);
                    if (rc is null) break; // Free practice has no authoritative class/reviewer.
                    if (pending || resolved)
                    {
                        var report = new[] { "DEFECTIVE_QUESTION", "QUESTION_TYPO", "DEFECTIVE_QUESTION_WRONG_CONTENT", "DEFECTIVE_QUESTION_WRONG_OPTIONS", "DEFECTIVE_QUESTION_TYPO_LATEX", "DEFECTIVE_QUESTION_OTHER" }
                            .Any(category => r.StudentComment.StartsWith($"[{category}]", StringComparison.OrdinalIgnoreCase));
                        var kind = resolved ? "ReviewRequestResolved" : report ? "QuestionReported" : "ReviewRequested";
                        await Notice($"review:{r.AttemptId}:{r.CreatedAt.Ticks}:{r.Status}", kind, resolved ? "Yêu cầu xem xét đã được xử lý" : report ? "Có báo cáo lỗi đề" : "Có yêu cầu xem xét bài làm",
                            resolved ? "Giáo viên đã xử lý yêu cầu của bạn. Xem trạng thái và phản hồi trong bài làm; không suy ra toàn bài đã chốt từ thông báo này." : "Học sinh gửi yêu cầu cần giáo viên phụ trách xem xét. Nội dung riêng nằm trong luồng nghiệp vụ có kiểm tra quyền.",
                            resolved ? [r.StudentId] : [rc.TeacherId], new(resolved ? "HistoricalAssignment" : "TeacherWork", rc.ClassId, ras!.AssignmentId, r.StudentId, r.AttemptId), ct);
                    }
                    break;
                case Class c when c.CenterId == Center:
                    var ce = db.Entry(c);
                    if (Changed(ce, nameof(c.Status)))
                    {
                        var archived = c.Status == ClassStatus.Archived;
                        await Notice($"class-state:{c.ClassId}:{c.RowVersion}", archived ? "ClassArchived" : "ClassReopened", archived ? "Lớp học đã lưu trữ" : "Lớp học được mở lại",
                            Short(c.ClassName) + (archived ? ": chỉ xem lịch sử; không giao/làm bài hoặc luyện tập mới trong lớp này." : ": đã mở lại. Giáo trình đã ngừng áp dụng không tự được khôi phục."),
                            (await Members(c.ClassId, ct)).Append(c.TeacherId).Concat(await Managers(ct)).ToArray(), new("ClassLifecycle", c.ClassId), ct);
                    }
                    if (Changed(ce, nameof(c.TeacherId)))
                    {
                        await Notice($"class-handover:{c.ClassId}:{c.RowVersion}", "ClassTeacherChanged", "Thay đổi giáo viên phụ trách", Short(c.ClassName) + ": giáo viên phụ trách đã thay đổi. Quyền mở dữ liệu kiểm tra theo phân công hiện tại.",
                            (await Members(c.ClassId, ct)).Append(c.TeacherId).Append(ce.Property(x => x.TeacherId).OriginalValue).ToArray(), new("ClassLifecycle", c.ClassId), ct);
                        await Notice($"class-pending:{c.ClassId}:{c.RowVersion}", "ClassWorkHandover", "Tiếp nhận công việc của lớp", Short(c.ClassName) + ": xem các bài/yêu cầu còn chờ trong hàng đợi lớp được phân công mới.",
                            [c.TeacherId], new("TeacherWork", c.ClassId), ct);
                    }
                    break;
                case ClassStudent m when m.CenterId == Center:
                    var me = db.Entry(m); if (me.State != EntityState.Added && !Changed(me, nameof(m.Status))) break;
                    var mc = await Class(m.ClassId, ct); if (mc is null) break;
                    var active = m.Status == ClassStudentStatus.Active;
                    await Notice($"member:{m.ClassId}:{m.StudentId}:{(m.RemovedAt ?? m.JoinedAt).Ticks}:{m.Status}", active ? "ClassMemberJoined" : "ClassMemberRemoved",
                        active ? "Tham gia lớp học" : "Đã rời lớp học", Short(mc.ClassName) + (active ? ": đã cập nhật thành viên. Bài tập cũ không tự được giao thêm." : ": đã rút khỏi lớp; bài làm và lịch sử hợp lệ được giữ nguyên."),
                        [m.StudentId, mc.TeacherId], new("ClassLifecycle", mc.ClassId), ct);
                    break;
                case ClassCurriculumApplication app when app.CenterId == Center:
                    var le = db.Entry(app); var ended = Changed(le, nameof(app.EndedAt)) && app.EndedAt != null;
                    if (le.State != EntityState.Added && !ended) break;
                    var lc = await Class(app.ClassId, ct); if (lc is null) break;
                    await Notice($"curriculum-ledger:{app.ApplicationId}:{(ended ? "end" : "start")}", ended ? "CurriculumApplicationEnded" : "CurriculumApplied",
                        ended ? "Ngừng áp dụng giáo trình" : "Giáo trình áp dụng đã cập nhật", Short(lc.ClassName) + ": " + (ended ? "đã kết thúc một phạm vi giáo trình; lịch sử và bài làm giữ nguyên." : "giáo viên đã áp dụng giáo trình. Học sinh chỉ xem, không tự thay đổi giáo trình."),
                        (await Members(lc.ClassId, ct)).Append(lc.TeacherId).ToArray(), new("ClassLifecycle", lc.ClassId), ct);
                    break;
                case Curriculum curriculum when curriculum.CenterId == Center && raw.State == EntityState.Added:
                    await Notice($"curriculum-created:{curriculum.CurriculumId}", "CurriculumCreated", "Giáo viên tạo giáo trình", "Một giáo trình được tạo; chỉ metadata quản trị, không công bố nội dung bản nháp hoặc cấp quyền học thuật.", await Managers(ct), new("Administrative"), ct);
                    break;
                case User account when account.CenterId == Center && raw.State == EntityState.Modified &&
                    (Changed(db.Entry(account), nameof(account.Status)) || Changed(db.Entry(account), nameof(account.AuthVersion))):
                    await Notice($"account-access:{account.UserId}:{account.AuthVersion}", "AccountAccessChanged", "Thông tin truy cập đã cập nhật",
                        "Trạng thái tài khoản, quyền hoặc phiên đăng nhập đã thay đổi. Không có mật khẩu/token trong thông báo; khả năng truy cập được kiểm tra lại tại server.",
                        (await Managers(ct)).Append(account.UserId).ToArray(), new("Administrative"), ct);
                    break;
                case AssignmentQuestion q when q.CenterId == Center && raw.State == EntityState.Modified &&
                    q.IsVoided && !Equals(raw.Property(nameof(q.IsVoided)).OriginalValue, true):
                    var qa = await Assignment(q.AssignmentId, ct); if (qa is null) break;
                    await Notice($"void:{q.AssignmentId}:{q.QuestionId}", "AssignmentQuestionVoided", "Một câu trong bài đã được hủy", Short(qa.Title) + ": câu lỗi đề được loại khỏi phạm vi tính điểm của bài này. Bài khác dùng chung câu không bị hủy; kết quả có thể cần chốt lại.",
                        await Targets(q.AssignmentId, ct), new("HistoricalAssignment", qa.ClassId, qa.AssignmentId), ct);
                    break;
                case AuthorizationAuditLog audit when audit.CenterId == Center && raw.State == EntityState.Added &&
                    audit.ActorUserId != null && IsAdministrative(audit.ActionType):
                    var audience = await Managers(ct);
                    if (audit.TargetType is "KnowledgeNode" or "KnowledgeEdge") audience = audience.Concat(await db.Users.AsNoTracking()
                        .Where(u => u.Status == UserStatus.Active && u.RoleName == UserRole.Teacher).Select(u => u.UserId).ToArrayAsync(ct)).ToArray();
                    if (audit.TargetUserId is { } target) audience = audience.Append(target).ToArray();
                    await Notice($"audit:{Hash(audit.TraceId + audit.ActionType + audit.TargetId)}", "AdministrativeActivity", "Hoạt động quản trị được ghi nhận",
                        "Có thay đổi " + (audit.TargetType is "KnowledgeNode" or "KnowledgeEdge" ? "đồ thị tri thức" : "tài khoản/phân quyền") + ". Chi tiết chỉ xem trong nhật ký theo quyền; thông báo không chứa dữ liệu bảo mật.", audience, new("Administrative"), ct);
                    break;
            }
        }
    }
    private static bool IsAdministrative(string action) => action.StartsWith("Knowledge", StringComparison.Ordinal) ||
        action.Contains("Role", StringComparison.Ordinal) || action.Contains("Permission", StringComparison.Ordinal) ||
        action is "TeacherDeleted" or "StudentDeleted" or "TeacherPasswordReset" or "StudentPasswordReset" or "UserStatusChanged" or
            "CenterCreated" or "CenterStatusUpdated" or "CenterMetadataUpdated" or "CenterManagerPasswordReset" or "CenterManagerCreated" or "CenterPrimaryManagerChanged" or
            "PlatformAdminPasswordChanged" or "PlatformAdminSessionsRevoked";
    internal static string Hash(string value) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    private async Task Notice(string key, string kind, string title, string body, IEnumerable<Guid> recipients, NotificationBusinessContext context, CancellationToken ct)
    {
        var ids = recipients.Where(x => x != Guid.Empty).Distinct().Order().ToArray();
        // Bounded chunks, no broad user roster and no error in the academic action merely because one recipient is locked.
        for (var offset = 0; offset < ids.Length; offset += 500)
        {
            var batch = ids.Skip(offset).Take(500).ToArray();
            batch = await db.Users.AsNoTracking().Where(NotificationRecipientScope.Matches(batch)).Where(u => u.Status == UserStatus.Active)
                .Select(u => u.UserId).OrderBy(x => x).ToArrayAsync(ct);
            if (batch.Length == 0) continue;
            var dedupe = key + ":" + offset / 500;
            if (db.NotificationEvents.Local.Any(e => e.CenterId == Center && e.DeduplicationKey == dedupe) ||
                await db.NotificationEvents.AnyAsync(e => e.DeduplicationKey == dedupe, ct)) continue;
            var now = clock.GetUtcNow().UtcDateTime; var id = Guid.NewGuid();
            var senderName = Human ? db.Users.Local.FirstOrDefault(u => u.UserId == tenant.UserId)?.DisplayName ??
                await db.Users.AsNoTracking().Where(u => u.UserId == tenant.UserId).Select(u => u.DisplayName).SingleOrDefaultAsync(ct) : null;
            db.NotificationEvents.Add(new NotificationEvent { EventId = id, CenterId = Center, CreatedBy = tenant.UserId,
                SenderName = senderName is { Length: > 160 } ? senderName[..160] : senderName,
                CreatedAt = now, DeduplicationKey = dedupe, EventType = kind, Title = title, Body = body,
                RecipientUserIds = JsonSerializer.Serialize(batch), ResourceContext = JsonSerializer.Serialize(context) });
            db.NotificationDispatches.Add(new NotificationDispatch { EventId = id, CenterId = Center, AvailableAt = now, RowVersion = 1 });
        }
    }
}
