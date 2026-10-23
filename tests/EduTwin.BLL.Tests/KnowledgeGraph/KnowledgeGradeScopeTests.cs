using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.CurriculumAndQuestions;
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
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTwin.BLL.Tests.KnowledgeGraph;

public sealed class KnowledgeGradeScopeTests
{
    [Fact]
    public async Task TeacherOverviewAndNodeCatalog_LabelOnlyApprovedCurrentGrades_AndKeepUnclassifiedEmpty()
    {
        using var f = await Fixture.Create();
        f.Version(10, 1, 2); f.Version(12, 2, 3);
        var historical = f.Version(11, 4); historical.IsCurrent = false; historical.IsLegacy = true;
        await f.Db.SaveChangesAsync();
        var overview = await f.Read(new() { SubjectId = f.Subject });
        Assert.Equal(new byte[] { 10 }, overview.Nodes.Single(n => n.NodeId == "1").GradeLevels);
        Assert.Equal(new byte[] { 10, 12 }, overview.Nodes.Single(n => n.NodeId == "2").GradeLevels);
        Assert.Empty(overview.Nodes.Single(n => n.NodeId == "4").GradeLevels);
        Assert.All(overview.Nodes, n => Assert.Empty(n.Provenance));
        var catalog = await new ListKnowledgeNodesUseCase(f.Db, f.Actor).ExecuteAsync(new KnowledgeNodeListQuery { SubjectId = f.Subject }, default);
        Assert.True(catalog.IsSuccess);
        Assert.Equal(new byte[] { 10, 12 }, catalog.Data!.Single(n => n.NodeId == "2").GradeLevels);
        Assert.Empty(catalog.Data!.Single(n => n.NodeId == "4").GradeLevels);
    }

    [Fact]
    public async Task GradeLibrary_UsesExplicitMappings_AndSeparatesExternalPrerequisites()
    {
        using var f = await Fixture.Create();
        var lower = f.Version(10, 1);
        var upper = f.Version(12, 2, 3);
        f.Db.KnowledgeEdgeGrades.Add(new() { CenterId = f.Center, GraphVersionId = upper.GraphVersionId, EdgeId = 1,
            SourceGraphVersionId = lower.GraphVersionId, RelationType = RelationType.PrerequisiteOf, Weight = .7m });
        await f.Db.SaveChangesAsync();

        var ten = await f.Read(new() { SubjectId = f.Subject, GradeLevel = 10 });
        Assert.Equal(new[] { "1" }, ten.Nodes.Select(n => n.NodeId));
        var twelve = await f.Read(new() { SubjectId = f.Subject, GradeLevel = 12 });
        Assert.Equal(new[] { "2", "3" }, twelve.Nodes.Select(n => n.NodeId));
        Assert.Empty(twelve.Edges);
        var prerequisite = Assert.Single(twelve.ExternalPrerequisites);
        Assert.Equal("1", prerequisite.SourceNodeId);
        Assert.Equal((byte)10, prerequisite.SourceGradeLevel);
        Assert.Equal(.7m, prerequisite.Weight);
        Assert.DoesNotContain(twelve.Nodes, n => n.NodeId == "4"); // Unclassified is never all grades.
        var empty = await f.Read(new() { SubjectId = f.Subject, GradeLevel = 11 });
        Assert.Empty(empty.Nodes);
        Assert.NotNull(empty.ResolvedScope!.Message);
    }

    [Fact]
    public async Task ClassProjection_UsesEachCurriculumComposition_AndRejectsYearAndOwnerConflicts()
    {
        using var f = await Fixture.Create();
        var version = f.Version(10, 1, 2, 3);
        var first = f.Klass(10, "2025-2026"); var second = f.Klass(10, "2026-2027");
        f.Apply(first, f.Curriculum(version, 1));
        f.Apply(second, f.Curriculum(version, 2));
        await f.Db.SaveChangesAsync();

        Assert.Equal("1", Assert.Single((await f.Read(new() { ClassId = first.ClassId })).Nodes).NodeId);
        Assert.Equal("2", Assert.Single((await f.Read(new() { ClassId = second.ClassId })).Nodes).NodeId);
        var conflict = await f.UseCase().ExecuteAsync(new KnowledgeGraphQuery { ClassId = first.ClassId, AcademicYear = second.AcademicYear });
        Assert.Equal(ErrorCodes.ValidationFailed, conflict.ErrorCode);
        var other = f.Tenant(Guid.NewGuid(), UserRole.Teacher);
        Assert.Equal(ErrorCodes.ResourceNotFound, (await f.UseCase(other).ExecuteAsync(new KnowledgeGraphQuery { ClassId = first.ClassId })).ErrorCode);
    }

