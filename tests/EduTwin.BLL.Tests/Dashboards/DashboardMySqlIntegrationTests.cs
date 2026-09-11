using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MySql.Data.MySqlClient;
using EduTwin.BLL.AssessmentAndReasoning.Feedback;
using EduTwin.BLL.Dashboards;
using EduTwin.BLL.DigitalTwin;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.DigitalTwin;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Assignments;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.DigitalTwin;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Xunit;

namespace EduTwin.BLL.Tests.Dashboards;

[Collection("MySqlDatabase")]
public sealed class DashboardMySqlIntegrationTests
{
    private const string AdminConnectionVariable = "EDUTWIN_TEST_MYSQL_ADMIN_CONNECTION_STRING";
    private static readonly DateTime UtcNow = new(2026, 9, 11, 10, 0, 0, DateTimeKind.Utc);

    private sealed class TestTenantContext : ITenantContext, ITenantIdAccessor
    {
        public Guid? CenterId { get; set; }
        public Guid? UserId { get; set; }
        public string? Role { get; set; }
        public uint? AuthVersion { get; set; } = 1;
        public bool IsResolved => CenterId.HasValue && CenterId.Value != Guid.Empty;
    }

    [MySqlIntegrationFact]
    public async Task ZeroFill_WeightedMastery_And_AssignmentCompletion_CalculatedCorrectly()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var student1Id = Guid.NewGuid();
        var student2Id = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var classId = Guid.NewGuid();

        await SeedHierarchyAsync(database.ConnectionString, centerId, teacherId, [student1Id, student2Id], subjectId, classId);

        var tenant = new TestTenantContext
        {
            CenterId = centerId,
            UserId = teacherId,
            Role = nameof(UserRole.Teacher)
        };
        await using var context = CreateContext(database.ConnectionString, tenant);

        // Topic 1: exam importance 60, Topic 2: exam importance 40
        // Student 1 has KnowledgeTwin on Topic 1 (mastery 80), missing KnowledgeTwin on Topic 2 (zero-fill => 0)
        // Student 1 weighted mastery = (80 * 60 + 0 * 40) / 100 = 48%
        // Student 2 has missing KnowledgeTwins on both topics (zero-fill => 0 on both)
        // Student 2 weighted mastery = 0%
        // Class average mastery = (48 + 0) / 2 = 24.0%
        context.KnowledgeTwins.Add(new KnowledgeTwin
        {
            KnowledgeTwinId = 1UL,
            CenterId = centerId,
            StudentId = student1Id,
            SubjectId = subjectId,
            TopicNodeId = 1,
            MasteryPercentage = 80m,
            EvidenceCount = 3,
            CreatedAt = UtcNow,
            UpdatedAt = UtcNow
        });

        // Add 1 published assignment with 2 student targets: student 1 Completed, student 2 InProgress
        // Completion rate = 1 / 2 = 50.0%
        var assignmentId = Guid.NewGuid();
        context.Assignments.Add(new Assignment
        {
            AssignmentId = assignmentId,
            CenterId = centerId,
            ClassId = classId,
            CreatedByTeacherId = teacherId,
            Title = "Math Assignment 1",
            Status = AssignmentStatus.Published,
            CreatedAt = UtcNow,
            UpdatedAt = UtcNow,
            RowVersion = 1
        });

        context.AssignmentTargets.AddRange(
            new AssignmentTarget
            {
                CenterId = centerId,
                AssignmentId = assignmentId,
                StudentId = student1Id,
                TargetSource = TargetSource.SelectedStudents,
                CreatedAt = UtcNow
            },
            new AssignmentTarget
            {
                CenterId = centerId,
                AssignmentId = assignmentId,
                StudentId = student2Id,
                TargetSource = TargetSource.SelectedStudents,
                CreatedAt = UtcNow
            }
        );

