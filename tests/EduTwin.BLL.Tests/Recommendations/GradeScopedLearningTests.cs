using EduTwin.BLL.DigitalTwin;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Organization;
using EduTwin.BLL.Recommendations;
using EduTwin.BLL.Recommendations.Planning;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.DigitalTwin;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace EduTwin.BLL.Tests.Recommendations;

public sealed class GradeScopedLearningTests : IDisposable
{
    private readonly Guid center = Guid.NewGuid(), subject = Guid.NewGuid(), student = Guid.NewGuid(), teacher = Guid.NewGuid();
    private readonly EduTwinDbContext db;
    public GradeScopedLearningTests()
    {
        var tenant = new TenantContext(); tenant.Initialize(center, student, "Student", 1);
        db = new(new DbContextOptionsBuilder<EduTwinDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options, tenant);
        db.Students.Add(new Student { CenterId = center, StudentId = student, FullName = "Scope student", GradeLevel = 12, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        db.Subjects.Add(new Subject { CenterId = center, SubjectId = subject, SubjectCode = "SCOPE", SubjectName = "Scope", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        for (ulong id = 1; id <= 3; id++) db.KnowledgeNodes.Add(new KnowledgeNode { CenterId = center, SubjectId = subject,
            NodeId = id, NodeCode = $"NODE-{id}", NodeName = $"Node {id}", NodeType = NodeType.Topic, IsActive = true,
            Description = "Authored study material", ExamImportance = 1, OrderIndex = (uint)id, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        db.SaveChanges();
    }
    public void Dispose() => db.Dispose();

    [Fact]
    public async Task IndependentPractice_UsesStudentGrade_RejectsOtherGradeQuestionsForSharedIdentity()
    {
        GraphScopeFixture.Approve(db, center, subject, 12, 1);
        GraphScopeFixture.Approve(db, center, subject, 11, 1, 2);
        db.Questions.AddRange(Question(10, 1, 11), Question(20, 1, 12));
        await db.SaveChangesAsync();
        var scope = await StudentLearningScope.ResolveAsync(db, center, student, subject, null, false, default);
        Assert.Equal(new ulong[] { 1 }, scope.TopicIds!.Order());
        Assert.False(scope.IncludesQuestion(1, 11)); Assert.False(scope.IncludesQuestion(1, null));
        Assert.True(scope.IncludesQuestion(1, 12));
        Assert.Equal(20ul, (await new AdaptiveQuestionSelector(db).SelectQuestionAsync(center, student, 1, 0, default))!.QuestionId);
        Assert.Equal(20ul, Assert.Single(await LearningPathV4Data.QuestionsAsync(db, center, student, subject, [1ul], null, default)).QuestionId);
    }

    [Fact]
    public async Task MissingApprovedLibraryAndPlanningOnlyClass_BothFailClosed()
    {
        var unknown = await StudentLearningScope.ResolveAsync(db, center, student, subject, null, false, default);
        Assert.False(unknown.Allowed); Assert.Empty(unknown.TopicIds!);
        var version = GraphScopeFixture.Approve(db, center, subject, 12, 1);
        var cls = AddClass(); var curriculum = AddCurriculum(version, 12, 1);
        db.CurriculumClasses.Add(new CurriculumClass { CenterId = center, ClassId = cls, CurriculumId = curriculum, AssignedBy = teacher, AssignedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var planned = await StudentLearningScope.ResolveAsync(db, center, student, subject, cls, false, default);
        Assert.False(planned.Allowed); Assert.Empty(planned.TopicIds!);
    }

    [Fact]
    public async Task ClassUsesPinnedLegacyComposition_AndSupplementalExplicitGradeException()
    {
        var legacy = GraphScopeFixture.Approve(db, center, subject, 12, 1);
        var legacyVersion = db.KnowledgeGraphVersions.Local.Single(v => v.GraphVersionId == legacy);
        legacyVersion.IsLegacy = true; legacyVersion.IsCurrent = false;
        GraphScopeFixture.Approve(db, center, subject, 12, 3);
        var extra = GraphScopeFixture.Approve(db, center, subject, 11, 1, 2);
        var cls = AddClass(); Apply(cls, AddCurriculum(legacy, 12, 1, 3), "Primary");
        Apply(cls, AddCurriculum(extra, 11, 1, 2), "Supplemental", exception: true);
        await db.SaveChangesAsync();
        var scope = await StudentLearningScope.ResolveAsync(db, center, student, subject, cls, false, default);
        Assert.Equal(new ulong[] { 1, 2 }, scope.TopicIds!.Order());
        Assert.True(scope.IncludesQuestion(1, 12)); Assert.True(scope.IncludesQuestion(1, 11));
        Assert.True(scope.IncludesQuestion(2, 11)); Assert.False(scope.IncludesQuestion(2, 12));
        Assert.False(scope.Includes(3));
    }

    [Fact]
    public async Task ExternalPrerequisite_UsesRecordedMastery_AndIgnoresUnscopedCanonicalEdges()
    {
        var lower = GraphScopeFixture.Approve(db, center, subject, 11, 1);
        var upper = GraphScopeFixture.Approve(db, center, subject, 12, 2);
        db.KnowledgeEdges.AddRange(Edge(10, 1, 2), Edge(20, 3, 2));
        db.KnowledgeEdgeGrades.Add(new KnowledgeEdgeGrade { CenterId = center, GraphVersionId = upper,
            EdgeId = 10, RelationType = RelationType.PrerequisiteOf, SourceGraphVersionId = lower, Weight = 1, CreatedAt = DateTime.UtcNow });
        db.KnowledgeTwins.Add(Twin(1, 80));
        db.Questions.Add(Question(20, 2, 12)); await db.SaveChangesAsync();
        var map = await LearningPathV4Data.MapAsync(db, center, student, subject, null, DateTime.UtcNow, default);
        var target = Assert.Single(map.Skills); Assert.Equal("2", target.SkillId);
        Assert.Equal(new[] { "1" }, target.PrerequisiteSkillIds); Assert.Equal(80m, map.ExternalPrerequisiteMastery["1"]);
        Assert.Equal("Available", target.Availability);
        var candidate = Assert.Single((await new OpportunityCandidateBuilder(db).BuildCandidatesAsync(center, student, subject, default)).EligibleCandidates);
        Assert.Equal(new[] { 80m }, candidate.PrerequisiteMasteries);
    }

    [Fact]
    public async Task EmptyVersionRelations_DoNotUseCanonicalEdgesAddedLater()
    {
        GraphScopeFixture.Approve(db, center, subject, 12, 1, 2);
        db.KnowledgeEdges.Add(Edge(10, 1, 2));
        await db.SaveChangesAsync();
        var map = await LearningPathV4Data.MapAsync(db, center, student, subject, null, DateTime.UtcNow, default);
        Assert.All(map.Skills, skill => Assert.Empty(skill.PrerequisiteSkillIds));
        var candidates = await new OpportunityCandidateBuilder(db).BuildCandidatesAsync(center, student, subject, default);
        Assert.Equal(new ulong[] { 1, 2 }, candidates.EligibleCandidates.Select(c => c.TopicNodeId).Order());
    }

    [Fact]
    public async Task DifferentVersionParents_KeepPrerequisitesForEachSelectedPlacement()
    {
        db.KnowledgeNodes.Add(new KnowledgeNode { CenterId = center, SubjectId = subject, NodeId = 4,
            NodeCode = "SHARED-LEAF", NodeName = "Shared skill", NodeType = NodeType.Skill,
            IsActive = true, Description = "Study material", CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        var a = GraphScopeFixture.Approve(db, center, subject, 12, 1, 2, 3, 4);
        var b = GraphScopeFixture.Approve(db, center, subject, 12, 1, 2, 3, 4);
        var ordered = new[] { a, b }.Order().ToArray();
        db.KnowledgeGraphVersions.Local.Single(v => v.GraphVersionId == ordered[1]).IsCurrent = false;
        db.KnowledgeNodeGrades.Local.Single(m => m.GraphVersionId == ordered[0] && m.NodeId == 4).ParentNodeId = 1;
        db.KnowledgeNodeGrades.Local.Single(m => m.GraphVersionId == ordered[1] && m.NodeId == 4).ParentNodeId = 2;
        var cls = AddClass(); Apply(cls, AddCurriculum(ordered[0], 12, 1, 4), "Primary");
        Apply(cls, AddCurriculum(ordered[1], 12, 2, 3, 4), "Supplemental");
        db.KnowledgeEdges.Add(Edge(10, 3, 2));
        db.KnowledgeEdgeGrades.Add(new KnowledgeEdgeGrade { CenterId = center, GraphVersionId = ordered[1],
            EdgeId = 10, RelationType = RelationType.PrerequisiteOf, Weight = 1, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        var map = await LearningPathV4Data.MapAsync(db, center, student, subject, cls, DateTime.UtcNow, default);
        Assert.Equal(new[] { "3" }, map.Skills.Single(s => s.SkillId == "4").PrerequisiteSkillIds);
        Assert.Equal("PrerequisiteBlocked", map.Skills.Single(s => s.SkillId == "4").Availability);
    }

    [Fact]
    public async Task GoalRisk_UsesDistinctCurrentUnion_NotWholeSubject()
    {
        var version = GraphScopeFixture.Approve(db, center, subject, 12, 1, 2);
        var cls = AddClass(); Apply(cls, AddCurriculum(version, 12, 1), "Primary");
        Apply(cls, AddCurriculum(version, 12, 1, 2), "Supplemental");
        db.KnowledgeTwins.AddRange(Twin(1, 100), Twin(2, 0), Twin(3, 100));
        db.StudentSubjectGoals.Add(new StudentSubjectGoal { CenterId = center, StudentId = student, SubjectId = subject, TargetScore = 8, RemainingDays = 90, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        Assert.Equal(5m, (await new StudentGoalRiskUpdater(db).UpdateAsync(center, student, subject, DateTime.UtcNow, default))!.CurrentPredictedScore);
    }

    private Guid AddClass()
    {
        var id = Guid.NewGuid(); db.Classes.Add(new Class { CenterId = center, ClassId = id, TeacherId = teacher, SubjectId = subject,
            ClassName = "Grade 12", AcademicYear = "2026-2027", GradeLevel = 12, Status = ClassStatus.Active, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        db.ClassStudents.Add(new ClassStudent { CenterId = center, ClassId = id, StudentId = student, Status = ClassStudentStatus.Active, JoinedAt = DateTime.UtcNow }); return id;
    }
    private Guid AddCurriculum(Guid version, byte grade, params ulong[] nodes)
    {
        var id = Guid.NewGuid(); db.Curriculums.Add(new Curriculum { CenterId = center, CurriculumId = id, TeacherId = teacher,
            SubjectId = subject, GradeLevel = grade, GraphVersionId = version, Title = "Pinned", ReviewStatus = ReviewStatus.Published, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        for (var index = 0; index < nodes.Length; index++) db.CurriculumNodes.Add(new CurriculumNode { CenterId = center,
            CurriculumId = id, NodeId = nodes[index], OrderIndex = (uint)(index + 1), CreatedAt = DateTime.UtcNow }); return id;
    }
    private void Apply(Guid cls, Guid curriculum, string role, bool exception = false) => db.ClassCurriculumApplications.Add(new ClassCurriculumApplication {
        ApplicationId = Guid.NewGuid(), CenterId = center, ClassId = cls, CurriculumId = curriculum, SubjectId = subject, AssignedBy = teacher,
        StartedAt = DateTime.UtcNow, ApplicationRole = role, IsGradeException = exception, GradeMismatchReason = exception ? "Ôn tập kiến thức nền tảng" : null });
    private Question Question(ulong id, ulong node, byte grade) => new() { CenterId = center, SubjectId = subject, QuestionId = id,
        PrimaryTopicNodeId = node, GradeLevel = grade, Status = QuestionStatus.Active, QuestionType = QuestionType.ShortAnswer,
        AnswerEvaluationMode = QuestionAnswerEvaluationMode.TextExact, QuestionText = "Question", CorrectAnswer = "A", Solution = "Solution", MaxScore = 10, LanguageCode = "vi", Difficulty = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
    private KnowledgeEdge Edge(ulong id, ulong source, ulong target) => new() { CenterId = center, SubjectId = subject, EdgeId = id,
        SourceNodeId = source, TargetNodeId = target, RelationType = RelationType.PrerequisiteOf, Weight = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
    private KnowledgeTwin Twin(ulong node, decimal mastery) => new() { CenterId = center, StudentId = student, SubjectId = subject,
        TopicNodeId = node, KnowledgeTwinId = node, MasteryPercentage = mastery, EvidenceCount = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
}