    [Fact]
    public async Task PrimarySupplemental_KeepsOccurrencesAndDifferentParents_WhileDeduplicatingMasteryNodes()
    {
        using var f = await Fixture.Create();
        var ten = f.Version(10, 1, 2); var twelve = f.Version(12, 2, 3);
        f.Db.KnowledgeNodeGrades.Local.Single(n => n.GraphVersionId == ten.GraphVersionId && n.NodeId == 2).ParentNodeId = 1;
        f.Db.KnowledgeNodeGrades.Local.Single(n => n.GraphVersionId == twelve.GraphVersionId && n.NodeId == 2).ParentNodeId = 3;
        var klass = f.Klass(12);
        var primary = f.Apply(klass, f.Curriculum(twelve, 2, 3));
        var supplemental = f.Apply(klass, f.Curriculum(ten, 1, 2), "Supplemental", true);
        await f.Db.SaveChangesAsync();

        var graph = await f.Read(new() { ClassId = klass.ClassId });
        Assert.Equal(3, graph.Nodes.Count);
        var common = graph.Nodes.Single(n => n.NodeId == "2");
        Assert.Null(common.ParentNodeId);
        Assert.Equal(new byte[] { 10, 12 }, common.GradeLevels);
        Assert.Equal(2, common.Provenance.Select(p => p.OccurrenceId).Distinct().Count());
        Assert.Equal("1", common.Provenance.Single(p => p.GradeLevel == 10).ParentNodeId);
        Assert.Equal("3", common.Provenance.Single(p => p.GradeLevel == 12).ParentNodeId);
        Assert.Equal(primary.ApplicationId.ToString("D"), common.Provenance.Single(p => p.ApplicationRole == "Primary").ApplicationId);
        var extra = common.Provenance.Single(p => p.ApplicationRole == "Supplemental");
        Assert.True(extra.IsGradeException);
        Assert.Equal(supplemental.GradeMismatchReason, extra.GradeMismatchReason);
        Assert.NotNull(extra.CurriculumTitle);
    }

    [Fact]
    public async Task EndedApplication_NeverFallsBackToLibrary_AndHistoryRetainsPinnedVersion()
    {
        using var f = await Fixture.Create();
        var old = f.Version(10, 1); old.IsCurrent = false;
        f.Version(10, 2, 3);
        var klass = f.Klass(10); var application = f.Apply(klass, f.Curriculum(old, 1));
        application.EndedAt = DateTime.UtcNow;
        await f.Db.SaveChangesAsync();
        var current = await f.Read(new() { ClassId = klass.ClassId });
        Assert.Empty(current.Nodes);
        Assert.NotNull(current.ResolvedScope!.Message);
        var history = await f.Read(new() { ClassId = klass.ClassId, History = true });
        Assert.Equal("1", Assert.Single(history.Nodes).NodeId);
        Assert.Equal(old.GraphVersionId.ToString("D"), history.ResolvedScope!.GraphVersionId);
        Assert.True(history.ResolvedScope.ReadOnly);
    }