        context.StudentAssignmentProgresses.Add(new StudentAssignmentProgress
        {
            ProgressId = 1UL,
            CenterId = centerId,
            AssignmentId = assignmentId,
            StudentId = student1Id,
            Status = ProgressStatus.Completed,
            CompletedQuestionCount = 5,
            TotalQuestionCount = 5,
            CompletedAt = UtcNow,
            CreatedAt = UtcNow,
            UpdatedAt = UtcNow,
            RowVersion = 1
        });

        // Add high-risk goal for student 2: Target 8.5, Predicted 1.0, Risk 75.0 (>= 70 threshold)
        context.StudentSubjectGoals.Add(new StudentSubjectGoal
        {
            GoalId = 1UL,
            CenterId = centerId,
            StudentId = student2Id,
            SubjectId = subjectId,
            TargetScore = 8.5m,
            RemainingDays = 30U,
            CurrentPredictedScore = 1.0m,
            RiskScore = 75.0m,
            CreatedAt = UtcNow,
            UpdatedAt = UtcNow
        });

        await context.SaveChangesAsync();

        // 1. Verify Class Dashboard Use Case
        var ownershipGuard = new OrganizationOwnershipGuard(context, tenant);
        var classDashboardUseCase = new GetClassDashboardUseCase(context, tenant, ownershipGuard, TimeProvider.System);
        var classResult = await classDashboardUseCase.ExecuteAsync(classId, 70m, CancellationToken.None);

        Assert.True(classResult.IsSuccess);
        Assert.NotNull(classResult.Data);
        Assert.Equal(2, classResult.Data.Overview.StudentCount);
        Assert.Equal(24.0m, classResult.Data.Overview.AverageMastery);
        Assert.Equal(50.0m, classResult.Data.Overview.AssignmentCompletionRate);
        Assert.Single(classResult.Data.HighRiskStudents);
        Assert.Equal(student2Id, classResult.Data.HighRiskStudents[0].StudentId);
        Assert.Equal(75.0m, classResult.Data.HighRiskStudents[0].RiskScore);

        // Weak topics: Topic 1 average = (80 + 0) / 2 = 40.0% (< 60), Topic 2 average = (0 + 0) / 2 = 0% (< 60)
        Assert.Equal(2, classResult.Data.WeakTopics.Count);

        // Gap groups: weak topics group students below 60
        Assert.True(classResult.Data.GapGroups.Count > 0);

        // 2. Verify Center Dashboard Use Case
        var centerTenant = new TestTenantContext
        {
            CenterId = centerId,
            UserId = Guid.NewGuid(),
            Role = nameof(UserRole.CenterManager)
        };
        await using var centerContext = CreateContext(database.ConnectionString, centerTenant);
        var centerDashboardUseCase = new GetCenterDashboardUseCase(centerContext, centerTenant, TimeProvider.System);
        var centerResult = await centerDashboardUseCase.ExecuteAsync(subjectId, 70m, CancellationToken.None);

