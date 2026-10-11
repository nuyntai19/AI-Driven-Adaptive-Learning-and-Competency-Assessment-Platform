using System.Text.Json;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Notifications;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Notifications;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Notifications;

public sealed class NotificationScopeException : Exception;

// Explicit relay boundary. Filter bypasses ALWAYS bind a validated source/target center and account type.
// Does not expose academic data, arbitrary tenant selection, or free student messaging.
public sealed class NotificationAudiencePolicy(EduTwinDbContext db)
{
    private IQueryable<User> ActiveUsers(Guid center, UserRole role) => db.Users.IgnoreQueryFilters().AsNoTracking()
        .Where(x => x.CenterId == center && !x.IsDeleted && x.Status == UserStatus.Active && x.RoleName == role);
    private IQueryable<EduTwin.DAL.Organization.Class> OwnClasses(User sender) => db.Classes.IgnoreQueryFilters().AsNoTracking()
        .Where(x => x.CenterId == sender.CenterId && !x.IsDeleted && x.TeacherId == sender.UserId &&
            x.Status == ClassStatus.Active && x.LearningScope == ClassLearningScope.Current);
    private async Task<Guid> PlatformCenterAsync(CancellationToken ct) => await db.Centers.AsNoTracking()
        .Where(c => !c.IsDeleted && c.Status == CenterStatus.Active && c.CenterCode == "PLATFORM")
        .Select(c => c.CenterId).SingleOrDefaultAsync(ct);

    public async Task<IReadOnlyList<NotificationAudienceOption>> OptionsAsync(User sender, CancellationToken ct)
    {
        var result = new List<NotificationAudienceOption>();
        if (sender.RoleName == UserRole.Teacher)
        {
            var classes = await OwnClasses(sender).OrderBy(c => c.ClassName).Take(501).Select(c => new NotificationChoice(c.ClassId, c.ClassName)).ToArrayAsync(ct);
            result.Add(new("ClassStudents", "Học sinh theo lớp", classes.Take(500).ToArray()));
            var students = await StudentUsers(sender, null).OrderBy(u => u.DisplayName).Take(500)
                .Select(u => new NotificationChoice(u.UserId, u.DisplayName)).ToArrayAsync(ct);
            result.Add(new("SelectedStudents", "Học sinh được chọn", students));
            result.Add(new("CenterManagers", "Quản lý trung tâm", await ChoicesAsync(ActiveUsers(sender.CenterId, UserRole.CenterManager), ct)));
        }
        else if (sender.RoleName == UserRole.CenterManager)
        {
            result.Add(new("CenterTeachers", "Giáo viên trong trung tâm", await ChoicesAsync(ActiveUsers(sender.CenterId, UserRole.Teacher), ct)));
            var platform = await PlatformCenterAsync(ct);
            result.Add(new("PlatformSupport", "Hỗ trợ nền tảng (Admin)", await ChoicesAsync(ActiveUsers(platform, UserRole.PlatformAdmin), ct)));
        }
        else if (sender.RoleName == UserRole.PlatformAdmin && sender.CenterId == await PlatformCenterAsync(ct))
        {
            var centers = await db.Centers.AsNoTracking().Where(c => !c.IsDeleted && c.Status == CenterStatus.Active && c.CenterCode != "PLATFORM")
                .OrderBy(c => c.CenterName).Take(500).Select(c => new NotificationChoice(c.CenterId, c.CenterName + " · " + c.CenterCode)).ToArrayAsync(ct);
            result.Add(new("CenterManagersRelay", "Quản lý của trung tâm được chọn", centers));
        }
        else throw new NotificationScopeException();
        return result;
    }
    private static Task<NotificationChoice[]> ChoicesAsync(IQueryable<User> query, CancellationToken ct) => query.OrderBy(u => u.DisplayName)
        .Take(500).Select(u => new NotificationChoice(u.UserId, u.DisplayName)).ToArrayAsync(ct);
    private IQueryable<User> StudentUsers(User sender, Guid[]? classIds)
    {
        var classes = OwnClasses(sender);
        if (classIds is not null) classes = NotificationRecipientScope.WhereIds(classes, c => c.ClassId, classIds);
        var memberIds = db.ClassStudents.IgnoreQueryFilters().Where(m => m.CenterId == sender.CenterId && m.Status == ClassStudentStatus.Active &&
            m.RemovedAt == null && classes.Any(c => c.ClassId == m.ClassId)).Select(m => m.StudentId);
        return ActiveUsers(sender.CenterId, UserRole.Student).Where(u => memberIds.Contains(u.UserId));
    }
    public async Task<NotificationRecipient[]> ResolveAsync(User sender, NotificationComposeRequest request, CancellationToken ct, bool revalidation = false)
    {
        if (request.TargetIds is null || request.TargetIds.Length is < 1 or > 500 || request.TargetIds.Any(i => i == Guid.Empty) ||
            request.TargetIds.Distinct().Count() != request.TargetIds.Length) throw new NotificationScopeException();
        IQueryable<User> query;
        switch (sender.RoleName, request.AudienceKind)
        {
            case (UserRole.Teacher, "ClassStudents"):
                var validClasses = await NotificationRecipientScope.WhereIds(OwnClasses(sender), c => c.ClassId, request.TargetIds).CountAsync(ct);
                if (!revalidation && validClasses != request.TargetIds.Length) throw new NotificationScopeException();
                query = StudentUsers(sender, request.TargetIds);
                break;
            case (UserRole.Teacher, "SelectedStudents"):
                query = NotificationRecipientScope.WhereIds(StudentUsers(sender, null), u => u.UserId, request.TargetIds);
                break;
            case (UserRole.Teacher, "CenterManagers"):
                query = NotificationRecipientScope.WhereIds(ActiveUsers(sender.CenterId, UserRole.CenterManager), u => u.UserId, request.TargetIds);
                break;
            case (UserRole.CenterManager, "CenterTeachers"):
                query = NotificationRecipientScope.WhereIds(ActiveUsers(sender.CenterId, UserRole.Teacher), u => u.UserId, request.TargetIds);
                break;
            case (UserRole.CenterManager, "PlatformSupport"):
                query = NotificationRecipientScope.WhereIds(ActiveUsers(await PlatformCenterAsync(ct), UserRole.PlatformAdmin), u => u.UserId, request.TargetIds);
                break;
            case (UserRole.PlatformAdmin, "CenterManagersRelay"):
                if (sender.CenterId != await PlatformCenterAsync(ct)) throw new NotificationScopeException();
                var centers = await NotificationRecipientScope.WhereIds(db.Centers.AsNoTracking().Where(c => !c.IsDeleted && c.Status == CenterStatus.Active && c.CenterCode != "PLATFORM"), c => c.CenterId, request.TargetIds)
                    .Select(c => c.CenterId).ToArrayAsync(ct);
                if (!revalidation && centers.Length != request.TargetIds.Length) throw new NotificationScopeException();
                query = NotificationRecipientScope.WhereIds(db.Users.IgnoreQueryFilters().AsNoTracking().Where(u => !u.IsDeleted && u.Status == UserStatus.Active && u.RoleName == UserRole.CenterManager), u => u.CenterId, centers);
                break;
            default: throw new NotificationScopeException();
        }
        var recipients = await query.Take(501).Select(u => new NotificationRecipient(u.CenterId, u.UserId, u.DisplayName)).ToArrayAsync(ct);
        if (recipients.Length > 500 || !revalidation && recipients.Length == 0) throw new NotificationScopeException();
        if (!revalidation && request.AudienceKind is not ("ClassStudents" or "CenterManagersRelay") && recipients.Length != request.TargetIds.Length)
            throw new NotificationScopeException();
        return recipients.OrderBy(x => x.CenterId).ThenBy(x => x.UserId).ToArray();
    }