    [Fact]
    public async Task StudentMissingProfile_AndRemovedMembership_CannotReadSubjectOverviewOrOtherGrades()
    {
        using var f = await Fixture.Create();
        f.Version(10, 1); var otherGrade = f.Version(12, 2);
        await f.Db.SaveChangesAsync();
        var student = Guid.NewGuid(); var tenant = f.Tenant(student, UserRole.Student);
        var missing = await f.Read(new() { SubjectId = f.Subject }, tenant);
        Assert.Empty(missing.Nodes);
        Assert.NotEqual("Subject", missing.ResolvedScope!.Kind);
        f.Db.Students.Add(new() { CenterId = f.Center, StudentId = student, FullName = "Student", GradeLevel = 10 });
        await f.Db.SaveChangesAsync();
        Assert.Equal("1", Assert.Single((await f.Read(new() { SubjectId = f.Subject }, tenant)).Nodes).NodeId);
        Assert.Equal(ErrorCodes.ResourceNotFound, (await f.UseCase(tenant).ExecuteAsync(new KnowledgeGraphQuery { GraphVersionId = otherGrade.GraphVersionId })).ErrorCode);
        Assert.Equal(ErrorCodes.ResourceNotFound, (await f.UseCase(tenant).ExecuteAsync(new KnowledgeGraphQuery { SubjectId = f.Subject, GradeLevel = 12 })).ErrorCode);

        var klass = f.Klass(10);
        f.Db.ClassStudents.Add(new() { CenterId = f.Center, ClassId = klass.ClassId, StudentId = student,
            Status = ClassStudentStatus.Removed, JoinedAt = DateTime.UtcNow.AddDays(-20), RemovedAt = DateTime.UtcNow });
        await f.Db.SaveChangesAsync();
        Assert.Empty((await f.Read(new() { SubjectId = f.Subject }, tenant)).Nodes);
        Assert.Equal(ErrorCodes.ResourceNotFound, (await f.UseCase(tenant).ExecuteAsync(new KnowledgeGraphQuery { ClassId = klass.ClassId })).ErrorCode);
    }

    [Fact]
    public async Task DraftVersions_ArePrivate_AndCannotBeUsedAsAnUnscopedStudentHistoryBypass()
    {
        using var f = await Fixture.Create();
        var draft = f.Version(10, 1); draft.Status = "Draft"; draft.IsCurrent = false;
        await f.Db.SaveChangesAsync();
        var other = f.Tenant(Guid.NewGuid(), UserRole.Teacher);
        Assert.Equal(ErrorCodes.ResourceNotFound, (await f.UseCase(other).ExecuteAsync(new KnowledgeGraphQuery { GraphVersionId = draft.GraphVersionId })).ErrorCode);
        Assert.Empty((await f.Service(other).ListAsync(f.Subject, 10, default))!);
        Assert.Equal(ErrorCodes.ResourceNotFound, (await f.Service(other).CreateAsync(new() { SubjectId = f.Subject, GradeLevel = 10,
            SourceGraphVersionId = draft.GraphVersionId }, default)).ErrorCode);
        Assert.Equal("1", Assert.Single((await f.Read(new() { GraphVersionId = draft.GraphVersionId })).Nodes).NodeId);
        Assert.Equal(ErrorCodes.ValidationFailed, (await f.UseCase().ExecuteAsync(new KnowledgeGraphQuery { SubjectId = f.Subject, GradeLevel = 10, History = true })).ErrorCode);
        Assert.Equal(ErrorCodes.ValidationFailed, (await f.UseCase().ExecuteAsync(new KnowledgeGraphQuery { SubjectId = f.Subject, GradeLevel = 10, AcademicYear = "2026-2027" })).ErrorCode);
    }

    [Fact]
    public async Task SharedCurriculum_DoesNotExposeOwnerApplications_AndPublishedLibraryFreezesCanonicalContent()
    {
        using var f = await Fixture.Create();
        var version = f.Version(10, 1, 2);
        var curriculum = f.Curriculum(version, 1); curriculum.Visibility = MaterialVisibility.Shared;
        var ownerClass = f.Klass(10); f.Apply(ownerClass, curriculum);
        await f.Db.SaveChangesAsync();
        var other = f.Tenant(Guid.NewGuid(), UserRole.Teacher);
        var shared = await f.Read(new() { CurriculumId = curriculum.CurriculumId }, other);
        Assert.True(shared.ResolvedScope!.ReadOnly);
        Assert.All(Assert.Single(shared.Nodes).Provenance, p => { Assert.Null(p.ApplicationId); Assert.Null(p.ApplicationRole); });
        Assert.Equal(ErrorCodes.ResourceNotFound, (await f.UseCase(other).ExecuteAsync(new KnowledgeGraphQuery { ClassId = ownerClass.ClassId })).ErrorCode);
        Assert.NotNull(await AcademicDependencyGuards.FrozenNodeMessageAsync(f.Db, f.Center, 2, default));
        Assert.NotNull(await AcademicDependencyGuards.DeactivationMessageAsync(f.Db, f.Center, 2, default));
    }

