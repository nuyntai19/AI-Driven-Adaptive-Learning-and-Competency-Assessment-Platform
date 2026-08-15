using EduTwin.BLL.AssessmentAndReasoning.AttemptSummaries;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Assignments;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.AttemptSummaries;

public sealed class ListAttemptsUseCaseTests
{
    private static readonly DateTime BaseTime =
        new(2026, 8, 15, 10, 20, 30, DateTimeKind.Utc);

    [Theory]
    [MemberData(nameof(InvalidActorContexts))]
    public async Task ExecuteAsync_InvalidActorContext_FailsClosedWithoutOwnership(
        TestActorContext actor)
    {
        using var fixture = CreateFixture(actor);

        var result = await fixture.UseCase.ExecuteAsync(new(), CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
        fixture.Ownership.VerifyNoOtherCalls();
    }

    [Theory]
    [MemberData(nameof(PresentInvalidValues))]
    public async Task ExecuteAsync_EmptyOrLiteralNullForAnyQueryField_IsValidationFailed(
        string propertyName,
        string value)
    {
        using var fixture = CreateFixture(UserRole.CenterManager);
        var query = new ListAttemptsQuery();
        typeof(ListAttemptsQuery).GetProperty(propertyName)!.SetValue(query, value);

        var result = await fixture.UseCase.ExecuteAsync(query, CancellationToken.None);

        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        fixture.Ownership.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(nameof(ListAttemptsQuery.StudentId), "{baf68743-a272-4983-a9e2-41663734a7c2}")]
    [InlineData(nameof(ListAttemptsQuery.SubjectId), "baf68743a2724983a9e241663734a7c2")]
    [InlineData(nameof(ListAttemptsQuery.AssignmentId), "(baf68743-a272-4983-a9e2-41663734a7c2)")]
    [InlineData(nameof(ListAttemptsQuery.QuestionId), "0")]
    [InlineData(nameof(ListAttemptsQuery.QuestionId), "+1")]
    [InlineData(nameof(ListAttemptsQuery.QuestionId), " 1")]
    [InlineData(nameof(ListAttemptsQuery.QuestionId), "1.0")]
    [InlineData(nameof(ListAttemptsQuery.QuestionId), "1e3")]
    [InlineData(nameof(ListAttemptsQuery.QuestionId), "18446744073709551616")]
    [InlineData(nameof(ListAttemptsQuery.QuestionId), "١٢")]
    [InlineData(nameof(ListAttemptsQuery.Status), "completed")]
    [InlineData(nameof(ListAttemptsQuery.Status), "0")]
    [InlineData(nameof(ListAttemptsQuery.Status), "Unknown")]
    [InlineData(nameof(ListAttemptsQuery.From), "2026-08-15T10:20:30")]
    [InlineData(nameof(ListAttemptsQuery.From), "2026-08-15")]
    [InlineData(nameof(ListAttemptsQuery.From), "not-a-date")]
    [InlineData(nameof(ListAttemptsQuery.Page), "0")]
    [InlineData(nameof(ListAttemptsQuery.Page), "+1")]
    [InlineData(nameof(ListAttemptsQuery.Page), "2147483648")]
    [InlineData(nameof(ListAttemptsQuery.PageSize), "0")]
    [InlineData(nameof(ListAttemptsQuery.PageSize), "101")]
    [InlineData(nameof(ListAttemptsQuery.PageSize), "1.5")]
    public async Task ExecuteAsync_MalformedQuery_IsValidationFailed(
        string propertyName,
        string value)
    {
        using var fixture = CreateFixture(UserRole.CenterManager);
        var query = new ListAttemptsQuery();
        typeof(ListAttemptsQuery).GetProperty(propertyName)!.SetValue(query, value);

        var result = await fixture.UseCase.ExecuteAsync(query, CancellationToken.None);

        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        fixture.Ownership.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExecuteAsync_FromAfterTo_IsValidationFailed()
    {
        using var fixture = CreateFixture(UserRole.CenterManager);

        var result = await fixture.UseCase.ExecuteAsync(
            new ListAttemptsQuery
            {
                From = "2026-08-15T10:20:31Z",
                To = "2026-08-15T10:20:30Z"
            },
            CancellationToken.None);

        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_UlongMaxQuestionId_IsValidSyntaxAndReachesResourceLookup()
    {
        using var fixture = CreateFixture(UserRole.CenterManager);

        var result = await fixture.UseCase.ExecuteAsync(
            new ListAttemptsQuery { QuestionId = ulong.MaxValue.ToString() },
            CancellationToken.None);

        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_OmittedFiltersAndPagination_ReturnsNormalizedDefaults()
    {
        using var fixture = CreateFixture(UserRole.CenterManager);

        var result = await fixture.UseCase.ExecuteAsync(new(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!);
        Assert.Equal(1, result.Page);
        Assert.Equal(20, result.PageSize);
        Assert.Equal(0, result.TotalItems);
        Assert.Equal(0, result.TotalPages);
        fixture.Ownership.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExecuteAsync_StudentOmittedId_IsScopedToCurrentStudent()
    {
        using var fixture = CreateFixture(UserRole.Student);
        fixture.Ownership
            .Setup(guard => guard.CheckStudentAccessAsync(
                fixture.Actor.UserId!.Value,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(OwnershipDecision.Allowed);

        var result = await fixture.UseCase.ExecuteAsync(new(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        fixture.Ownership.Verify(guard => guard.CheckStudentAccessAsync(
            fixture.Actor.UserId!.Value,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(OwnershipDecision.Allowed, null)]
    [InlineData(OwnershipDecision.Forbidden, ErrorCodes.ForbiddenResource)]
    [InlineData(OwnershipDecision.NotFound, ErrorCodes.ResourceNotFound)]
    [InlineData((OwnershipDecision)999, ErrorCodes.ResourceNotFound)]
    public async Task ExecuteAsync_OwnershipDecision_MapsFailClosed(
        OwnershipDecision decision,
        string? expectedError)
    {
        using var fixture = CreateFixture(UserRole.Student);
        var target = Guid.NewGuid();
        fixture.Ownership
            .Setup(guard => guard.CheckStudentAccessAsync(
                target,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(decision);

        var result = await fixture.UseCase.ExecuteAsync(
            new ListAttemptsQuery { StudentId = target.ToString("D") },
            CancellationToken.None);

        Assert.Equal(expectedError is null, result.IsSuccess);
        Assert.Equal(expectedError, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_TeacherRequiresStudentId()
    {
        using var fixture = CreateFixture(UserRole.Teacher);

        var result = await fixture.UseCase.ExecuteAsync(new(), CancellationToken.None);

        Assert.Equal(ErrorCodes.ValidationFailed, result.ErrorCode);
        fixture.Ownership.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ExecuteAsync_PassesExactCancellationTokenToOwnershipGuard()
    {
        using var fixture = CreateFixture(UserRole.Teacher);
        var studentId = Guid.NewGuid();
        using var cancellation = new CancellationTokenSource();
        fixture.Ownership
            .Setup(guard => guard.CheckStudentAccessAsync(studentId, cancellation.Token))
            .ReturnsAsync(OwnershipDecision.Allowed);

        await fixture.UseCase.ExecuteAsync(
            new ListAttemptsQuery { StudentId = studentId.ToString("D") },
            cancellation.Token);

        fixture.Ownership.Verify(guard => guard.CheckStudentAccessAsync(
            studentId,
            cancellation.Token), Times.Once);
    }

    [Theory]
    [InlineData(true, ClassStatus.Active, ClassStudentStatus.Active, true)]
    [InlineData(false, ClassStatus.Active, ClassStudentStatus.Active, false)]
    [InlineData(true, ClassStatus.Archived, ClassStudentStatus.Active, false)]
    [InlineData(true, ClassStatus.Active, ClassStudentStatus.Removed, false)]
    public async Task ExecuteAsync_TeacherOwnership_RequiresActiveOwnClassMembership(
        bool ownTeacher,
        ClassStatus classStatus,
        ClassStudentStatus membershipStatus,
        bool expectedAllowed)
    {
        using var fixture = CreateFixture(UserRole.Teacher, useRealOwnership: true);
        var studentId = Guid.NewGuid();
        await SeedStudentAsync(fixture.Context, fixture.Actor.CenterId!.Value, studentId);
        await SeedMembershipAsync(
            fixture.Context,
            studentId,
            ownTeacher ? fixture.Actor.UserId!.Value : Guid.NewGuid(),
            classStatus,
            membershipStatus);

        var result = await fixture.UseCase.ExecuteAsync(
            new ListAttemptsQuery { StudentId = studentId.ToString("D") },
            CancellationToken.None);

        Assert.Equal(expectedAllowed, result.IsSuccess);
        Assert.Equal(
            expectedAllowed ? null : ErrorCodes.ForbiddenResource,
            result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_StudentSameTenantOtherIsForbiddenAndCrossTenantIsNotFound()
    {
        using var fixture = CreateFixture(UserRole.Student, useRealOwnership: true);
        var sameCenterStudent = Guid.NewGuid();
        var crossCenterStudent = Guid.NewGuid();
        await SeedStudentAsync(fixture.Context, fixture.Actor.CenterId!.Value, sameCenterStudent);
        await SeedStudentAsync(fixture.Context, Guid.NewGuid(), crossCenterStudent);

        var forbidden = await fixture.UseCase.ExecuteAsync(
            new ListAttemptsQuery { StudentId = sameCenterStudent.ToString("D") },
            CancellationToken.None);
        var notFound = await fixture.UseCase.ExecuteAsync(
            new ListAttemptsQuery { StudentId = crossCenterStudent.ToString("D") },
            CancellationToken.None);

        Assert.Equal(ErrorCodes.ForbiddenResource, forbidden.ErrorCode);
        Assert.Equal(ErrorCodes.ResourceNotFound, notFound.ErrorCode);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExecuteAsync_StudentOwnScopeIsAllowedWithOmittedOrSuppliedId(bool supplied)
    {
        using var fixture = CreateFixture(UserRole.Student, useRealOwnership: true);
        var ownStudentId = fixture.Actor.UserId!.Value;
        await SeedGraphAsync(fixture, studentId: ownStudentId);
        var query = new ListAttemptsQuery
        {
            StudentId = supplied ? ownStudentId.ToString("D") : null
        };

        var result = await fixture.UseCase.ExecuteAsync(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            ownStudentId.ToString("D").ToLowerInvariant(),
            Assert.Single(result.Data!).StudentId);
    }

    [Fact]
    public async Task ExecuteAsync_TeacherCrossTenantStudentIsNotFound()
    {
        using var fixture = CreateFixture(UserRole.Teacher, useRealOwnership: true);
        var crossTenantStudent = Guid.NewGuid();
        await SeedStudentAsync(fixture.Context, Guid.NewGuid(), crossTenantStudent);

        var result = await fixture.UseCase.ExecuteAsync(
            new ListAttemptsQuery { StudentId = crossTenantStudent.ToString("D") },
            CancellationToken.None);

        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Fact]
    public async Task ExecuteAsync_CenterManagerOmittedStudent_ReturnsOnlyCurrentCenterAttempts()
    {
        using var fixture = CreateFixture(UserRole.CenterManager);
        await SeedGraphAsync(fixture, attemptId: 12001, jobId: 13001);
        await SeedGraphAsync(
            fixture,
            centerId: Guid.NewGuid(),
            studentId: Guid.NewGuid(),
            subjectId: Guid.NewGuid(),
            questionId: 9100,
            attemptId: 12002,
            jobId: 13002);

        var result = await fixture.UseCase.ExecuteAsync(new(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        var item = Assert.Single(result.Data!);
        Assert.Equal("12001", item.AttemptId);
        Assert.Equal(1, result.TotalItems);
    }

    [Fact]
    public async Task ExecuteAsync_CenterManagerSuppliedStudent_UsesOwnershipGuard()
    {
        using var fixture = CreateFixture(UserRole.CenterManager);
        var studentId = Guid.NewGuid();
        fixture.Ownership
            .Setup(guard => guard.CheckStudentAccessAsync(
                studentId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(OwnershipDecision.Allowed);

        var result = await fixture.UseCase.ExecuteAsync(
            new ListAttemptsQuery { StudentId = studentId.ToString("D").ToUpperInvariant() },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public async Task ExecuteAsync_CenterManagerSuppliedCurrentTenantStudent_IsAllowed()
    {
        using var fixture = CreateFixture(UserRole.CenterManager, useRealOwnership: true);
        await SeedGraphAsync(fixture);

        var result = await fixture.UseCase.ExecuteAsync(
            new ListAttemptsQuery { StudentId = fixture.StudentId.ToString("D") },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            fixture.StudentId.ToString("D").ToLowerInvariant(),
            Assert.Single(result.Data!).StudentId);
    }

    [Theory]
    [InlineData("subject", false)]
    [InlineData("subject", true)]
    [InlineData("question", false)]
    [InlineData("question", true)]
    [InlineData("assignment", false)]
    [InlineData("assignment", true)]
    public async Task ExecuteAsync_MissingOrCrossTenantResourceFilter_IsNotFound(
        string kind,
        bool seedCrossTenantResource)
    {
        using var fixture = CreateFixture(UserRole.CenterManager);
        var otherCenter = Guid.NewGuid();
        var query = new ListAttemptsQuery();
        switch (kind)
        {
            case "subject":
                var subjectId = Guid.NewGuid();
                if (seedCrossTenantResource)
                {
                    fixture.Context.Subjects.Add(CreateSubject(otherCenter, subjectId));
                }
                query.SubjectId = subjectId.ToString("D");
                break;
            case "question":
                var questionId = 9999ul;
                if (seedCrossTenantResource)
                {
                    fixture.Context.Questions.Add(CreateQuestion(
                        otherCenter,
                        Guid.NewGuid(),
                        questionId));
                }
                query.QuestionId = questionId.ToString();
                break;
            case "assignment":
                var assignmentId = Guid.NewGuid();
                if (seedCrossTenantResource)
                {
                    fixture.Context.Assignments.Add(CreateAssignment(otherCenter, assignmentId));
                }
                query.AssignmentId = assignmentId.ToString("D");
                break;
        }
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();

        var result = await fixture.UseCase.ExecuteAsync(query, CancellationToken.None);

        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }

    [Theory]
    [InlineData("student", "12001")]
    [InlineData("subject", "12001")]
    [InlineData("question", "12001")]
    [InlineData("assignment", "12001")]
    [InlineData("status", "12001")]
    [InlineData("from", "12002")]
    [InlineData("to", "12001")]
    public async Task ExecuteAsync_EachFilterUsesItsLockedPersistedPredicate(
        string filter,
        string expectedAttemptId)
    {
        using var fixture = CreateFixture(UserRole.CenterManager);
        var secondStudent = Guid.NewGuid();
        var secondSubject = Guid.NewGuid();
        var secondAssignment = Guid.NewGuid();
        await SeedGraphAsync(
            fixture,
            assignmentId: fixture.AssignmentId,
            attemptStatus: AttemptStatus.Completed,
            createdAt: BaseTime);
        await SeedGraphAsync(
            fixture,
            studentId: secondStudent,
            subjectId: secondSubject,
            questionId: 9002,
            attemptId: 12002,
            jobId: 13002,
            assignmentId: secondAssignment,
            attemptStatus: AttemptStatus.Processing,
            createdAt: BaseTime.AddSeconds(1));

        var query = filter switch
        {
            "student" => new ListAttemptsQuery
            {
                StudentId = fixture.StudentId.ToString("D")
            },
            "subject" => new ListAttemptsQuery
            {
                SubjectId = fixture.SubjectId.ToString("D")
            },
            "question" => new ListAttemptsQuery
            {
                QuestionId = fixture.QuestionId.ToString()
            },
            "assignment" => new ListAttemptsQuery
            {
                AssignmentId = fixture.AssignmentId.ToString("D")
            },
            "status" => new ListAttemptsQuery
            {
                Status = nameof(AttemptStatus.Completed)
            },
            "from" => new ListAttemptsQuery
            {
                From = "2026-08-15T10:20:31Z"
            },
            "to" => new ListAttemptsQuery
            {
                To = "2026-08-15T10:20:30Z"
            },
            _ => throw new InvalidOperationException()
        };
        if (filter == "student")
        {
            fixture.Ownership
                .Setup(guard => guard.CheckStudentAccessAsync(
                    fixture.StudentId,
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(OwnershipDecision.Allowed);
        }

        var result = await fixture.UseCase.ExecuteAsync(query, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(expectedAttemptId, Assert.Single(result.Data!).AttemptId);
    }

    [Fact]
    public async Task ExecuteAsync_AllResourceFiltersExistButDoNotMatch_ReturnsEmpty()
    {
        using var fixture = CreateFixture(UserRole.CenterManager);
        await SeedGraphAsync(fixture);
        var extraQuestion = CreateQuestion(
            fixture.Actor.CenterId!.Value,
            fixture.SubjectId,
            9002);
        fixture.Context.Questions.Add(extraQuestion);
        await fixture.Context.SaveChangesAsync();
        fixture.Context.ChangeTracker.Clear();

        var result = await fixture.UseCase.ExecuteAsync(
            new ListAttemptsQuery { QuestionId = "9002" },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!);
    }

    [Fact]
    public async Task ExecuteAsync_TeacherAllowedStudent_DoesNotRequireResourceOwnership()
    {
        using var fixture = CreateFixture(UserRole.Teacher, useRealOwnership: true);
        await SeedGraphAsync(
            fixture,
            studentId: fixture.StudentId,
            createdByTeacherId: Guid.NewGuid(),
            assignmentId: fixture.AssignmentId);
        await SeedMembershipAsync(
            fixture.Context,
            fixture.StudentId,
            fixture.Actor.UserId!.Value,
            ClassStatus.Active,
            ClassStudentStatus.Active);

        var result = await fixture.UseCase.ExecuteAsync(
            new ListAttemptsQuery
            {
                StudentId = fixture.StudentId.ToString("D"),
                SubjectId = fixture.SubjectId.ToString("D"),
                QuestionId = fixture.QuestionId.ToString(),
                AssignmentId = fixture.AssignmentId.ToString("D")
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!);
    }

    [Fact]
    public async Task ExecuteAsync_OmittedAssignmentFilter_ReturnsFreePracticeAttempt()
    {
        using var fixture = CreateFixture(UserRole.CenterManager);
        await SeedGraphAsync(fixture, assignmentId: null);

        var result = await fixture.UseCase.ExecuteAsync(new(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Null(Assert.Single(result.Data!).AssignmentId);
    }

    [Fact]
    public async Task ExecuteAsync_AllFiltersUseAndAndDateBoundsAreInclusive()
    {
        using var fixture = CreateFixture(UserRole.CenterManager);
        await SeedGraphAsync(
            fixture,
            assignmentId: fixture.AssignmentId,
            attemptStatus: AttemptStatus.Completed,
            createdAt: BaseTime);
        await SeedGraphAsync(
            fixture,
            studentId: Guid.NewGuid(),
            attemptId: 12002,
            jobId: 13002,
            assignmentId: null,
            attemptStatus: AttemptStatus.Processing,
            createdAt: BaseTime.AddSeconds(1));

        var result = await fixture.UseCase.ExecuteAsync(
            new ListAttemptsQuery
            {
                SubjectId = fixture.SubjectId.ToString("D"),
                QuestionId = fixture.QuestionId.ToString(),
                AssignmentId = fixture.AssignmentId.ToString("D"),
                Status = nameof(AttemptStatus.Completed),
                From = "2026-08-15T17:20:30+07:00",
                To = "2026-08-15T10:20:30Z"
            },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal("12001", Assert.Single(result.Data!).AttemptId);
    }

    [Fact]
    public async Task ExecuteAsync_UnknownQueryHasNoRepresentationAndCannotAffectResult()
    {
        using var fixture = CreateFixture(UserRole.CenterManager);
        await SeedGraphAsync(fixture);

        var result = await fixture.UseCase.ExecuteAsync(new(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Data!);
        Assert.DoesNotContain(
            typeof(ListAttemptsQuery).GetProperties(),
            property => property.Name.Equals("unknown", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData(AIJobStatus.Pending, false)]
    [InlineData(AIJobStatus.Processing, false)]
    [InlineData(AIJobStatus.Completed, true)]
    [InlineData(AIJobStatus.FallbackCompleted, true)]
    [InlineData(AIJobStatus.FailedTerminal, true)]
    public async Task ExecuteAsync_MapsEveryJobStatus(AIJobStatus status, bool terminal)
    {
        using var fixture = CreateFixture(UserRole.CenterManager);
        await SeedGraphAsync(fixture, jobStatus: status);

        var result = await fixture.UseCase.ExecuteAsync(new(), CancellationToken.None);

        var item = Assert.Single(result.Data!);
        Assert.Equal(status.ToString(), item.JobStatus);
        Assert.Equal(terminal, item.Terminal);
        Assert.Equal("/api/v1/learning/analysis-jobs/13001", item.PollUrl);
    }

    [Fact]
    public async Task ExecuteAsync_ProjectionHasCanonicalValuesAndNullableGrading()
    {
        using var fixture = CreateFixture(UserRole.CenterManager);
        await SeedGraphAsync(
            fixture,
            questionId: 9_007_199_254_740_993,
            attemptId: 9_007_199_254_740_994,
            jobId: 9_007_199_254_740_995,
            assignmentId: fixture.AssignmentId,
            isCorrect: null,
            awardedScore: null);

        var result = await fixture.UseCase.ExecuteAsync(
            new ListAttemptsQuery { QuestionId = "9007199254740993" },
            CancellationToken.None);

        var item = Assert.Single(result.Data!);
        Assert.Equal("9007199254740994", item.AttemptId);
        Assert.Equal("9007199254740993", item.QuestionId);
        Assert.Equal("9007199254740995", item.AnalysisJobId);
        Assert.Equal(fixture.StudentId.ToString("D").ToLowerInvariant(), item.StudentId);
        Assert.Equal(fixture.SubjectId.ToString("D").ToLowerInvariant(), item.SubjectId);
        Assert.Equal(fixture.AssignmentId.ToString("D").ToLowerInvariant(), item.AssignmentId);
        Assert.Null(item.Grading.IsCorrect);
        Assert.Null(item.Grading.AwardedScore);
        Assert.Equal(DateTimeKind.Utc, item.CreatedAt.Kind);
        Assert.Equal(DateTimeKind.Utc, item.UpdatedAt.Kind);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ExecuteAsync_UnknownPersistedStatus_ThrowsDeterministically(bool jobStatus)
    {
        using var fixture = CreateFixture(UserRole.CenterManager);
        await SeedGraphAsync(
            fixture,
            attemptStatus: jobStatus ? AttemptStatus.Completed : (AttemptStatus)999,
            jobStatus: jobStatus ? (AIJobStatus)999 : AIJobStatus.Completed);

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.UseCase.ExecuteAsync(new(), CancellationToken.None));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(2)]
    public async Task ExecuteAsync_InvalidJobCardinality_ThrowsDeterministically(int jobCount)
    {
        using var fixture = CreateFixture(UserRole.CenterManager);
        await SeedGraphAsync(fixture, createJob: jobCount > 0);
        if (jobCount == 2)
        {
            fixture.Context.AIAnalysisJobs.Add(CreateJob(
                fixture.Actor.CenterId!.Value,
                13002,
                12001,
                AIJobStatus.Pending));
            await fixture.Context.SaveChangesAsync();
            fixture.Context.ChangeTracker.Clear();
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            fixture.UseCase.ExecuteAsync(new(), CancellationToken.None));
    }

    [Fact]
    public async Task ExecuteAsync_OrdersBeforePagingWithStableTieBreakerAndExactMeta()
    {
        using var fixture = CreateFixture(UserRole.CenterManager);
        await SeedGraphAsync(fixture, attemptId: 12001, jobId: 13001, createdAt: BaseTime);
        await SeedGraphAsync(fixture, attemptId: 12003, jobId: 13003, createdAt: BaseTime.AddMinutes(1));
        await SeedGraphAsync(fixture, attemptId: 12002, jobId: 13002, createdAt: BaseTime.AddMinutes(1));

        var first = await fixture.UseCase.ExecuteAsync(
            new ListAttemptsQuery { Page = "1", PageSize = "2" },
            CancellationToken.None);
        var second = await fixture.UseCase.ExecuteAsync(
            new ListAttemptsQuery { Page = "2", PageSize = "2" },
            CancellationToken.None);

        Assert.Equal(new[] { "12003", "12002" }, first.Data!.Select(item => item.AttemptId));
        Assert.Equal("12001", Assert.Single(second.Data!).AttemptId);
        Assert.Equal(3, first.TotalItems);
        Assert.Equal(2, first.TotalPages);
        Assert.Equal(2, first.PageSize);
        Assert.Equal(2, second.Page);
    }

    [Fact]
    public async Task ExecuteAsync_VeryLargePageReturnsEmptyWithoutOverflow()
    {
        using var fixture = CreateFixture(UserRole.CenterManager);
        await SeedGraphAsync(fixture);

        var result = await fixture.UseCase.ExecuteAsync(
            new ListAttemptsQuery { Page = int.MaxValue.ToString(), PageSize = "100" },
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Data!);
        Assert.Equal(1, result.TotalItems);
        Assert.Equal(1, result.TotalPages);
    }

    [Fact]
    public async Task ExecuteAsync_PreCancelledTokenCancelsDatabaseQuery()
    {
        using var fixture = CreateFixture(UserRole.CenterManager);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            fixture.UseCase.ExecuteAsync(new(), cancellation.Token));
    }

    [Fact]
    public async Task ExecuteAsync_ReadOnlyQueryDoesNotTrackMutateOrSave()
    {
        var store = new InMemoryDatabaseRoot();
        var databaseName = Guid.NewGuid().ToString();
        var actor = ValidActor(UserRole.CenterManager);
        using (var seedFixture = CreateFixture(actor, store, databaseName))
        {
            await SeedGraphAsync(seedFixture);
        }

        var counter = new SaveCounterInterceptor();
        using var queryFixture = CreateFixture(actor, store, databaseName, counter: counter);

        var result = await queryFixture.UseCase.ExecuteAsync(new(), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(queryFixture.Context.ChangeTracker.Entries());
        Assert.Equal(0, counter.SaveCallCount);
    }

    public static TheoryData<TestActorContext> InvalidActorContexts =>
        new()
        {
            new TestActorContext(null, null, null, false),
            new TestActorContext(Guid.Empty, Guid.NewGuid(), nameof(UserRole.Student), true),
            new TestActorContext(Guid.NewGuid(), Guid.Empty, nameof(UserRole.Student), true),
            new TestActorContext(Guid.NewGuid(), Guid.NewGuid(), "student", true),
            new TestActorContext(Guid.NewGuid(), Guid.NewGuid(), "0", true),
            new TestActorContext(Guid.NewGuid(), Guid.NewGuid(), "Administrator", true)
        };

    public static TheoryData<string, string> PresentInvalidValues
    {
        get
        {
            var data = new TheoryData<string, string>();
            foreach (var property in typeof(ListAttemptsQuery).GetProperties())
            {
                data.Add(property.Name, string.Empty);
                data.Add(property.Name, "null");
            }

            return data;
        }
    }

    private static Fixture CreateFixture(
        UserRole role,
        bool useRealOwnership = false) =>
        CreateFixture(ValidActor(role), useRealOwnership: useRealOwnership);

    private static Fixture CreateFixture(
        TestActorContext actor,
        InMemoryDatabaseRoot? store = null,
        string? databaseName = null,
        bool useRealOwnership = false,
        SaveCounterInterceptor? counter = null)
    {
        store ??= new InMemoryDatabaseRoot();
        databaseName ??= Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(databaseName, store);
        if (counter is not null)
        {
            options.AddInterceptors(counter);
        }

        var context = new EduTwinDbContext(options.Options, actor);
        var ownership = new Mock<IStudentOwnershipGuard>(MockBehavior.Strict);
        IStudentOwnershipGuard guard = useRealOwnership
            ? new OrganizationOwnershipGuard(context, actor)
            : ownership.Object;
        return new Fixture(actor, context, ownership, new ListAttemptsUseCase(context, actor, guard));
    }

    private static TestActorContext ValidActor(UserRole role) =>
        new(Guid.NewGuid(), Guid.NewGuid(), role.ToString(), true);

    private static async Task SeedGraphAsync(
        Fixture fixture,
        Guid? centerId = null,
        Guid? studentId = null,
        Guid? subjectId = null,
        ulong? questionId = null,
        ulong attemptId = 12001,
        ulong jobId = 13001,
        Guid? assignmentId = null,
        Guid? createdByTeacherId = null,
        AttemptStatus attemptStatus = AttemptStatus.PendingAnalysis,
        AIJobStatus jobStatus = AIJobStatus.Pending,
        DateTime? createdAt = null,
        bool? isCorrect = true,
        decimal? awardedScore = 1m,
        bool createJob = true)
    {
        var context = fixture.Context;
        var actualCenter = centerId ?? fixture.Actor.CenterId!.Value;
        var actualStudent = studentId ?? fixture.StudentId;
        var actualSubject = subjectId ?? fixture.SubjectId;
        var actualQuestion = questionId ?? fixture.QuestionId;
        var actualCreatedAt = createdAt ?? BaseTime;

        if (!context.Students.Local.Any(student =>
                student.CenterId == actualCenter && student.StudentId == actualStudent) &&
            !await context.Students.AnyAsync(student => student.StudentId == actualStudent))
        {
            context.Students.Add(CreateStudent(actualCenter, actualStudent));
        }

        if (!context.Subjects.Local.Any(subject =>
                subject.CenterId == actualCenter && subject.SubjectId == actualSubject) &&
            !await context.Subjects.AnyAsync(subject => subject.SubjectId == actualSubject))
        {
            context.Subjects.Add(CreateSubject(actualCenter, actualSubject));
        }

        if (!context.Questions.Local.Any(question =>
                question.CenterId == actualCenter && question.QuestionId == actualQuestion) &&
            !await context.Questions.AnyAsync(question => question.QuestionId == actualQuestion))
        {
            context.Questions.Add(CreateQuestion(
                actualCenter,
                actualSubject,
                actualQuestion,
                createdByTeacherId));
        }

        if (assignmentId.HasValue &&
            !context.Assignments.Local.Any(assignment =>
                assignment.CenterId == actualCenter && assignment.AssignmentId == assignmentId) &&
            !await context.Assignments.AnyAsync(assignment => assignment.AssignmentId == assignmentId))
        {
            context.Assignments.Add(CreateAssignment(
                actualCenter,
                assignmentId.Value,
                createdByTeacherId));
        }

        context.Attempts.Add(new Attempt
        {
            AttemptId = attemptId,
            CenterId = actualCenter,
            StudentId = actualStudent,
            QuestionId = actualQuestion,
            AssignmentId = assignmentId,
            FinalAnswer = "must-not-leak",
            ReasoningText = "must-not-leak",
            IsCorrect = isCorrect,
            AwardedScore = awardedScore,
            TimeSpentSeconds = 30,
            Confidence = 75,
            AnswerChanges = 1,
            Skipped = false,
            ReasoningLanguage = "vi",
            Status = attemptStatus,
            ClientSubmissionId = Guid.NewGuid(),
            CreatedAt = actualCreatedAt,
            UpdatedAt = actualCreatedAt.AddSeconds(5)
        });
        if (createJob)
        {
            context.AIAnalysisJobs.Add(CreateJob(
                actualCenter,
                jobId,
                attemptId,
                jobStatus));
        }

        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    private static async Task SeedStudentAsync(
        EduTwinDbContext context,
        Guid centerId,
        Guid studentId)
    {
        context.Students.Add(CreateStudent(centerId, studentId));
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    private static async Task SeedMembershipAsync(
        EduTwinDbContext context,
        Guid studentId,
        Guid teacherId,
        ClassStatus classStatus,
        ClassStudentStatus membershipStatus)
    {
        var classId = Guid.NewGuid();
        context.Classes.Add(new Class
        {
            ClassId = classId,
            CenterId = context.CurrentTenantId,
            TeacherId = teacherId,
            SubjectId = Guid.NewGuid(),
            ClassName = "Attempt summary class",
            AcademicYear = "2026-2027",
            Status = classStatus,
            CreatedAt = BaseTime.AddDays(-1),
            UpdatedAt = BaseTime.AddDays(-1)
        });
        context.ClassStudents.Add(new ClassStudent
        {
            CenterId = context.CurrentTenantId,
            ClassId = classId,
            StudentId = studentId,
            Status = membershipStatus,
            JoinedAt = BaseTime.AddDays(-1),
            RemovedAt = membershipStatus == ClassStudentStatus.Removed ? BaseTime : null
        });
        await context.SaveChangesAsync();
        context.ChangeTracker.Clear();
    }

    private static Student CreateStudent(Guid centerId, Guid studentId) =>
        new()
        {
            StudentId = studentId,
            CenterId = centerId,
            FullName = "Attempt summary student",
            GradeLevel = 12,
            CreatedAt = BaseTime.AddDays(-1),
            UpdatedAt = BaseTime.AddDays(-1)
        };

    private static Subject CreateSubject(Guid centerId, Guid subjectId) =>
        new()
        {
            SubjectId = subjectId,
            CenterId = centerId,
            SubjectCode = "MATH",
            SubjectName = "Mathematics",
            IsActive = true,
            CreatedAt = BaseTime.AddDays(-1),
            UpdatedAt = BaseTime.AddDays(-1)
        };

    private static Question CreateQuestion(
        Guid centerId,
        Guid subjectId,
        ulong questionId,
        Guid? createdByTeacherId = null) =>
        new()
        {
            QuestionId = questionId,
            CenterId = centerId,
            SubjectId = subjectId,
            PrimaryTopicNodeId = 101,
            CreatedByTeacherId = createdByTeacherId ?? Guid.NewGuid(),
            QuestionType = QuestionType.ShortAnswer,
            Difficulty = 2,
            QuestionText = "Question text safe to expose",
            CorrectAnswer = "must-not-leak",
            Solution = "must-not-leak",
            ExpectedReasoning = "must-not-leak",
            MaxScore = 2.5m,
            EstimatedTimeSeconds = 60,
            ReasoningRequired = true,
            LanguageCode = "vi",
            Status = QuestionStatus.Active,
            CreatedAt = BaseTime.AddDays(-1),
            UpdatedAt = BaseTime.AddDays(-1)
        };

    private static Assignment CreateAssignment(
        Guid centerId,
        Guid assignmentId,
        Guid? createdByTeacherId = null) =>
        new()
        {
            AssignmentId = assignmentId,
            CenterId = centerId,
            ClassId = Guid.NewGuid(),
            CreatedByTeacherId = createdByTeacherId ?? Guid.NewGuid(),
            Title = "Attempt summary assignment",
            Status = AssignmentStatus.Published,
            CreatedAt = BaseTime.AddDays(-1),
            UpdatedAt = BaseTime.AddDays(-1)
        };

    private static AIAnalysisJob CreateJob(
        Guid centerId,
        ulong jobId,
        ulong attemptId,
        AIJobStatus status) =>
        new()
        {
            AnalysisJobId = jobId,
            CenterId = centerId,
            AttemptId = attemptId,
            Status = status,
            AvailableAt = BaseTime,
            LeaseOwner = "must-not-leak",
            LastErrorCode = "must-not-leak",
            LastErrorMessage = "must-not-leak",
            CorrelationId = "must-not-leak",
            CreatedAt = BaseTime,
            UpdatedAt = BaseTime
        };

    private sealed class Fixture : IDisposable
    {
        public Fixture(
            TestActorContext actor,
            EduTwinDbContext context,
            Mock<IStudentOwnershipGuard> ownership,
            ListAttemptsUseCase useCase)
        {
            Actor = actor;
            Context = context;
            Ownership = ownership;
            UseCase = useCase;
        }

        public TestActorContext Actor { get; }
        public EduTwinDbContext Context { get; }
        public Mock<IStudentOwnershipGuard> Ownership { get; }
        public ListAttemptsUseCase UseCase { get; }
        public Guid StudentId { get; } = Guid.Parse("BAF68743-A272-4983-A9E2-41663734A7C2");
        public Guid SubjectId { get; } = Guid.Parse("2ED34B81-0B0D-457C-888D-6A78F50A33D2");
        public Guid AssignmentId { get; } = Guid.Parse("12AE0F80-D90E-4627-964F-4404E692E3D6");
        public ulong QuestionId { get; } = 9001;

        public void Dispose() => Context.Dispose();
    }

    public sealed class TestActorContext(
        Guid? centerId,
        Guid? userId,
        string? role,
        bool isResolved) : ITenantContext, ITenantIdAccessor
    {
        public Guid? CenterId { get; } = centerId;
        public Guid? UserId { get; } = userId;
        public string? Role { get; } = role;
        public int? AuthVersion { get; } = 1;
        public bool IsResolved { get; } = isResolved;
    }

    private sealed class SaveCounterInterceptor : SaveChangesInterceptor
    {
        public int SaveCallCount { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            SaveCallCount++;
            return ValueTask.FromResult(result);
        }
    }
}
