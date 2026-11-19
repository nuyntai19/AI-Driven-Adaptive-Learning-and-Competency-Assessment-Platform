using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.KnowledgeGraph;
using EduTwin.BLL.Organization;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using MySql.Data.MySqlClient;
using Xunit;

namespace EduTwin.BLL.Tests.KnowledgeGraph;

public sealed class SubjectLifecycleMySqlFactAttribute : FactAttribute
{
    public SubjectLifecycleMySqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("EDUTWIN_SUBJECT_LIFECYCLE_MYSQL")))
            Skip = "Set EDUTWIN_SUBJECT_LIFECYCLE_MYSQL to an isolated MySQL test server.";
    }
}

public sealed class SubjectLifecycleMySqlTests
{
    // SavingChanges runs after validation but before the database transaction: pause exactly at that race boundary.
    private sealed class SaveGate : SaveChangesInterceptor
    {
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Reached.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return result;
        }
    }

    [SubjectLifecycleMySqlFact]
    public async Task NewClassCommittingFirst_MakesStaleDeactivationFailAtomically()
    {
        await using var fixture = await Database.CreateAsync();
        var gate = new SaveGate();
        await using var deactivationContext = fixture.Context(gate);
        var deactivate = new UpdateSubjectUseCase(deactivationContext, fixture.Tenant, TimeProvider.System).ExecuteAsync(fixture.SubjectId,
            new UpdateSubjectRequest { SubjectCode = "MATH", SubjectName = "Toán", IsActive = false, RowVersion = "1" }, CancellationToken.None);
        await gate.Reached.Task.WaitAsync(TimeSpan.FromSeconds(30));
        try
        {
            await using var createContext = fixture.Context();
            Assert.True((await fixture.CreateClass(createContext)).IsSuccess);
        }
        finally { gate.Release.TrySetResult(); }
        Assert.Equal(ErrorCodes.ConcurrencyConflict, (await deactivate).ErrorCode);
        await using var verify = fixture.Context();
        Assert.True((await verify.Subjects.SingleAsync()).IsActive);
        Assert.Single(await verify.Classes.ToListAsync());
    }

    [SubjectLifecycleMySqlFact]
    public async Task DeactivationCommittingFirst_RollsBackConcurrentClassCreation()
    {
        await using var fixture = await Database.CreateAsync();
        var gate = new SaveGate();
        await using var createContext = fixture.Context(gate);
        var create = fixture.CreateClass(createContext);
        await gate.Reached.Task.WaitAsync(TimeSpan.FromSeconds(30));
        try
        {
            await using var deactivateContext = fixture.Context();
            var result = await new UpdateSubjectUseCase(deactivateContext, fixture.Tenant, TimeProvider.System).ExecuteAsync(fixture.SubjectId,
                new UpdateSubjectRequest { SubjectCode = "MATH", SubjectName = "Toán", IsActive = false, RowVersion = "1" }, CancellationToken.None);
            Assert.True(result.IsSuccess);
        }
        finally { gate.Release.TrySetResult(); }
        Assert.Equal(ErrorCodes.ConcurrencyConflict, (await create).ErrorCode);
        await using var verify = fixture.Context();
        Assert.False((await verify.Subjects.SingleAsync()).IsActive);
        Assert.Empty(await verify.Classes.ToListAsync());
    }

    [SubjectLifecycleMySqlFact]
    public async Task DeactivationCommittingFirst_RollsBackConcurrentClassReopening()
    {
        await using var fixture = await Database.CreateAsync();
        await using var seed = fixture.Context();
        Assert.True((await fixture.CreateClass(seed)).IsSuccess);
        var entity = await seed.Classes.SingleAsync();
        entity.Status = ClassStatus.Archived;
        entity.LearningScope = ClassLearningScope.History;
        await seed.SaveChangesAsync();
        var subjectVersion = (await seed.Subjects.AsNoTracking().SingleAsync()).RowVersion;

        var gate = new SaveGate();
        await using var reopenContext = fixture.Context(gate);
        var reopen = new UpdateClassUseCase(reopenContext, fixture.Tenant, TimeProvider.System, NullLogger<UpdateClassUseCase>.Instance)
            .ExecuteAsync(entity.ClassId, new UpdateClassRequest { ClassName = entity.ClassName, TeacherId = fixture.TeacherId,
                GradeLevel = 12, Status = ClassStatus.Active, LifecycleReason = "Mở lại lớp", RowVersion = entity.RowVersion.ToString() });
        await gate.Reached.Task.WaitAsync(TimeSpan.FromSeconds(30));
        try
        {
            await using var deactivateContext = fixture.Context();
            Assert.True((await new UpdateSubjectUseCase(deactivateContext, fixture.Tenant, TimeProvider.System).ExecuteAsync(fixture.SubjectId,
                new UpdateSubjectRequest { SubjectCode = "MATH", SubjectName = "Toán", IsActive = false, RowVersion = subjectVersion.ToString() }, CancellationToken.None)).IsSuccess);
        }
        finally { gate.Release.TrySetResult(); }
        Assert.Equal(ErrorCodes.ConcurrencyConflict, (await reopen).ErrorCode);
        await using var verify = fixture.Context();
        Assert.False((await verify.Subjects.SingleAsync()).IsActive);
        Assert.Equal(ClassStatus.Archived, (await verify.Classes.SingleAsync()).Status);
    }

    private sealed class Database : IAsyncDisposable
    {
        private readonly string _adminConnection;
        private readonly string _databaseName = $"edutwin_subject_{Guid.NewGuid():N}";
        private string _connection = null!;
        public Guid SubjectId { get; } = Guid.NewGuid();
        public Guid TeacherId { get; } = Guid.NewGuid();
        public TenantContext Tenant { get; } = new();
        private Database(string admin) { _adminConnection = admin; Tenant.Initialize(Guid.NewGuid(), Guid.NewGuid(), nameof(UserRole.CenterManager), 1); }
        public EduTwinDbContext Context(SaveGate? gate = null)
        {
            var options = new DbContextOptionsBuilder<EduTwinDbContext>().UseMySQL(_connection);
            if (gate != null) options.AddInterceptors(gate);
            return new EduTwinDbContext(options.Options, Tenant);
        }
        public Task<CreateClassResult> CreateClass(EduTwinDbContext db) =>
            new CreateClassUseCase(db, Tenant, TimeProvider.System, NullLogger<CreateClassUseCase>.Instance).ExecuteAsync(
                new CreateClassRequest { ClassName = "Lớp Toán", AcademicYear = "2026-2027", GradeLevel = 12, SubjectId = SubjectId, TeacherId = TeacherId });
        public static async Task<Database> CreateAsync()
        {
            var admin = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("EDUTWIN_SUBJECT_LIFECYCLE_MYSQL")) { Database = "", Pooling = false };
            var fixture = new Database(admin.ConnectionString);
            try
            {
                await using var connection = new MySqlConnection(admin.ConnectionString);
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = $"CREATE DATABASE `{fixture._databaseName}` CHARACTER SET utf8mb4;";
                await command.ExecuteNonQueryAsync();
                admin.Database = fixture._databaseName;
                fixture._connection = admin.ConnectionString;
                await using var db = fixture.Context();
                await db.Database.EnsureCreatedAsync();
                var now = DateTime.UtcNow;
                var center = fixture.Tenant.CenterId!.Value;
                db.Centers.Add(new Center { CenterId = center, CenterCode = "TEST", CenterName = "Center", Timezone = "UTC", Status = CenterStatus.Active, CreatedAt = now, UpdatedAt = now });
                db.Subjects.Add(new Subject { CenterId = center, SubjectId = fixture.SubjectId, SubjectCode = "MATH", SubjectName = "Toán", IsActive = true, CreatedAt = now, UpdatedAt = now });
                db.Users.Add(new User { CenterId = center, UserId = fixture.Tenant.UserId!.Value, Username = "manager", DisplayName = "Manager", RoleName = UserRole.CenterManager, Status = UserStatus.Active, PasswordHash = "test", CreatedAt = now, UpdatedAt = now });
                var teacherUser = new User { CenterId = center, UserId = fixture.TeacherId, Username = "teacher", DisplayName = "Teacher", RoleName = UserRole.Teacher, Status = UserStatus.Active, PasswordHash = "test", CreatedAt = now, UpdatedAt = now };
                db.Teachers.Add(new Teacher { CenterId = center, TeacherId = fixture.TeacherId, SubjectId = fixture.SubjectId, User = teacherUser, CreatedAt = now, UpdatedAt = now });
                await db.SaveChangesAsync();
                return fixture;
            }
            catch { await fixture.DisposeAsync(); throw; }
        }
        public async ValueTask DisposeAsync()
        {
            await using var connection = new MySqlConnection(_adminConnection);
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = $"DROP DATABASE IF EXISTS `{_databaseName}`;";
            await command.ExecuteNonQueryAsync();
        }
    }
}