    [Theory]
    [InlineData("importance")]
    [InlineData("minutes")]
    [InlineData("order")]
    [InlineData("deactivate")]
    public async Task CanonicalNodeMetadata_UsedByPublishedGraph_CannotRewriteFrozenHistory(string field)
    {
        using var f = await Fixture.Create();
        f.Version(10, 2); await f.Db.SaveChangesAsync();
        var request = new UpdateKnowledgeNodeRequest { NodeName = "Topic 2", IsActive = true, RowVersion = "1",
            ExamImportance = 0, EstimatedLearningMinutes = 30 };
        if (field == "importance") request.ExamImportance = 75;
        if (field == "minutes") request.EstimatedLearningMinutes = 90;
        if (field == "order") request.OrderIndex = 4;
        if (field == "deactivate") request.IsActive = false;
        var result = await new UpdateKnowledgeNodeUseCase(f.Db, f.Actor, TimeProvider.System, new KnowledgeNodeHierarchyCycleDetector()).ExecuteAsync("2", request);
        Assert.Equal(ErrorCodes.InvalidStateTransition, result.ErrorCode);
        var node = await f.Db.KnowledgeNodes.SingleAsync(n => n.NodeId == 2);
        Assert.True(node.IsActive); Assert.Equal(0, node.ExamImportance); Assert.Equal(30u, node.EstimatedLearningMinutes); Assert.Equal(0u, node.OrderIndex);
        Assert.Empty(await f.Db.AuthorizationAuditLogs.ToListAsync());
    }

    [Fact]
    public async Task VersionFork_ChangesOnlyDraftTopology_RejectsCyclesAndStaleWrites_AndFreezesAfterPublish()
    {
        using var f = await Fixture.Create();
        var original = f.Version(10, 1, 2);
        f.Db.KnowledgeNodeGrades.Local.Single(n => n.GraphVersionId == original.GraphVersionId && n.NodeId == 2).ParentNodeId = 1;
        f.Db.KnowledgeEdgeGrades.Add(new() { CenterId = f.Center, GraphVersionId = original.GraphVersionId, EdgeId = 1,
            RelationType = RelationType.PrerequisiteOf, Weight = .9m });
        await f.Db.SaveChangesAsync();
        var service = f.Service();
        var created = await service.CreateAsync(new() { SubjectId = f.Subject, GradeLevel = 10, SourceGraphVersionId = original.GraphVersionId }, default);
        Assert.Null(created.ErrorCode);
        var fork = created.Data!; var id = Guid.Parse(fork.GraphVersionId);
        Assert.Equal("1", fork.NodeMappings.Single(n => n.NodeId == "2").ParentNodeId);
        Assert.Equal("1", Assert.Single(fork.EdgeMappings).EdgeId);

        var cycle = await service.UpdateAsync(id, new() { RowVersion = fork.RowVersion,
            NodeMappings = [new() { NodeId = "1", ParentNodeId = "2" }, new() { NodeId = "2", ParentNodeId = "1" }] }, default);
        Assert.Equal(ErrorCodes.ValidationFailed, cycle.ErrorCode);
        var dag = await service.UpdateAsync(id, new() { RowVersion = fork.RowVersion,
            NodeMappings = [new() { NodeId = "1" }, new() { NodeId = "2" }],
            EdgeMappings = [new() { EdgeId = "1" }, new() { EdgeId = "2" }] }, default);
        Assert.Equal(ErrorCodes.ValidationFailed, dag.ErrorCode);
        var saved = await service.UpdateAsync(id, new() { RowVersion = fork.RowVersion,
            NodeMappings = [new() { NodeId = "1" }, new() { NodeId = "2" }],
            EdgeMappings = [new() { EdgeId = "1", RelationType = "RelatedTo", Weight = .3m }] }, default);
        Assert.Null(saved.ErrorCode);
        Assert.Equal(ErrorCodes.ConcurrencyConflict, (await service.UpdateAsync(id, new() { RowVersion = fork.RowVersion }, default)).ErrorCode);
        var published = await service.PublishAsync(id, new() { RowVersion = saved.Data!.RowVersion }, default);
        Assert.Null(published.ErrorCode);
        Assert.False(original.IsCurrent);
        Assert.Equal(ErrorCodes.InvalidStateTransition, (await service.UpdateAsync(id, new() { RowVersion = published.Data!.RowVersion }, default)).ErrorCode);
        var old = await f.Read(new() { GraphVersionId = original.GraphVersionId });
        Assert.Equal("1", old.Nodes.Single(n => n.NodeId == "2").ParentNodeId);
        Assert.Equal(.9m, Assert.Single(old.Edges).Weight);
        var current = await f.Read(new() { SubjectId = f.Subject, GradeLevel = 10 });
        Assert.Null(current.Nodes.Single(n => n.NodeId == "2").ParentNodeId);
        Assert.Equal("RelatedTo", Assert.Single(current.Edges).RelationType);
        Assert.Equal(.3m, Assert.Single(current.Edges).Weight);
    }