    public async Task<HashSet<Guid>> RevalidateAsync(NotificationEvent envelope, CancellationToken ct)
    {
        if (envelope.MessageId is not { } messageId) return JsonSerializer.Deserialize<Guid[]>(envelope.RecipientUserIds)!.ToHashSet();
        if (await db.NotificationMessageActions.IgnoreQueryFilters().AnyAsync(x => x.MessageId == messageId, ct)) return [];
        // The event in the worker's tenant is authoritative proof of this relay relationship.
        var message = await db.NotificationMessages.IgnoreQueryFilters().AsNoTracking().SingleAsync(m => m.MessageId == messageId, ct);
        var sender = await db.Users.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(u => u.CenterId == message.CenterId && u.UserId == message.SenderUserId &&
            !u.IsDeleted && u.Status == UserStatus.Active && u.AuthVersion == message.SenderAuthVersion, ct);
        if (sender is null || !await db.Centers.AnyAsync(c => c.CenterId == message.CenterId && !c.IsDeleted && c.Status == CenterStatus.Active, ct)) return [];
        var request = JsonSerializer.Deserialize<NotificationComposeRequest>(message.Audience) ?? throw new InvalidOperationException("Invalid message audience.");
        try { return (await ResolveAsync(sender, request, ct, revalidation: true)).Where(r => r.CenterId == envelope.CenterId).Select(r => r.UserId).ToHashSet(); }
        catch (NotificationScopeException) { return []; }
    }
}
