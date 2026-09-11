using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.AssessmentAndReasoning.Feedback;
using EduTwin.BLL.Dashboards;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.Dashboards;
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

public sealed class DashboardBoundaryUnitTests
{
    private static readonly DateTime UtcNow = new(2026, 9, 11, 10, 0, 0, DateTimeKind.Utc);

    private sealed class TestTenantContext : ITenantContext, ITenantIdAccessor
    {
        public Guid? CenterId { get; set; }
        public Guid? UserId { get; set; }
        public string? Role { get; set; }
        public uint? AuthVersion { get; set; } = 1;
        public bool IsResolved => CenterId.HasValue && CenterId.Value != Guid.Empty;
    }

    private static Center CreateCenter(Guid centerId, string name = "Test Center")
    {
        return new Center
        {
            CenterId = centerId,
            CenterCode = $"C-{centerId:N}"[..10],
            CenterName = name,
            Timezone = "Asia/Ho_Chi_Minh",
            CreatedAt = UtcNow,
            UpdatedAt = UtcNow
        };
    }

    private static (EduTwinDbContext DbContext, TestTenantContext TenantContext) CreateDbContext(string dbName)
    {
        var tenantContext = new TestTenantContext();
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var dbContext = new EduTwinDbContext(options, tenantContext);
        return (dbContext, tenantContext);
    }

    [Fact]
    public async Task StudentDashboard_EmptyState_NoGoal_NoRecommendation_HandledSafely()
    {
        var dbName = $"StudentDashboard_EmptyState_{Guid.NewGuid():N}";
        var (dbContext, tenantContext) = CreateDbContext(dbName);

        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        tenantContext.CenterId = centerId;
        tenantContext.UserId = studentId;
        tenantContext.Role = nameof(UserRole.Student);

        var center = CreateCenter(centerId);
        var user = new User { UserId = studentId, CenterId = centerId, Username = "std1", DisplayName = "Student One", PasswordHash = "h", RoleName = UserRole.Student, Status = UserStatus.Active, CreatedAt = UtcNow, UpdatedAt = UtcNow };
        var student = new Student { StudentId = studentId, CenterId = centerId, FullName = "Student One", GradeLevel = 10, CreatedAt = UtcNow, UpdatedAt = UtcNow };
        var subject = new Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "MATH", SubjectName = "Toan Hoc", IsActive = true, CreatedAt = UtcNow, UpdatedAt = UtcNow };

        dbContext.Centers.Add(center);
        dbContext.Users.Add(user);
        dbContext.Students.Add(student);
        dbContext.Subjects.Add(subject);
        await dbContext.SaveChangesAsync();

        var useCase = new GetStudentDashboardUseCase(dbContext, tenantContext, TimeProvider.System);
        var result = await useCase.ExecuteAsync(subjectId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(0m, result.Data.Goal.CurrentPredictedScore);
        Assert.Equal(0m, result.Data.Goal.RiskScore);
        Assert.Equal(0m, result.Data.Goal.TargetScore);
        Assert.Null(result.Data.Action);
        Assert.Empty(result.Data.MasteryRadar);
        Assert.Empty(result.Data.ProgressLine);
    }

    [Fact]
    public async Task StudentDashboard_ProgressLineReconstruction_SameCreatedAt_OrderedDeterministicallyByHistoryId()
    {
        var dbName = $"StudentDashboard_TieBreak_{Guid.NewGuid():N}";
        var (dbContext, tenantContext) = CreateDbContext(dbName);

        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        tenantContext.CenterId = centerId;
        tenantContext.UserId = studentId;
        tenantContext.Role = nameof(UserRole.Student);

        var center = CreateCenter(centerId);
        var user = new User { UserId = studentId, CenterId = centerId, Username = "std", DisplayName = "Student", PasswordHash = "h", RoleName = UserRole.Student, Status = UserStatus.Active, CreatedAt = UtcNow, UpdatedAt = UtcNow };
        var student = new Student { StudentId = studentId, CenterId = centerId, FullName = "Student", GradeLevel = 10, CreatedAt = UtcNow, UpdatedAt = UtcNow };
        var subject = new Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "PHY", SubjectName = "Vat Ly", IsActive = true, CreatedAt = UtcNow, UpdatedAt = UtcNow };