    [Fact]
    public async Task EdgeCatalogCreation_RequiresOwnedDraftToBypassActiveClassGuard_AndDoesNotChangeClassTopology()
    {
        using var f = await Fixture.Create();
        var version = f.Version(10, 1, 2, 3); var klass = f.Klass(10);
        f.Apply(klass, f.Curriculum(version, 1, 2, 3));
        var draft = f.Version(10, 1, 2, 3); draft.Status = "Draft"; draft.IsCurrent = false;
        await f.Db.SaveChangesAsync();
        var request = new CreateKnowledgeEdgeRequest { SubjectId = f.Subject, SourceNodeId = "2", TargetNodeId = "3", RelationType = "PrerequisiteOf", Weight = .4m };
        var useCase = new CreateKnowledgeEdgeUseCase(f.Db, f.Actor, TimeProvider.System, new());
        Assert.Equal(ErrorCodes.InvalidStateTransition, (await useCase.ExecuteAsync(request, default)).ErrorCode);
        request.GraphVersionId = version.GraphVersionId;
        Assert.Equal(ErrorCodes.ResourceNotFound, (await useCase.ExecuteAsync(request, default)).ErrorCode);
        request.GraphVersionId = draft.GraphVersionId;
        var created = await useCase.ExecuteAsync(request, default);
        Assert.True(created.IsSuccess, created.Message);
        Assert.Empty((await f.Read(new() { ClassId = klass.ClassId })).Edges);
    }

    [Fact]
    public async Task MappingWrites_AlwaysAdvanceConcurrencyToken_EvenWithIdenticalActorAndTimestamp()
    {
        using var f = await Fixture.Create();
        var service = new KnowledgeGraphVersionService(f.Db, f.Actor, new FixedClock());
        var created = (await service.CreateAsync(new() { SubjectId = f.Subject, GradeLevel = 10 }, default)).Data!;
        var id = Guid.Parse(created.GraphVersionId);
        var first = (await service.UpdateAsync(id, new() { RowVersion = created.RowVersion,
            NodeMappings = [new() { NodeId = "1" }] }, default)).Data!;
        var second = (await service.UpdateAsync(id, new() { RowVersion = first.RowVersion,
            NodeMappings = [new() { NodeId = "2" }] }, default)).Data!;
        Assert.Equal(ulong.Parse(first.RowVersion) + 1, ulong.Parse(second.RowVersion));
        Assert.Equal(ErrorCodes.ConcurrencyConflict, (await service.UpdateAsync(id, new() { RowVersion = first.RowVersion }, default)).ErrorCode);
    }

    private sealed class FixedClock : TimeProvider
    { public override DateTimeOffset GetUtcNow() => new(2026, 10, 23, 0, 0, 0, TimeSpan.Zero); }

