using System.Text.Json;
using EduTwin.BLL.AssessmentAndReasoning;
using EduTwin.BLL.AssessmentAndReasoning.Evidence;
using EduTwin.BLL.AssessmentAndReasoning.PreliminaryGrading;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.BLL.Recommendations.Planning;
using EduTwin.BLL.Recommendations.Services;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.Contracts.Recommendations;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

namespace EduTwin.BLL.Tests.Recommendations;

internal sealed class LearningPathV4Fixture : IDisposable
{
    public Guid Center { get; } = Guid.NewGuid();
    public Guid Student { get; } = Guid.NewGuid();
    public Guid English { get; } = Guid.NewGuid();
    public Guid Math { get; } = Guid.NewGuid();
    public EduTwinDbContext Db { get; }
    public Guid EnglishGraphVersionId { get; }
    public LearningPathV4Planner Planner { get; }
    public LearningActivityExecutionService Execution { get; }
    public LearningPathV4Evaluator Evaluator { get; }
    public LearningPathV4Fixture()
    {
        var tenant = new TenantContext(); tenant.Initialize(Center, Student, "Student", 1);
        Db = new EduTwinDbContext(new DbContextOptionsBuilder<EduTwinDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options, tenant);
        var now = DateTime.UtcNow;
        Db.Subjects.AddRange(new Subject { CenterId = Center, SubjectId = English, SubjectCode = "EN", SubjectName = "Tiếng Anh", CreatedAt = now, UpdatedAt = now },
            new Subject { CenterId = Center, SubjectId = Math, SubjectCode = "MATH", SubjectName = "Toán", CreatedAt = now, UpdatedAt = now });
        Db.Users.Add(new User { CenterId = Center, UserId = Student, Username = "test-student", DisplayName = "Test student", PasswordHash = "test-only",
            RoleName = UserRole.Student, Status = UserStatus.Active, CreatedAt = now, UpdatedAt = now });
        Db.Students.Add(new Student { CenterId = Center, StudentId = Student, FullName = "Test student", GradeLevel = 10, CreatedAt = now, UpdatedAt = now });
        Db.KnowledgeNodes.Add(new KnowledgeNode { NodeId = 100, CenterId = Center, SubjectId = English, NodeType = NodeType.Topic, NodeCode = "TENSES", NodeName = "Thì", IsActive = true, CreatedAt = now, UpdatedAt = now });
        foreach (var (id, name, parent, subject) in new[] { (101ul, "Hiện tại đơn", (ulong?)100, English), (102ul, "Quá khứ đơn", (ulong?)100, English),
            (103ul, "Đọc hiểu", (ulong?)null, English), (201ul, "Phương trình", (ulong?)null, Math) })
        {
            Db.KnowledgeNodes.Add(new KnowledgeNode { NodeId = id, CenterId = Center, SubjectId = subject, NodeCode = $"SKILL-{id}", NodeName = name,
                NodeType = NodeType.Skill, ParentNodeId = parent, Description = $"Kiến thức đã biên soạn: {name}", IsActive = true, OrderIndex = (uint)id, CreatedAt = now, UpdatedAt = now });
            for (ulong i = 1; i <= 12; i++) Db.Questions.Add(new Question { QuestionId = id * 100 + i, CenterId = Center, SubjectId = subject,
                PrimaryTopicNodeId = id, GradeLevel = 10, QuestionType = QuestionType.ShortAnswer, AnswerEvaluationMode = QuestionAnswerEvaluationMode.TextExact,
                QuestionText = $"Câu hỏi {name} #{i}", CorrectAnswer = $"answer-{i}", Solution = $"Giải thích đã xuất bản cho {name} #{i}",
                LanguageCode = "vi", Status = QuestionStatus.Active, MaxScore = 10, Difficulty = 1, CreatedAt = now, UpdatedAt = now });
        }
        EnglishGraphVersionId = GraphScopeFixture.Approve(Db, Center, English, 10, 100, 101, 102, 103);
        GraphScopeFixture.Approve(Db, Center, Math, 10, 201);
        Db.SaveChanges();
        Planner = new LearningPathV4Planner(Db, TimeProvider.System);
        Evaluator = new LearningPathV4Evaluator(Db, TimeProvider.System);
        var validator = new AttemptSubmissionValidator(Db, tenant, new PreliminaryGraderFactory(new MultipleChoiceGrader(), new ShortAnswerGrader(), new EssayGrader()));
        var submit = new SubmitAttemptUseCase(Db, validator, TimeProvider.System);
        Execution = new LearningActivityExecutionService(Db, TimeProvider.System, submit,
            new LearningSessionExecutionService(Db, NullLogger<LearningSessionExecutionService>.Instance));
    }