        var node1 = new KnowledgeNode { NodeId = 101, CenterId = centerId, SubjectId = subjectId, NodeCode = "TOPIC-101", NodeName = "Topic 1", NodeType = NodeType.Topic, OrderIndex = 1, ExamImportance = 2.0m, EstimatedLearningMinutes = 60, IsActive = true, CreatedAt = UtcNow, UpdatedAt = UtcNow };
        var node2 = new KnowledgeNode { NodeId = 102, CenterId = centerId, SubjectId = subjectId, NodeCode = "TOPIC-102", NodeName = "Topic 2", NodeType = NodeType.Topic, OrderIndex = 2, ExamImportance = 1.0m, EstimatedLearningMinutes = 60, IsActive = true, CreatedAt = UtcNow, UpdatedAt = UtcNow };

        dbContext.Centers.Add(center);
        dbContext.Users.Add(user);
        dbContext.Students.Add(student);
        dbContext.Subjects.Add(subject);
        dbContext.KnowledgeNodes.AddRange(node1, node2);

        using var emptyJson = JsonDocument.Parse("{}");

        // Add 2 history events with IDENTICAL CreatedAt but different HistoryId
        // HistoryId 200 should be applied AFTER HistoryId 100
        var identicalTimestamp = UtcNow;
        var history1 = new TwinUpdateHistory
        {
            HistoryId = 100,
            CenterId = centerId,
            StudentId = studentId,
            SubjectId = subjectId,
            TopicNodeId = 101,
            PreviousMastery = 0m,
            NewMastery = 50m,
            MasteryDelta = 50m,
            EventSource = TwinEventSource.AIAnalysis,
            CalculationVersion = "v1",
            CalculationBreakdown = JsonDocument.Parse("{}"),
            Explanation = "Attempt 1",
            CreatedAt = identicalTimestamp
        };
        var history2 = new TwinUpdateHistory
        {
            HistoryId = 200,
            CenterId = centerId,
            StudentId = studentId,
            SubjectId = subjectId,
            TopicNodeId = 102,
            PreviousMastery = 0m,
            NewMastery = 80m,
            MasteryDelta = 80m,
            EventSource = TwinEventSource.AIAnalysis,
            CalculationVersion = "v1",
            CalculationBreakdown = JsonDocument.Parse("{}"),
            Explanation = "Attempt 2",
            CreatedAt = identicalTimestamp
        };

        dbContext.TwinUpdateHistories.AddRange(history1, history2);
        await dbContext.SaveChangesAsync();

        var useCase = new GetStudentDashboardUseCase(dbContext, tenantContext, TimeProvider.System);
        var result = await useCase.ExecuteAsync(subjectId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(2, result.Data.ProgressLine.Count);

        // Point 1: Node 1 = 50% (weight 2), Node 2 = 0% (weight 1). Total = (50*2 + 0*1)/3 = 33.33%
        Assert.Equal(Math.Round(100m / 3m, 2), result.Data.ProgressLine[0].OverallSubjectMastery);

        // Point 2: Node 1 = 50% (weight 2), Node 2 = 80% (weight 1). Total = (50*2 + 80*1)/3 = 180/3 = 60.0%
        Assert.Equal(60.0m, result.Data.ProgressLine[1].OverallSubjectMastery);
    }

    [Fact]
    public async Task StudentDashboard_ZeroExamImportance_DoesNotDivideByZero()
    {
        var dbName = $"StudentDashboard_ZeroImportance_{Guid.NewGuid():N}";
        var (dbContext, tenantContext) = CreateDbContext(dbName);

        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        tenantContext.CenterId = centerId;
        tenantContext.UserId = studentId;
        tenantContext.Role = nameof(UserRole.Student);

        var center = CreateCenter(centerId);
        var user = new User { UserId = studentId, CenterId = centerId, Username = "u", DisplayName = "U", PasswordHash = "h", RoleName = UserRole.Student, Status = UserStatus.Active, CreatedAt = UtcNow, UpdatedAt = UtcNow };
        var student = new Student { StudentId = studentId, CenterId = centerId, FullName = "U", GradeLevel = 10, CreatedAt = UtcNow, UpdatedAt = UtcNow };
        var subject = new Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "S", SubjectName = "S", IsActive = true, CreatedAt = UtcNow, UpdatedAt = UtcNow };
        var node = new KnowledgeNode { NodeId = 1, CenterId = centerId, SubjectId = subjectId, NodeCode = "N-1", NodeName = "Zero Weight Node", NodeType = NodeType.Topic, OrderIndex = 1, ExamImportance = 0.0m, EstimatedLearningMinutes = 60, IsActive = true, CreatedAt = UtcNow, UpdatedAt = UtcNow };