    private sealed class Fixture : IDisposable
    {
        public Guid Center { get; } = Guid.NewGuid();
        public Guid Subject { get; } = Guid.NewGuid();
        public Guid Teacher { get; } = Guid.NewGuid();
        public EduTwinDbContext Db { get; private set; } = null!;
        public TenantContext Actor { get; private set; } = null!;
        public static async Task<Fixture> Create()
        {
            var f = new Fixture(); f.Actor = f.Tenant(f.Teacher, UserRole.Teacher);
            f.Db = new(new DbContextOptionsBuilder<EduTwinDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, f.Actor);
            f.Db.SavingChanges += (_, _) =>
            {
                foreach (var entry in f.Db.ChangeTracker.Entries().Where(e => e.State == EntityState.Added))
                    foreach (var property in entry.Properties.Where(p => p.Metadata.ClrType == typeof(DateTime) && (DateTime)p.CurrentValue! == default))
                        property.CurrentValue = DateTime.UtcNow;
            };
            f.Db.Centers.Add(new() { CenterId = f.Center, CenterCode = "TEST", CenterName = "Test", Timezone = "UTC", Status = CenterStatus.Active });
            f.Db.Subjects.Add(new() { CenterId = f.Center, SubjectId = f.Subject, SubjectCode = "MATH", SubjectName = "Math", IsActive = true });
            foreach (ulong id in new ulong[] { 1, 2, 3, 4 }) f.Db.KnowledgeNodes.Add(new() { CenterId = f.Center, SubjectId = f.Subject,
                NodeId = id, NodeCode = $"N{id}", NodeName = $"Topic {id}", NodeType = NodeType.Topic, IsActive = true, EstimatedLearningMinutes = 30 });
            f.Db.KnowledgeEdges.AddRange(new KnowledgeEdge { CenterId = f.Center, SubjectId = f.Subject, EdgeId = 1,
                SourceNodeId = 1, TargetNodeId = 2, RelationType = RelationType.PrerequisiteOf, Weight = 1 },
                new KnowledgeEdge { CenterId = f.Center, SubjectId = f.Subject, EdgeId = 2,
                SourceNodeId = 2, TargetNodeId = 1, RelationType = RelationType.PrerequisiteOf, Weight = 1 });
            await f.Db.SaveChangesAsync(); return f;
        }
        public TenantContext Tenant(Guid actor, UserRole role) { var t = new TenantContext(); t.Initialize(Center, actor, role.ToString(), 1); return t; }
        public KnowledgeGraphVersion Version(byte grade, params ulong[] nodes)
        {
            var v = new KnowledgeGraphVersion { CenterId = Center, GraphVersionId = Guid.NewGuid(), SubjectId = Subject,
                GradeLevel = grade, Status = "Published", IsCurrent = true, CreatedBy = Teacher };
            Db.KnowledgeGraphVersions.Add(v);
            foreach (var id in nodes) Db.KnowledgeNodeGrades.Add(new() { CenterId = Center, GraphVersionId = v.GraphVersionId, NodeId = id });
            return v;
        }
        public Class Klass(byte grade, string year = "2026-2027")
        {
            var c = new Class { CenterId = Center, ClassId = Guid.NewGuid(), SubjectId = Subject, TeacherId = Teacher,
                GradeLevel = grade, ClassName = "A1", AcademicYear = year, Status = ClassStatus.Active };
            Db.Classes.Add(c); return c;
        }
        public Curriculum Curriculum(KnowledgeGraphVersion version, params ulong[] nodes)
        {
            var c = new Curriculum { CenterId = Center, CurriculumId = Guid.NewGuid(), TeacherId = Teacher, SubjectId = Subject,
                Title = $"Curriculum {version.GradeLevel}", GradeLevel = version.GradeLevel, GraphVersionId = version.GraphVersionId, ReviewStatus = ReviewStatus.Published };
            Db.Curriculums.Add(c);
            foreach (var id in nodes) Db.CurriculumNodes.Add(new() { CenterId = Center, CurriculumId = c.CurriculumId, NodeId = id });
            return c;
        }
        public ClassCurriculumApplication Apply(Class klass, Curriculum curriculum, string role = "Primary", bool exception = false)
        {
            var a = new ClassCurriculumApplication { CenterId = Center, ApplicationId = Guid.NewGuid(), ClassId = klass.ClassId,
                CurriculumId = curriculum.CurriculumId, SubjectId = Subject, ApplicationRole = role, StartedAt = DateTime.UtcNow,
                AssignedBy = Teacher, IsGradeException = exception, GradeMismatchReason = exception ? "Ôn nền đã duyệt" : null };
            Db.ClassCurriculumApplications.Add(a); return a;
        }
        public GetKnowledgeGraphUseCase UseCase(TenantContext? actor = null) => new(Db, actor ?? Actor);
        public KnowledgeGraphVersionService Service(TenantContext? actor = null) => new(Db, actor ?? Actor, TimeProvider.System);
        public async Task<KnowledgeGraphDto> Read(KnowledgeGraphQuery query, TenantContext? actor = null)
        { var result = await UseCase(actor).ExecuteAsync(query); Assert.True(result.IsSuccess, result.ErrorCode); return result.Data!; }
        public void Dispose() => Db.Dispose();
    }
}