    public void AddPrerequisite(ulong source, ulong target)
    {
        var edge = new KnowledgeEdge { CenterId = Center, SubjectId = English, SourceNodeId = source,
            TargetNodeId = target, RelationType = RelationType.PrerequisiteOf, Weight = 1,
            CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        Db.KnowledgeEdges.Add(edge);
        Db.KnowledgeEdgeGrades.Add(new KnowledgeEdgeGrade { CenterId = Center, GraphVersionId = EnglishGraphVersionId,
            EdgeId = edge.EdgeId, RelationType = RelationType.PrerequisiteOf, Weight = 1, CreatedAt = DateTime.UtcNow });
    }

    public async Task<PathProposalDto> Proposal(params string[] skills)
    {
        var goal = await Planner.CreateOrUpdateGoalAsync(Center, Student, new CreateLearningGoalRequest { SubjectId = English,
            Title = "Mục tiêu tiếng Anh", TargetSkillIds = skills.ToList(), AvailableMinutesPerDay = 15, AvailableDaysPerWeek = 5 });
        return await Planner.GeneratePathProposalAsync(Center, Student, new GeneratePathProposalRequest { GoalId = goal.GoalId, SubjectId = English });
    }
    public async Task<ProposedMissionDto> Activate(string type, params string[] skills)
    {
        var proposal = await Proposal(skills);
        await Planner.AcceptPathProposalAsync(Center, Student, proposal.ProposalId);
        return proposal.Milestones.SelectMany(m => m.Missions).First(m => m.Purpose == type);
    }
    public async Task<Attempt> RecordEvidence(ulong topic, ulong index, bool correct = true, bool review = false, bool postFeedback = false)
    {
        var now = DateTime.UtcNow;
        var attempt = new Attempt { CenterId = Center, StudentId = Student, QuestionId = topic * 100 + index, FinalAnswer = "fixture",
            ReasoningLanguage = "vi", IsCorrect = correct, AwardedScore = correct ? 10 : 0, Status = AttemptStatus.Completed,
            ClientSubmissionId = Guid.NewGuid(), IsPostFeedback = postFeedback, CreatedAt = now, UpdatedAt = now };
        Db.Attempts.Add(attempt); await Db.SaveChangesAsync();
        await Assess(attempt, review); return attempt;
    }
    public async Task Assess(Attempt attempt, bool review = false)
    {
        var decision = new EvidenceGate().Evaluate(new EvidenceGateInput(EvidenceSourceType.AI, true, true, false, false, true,
            attempt.IsCorrect, review ? 20 : 90, 0));
        Db.EvidenceAssessments.Add(new EvidenceAssessment { CenterId = Center, AttemptId = attempt.AttemptId, SourceType = decision.SourceType,
            TrustLevel = decision.TrustLevel, DecisionMode = decision.DecisionMode, ReasoningWeight = decision.ReasoningWeight,
            RequiresTeacherReview = decision.RequiresTeacherReview, PolicyVersion = decision.PolicyVersion, ReasonCodes = JsonSerializer.SerializeToDocument(decision.ReasonCodes),
            CreatedAt = DateTime.UtcNow, EvaluatedAt = DateTime.UtcNow });
        await Db.SaveChangesAsync();
    }
    public void Dispose() => Db.Dispose();
}
