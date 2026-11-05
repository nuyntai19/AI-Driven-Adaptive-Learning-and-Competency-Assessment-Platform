using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.KnowledgeGraph;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.Data.MySqlClient;
using Xunit;

namespace EduTwin.BLL.Tests.KnowledgeGraph;

[Collection("MySqlDatabase")]
public sealed class KnowledgeGradeScopeMySqlTests
{
    private const string ConnectionVariable = "EDUTWIN_TEST_MYSQL_ADMIN_CONNECTION_STRING";

    [ScopeMySqlFact]
    public async Task RealMySql_RejectsScopedCycles_InvalidGrade_DraftPlanMismatch_AndPublishedMutation()
    {
        await using var f = await Database.Create();
        await using var db = f.Context();
        var service = new KnowledgeGraphVersionService(db, f.Tenant, TimeProvider.System);
        var created = await service.CreateAsync(new() { SubjectId = f.Subject, GradeLevel = 10 }, default);
        var draft = created.Data!; var versionId = Guid.Parse(draft.GraphVersionId);
        var mapped = await service.UpdateAsync(versionId, new() { RowVersion = draft.RowVersion,
            NodeMappings = [new() { NodeId = f.First.ToString() }, new() { NodeId = f.Second.ToString(), ParentNodeId = f.First.ToString() }] }, default);
        Assert.Null(mapped.ErrorCode);
        await Guard(db, "UPDATE knowledge_node_grades SET parent_node_id={0} WHERE center_id={1} AND graph_version_id={2} AND node_id={3}",
            f.Second, f.Center, versionId, f.First);
        await Guard(db, "UPDATE knowledge_node_grades SET node_id={0} WHERE center_id={1} AND graph_version_id={2} AND node_id={3}",
            f.Third, f.Center, versionId, f.First);

        var forward = await db.KnowledgeEdges.SingleAsync(e => e.SourceNodeId == f.First && e.TargetNodeId == f.Second);
        var opposite = new KnowledgeEdge { CenterId = f.Center, SubjectId = f.Subject, SourceNodeId = f.Second,
            TargetNodeId = f.First, RelationType = RelationType.PrerequisiteOf, Weight = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.KnowledgeEdges.Add(opposite); await db.SaveChangesAsync();
        await db.Database.ExecuteSqlRawAsync("INSERT INTO knowledge_edge_grades(center_id,graph_version_id,edge_id,relation_type,weight,created_at) VALUES({0},{1},{2},'PrerequisiteOf',1,UTC_TIMESTAMP(6))",
            f.Center, versionId, forward.EdgeId);
        await Guard(db, "INSERT INTO knowledge_edge_grades(center_id,graph_version_id,edge_id,relation_type,weight,created_at) VALUES({0},{1},{2},'PrerequisiteOf',1,UTC_TIMESTAMP(6))",
            f.Center, versionId, opposite.EdgeId);
        var published = await service.PublishAsync(versionId, new() { RowVersion = mapped.Data!.RowVersion }, default);
        Assert.Null(published.ErrorCode);
        await Guard(db, "DELETE FROM knowledge_node_grades WHERE center_id={0} AND graph_version_id={1} AND node_id={2}", f.Center, versionId, f.Second);
        await Guard(db, "UPDATE knowledge_edge_grades SET weight=0.2 WHERE center_id={0} AND graph_version_id={1}", f.Center, versionId);
        await Guard(db, "DELETE FROM knowledge_edge_grades WHERE center_id={0} AND graph_version_id={1}", f.Center, versionId);
        await Guard(db, "UPDATE knowledge_nodes SET node_name='Overwrite history' WHERE center_id={0} AND node_id={1}", f.Center, f.First);
        await Guard(db, "UPDATE knowledge_graph_versions SET status='Draft',is_current=0 WHERE center_id={0} AND graph_version_id={1}", f.Center, versionId);

        var curriculum = new Curriculum { CenterId = f.Center, CurriculumId = Guid.NewGuid(), SubjectId = f.Subject,
            TeacherId = f.Teacher, Title = "Scope test", GradeLevel = 10, GraphVersionId = versionId,
            ReviewStatus = ReviewStatus.Draft, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var klass = new Class { CenterId = f.Center, ClassId = Guid.NewGuid(), SubjectId = f.Subject,
            TeacherId = f.Teacher, ClassName = "10A1", AcademicYear = "2026-2027", GradeLevel = 10,
            Status = ClassStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var wrong = new Class { CenterId = f.Center, ClassId = Guid.NewGuid(), SubjectId = f.Subject,
            TeacherId = f.Teacher, ClassName = "12A1", AcademicYear = "2026-2027", GradeLevel = 12,
            Status = ClassStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.Curriculums.Add(curriculum); db.Classes.AddRange(klass, wrong); await db.SaveChangesAsync();
        await Guard(db, "UPDATE curriculums SET grade_level=NULL WHERE center_id={0} AND curriculum_id={1}", f.Center, curriculum.CurriculumId);
        await Guard(db, "INSERT INTO curriculum_nodes(center_id,curriculum_id,node_id,order_index,created_at) VALUES({0},{1},{2},1,UTC_TIMESTAMP(6))", f.Center, curriculum.CurriculumId, f.Third);
        await db.Database.ExecuteSqlRawAsync("INSERT INTO curriculum_nodes(center_id,curriculum_id,node_id,order_index,created_at) VALUES({0},{1},{2},1,UTC_TIMESTAMP(6))", f.Center, curriculum.CurriculumId, f.First);
        await Guard(db, "UPDATE curriculums SET graph_version_id=NULL WHERE center_id={0} AND curriculum_id={1}", f.Center, curriculum.CurriculumId);
        await Guard(db, "INSERT INTO curriculum_classes(center_id,curriculum_id,class_id,assigned_by,assigned_at) VALUES({0},{1},{2},{3},UTC_TIMESTAMP(6))",
            f.Center, curriculum.CurriculumId, wrong.ClassId, f.Teacher);
        db.CurriculumClasses.Add(new() { CenterId = f.Center, CurriculumId = curriculum.CurriculumId, ClassId = klass.ClassId,
            AssignedBy = f.Teacher, AssignedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        await Guard(db, "UPDATE curriculums SET grade_level=12,graph_version_id=NULL WHERE center_id={0} AND curriculum_id={1}", f.Center, curriculum.CurriculumId);
        await Guard(db, "UPDATE classes SET grade_level=12 WHERE center_id={0} AND class_id={1}", f.Center, klass.ClassId);
        Assert.Equal(10, await db.Database.SqlQueryRaw<int>("SELECT grade_level AS Value FROM curriculums WHERE curriculum_id={0}", curriculum.CurriculumId).SingleAsync());

        var upperDraft = (await service.CreateAsync(new() { SubjectId = f.Subject, GradeLevel = 12 }, default)).Data!;
        var upperId = Guid.Parse(upperDraft.GraphVersionId);
        var upperMapped = (await service.UpdateAsync(upperId, new() { RowVersion = upperDraft.RowVersion,
            NodeMappings = [new() { NodeId = f.Third.ToString() }] }, default)).Data!;
        Assert.Null((await service.PublishAsync(upperId, new() { RowVersion = upperMapped.RowVersion }, default)).ErrorCode);
        var question = new Question { CenterId = f.Center, SubjectId = f.Subject, PrimaryTopicNodeId = f.Third,
            CreatedByTeacherId = f.Teacher, QuestionType = QuestionType.ShortAnswer, Difficulty = 2, GradeLevel = 12,
            QuestionText = "Scoped question", CorrectAnswer = "1", Solution = "1", MaxScore = 1, EstimatedTimeSeconds = 60,
            LanguageCode = "vi", Status = QuestionStatus.Draft, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        db.Questions.Add(question); await db.SaveChangesAsync();
        await Guard(db, "UPDATE questions SET grade_level=10 WHERE center_id={0} AND question_id={1}", f.Center, question.QuestionId);
        await Guard(db, "INSERT INTO question_knowledge_nodes(center_id,question_id,node_id,mapping_role,created_at) VALUES({0},{1},{2},'Secondary',UTC_TIMESTAMP(6))",
            f.Center, question.QuestionId, f.First);
        await db.Database.ExecuteSqlRawAsync("INSERT INTO question_knowledge_nodes(center_id,question_id,node_id,mapping_role,created_at) VALUES({0},{1},{2},'Prerequisite',UTC_TIMESTAMP(6))",
            f.Center, question.QuestionId, f.First);
        Assert.Equal(1, await db.QuestionKnowledgeNodes.CountAsync(n => n.QuestionId == question.QuestionId && n.MappingRole == MappingRole.Prerequisite));

        curriculum.ReviewStatus = ReviewStatus.Published; await db.SaveChangesAsync();
        db.ClassCurriculumApplications.Add(new() { CenterId = f.Center, ApplicationId = Guid.NewGuid(), ClassId = klass.ClassId,
            CurriculumId = curriculum.CurriculumId, SubjectId = f.Subject, ApplicationRole = "Primary", StartedAt = DateTime.UtcNow,
            AssignedBy = f.Teacher, ClassGradeAtStart = 10, CurriculumGradeAtStart = 10 });
        await db.SaveChangesAsync();
        var catalogDraft = (await service.CreateAsync(new() { SubjectId = f.Subject, GradeLevel = 12 }, default)).Data!;
        var catalog = await new CreateKnowledgeEdgeUseCase(db, f.Tenant, TimeProvider.System, new()).ExecuteAsync(new()
        { SubjectId = f.Subject, GraphVersionId = Guid.Parse(catalogDraft.GraphVersionId), SourceNodeId = f.First.ToString(),
            TargetNodeId = f.Third.ToString(), RelationType = "RelatedTo", Weight = .5m }, default);
        Assert.True(catalog.IsSuccess, catalog.Message);
        var classGraph = await new GetKnowledgeGraphUseCase(db, f.Tenant).ExecuteAsync(new KnowledgeGraphQuery { ClassId = klass.ClassId });
        Assert.True(classGraph.IsSuccess, classGraph.ErrorCode);
        Assert.Empty(classGraph.Data!.Edges); // A new catalog edge never rewrites an applied immutable topology.
    }

    [ScopeMySqlFact]
    public async Task RealMySql_ConcurrentOppositeEdges_CannotBothCommit_AndForkPreservesOldTopology()
    {
        await using var f = await Database.Create();
        Guid versionId; ulong forwardId; ulong reverseId;
        await using (var db = f.Context())
        {
            var service = new KnowledgeGraphVersionService(db, f.Tenant, TimeProvider.System);
            var draft = (await service.CreateAsync(new() { SubjectId = f.Subject, GradeLevel = 10 }, default)).Data!;
            versionId = Guid.Parse(draft.GraphVersionId);
            var mapped = await service.UpdateAsync(versionId, new() { RowVersion = draft.RowVersion,
                NodeMappings = [new() { NodeId = f.First.ToString() }, new() { NodeId = f.Second.ToString() }] }, default);
            Assert.Null(mapped.ErrorCode);
            forwardId = await db.KnowledgeEdges.Where(e => e.SourceNodeId == f.First && e.TargetNodeId == f.Second).Select(e => e.EdgeId).SingleAsync();
            var reverse = new KnowledgeEdge { CenterId = f.Center, SubjectId = f.Subject, SourceNodeId = f.Second, TargetNodeId = f.First,
                RelationType = RelationType.PrerequisiteOf, Weight = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
            db.KnowledgeEdges.Add(reverse); await db.SaveChangesAsync(); reverseId = reverse.EdgeId;
        }
        var ready = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var arrived = 0;
        async Task<bool> Insert(ulong edgeId)
        {
            await using var connection = new MySqlConnection(f.ConnectionString); await connection.OpenAsync();
            await using var transaction = await connection.BeginTransactionAsync(System.Data.IsolationLevel.ReadCommitted);
            if (Interlocked.Increment(ref arrived) == 2) ready.SetResult();
            await ready.Task;
            await using var command = connection.CreateCommand(); command.Transaction = transaction;
            command.CommandText = "INSERT INTO knowledge_edge_grades(center_id,graph_version_id,edge_id,relation_type,weight,created_at) VALUES(@center,@version,@edge,'PrerequisiteOf',1,UTC_TIMESTAMP(6))";
            command.Parameters.AddWithValue("@center", f.Center.ToString("D")); command.Parameters.AddWithValue("@version", versionId.ToString("D")); command.Parameters.AddWithValue("@edge", edgeId);
            try { await command.ExecuteNonQueryAsync(); await transaction.CommitAsync(); return true; }
            catch (MySqlException ex) when (ex.Message.Contains("EDUTWIN_ACADEMIC_DEPENDENCY", StringComparison.Ordinal) || ex.Number == 1213)
            { await transaction.RollbackAsync(); return false; }
        }
        var results = await Task.WhenAll(Insert(forwardId), Insert(reverseId));
        Assert.Single(results, x => x);
        await using var verify = f.Context();
        Assert.Equal(1, await verify.KnowledgeEdgeGrades.CountAsync(e => e.GraphVersionId == versionId));
        var oldVersion = await verify.KnowledgeGraphVersions.SingleAsync(v => v.GraphVersionId == versionId);
        var serviceVerify = new KnowledgeGraphVersionService(verify, f.Tenant, TimeProvider.System);
        Assert.Null((await serviceVerify.PublishAsync(versionId, new() { RowVersion = oldVersion.RowVersion.ToString() }, default)).ErrorCode);
        var fork = (await serviceVerify.CreateAsync(new() { SubjectId = f.Subject, GradeLevel = 10, SourceGraphVersionId = versionId }, default)).Data!;
        var forkId = Guid.Parse(fork.GraphVersionId);
        var saved = await serviceVerify.UpdateAsync(forkId, new() { RowVersion = fork.RowVersion, NodeMappings = fork.NodeMappings,
            EdgeMappings = [new() { EdgeId = Assert.Single(fork.EdgeMappings).EdgeId, RelationType = "RelatedTo", Weight = .2m }] }, default);
        Assert.Null(saved.ErrorCode);
        Assert.Null((await serviceVerify.PublishAsync(forkId, new() { RowVersion = saved.Data!.RowVersion }, default)).ErrorCode);
        var graph = new GetKnowledgeGraphUseCase(verify, f.Tenant);
        var old = (await graph.ExecuteAsync(new KnowledgeGraphQuery { GraphVersionId = versionId })).Data!;
        var current = (await graph.ExecuteAsync(new KnowledgeGraphQuery { SubjectId = f.Subject, GradeLevel = 10 })).Data!;
        Assert.Equal("PrerequisiteOf", Assert.Single(old.Edges).RelationType);
        Assert.Equal(1, Assert.Single(old.Edges).Weight);
        Assert.Equal("RelatedTo", Assert.Single(current.Edges).RelationType);
        Assert.Equal(.2m, Assert.Single(current.Edges).Weight);
    }

    private static async Task Guard(EduTwinDbContext db, string sql, params object[] parameters)
    {
        var exception = await Assert.ThrowsAnyAsync<Exception>(() => db.Database.ExecuteSqlRawAsync(sql, parameters));
        Assert.Contains("EDUTWIN_ACADEMIC_DEPENDENCY", exception.ToString(), StringComparison.Ordinal);
    }

    private sealed class ScopeMySqlFactAttribute : FactAttribute
    {
        public ScopeMySqlFactAttribute()
        { if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionVariable))) Skip = $"Set {ConnectionVariable} for real MySQL verification."; }
    }

    private sealed class Database : IAsyncDisposable
    {
        private readonly string admin;
        private readonly string name = "edutwin_scope_" + Guid.NewGuid().ToString("N");
        public string ConnectionString { get; private set; } = "";
        public TenantContext Tenant { get; } = new();
        public Guid Center { get; private set; }
        public Guid Teacher { get; private set; }
        public Guid Subject { get; private set; }
        public ulong First { get; private set; }
        public ulong Second { get; private set; }
        public ulong Third { get; private set; }
        private Database(string configured) => admin = new MySqlConnectionStringBuilder(configured) { Database = "", Pooling = false }.ConnectionString;
        public EduTwinDbContext Context() => new(new DbContextOptionsBuilder<EduTwinDbContext>().UseMySQL(ConnectionString).Options, Tenant);
        public static async Task<Database> Create()
        {
            var f = new Database(Environment.GetEnvironmentVariable(ConnectionVariable)!);
            await f.Admin($"CREATE DATABASE `{f.name}` CHARACTER SET utf8mb4");
            f.ConnectionString = new MySqlConnectionStringBuilder(f.admin) { Database = f.name, Pooling = false }.ConnectionString;
            try
            {
                await using var db = f.Context(); await db.Database.GetService<IMigrator>().MigrateAsync();
                var seed = new EduTwinSeedFactory(true).CreateData();
                db.Centers.Add(seed.Center);
                foreach (var user in seed.Users) user.PasswordHash = "disposable-test-hash";
                db.Users.AddRange(seed.Users); db.Teachers.AddRange(seed.Teachers); db.Subjects.AddRange(seed.Subjects);
                db.KnowledgeNodes.AddRange(seed.Topics); db.KnowledgeEdges.AddRange(seed.Edges); await db.SaveChangesAsync();
                f.Center = seed.Center.CenterId; f.Teacher = seed.Teachers[0].TeacherId; f.Subject = seed.Subjects[0].SubjectId;
                var topics = seed.Topics.Where(n => n.SubjectId == f.Subject).OrderBy(n => n.NodeId).ToList();
                f.First = topics[0].NodeId; f.Second = topics[1].NodeId; f.Third = topics[2].NodeId;
                f.Tenant.Initialize(f.Center, f.Teacher, nameof(UserRole.Teacher), 1); return f;
            }
            catch { await f.DisposeAsync(); throw; }
        }
        private async Task Admin(string sql)
        { await using var c = new MySqlConnection(admin); await c.OpenAsync(); await using var command = c.CreateCommand(); command.CommandText = sql; await command.ExecuteNonQueryAsync(); }
        public ValueTask DisposeAsync() => new(Admin($"DROP DATABASE IF EXISTS `{name}`"));
    }
}