        dbContext.Centers.Add(center);
        dbContext.Users.Add(user);
        dbContext.Students.Add(student);
        dbContext.Subjects.Add(subject);
        dbContext.KnowledgeNodes.Add(node);

        var history = new TwinUpdateHistory
        {
            HistoryId = 1,
            CenterId = centerId,
            StudentId = studentId,
            SubjectId = subjectId,
            TopicNodeId = 1,
            PreviousMastery = 0m,
            NewMastery = 100m,
            MasteryDelta = 100m,
            EventSource = TwinEventSource.AIAnalysis,
            CalculationVersion = "v1",
            CalculationBreakdown = JsonDocument.Parse("{}"),
            Explanation = "Attempt",
            CreatedAt = UtcNow
        };
        dbContext.TwinUpdateHistories.Add(history);
        await dbContext.SaveChangesAsync();

        var useCase = new GetStudentDashboardUseCase(dbContext, tenantContext, TimeProvider.System);
        var result = await useCase.ExecuteAsync(subjectId, CancellationToken.None);

        Assert.True(result.IsSuccess);
        // Does not throw DivideByZeroException, returns empty progress line when total weight is zero
        Assert.Empty(result.Data!.ProgressLine);
    }

    [Theory]
    [InlineData(70.0, true)]
    [InlineData(69.9, false)]
    [InlineData(85.5, true)]
    public async Task ClassDashboard_RiskThresholdBoundary_Exact70_IsHighRisk_69_9_IsNot(decimal riskScore, bool expectedHighRisk)
    {
        var dbName = $"ClassDashboard_RiskThreshold_{Guid.NewGuid():N}";
        var (dbContext, tenantContext) = CreateDbContext(dbName);

        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        tenantContext.CenterId = centerId;
        tenantContext.UserId = teacherId;
        tenantContext.Role = nameof(UserRole.Teacher);

        var center = CreateCenter(centerId);
        var teacherUser = new User { UserId = teacherId, CenterId = centerId, Username = "t", DisplayName = "Teacher", PasswordHash = "h", RoleName = UserRole.Teacher, Status = UserStatus.Active, CreatedAt = UtcNow, UpdatedAt = UtcNow };
        var teacher = new Teacher { TeacherId = teacherId, CenterId = centerId, Department = "Math", CreatedAt = UtcNow, UpdatedAt = UtcNow };
        var studentUser = new User { UserId = studentId, CenterId = centerId, Username = "s", DisplayName = "Student", PasswordHash = "h", RoleName = UserRole.Student, Status = UserStatus.Active, CreatedAt = UtcNow, UpdatedAt = UtcNow };
        var student = new Student { StudentId = studentId, CenterId = centerId, FullName = "Student", GradeLevel = 10, CreatedAt = UtcNow, UpdatedAt = UtcNow };
        var subject = new Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "M", SubjectName = "M", IsActive = true, CreatedAt = UtcNow, UpdatedAt = UtcNow };
        var classEntity = new Class { ClassId = classId, CenterId = centerId, SubjectId = subjectId, TeacherId = teacherId, ClassName = "Class 10A", AcademicYear = "2026", Status = ClassStatus.Active, CreatedAt = UtcNow, UpdatedAt = UtcNow };
        var classStudent = new ClassStudent { ClassId = classId, StudentId = studentId, CenterId = centerId, Status = ClassStudentStatus.Active, JoinedAt = UtcNow };

        dbContext.Centers.Add(center);
        dbContext.Users.AddRange(teacherUser, studentUser);
        dbContext.Teachers.Add(teacher);
        dbContext.Students.Add(student);
        dbContext.Subjects.Add(subject);
        dbContext.Classes.Add(classEntity);
        dbContext.ClassStudents.Add(classStudent);

        // Student goal with specific risk score
        var goal = new StudentSubjectGoal
        {
            GoalId = 1UL,
            CenterId = centerId,
            StudentId = studentId,
            SubjectId = subjectId,
            TargetScore = 8.0m,
            CurrentPredictedScore = 5.0m,
            RiskScore = riskScore,
            RemainingDays = 30U,
            CreatedAt = UtcNow,
            UpdatedAt = UtcNow
        };
        dbContext.StudentSubjectGoals.Add(goal);
        await dbContext.SaveChangesAsync();

        var guard = new OrganizationOwnershipGuard(dbContext, tenantContext);
        var useCase = new GetClassDashboardUseCase(dbContext, tenantContext, guard, TimeProvider.System);
        var result = await useCase.ExecuteAsync(classId, 70m, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);

        if (expectedHighRisk)
        {
            Assert.Single(result.Data.HighRiskStudents);
            Assert.Equal(riskScore, result.Data.HighRiskStudents[0].RiskScore);
        }
        else
        {
            Assert.Empty(result.Data.HighRiskStudents);
        }
    }

    [Theory]
    [InlineData(60.0, false)]
    [InlineData(59.9, true)]
    [InlineData(45.0, true)]
    public async Task ClassDashboard_WeakTopicBoundary_Exact60_IsNotWeak_59_9_IsWeak(decimal topicMastery, bool expectedWeak)
    {
        var dbName = $"ClassDashboard_WeakTopic_{Guid.NewGuid():N}";
        var (dbContext, tenantContext) = CreateDbContext(dbName);

        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var curriculumId = Guid.NewGuid();

        tenantContext.CenterId = centerId;
        tenantContext.UserId = teacherId;
        tenantContext.Role = nameof(UserRole.Teacher);

        var center = CreateCenter(centerId);
        var teacherUser = new User { UserId = teacherId, CenterId = centerId, Username = "t", DisplayName = "T", PasswordHash = "h", RoleName = UserRole.Teacher, Status = UserStatus.Active, CreatedAt = UtcNow, UpdatedAt = UtcNow };
        var teacher = new Teacher { TeacherId = teacherId, CenterId = centerId, Department = "Math", CreatedAt = UtcNow, UpdatedAt = UtcNow };
        var studentUser = new User { UserId = studentId, CenterId = centerId, Username = "s", DisplayName = "S", PasswordHash = "h", RoleName = UserRole.Student, Status = UserStatus.Active, CreatedAt = UtcNow, UpdatedAt = UtcNow };
        var student = new Student { StudentId = studentId, CenterId = centerId, FullName = "S", GradeLevel = 10, CreatedAt = UtcNow, UpdatedAt = UtcNow };
        var subject = new Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "M", SubjectName = "M", IsActive = true, CreatedAt = UtcNow, UpdatedAt = UtcNow };
        var classEntity = new Class { ClassId = classId, CenterId = centerId, SubjectId = subjectId, TeacherId = teacherId, ClassName = "Class 10A", AcademicYear = "2026", Status = ClassStatus.Active, CreatedAt = UtcNow, UpdatedAt = UtcNow };
        var classStudent = new ClassStudent { ClassId = classId, StudentId = studentId, CenterId = centerId, Status = ClassStudentStatus.Active, JoinedAt = UtcNow };

        var node = new KnowledgeNode { NodeId = 10, CenterId = centerId, SubjectId = subjectId, NodeCode = "TOPIC-10", NodeName = "Topic 10", NodeType = NodeType.Topic, OrderIndex = 1, ExamImportance = 1.0m, EstimatedLearningMinutes = 60, IsActive = true, CreatedAt = UtcNow, UpdatedAt = UtcNow };
        var curriculum = new Curriculum { CurriculumId = curriculumId, CenterId = centerId, TeacherId = teacherId, SubjectId = subjectId, Title = "Curr", ReviewStatus = ReviewStatus.Published, CreatedAt = UtcNow, UpdatedAt = UtcNow };
        var curriculumNode = new CurriculumNode { CurriculumId = curriculumId, NodeId = 10, CenterId = centerId, OrderIndex = 1, CreatedAt = UtcNow };
        var curriculumClass = new CurriculumClass { CurriculumId = curriculumId, ClassId = classId, CenterId = centerId, AssignedAt = UtcNow, AssignedBy = teacherId };

        dbContext.Centers.Add(center);
        dbContext.Users.AddRange(teacherUser, studentUser);
        dbContext.Teachers.Add(teacher);
        dbContext.Students.Add(student);
        dbContext.Subjects.Add(subject);
        dbContext.Classes.Add(classEntity);
        dbContext.ClassStudents.Add(classStudent);
        dbContext.KnowledgeNodes.Add(node);
        dbContext.Curriculums.Add(curriculum);
        dbContext.CurriculumNodes.Add(curriculumNode);
        dbContext.CurriculumClasses.Add(curriculumClass);

        var kt = new KnowledgeTwin
        {
            KnowledgeTwinId = 1,
            CenterId = centerId,
            StudentId = studentId,
            SubjectId = subjectId,
            TopicNodeId = 10,
            MasteryPercentage = topicMastery,
            EvidenceCount = 1,
            CreatedAt = UtcNow,
            UpdatedAt = UtcNow
        };
        dbContext.KnowledgeTwins.Add(kt);
        await dbContext.SaveChangesAsync();

        var guard = new OrganizationOwnershipGuard(dbContext, tenantContext);
        var useCase = new GetClassDashboardUseCase(dbContext, tenantContext, guard, TimeProvider.System);
        var result = await useCase.ExecuteAsync(classId, 70m, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);

        if (expectedWeak)
        {
            Assert.Single(result.Data.WeakTopics);
            Assert.Equal(topicMastery, result.Data.WeakTopics[0].AverageMastery);
        }
        else
        {
            Assert.Empty(result.Data.WeakTopics);
        }
    }

    [Fact]
    public async Task ClassDashboard_ZeroFillMastery_MissingKnowledgeTwinsCountAsZeroInClassAverage()
    {
        var dbName = $"ClassDashboard_ZeroFill_{Guid.NewGuid():N}";
        var (dbContext, tenantContext) = CreateDbContext(dbName);

        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();
        var curriculumId = Guid.NewGuid();

        tenantContext.CenterId = centerId;
        tenantContext.UserId = teacherId;
        tenantContext.Role = nameof(UserRole.Teacher);

        var center = CreateCenter(centerId);
        var teacherUser = new User { UserId = teacherId, CenterId = centerId, Username = "t", DisplayName = "T", PasswordHash = "h", RoleName = UserRole.Teacher, Status = UserStatus.Active, CreatedAt = UtcNow, UpdatedAt = UtcNow };
        var teacher = new Teacher { TeacherId = teacherId, CenterId = centerId, Department = "Math", CreatedAt = UtcNow, UpdatedAt = UtcNow };
        var subject = new Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "M", SubjectName = "M", IsActive = true, CreatedAt = UtcNow, UpdatedAt = UtcNow };
        var classEntity = new Class { ClassId = classId, CenterId = centerId, SubjectId = subjectId, TeacherId = teacherId, ClassName = "Class 10A", AcademicYear = "2026", Status = ClassStatus.Active, CreatedAt = UtcNow, UpdatedAt = UtcNow };

        var node = new KnowledgeNode { NodeId = 1, CenterId = centerId, SubjectId = subjectId, NodeCode = "TOPIC-1", NodeName = "Topic 1", NodeType = NodeType.Topic, OrderIndex = 1, ExamImportance = 1.0m, EstimatedLearningMinutes = 60, IsActive = true, CreatedAt = UtcNow, UpdatedAt = UtcNow };
        var curriculum = new Curriculum { CurriculumId = curriculumId, CenterId = centerId, TeacherId = teacherId, SubjectId = subjectId, Title = "Curr", ReviewStatus = ReviewStatus.Published, CreatedAt = UtcNow, UpdatedAt = UtcNow };
        var curriculumNode = new CurriculumNode { CurriculumId = curriculumId, NodeId = 1, CenterId = centerId, OrderIndex = 1, CreatedAt = UtcNow };
        var curriculumClass = new CurriculumClass { CurriculumId = curriculumId, ClassId = classId, CenterId = centerId, AssignedAt = UtcNow, AssignedBy = teacherId };

        dbContext.Centers.Add(center);
        dbContext.Users.Add(teacherUser);
        dbContext.Teachers.Add(teacher);
        dbContext.Subjects.Add(subject);
        dbContext.Classes.Add(classEntity);
        dbContext.KnowledgeNodes.Add(node);
        dbContext.Curriculums.Add(curriculum);
        dbContext.CurriculumNodes.Add(curriculumNode);
        dbContext.CurriculumClasses.Add(curriculumClass);

        // Add 2 students to class: Student 1 has mastery = 80%, Student 2 has NO KnowledgeTwin (uninitialized)
        var s1Id = Guid.NewGuid();
        var s2Id = Guid.NewGuid();

        dbContext.Users.AddRange(
            new User { UserId = s1Id, CenterId = centerId, Username = "s1", DisplayName = "S1", PasswordHash = "h", RoleName = UserRole.Student, Status = UserStatus.Active, CreatedAt = UtcNow, UpdatedAt = UtcNow },
            new User { UserId = s2Id, CenterId = centerId, Username = "s2", DisplayName = "S2", PasswordHash = "h", RoleName = UserRole.Student, Status = UserStatus.Active, CreatedAt = UtcNow, UpdatedAt = UtcNow }
        );
        dbContext.Students.AddRange(
            new Student { StudentId = s1Id, CenterId = centerId, FullName = "S1", GradeLevel = 10, CreatedAt = UtcNow, UpdatedAt = UtcNow },
            new Student { StudentId = s2Id, CenterId = centerId, FullName = "S2", GradeLevel = 10, CreatedAt = UtcNow, UpdatedAt = UtcNow }
        );
        dbContext.ClassStudents.AddRange(
            new ClassStudent { ClassId = classId, StudentId = s1Id, CenterId = centerId, Status = ClassStudentStatus.Active, JoinedAt = UtcNow },
            new ClassStudent { ClassId = classId, StudentId = s2Id, CenterId = centerId, Status = ClassStudentStatus.Active, JoinedAt = UtcNow }
        );

        // Only S1 has knowledge twin
        dbContext.KnowledgeTwins.Add(new KnowledgeTwin
        {
            KnowledgeTwinId = 1,
            CenterId = centerId,
            StudentId = s1Id,
            SubjectId = subjectId,
            TopicNodeId = 1,
            MasteryPercentage = 80.0m,
            EvidenceCount = 1,
            CreatedAt = UtcNow,
            UpdatedAt = UtcNow
        });
        await dbContext.SaveChangesAsync();

        var guard = new OrganizationOwnershipGuard(dbContext, tenantContext);
        var useCase = new GetClassDashboardUseCase(dbContext, tenantContext, guard, TimeProvider.System);
        var result = await useCase.ExecuteAsync(classId, 70m, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);

        // Class average mastery must include both S1 (80%) and S2 (0% zero-filled). (80 + 0) / 2 = 40.0%
        Assert.Equal(40.0m, result.Data.Overview.AverageMastery);
    }

    [Fact]
    public async Task ClassDashboard_ZeroStudents_ReturnsZeroAverageMasteryWithoutThrowing()
    {
        var dbName = $"ClassDashboard_ZeroStudents_{Guid.NewGuid():N}";
        var (dbContext, tenantContext) = CreateDbContext(dbName);

        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        tenantContext.CenterId = centerId;
        tenantContext.UserId = teacherId;
        tenantContext.Role = nameof(UserRole.Teacher);

        var center = CreateCenter(centerId);
        var teacherUser = new User { UserId = teacherId, CenterId = centerId, Username = "t", DisplayName = "T", PasswordHash = "h", RoleName = UserRole.Teacher, Status = UserStatus.Active, CreatedAt = UtcNow, UpdatedAt = UtcNow };
        var teacher = new Teacher { TeacherId = teacherId, CenterId = centerId, Department = "Math", CreatedAt = UtcNow, UpdatedAt = UtcNow };
        var subject = new Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "M", SubjectName = "M", IsActive = true, CreatedAt = UtcNow, UpdatedAt = UtcNow };
        var classEntity = new Class { ClassId = classId, CenterId = centerId, SubjectId = subjectId, TeacherId = teacherId, ClassName = "Empty Class", AcademicYear = "2026", Status = ClassStatus.Active, CreatedAt = UtcNow, UpdatedAt = UtcNow };

        dbContext.Centers.Add(center);
        dbContext.Users.Add(teacherUser);
        dbContext.Teachers.Add(teacher);
        dbContext.Subjects.Add(subject);
        dbContext.Classes.Add(classEntity);
        await dbContext.SaveChangesAsync();

        var guard = new OrganizationOwnershipGuard(dbContext, tenantContext);
        var useCase = new GetClassDashboardUseCase(dbContext, tenantContext, guard, TimeProvider.System);
        var result = await useCase.ExecuteAsync(classId, 70m, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(0, result.Data.Overview.StudentCount);
        Assert.Equal(0.0m, result.Data.Overview.AverageMastery);
        Assert.Equal(0.0m, result.Data.Overview.AssignmentCompletionRate);
    }

    [Fact]
    public async Task ClassDashboard_ZeroPublishedAssignments_CompletionRateIsZeroWithoutThrowing()
    {
        var dbName = $"ClassDashboard_ZeroAssignments_{Guid.NewGuid():N}";
        var (dbContext, tenantContext) = CreateDbContext(dbName);

        var centerId = Guid.NewGuid();
        var teacherId = Guid.NewGuid();
        var classId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        tenantContext.CenterId = centerId;
        tenantContext.UserId = teacherId;
        tenantContext.Role = nameof(UserRole.Teacher);

        var center = CreateCenter(centerId);
        var teacherUser = new User { UserId = teacherId, CenterId = centerId, Username = "t", DisplayName = "T", PasswordHash = "h", RoleName = UserRole.Teacher, Status = UserStatus.Active, CreatedAt = UtcNow, UpdatedAt = UtcNow };
        var teacher = new Teacher { TeacherId = teacherId, CenterId = centerId, Department = "Math", CreatedAt = UtcNow, UpdatedAt = UtcNow };
        var subject = new Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "M", SubjectName = "M", IsActive = true, CreatedAt = UtcNow, UpdatedAt = UtcNow };
        var classEntity = new Class { ClassId = classId, CenterId = centerId, SubjectId = subjectId, TeacherId = teacherId, ClassName = "Class 10A", AcademicYear = "2026", Status = ClassStatus.Active, CreatedAt = UtcNow, UpdatedAt = UtcNow };

        dbContext.Centers.Add(center);
        dbContext.Users.Add(teacherUser);
        dbContext.Teachers.Add(teacher);
        dbContext.Subjects.Add(subject);
        dbContext.Classes.Add(classEntity);
        await dbContext.SaveChangesAsync();

        var guard = new OrganizationOwnershipGuard(dbContext, tenantContext);
        var useCase = new GetClassDashboardUseCase(dbContext, tenantContext, guard, TimeProvider.System);
        var result = await useCase.ExecuteAsync(classId, 70m, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(0.0m, result.Data!.Overview.AssignmentCompletionRate);
    }

    [Fact]
    public async Task CenterDashboard_ZeroClasses_ReturnsEmptyRankingSafely()
    {
        var dbName = $"CenterDashboard_ZeroClasses_{Guid.NewGuid():N}";
        var (dbContext, tenantContext) = CreateDbContext(dbName);

        var centerId = Guid.NewGuid();
        var managerUserId = Guid.NewGuid();

        tenantContext.CenterId = centerId;
        tenantContext.UserId = managerUserId;
        tenantContext.Role = nameof(UserRole.CenterManager);

        var center = CreateCenter(centerId);
        var managerUser = new User { UserId = managerUserId, CenterId = centerId, Username = "mgr", DisplayName = "Manager", PasswordHash = "h", RoleName = UserRole.CenterManager, Status = UserStatus.Active, CreatedAt = UtcNow, UpdatedAt = UtcNow };

        dbContext.Centers.Add(center);
        dbContext.Users.Add(managerUser);
        await dbContext.SaveChangesAsync();

        var useCase = new GetCenterDashboardUseCase(dbContext, tenantContext, TimeProvider.System);
        var result = await useCase.ExecuteAsync(null, 70m, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Equal(0, result.Data.Summary.ClassCount);
        Assert.Equal(0, result.Data.Summary.StudentCount);
        Assert.Equal(0, result.Data.Summary.TeacherCount);
        Assert.Empty(result.Data.ClassRanking);
        Assert.Empty(result.Data.MasteryBySubject);
        Assert.Empty(result.Data.HighRiskByClass);
    }

    [Fact]
    public async Task AttemptFeedback_EssayQuestion_PreservesNullableCorrectness()
    {
        var dbName = $"Feedback_NullableEssay_{Guid.NewGuid():N}";
        var (dbContext, tenantContext) = CreateDbContext(dbName);

        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var subjectId = Guid.NewGuid();

        tenantContext.CenterId = centerId;
        tenantContext.UserId = studentId;
        tenantContext.Role = nameof(UserRole.Student);

        var center = CreateCenter(centerId);
        var studentUser = new User { UserId = studentId, CenterId = centerId, Username = "s", DisplayName = "Student", PasswordHash = "h", RoleName = UserRole.Student, Status = UserStatus.Active, CreatedAt = UtcNow, UpdatedAt = UtcNow };
        var student = new Student { StudentId = studentId, CenterId = centerId, FullName = "Student", GradeLevel = 10, CreatedAt = UtcNow, UpdatedAt = UtcNow };
        var subject = new Subject { SubjectId = subjectId, CenterId = centerId, SubjectCode = "M", SubjectName = "M", IsActive = true, CreatedAt = UtcNow, UpdatedAt = UtcNow };
        var node = new KnowledgeNode { NodeId = 1, CenterId = centerId, SubjectId = subjectId, NodeCode = "N-1", NodeName = "N1", NodeType = NodeType.Topic, OrderIndex = 1, ExamImportance = 1.0m, EstimatedLearningMinutes = 60, IsActive = true, CreatedAt = UtcNow, UpdatedAt = UtcNow };
        var question = new Question { QuestionId = 10, CenterId = centerId, SubjectId = subjectId, PrimaryTopicNodeId = 1, QuestionText = "Solve...", CorrectAnswer = "42", Solution = "Step 1", LanguageCode = "vi", QuestionType = QuestionType.Essay, MaxScore = 10m, CreatedAt = UtcNow, UpdatedAt = UtcNow };

        var attempt = new Attempt
        {
            AttemptId = 555,
            CenterId = centerId,
            StudentId = studentId,
            QuestionId = 10,
            FinalAnswer = "Draft Essay Answer",
            TimeSpentSeconds = 120,
            Confidence = 80,
            ReasoningLanguage = "vi",
            Status = AttemptStatus.PendingAnalysis,
            IsCorrect = null, // Essay has null correctness prior to teacher grading
            AwardedScore = null,
            CreatedAt = UtcNow,
            UpdatedAt = UtcNow
        };

        dbContext.Centers.Add(center);
        dbContext.Users.Add(studentUser);
        dbContext.Students.Add(student);
        dbContext.Subjects.Add(subject);
        dbContext.KnowledgeNodes.Add(node);
        dbContext.Questions.Add(question);
        dbContext.Attempts.Add(attempt);
        await dbContext.SaveChangesAsync();

        var guard = new OrganizationOwnershipGuard(dbContext, tenantContext);
        var useCase = new GetAttemptFeedbackUseCase(dbContext, tenantContext, guard);
        var result = await useCase.ExecuteAsync(555, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.NotNull(result.Data);
        Assert.Null(result.Data.Grading.IsCorrect);
        Assert.Null(result.Data.Grading.AwardedScore);
        Assert.Equal(10, result.Data.Grading.MaxScore);
    }
}
