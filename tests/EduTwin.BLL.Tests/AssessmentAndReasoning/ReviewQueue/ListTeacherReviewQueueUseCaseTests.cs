using System.Text.Json;
using EduTwin.BLL.AssessmentAndReasoning.ReviewQueue;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.ReviewQueue;

public sealed class ListTeacherReviewQueueUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_OwnedStudents_ReturnsOnlyCurrentUnresolvedEvidence()
    {
        var fixture = await CreateFixtureAsync(UserRole.Teacher);
        await using var context = fixture.Context;
        var sut = new ListTeacherReviewQueueUseCase(
            context,
            fixture.Tenant,
            new StubClassOwnershipGuard(OwnershipDecision.Allowed));

        var result = await sut.ExecuteAsync(new TeacherReviewQueueQuery(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Data!);
        Assert.Equal("1", item.AttemptId);
        Assert.Equal("11", item.AnalysisId);
        Assert.Equal("answer", item.FinalAnswer);
        Assert.False(item.IsFallback);
        Assert.Equal("Teacher review required", item.AnalysisFeedback);
        Assert.Equal(EvidenceTrustLevel.ReviewOnly.ToString(), item.Evidence.TrustLevel);
        Assert.Equal(0m, item.Evidence.ReasoningWeight);
        Assert.True(item.Evidence.RequiresTeacherReview);
        Assert.Equal(1, result.TotalItems);
    }

    [Fact]
    public async Task ExecuteAsync_OtherTeachersClass_ReturnsForbiddenAndPassesToken()
    {
        var fixture = await CreateFixtureAsync(UserRole.Teacher);
        await using var context = fixture.Context;
        var guard = new StubClassOwnershipGuard(OwnershipDecision.Forbidden);
        var sut = new ListTeacherReviewQueueUseCase(context, fixture.Tenant, guard);
        using var cancellation = new CancellationTokenSource();

        var result = await sut.ExecuteAsync(
            new TeacherReviewQueueQuery { ClassId = Guid.NewGuid() },
            cancellation.Token);

        Assert.False(result.IsSuccess);
        Assert.Equal(EduTwin.Contracts.Common.ErrorCodes.ForbiddenResource, result.ErrorCode);
        Assert.Equal(cancellation.Token, guard.Token);
    }

    [Fact]
    public async Task ExecuteAsync_CenterManagerRole_FailsClosed()
    {
        var fixture = await CreateFixtureAsync(UserRole.CenterManager);
        await using var context = fixture.Context;
        var sut = new ListTeacherReviewQueueUseCase(
            context,
            fixture.Tenant,
            new StubClassOwnershipGuard(OwnershipDecision.Allowed));

        var result = await sut.ExecuteAsync(new TeacherReviewQueueQuery(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(EduTwin.Contracts.Common.ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    public async Task ExecuteAsync_InvalidPagination_ReturnsValidationFailed(int page, int pageSize)
    {
        var fixture = await CreateFixtureAsync(UserRole.Teacher);
        await using var context = fixture.Context;
        var sut = new ListTeacherReviewQueueUseCase(
            context,
            fixture.Tenant,
            new StubClassOwnershipGuard(OwnershipDecision.Allowed));

        var result = await sut.ExecuteAsync(
            new TeacherReviewQueueQuery { Page = page, PageSize = pageSize },
            CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(EduTwin.Contracts.Common.ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    private static async Task<Fixture> CreateFixtureAsync(UserRole role)
    {
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var otherTeacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var otherStudentId = Guid.NewGuid();
        var tenant = new TenantContext();
        tenant.Initialize(centerId, teacherId, role.ToString(), 1);
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new EduTwinDbContext(options, tenant);
        var now = new DateTime(2026, 9, 10, 5, 0, 0, DateTimeKind.Utc);

        context.Teachers.AddRange(
            new Teacher { CenterId = centerId, TeacherId = teacherId, CreatedAt = now, UpdatedAt = now },
            new Teacher { CenterId = centerId, TeacherId = otherTeacherId, CreatedAt = now, UpdatedAt = now });
        context.Students.AddRange(
            new Student { CenterId = centerId, StudentId = studentId, FullName = "Owned Student", CreatedAt = now, UpdatedAt = now },
            new Student { CenterId = centerId, StudentId = otherStudentId, FullName = "Other Student", CreatedAt = now, UpdatedAt = now });
        context.Questions.Add(new Question
        {
            CenterId = centerId,
            QuestionId = 1,
            SubjectId = Guid.NewGuid(),
            QuestionText = "Explain the solution",
            CorrectAnswer = "42",
            Solution = "Reasoned solution",
            LanguageCode = "en",
            MaxScore = 10,
            CreatedAt = now,
            UpdatedAt = now
        });

        var ownedClassId = Guid.NewGuid();
        var otherClassId = Guid.NewGuid();
        context.Classes.AddRange(
            new EduTwin.DAL.Organization.Class
            {
                CenterId = centerId,
                ClassId = ownedClassId,
                TeacherId = teacherId,
                ClassName = "Owned Class",
                AcademicYear = "2026-2027",
                CreatedAt = now,
                UpdatedAt = now
            },
            new EduTwin.DAL.Organization.Class
            {
                CenterId = centerId,
                ClassId = otherClassId,
                TeacherId = otherTeacherId,
                ClassName = "Other Class",
                AcademicYear = "2026-2027",
                CreatedAt = now,
                UpdatedAt = now
            });
        context.ClassStudents.AddRange(
            new ClassStudent
            {
                CenterId = centerId,
                ClassId = ownedClassId,
                StudentId = studentId,
                Status = ClassStudentStatus.Active,
                JoinedAt = now
            },
            new ClassStudent
            {
                CenterId = centerId,
                ClassId = otherClassId,
                StudentId = otherStudentId,
                Status = ClassStudentStatus.Active,
                JoinedAt = now
            });

        context.Attempts.AddRange(
            Attempt(1, studentId, now),
            Attempt(2, studentId, now.AddMinutes(1)),
            Attempt(3, otherStudentId, now.AddMinutes(2)),
            Attempt(4, studentId, now.AddMinutes(3)));
        context.ReasoningAnalyses.AddRange(
            Analysis(11, 1, now),
            Analysis(12, 2, now.AddMinutes(1)),
            Analysis(13, 3, now.AddMinutes(2)),
            Analysis(14, 4, now.AddMinutes(3)));
        context.EvidenceAssessments.AddRange(
            Evidence(101, 1, 11, true, null, now),
            Evidence(102, 2, 12, false, null, now.AddMinutes(1)),
            Evidence(103, 3, 13, true, null, now.AddMinutes(2)),
            Evidence(104, 4, 14, true, null, now.AddMinutes(3)),
            Evidence(105, 4, 14, false, 104, now.AddMinutes(4)));

        await context.SaveChangesAsync();
        return new Fixture(context, tenant);

        Attempt Attempt(ulong attemptId, Guid ownerStudentId, DateTime createdAt) => new()
        {
            CenterId = centerId,
            AttemptId = attemptId,
            StudentId = ownerStudentId,
            QuestionId = 1,
            FinalAnswer = "answer",
            ReasoningLanguage = "en",
            ClientSubmissionId = Guid.NewGuid(),
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };

        ReasoningAnalysis Analysis(ulong analysisId, ulong attemptId, DateTime createdAt) => new()
        {
            CenterId = centerId,
            AnalysisId = analysisId,
            AttemptId = attemptId,
            SchemaVersion = "1.0",
            MissingSteps = JsonDocument.Parse("[]"),
            RootCauseNodeIds = JsonDocument.Parse("[]"),
            Feedback = "Teacher review required",
            AnalysisConfidence = 40m,
            OverrideVersion = 0,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };

        EvidenceAssessment Evidence(
            ulong evidenceId,
            ulong attemptId,
            ulong analysisId,
            bool requiresReview,
            ulong? supersedes,
            DateTime evaluatedAt) => new()
        {
            CenterId = centerId,
            EvidenceAssessmentId = evidenceId,
            AttemptId = attemptId,
            AnalysisId = analysisId,
            SupersedesAssessmentId = supersedes,
            SourceType = EvidenceSourceType.AI,
            TrustLevel = requiresReview ? EvidenceTrustLevel.ReviewOnly : EvidenceTrustLevel.Trusted,
            DecisionMode = EvidenceDecisionMode.AIWeighted,
            ReasoningWeight = requiresReview ? 0m : 1m,
            ReasonCodes = JsonDocument.Parse(requiresReview
                ? "[\"AI_CONFIDENCE_BELOW_50\"]"
                : "[\"AI_CONFIDENCE_80_100\"]"),
            RequiresTeacherReview = requiresReview,
            PolicyVersion = "evidence-gate-v1",
            EvaluatedAt = evaluatedAt,
            CreatedAt = evaluatedAt
        };
    }

    private sealed record Fixture(EduTwinDbContext Context, TenantContext Tenant);

    private sealed class StubClassOwnershipGuard : IClassOwnershipGuard
    {
        private readonly OwnershipDecision _decision;

        public StubClassOwnershipGuard(OwnershipDecision decision)
        {
            _decision = decision;
        }

        public CancellationToken Token { get; private set; }

        public Task<OwnershipDecision> CheckClassAccessAsync(
            Guid classId,
            CancellationToken cancellationToken)
        {
            Token = cancellationToken;
            return Task.FromResult(_decision);
        }
    }
}