        Assert.True(centerResult.IsSuccess);
        Assert.NotNull(centerResult.Data);
        Assert.Equal(2, centerResult.Data.Summary.StudentCount);
        Assert.Equal(1, centerResult.Data.Summary.ClassCount);
        Assert.Equal(1, centerResult.Data.Summary.TeacherCount);
        Assert.Single(centerResult.Data.MasteryBySubject);
        Assert.Equal(24.0m, centerResult.Data.MasteryBySubject[0].AverageMastery);
        Assert.Single(centerResult.Data.ClassRanking);
        Assert.Equal(24.0m, centerResult.Data.ClassRanking[0].AverageMastery);
    }

    [MySqlIntegrationFact]
    public async Task CrossTenantIsolation_ReturnsNotFound_And_Forbidden()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var centerAId = Guid.NewGuid();
        var centerBId = Guid.NewGuid();
        var teacherAId = Guid.NewGuid();
        var teacherBId = Guid.NewGuid();
        var studentAId = Guid.NewGuid();
        var studentBId = Guid.NewGuid();
        var subjectAId = Guid.NewGuid();
        var subjectBId = Guid.NewGuid();
        var classAId = Guid.NewGuid();
        var classBId = Guid.NewGuid();

        await SeedHierarchyAsync(database.ConnectionString, centerAId, teacherAId, [studentAId], subjectAId, classAId);
        await SeedHierarchyAsync(database.ConnectionString, centerBId, teacherBId, [studentBId], subjectBId, classBId, null, baseNodeId: 10UL);

        // Set tenant context to Center A
        var tenantA = new TestTenantContext
        {
            CenterId = centerAId,
            UserId = teacherAId,
            Role = nameof(UserRole.Teacher)
        };
        await using var contextA = CreateContext(database.ConnectionString, tenantA);

        var ownershipGuardA = new OrganizationOwnershipGuard(contextA, tenantA);
        var classDashboardUseCase = new GetClassDashboardUseCase(contextA, tenantA, ownershipGuardA, TimeProvider.System);

        // Query class from Center B while logged into Center A
        var crossTenantClassResult = await classDashboardUseCase.ExecuteAsync(classBId, 70m, CancellationToken.None);
        Assert.False(crossTenantClassResult.IsSuccess);
        Assert.Equal("RESOURCE_NOT_FOUND", crossTenantClassResult.ErrorCode);

        // Query student twin from Center B while logged into Center A
        var teacherTwinUseCase = new GetTeacherStudentTwinUseCase(contextA, tenantA, ownershipGuardA);
        var crossTenantStudentResult = await teacherTwinUseCase.ExecuteAsync(studentBId, subjectAId, CancellationToken.None);
        Assert.False(crossTenantStudentResult.IsSuccess);
        Assert.Equal("RESOURCE_NOT_FOUND", crossTenantStudentResult.ErrorCode);
    }

    [MySqlIntegrationFact]
    public async Task AuthorizationGuards_TeacherOwnership_EnforcesForbiddenProperly()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var centerId = Guid.NewGuid();
        var teacherAssignedId = Guid.NewGuid();
        var teacherUnassignedId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var classId = Guid.NewGuid();

        await SeedHierarchyAsync(database.ConnectionString, centerId, teacherAssignedId, [studentId], subjectId, classId, [teacherUnassignedId]);

        // Test with unassigned teacher context
        var unassignedTenant = new TestTenantContext
        {
            CenterId = centerId,
            UserId = teacherUnassignedId,
            Role = nameof(UserRole.Teacher)
        };
        await using var unassignedContext = CreateContext(database.ConnectionString, unassignedTenant);

        var unassignedGuard = new OrganizationOwnershipGuard(unassignedContext, unassignedTenant);
        var unassignedClassDashboardUseCase = new GetClassDashboardUseCase(unassignedContext, unassignedTenant, unassignedGuard, TimeProvider.System);

        var forbiddenClassResult = await unassignedClassDashboardUseCase.ExecuteAsync(classId, 70m, CancellationToken.None);
        Assert.False(forbiddenClassResult.IsSuccess);
        Assert.Equal("FORBIDDEN_RESOURCE", forbiddenClassResult.ErrorCode);

        var unassignedTwinUseCase = new GetTeacherStudentTwinUseCase(unassignedContext, unassignedTenant, unassignedGuard);
        var forbiddenTwinResult = await unassignedTwinUseCase.ExecuteAsync(studentId, subjectId, CancellationToken.None);
        Assert.False(forbiddenTwinResult.IsSuccess);
        Assert.Equal("FORBIDDEN_RESOURCE", forbiddenTwinResult.ErrorCode);

        // Now test with assigned teacher context
        var assignedTenant = new TestTenantContext
        {
            CenterId = centerId,
            UserId = teacherAssignedId,
            Role = nameof(UserRole.Teacher)
        };
        await using var assignedContext = CreateContext(database.ConnectionString, assignedTenant);

        var assignedGuard = new OrganizationOwnershipGuard(assignedContext, assignedTenant);
        var assignedClassDashboardUseCase = new GetClassDashboardUseCase(assignedContext, assignedTenant, assignedGuard, TimeProvider.System);
        var allowedClassResult = await assignedClassDashboardUseCase.ExecuteAsync(classId, 70m, CancellationToken.None);
        Assert.True(allowedClassResult.IsSuccess);

        var assignedTwinUseCase = new GetTeacherStudentTwinUseCase(assignedContext, assignedTenant, assignedGuard);
        var allowedTwinResult = await assignedTwinUseCase.ExecuteAsync(studentId, subjectId, CancellationToken.None);
        Assert.True(allowedTwinResult.IsSuccess);
    }

    [MySqlIntegrationFact]
    public async Task StudentDashboard_ProgressLineReconstruction_And_GoalRead()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var classId = Guid.NewGuid();

        await SeedHierarchyAsync(database.ConnectionString, centerId, teacherId, [studentId], subjectId, classId);

        var tenant = new TestTenantContext
        {
            CenterId = centerId,
            UserId = studentId,
            Role = nameof(UserRole.Student)
        };
        await using var context = CreateContext(database.ConnectionString, tenant);

        // Persist governed Goal state (R06 single source of truth)
        context.StudentSubjectGoals.Add(new StudentSubjectGoal
        {
            GoalId = 1UL,
            CenterId = centerId,
            StudentId = studentId,
            SubjectId = subjectId,
            TargetScore = 9.0m,
            RemainingDays = 60U,
            CurrentPredictedScore = 6.5m,
            RiskScore = 18.2m,
            CreatedAt = UtcNow,
            UpdatedAt = UtcNow
        });

        using var jsonDoc = JsonDocument.Parse("{}");

        // Add topic history events:
        // Event 1: Topic 1 (importance 60) goes to 50%
        // Event 2: Topic 2 (importance 40) goes to 40%
        // Weighted subject mastery at event 1: (50 * 60 + 0 * 40) / 100 = 30.0%
        // Weighted subject mastery at event 2: (50 * 60 + 40 * 40) / 100 = 46.0%
        context.TwinUpdateHistories.AddRange(
            new TwinUpdateHistory
            {
                HistoryId = 1UL,
                CenterId = centerId,
                StudentId = studentId,
                SubjectId = subjectId,
                TopicNodeId = 1UL,
                EventSource = TwinEventSource.AIAnalysis,
                PreviousMastery = 0m,
                NewMastery = 50m,
                MasteryDelta = 50m,
                CalculationVersion = "1.0",
                CalculationBreakdown = jsonDoc,
                Explanation = "Test 1",
                CreatedAt = UtcNow.AddMinutes(1)
            },
            new TwinUpdateHistory
            {
                HistoryId = 2UL,
                CenterId = centerId,
                StudentId = studentId,
                SubjectId = subjectId,
                TopicNodeId = 2UL,
                EventSource = TwinEventSource.AIAnalysis,
                PreviousMastery = 0m,
                NewMastery = 40m,
                MasteryDelta = 40m,
                CalculationVersion = "1.0",
                CalculationBreakdown = jsonDoc,
                Explanation = "Test 2",
                CreatedAt = UtcNow.AddMinutes(2)
            }
        );

        // Also add KnowledgeTwin for radar
        context.KnowledgeTwins.AddRange(
            new KnowledgeTwin
            {
                KnowledgeTwinId = 1UL,
                CenterId = centerId,
                StudentId = studentId,
                SubjectId = subjectId,
                TopicNodeId = 1UL,
                MasteryPercentage = 50m,
                CreatedAt = UtcNow,
                UpdatedAt = UtcNow
            },
            new KnowledgeTwin
            {
                KnowledgeTwinId = 2UL,
                CenterId = centerId,
                StudentId = studentId,
                SubjectId = subjectId,
                TopicNodeId = 2UL,
                MasteryPercentage = 40m,
                CreatedAt = UtcNow,
                UpdatedAt = UtcNow
            }
        );

        await context.SaveChangesAsync();

        var studentDashboardUseCase = new GetStudentDashboardUseCase(context, tenant, TimeProvider.System);
        var result = await studentDashboardUseCase.ExecuteAsync(subjectId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);

        // Governed goal read directly, not re-calculated in R08
        Assert.Equal(9.0m, result.Data.Goal.TargetScore);
        Assert.Equal(6.5m, result.Data.Goal.CurrentPredictedScore);
        Assert.Equal(18.2m, result.Data.Goal.RiskScore);
        Assert.Equal(60U, result.Data.Goal.RemainingDays);

        // Progress line reconstruction: exactly 2 points reflecting cumulative weighted mastery
        Assert.Equal(2, result.Data.ProgressLine.Count);
        Assert.Equal(30.0m, result.Data.ProgressLine[0].OverallSubjectMastery);
        Assert.Equal(46.0m, result.Data.ProgressLine[1].OverallSubjectMastery);

        // Mastery radar: 2 topics
        Assert.Equal(2, result.Data.MasteryRadar.Count);
    }

    [MySqlIntegrationFact]
    public async Task AttemptFeedback_ExactContract54_And_OwnershipAccess()
    {
        await using var database = await MySqlTestDatabase.CreateAsync();
        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var student1Id = Guid.NewGuid();
        var student2Id = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var classId = Guid.NewGuid();

        await SeedHierarchyAsync(database.ConnectionString, centerId, teacherId, [student1Id, student2Id], subjectId, classId);

        var adminTenant = new TestTenantContext { CenterId = centerId, UserId = teacherId, Role = nameof(UserRole.Teacher) };
        await using var setupContext = CreateContext(database.ConnectionString, adminTenant);

        // Seed question
        var question = new Question
        {
            QuestionId = 100UL,
            CenterId = centerId,
            SubjectId = subjectId,
            PrimaryTopicNodeId = 1UL,
            CreatedByTeacherId = teacherId,
            QuestionType = QuestionType.MultipleChoice,
            Difficulty = 3,
            QuestionText = "Calculate derivative of f(x) = x^2",
            CorrectAnswer = "2x",
            Solution = "Using power rule: 2x",
            MaxScore = 10.0m,
            EstimatedTimeSeconds = 120,
            ReasoningRequired = true,
            LanguageCode = "vi",
            Status = QuestionStatus.Active,
            CreatedAt = UtcNow,
            UpdatedAt = UtcNow,
            RowVersion = 1
        };
        setupContext.Questions.Add(question);

        // Seed attempt for student1
        var attempt = new Attempt
        {
            AttemptId = 200UL,
            CenterId = centerId,
            StudentId = student1Id,
            QuestionId = 100UL,
            FinalAnswer = "2x",
            ReasoningText = "f'(x) = 2*x^(2-1) = 2x",
            IsCorrect = true,
            AwardedScore = 10.0m,
            TimeSpentSeconds = 65,
            Confidence = 0.95m,
            AnswerChanges = 0,
            Skipped = false,
            ReasoningLanguage = "vi",
            Status = AttemptStatus.Completed,
            ClientSubmissionId = Guid.NewGuid(),
            CreatedAt = UtcNow,
            UpdatedAt = UtcNow,
            RowVersion = 1
        };
        setupContext.Attempts.Add(attempt);

        using var emptyArrayDoc = JsonDocument.Parse("[]");
        var reasoningAnalysis = new ReasoningAnalysis
        {
            AnalysisId = 300UL,
            CenterId = centerId,
            AttemptId = 200UL,
            SchemaVersion = "1.0",
            MethodDetected = "Power Rule",
            ReasoningQuality = 95m,
            ErrorType = ErrorType.None,
            Misconception = null,
            MissingSteps = emptyArrayDoc,
            RootCauseNodeIds = emptyArrayDoc,
            AnalysisConfidence = 0.98m,
            Feedback = "Rất tốt, lập luận chặt chẽ và chính xác.",
            IsFallback = false,
            NeedsTeacherReview = false,
            Provider = AnalysisProvider.RuleBased,
            CreatedAt = UtcNow,
            UpdatedAt = UtcNow,
            RowVersion = 1
        };
        setupContext.ReasoningAnalyses.Add(reasoningAnalysis);

        await setupContext.SaveChangesAsync();

        // 1. Student 1 (Owner) can read feedback
        var student1Tenant = new TestTenantContext
        {
            CenterId = centerId,
            UserId = student1Id,
            Role = nameof(UserRole.Student)
        };
        await using var s1Context = CreateContext(database.ConnectionString, student1Tenant);
        var s1Guard = new OrganizationOwnershipGuard(s1Context, student1Tenant);
        var feedbackUseCase = new GetAttemptFeedbackUseCase(s1Context, student1Tenant, s1Guard);

        var s1Result = await feedbackUseCase.ExecuteAsync(200UL, CancellationToken.None);
        Assert.True(s1Result.IsSuccess);
        Assert.NotNull(s1Result.Data);
        Assert.True(s1Result.Data.Grading.IsCorrect);
        Assert.Equal(10.0m, s1Result.Data.Grading.AwardedScore);
        Assert.Equal(10.0m, s1Result.Data.Grading.MaxScore);
        Assert.NotNull(s1Result.Data.Analysis);
        Assert.Equal("300", s1Result.Data.Analysis.AnalysisId);
        Assert.Equal("Power Rule", s1Result.Data.Analysis.MethodDetected);
        Assert.Equal(95, s1Result.Data.Analysis.ReasoningQuality);
        Assert.Equal("Good", s1Result.Data.Analysis.QualityBand);
        Assert.False(s1Result.Data.Analysis.NeedsTeacherReview);

        // 2. Student 2 cannot read student 1's feedback -> Forbidden
        var student2Tenant = new TestTenantContext
        {
            CenterId = centerId,
            UserId = student2Id,
            Role = nameof(UserRole.Student)
        };
        await using var s2Context = CreateContext(database.ConnectionString, student2Tenant);
        var s2Guard = new OrganizationOwnershipGuard(s2Context, student2Tenant);
        var s2FeedbackUseCase = new GetAttemptFeedbackUseCase(s2Context, student2Tenant, s2Guard);

        var s2Result = await s2FeedbackUseCase.ExecuteAsync(200UL, CancellationToken.None);
        Assert.False(s2Result.IsSuccess);
        Assert.Equal("FORBIDDEN_RESOURCE", s2Result.ErrorCode);

        // 3. Teacher assigned to student 1's class can read feedback
        var teacherTenant = new TestTenantContext
        {
            CenterId = centerId,
            UserId = teacherId,
            Role = nameof(UserRole.Teacher)
        };
        await using var tContext = CreateContext(database.ConnectionString, teacherTenant);
        var tGuard = new OrganizationOwnershipGuard(tContext, teacherTenant);
        var tFeedbackUseCase = new GetAttemptFeedbackUseCase(tContext, teacherTenant, tGuard);

        var tResult = await tFeedbackUseCase.ExecuteAsync(200UL, CancellationToken.None);
        Assert.True(tResult.IsSuccess);
        Assert.NotNull(tResult.Data);

        // 4. CenterManager can read feedback
        var mgrTenant = new TestTenantContext
        {
            CenterId = centerId,
            UserId = Guid.NewGuid(),
            Role = nameof(UserRole.CenterManager)
        };
        await using var mgrContext = CreateContext(database.ConnectionString, mgrTenant);
        var mgrGuard = new OrganizationOwnershipGuard(mgrContext, mgrTenant);
        var mgrFeedbackUseCase = new GetAttemptFeedbackUseCase(mgrContext, mgrTenant, mgrGuard);

        var mgrResult = await mgrFeedbackUseCase.ExecuteAsync(200UL, CancellationToken.None);
        Assert.True(mgrResult.IsSuccess);
        Assert.NotNull(mgrResult.Data);
    }

    private static async Task SeedHierarchyAsync(
        string connectionString,
        Guid centerId,
        Guid teacherId,
        IReadOnlyList<Guid> studentIds,
        Guid subjectId,
        Guid classId,
        IReadOnlyList<Guid>? extraTeacherIds = null,
        ulong baseNodeId = 1)
    {
        var tenant = new TestTenantContext { CenterId = centerId };
        await using var context = CreateContext(connectionString, tenant);
        await context.Database.OpenConnectionAsync();
        try
        {
            await context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 0;");

            context.Centers.Add(new Center
            {
                CenterId = centerId,
                CenterCode = $"C-{centerId:N}"[..10],
                CenterName = "Dashboard Test Center",
                Status = CenterStatus.Active,
                Timezone = "UTC",
                CreatedAt = UtcNow,
                UpdatedAt = UtcNow
            });

            context.Users.Add(new User
            {
                UserId = teacherId,
                CenterId = centerId,
                Username = $"teacher-{teacherId:N}"[..20],
                PasswordHash = "hash",
                RoleName = UserRole.Teacher,
                DisplayName = "Teacher",
                Status = UserStatus.Active,
                AuthVersion = 1,
                CreatedAt = UtcNow,
                UpdatedAt = UtcNow,
                RowVersion = 1
            });

            context.Teachers.Add(new Teacher
            {
                TeacherId = teacherId,
                CenterId = centerId,
                Department = "Mathematics",
                CreatedAt = UtcNow,
                UpdatedAt = UtcNow
            });

            if (extraTeacherIds != null)
            {
                foreach (var extraTeacherId in extraTeacherIds)
                {
                    context.Users.Add(new User
                    {
                        UserId = extraTeacherId,
                        CenterId = centerId,
                        Username = $"teacher-{extraTeacherId:N}"[..20],
                        PasswordHash = "hash",
                        RoleName = UserRole.Teacher,
                        DisplayName = "Teacher Extra",
                        Status = UserStatus.Active,
                        AuthVersion = 1,
                        CreatedAt = UtcNow,
                        UpdatedAt = UtcNow,
                        RowVersion = 1
                    });

                    context.Teachers.Add(new Teacher
                    {
                        TeacherId = extraTeacherId,
                        CenterId = centerId,
                        Department = "Mathematics",
                        CreatedAt = UtcNow,
                        UpdatedAt = UtcNow
                    });
                }
            }

            context.Subjects.Add(new Subject
            {
                SubjectId = subjectId,
                CenterId = centerId,
                SubjectCode = $"SUB-{subjectId:N}"[..10],
                SubjectName = "Mathematics",
                IsActive = true,
                CreatedAt = UtcNow,
                UpdatedAt = UtcNow
            });

            context.Classes.Add(new Class
            {
                ClassId = classId,
                CenterId = centerId,
                SubjectId = subjectId,
                TeacherId = teacherId,
                ClassName = "Class 12A",
                AcademicYear = "2026",
                Status = ClassStatus.Active,
                CreatedAt = UtcNow,
                UpdatedAt = UtcNow
            });

            foreach (var studentId in studentIds)
            {
                context.Users.Add(new User
                {
                    UserId = studentId,
                    CenterId = centerId,
                    Username = $"student-{studentId:N}"[..20],
                    PasswordHash = "hash",
                    RoleName = UserRole.Student,
                    DisplayName = $"Student {studentId:N}"[..15],
                    Status = UserStatus.Active,
                    AuthVersion = 1,
                    CreatedAt = UtcNow,
                    UpdatedAt = UtcNow,
                    RowVersion = 1
                });

                context.Students.Add(new Student
                {
                    StudentId = studentId,
                    CenterId = centerId,
                    FullName = $"Student {studentId:N}"[..15],
                    GradeLevel = 12,
                    CreatedAt = UtcNow,
                    UpdatedAt = UtcNow
                });

                context.ClassStudents.Add(new ClassStudent
                {
                    CenterId = centerId,
                    ClassId = classId,
                    StudentId = studentId,
                    Status = ClassStudentStatus.Active,
                    JoinedAt = UtcNow
                });
            }

            // Topic 1: importance 60, Topic 2: importance 40
            context.KnowledgeNodes.AddRange(
                new KnowledgeNode
                {
                    CenterId = centerId,
                    SubjectId = subjectId,
                    NodeId = baseNodeId,
                    NodeCode = $"TOPIC-{baseNodeId}",
                    NodeName = "Topic One",
                    NodeType = NodeType.Topic,
                    OrderIndex = 1,
                    ExamImportance = 60m,
                    EstimatedLearningMinutes = 60,
                    IsActive = true,
                    CreatedAt = UtcNow,
                    UpdatedAt = UtcNow
                },
                new KnowledgeNode
                {
                    CenterId = centerId,
                    SubjectId = subjectId,
                    NodeId = baseNodeId + 1,
                    NodeCode = $"TOPIC-{baseNodeId + 1}",
                    NodeName = "Topic Two",
                    NodeType = NodeType.Topic,
                    OrderIndex = 2,
                    ExamImportance = 40m,
                    EstimatedLearningMinutes = 60,
                    IsActive = true,
                    CreatedAt = UtcNow,
                    UpdatedAt = UtcNow
                }
            );

            await context.SaveChangesAsync();
        }
        finally
        {
            await context.Database.ExecuteSqlRawAsync("SET FOREIGN_KEY_CHECKS = 1;");
        }
    }

    private static EduTwinDbContext CreateContext(string connectionString, ITenantIdAccessor tenant)
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseMySQL(connectionString);
        return new EduTwinDbContext(options.Options, tenant);
    }

    private sealed class MySqlIntegrationFactAttribute : FactAttribute
    {
        public MySqlIntegrationFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(AdminConnectionVariable)))
            {
                Skip = $"Set {AdminConnectionVariable} to run MySQL integration tests.";
            }
        }
    }

    private sealed class MySqlTestDatabase : IAsyncDisposable
    {
        private readonly string _adminConnectionString;
        private readonly string _databaseName;

        private MySqlTestDatabase(string adminConnectionString, string databaseName, string connectionString)
        {
            _adminConnectionString = adminConnectionString;
            _databaseName = databaseName;
            ConnectionString = connectionString;
        }

        public string ConnectionString { get; }

        public static async Task<MySqlTestDatabase> CreateAsync()
        {
            var configuredConnection = Environment.GetEnvironmentVariable(AdminConnectionVariable)
                ?? throw new InvalidOperationException($"{AdminConnectionVariable} is required.");
            var adminBuilder = new MySqlConnectionStringBuilder(configuredConnection)
            {
                Database = string.Empty,
                Pooling = false
            };
            var databaseName = $"edutwin_dash_{Guid.NewGuid():N}";
            await using (var connection = new MySqlConnection(adminBuilder.ConnectionString))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = $"CREATE DATABASE `{databaseName}` CHARACTER SET utf8mb4;";
                await command.ExecuteNonQueryAsync();
            }

            var databaseBuilder = new MySqlConnectionStringBuilder(adminBuilder.ConnectionString)
            {
                Database = databaseName,
                Pooling = false
            };
            var database = new MySqlTestDatabase(
                adminBuilder.ConnectionString,
                databaseName,
                databaseBuilder.ConnectionString);
            try
            {
                var tenant = new TestTenantContext();
                await using var context = CreateContext(database.ConnectionString, tenant);
                await context.Database.MigrateAsync();
                return database;
            }
            catch
            {
                await database.DisposeAsync();
                throw;
            }
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                await using var connection = new MySqlConnection(_adminConnectionString);
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = $"DROP DATABASE IF EXISTS `{_databaseName}`;";
                await command.ExecuteNonQueryAsync();
            }
            catch
            {
                // Best-effort cleanup
            }
        }
    }
}
