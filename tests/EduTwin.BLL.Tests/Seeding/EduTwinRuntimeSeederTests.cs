using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Query;
using Moq;

using Xunit;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Organization;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.DigitalTwin;
using EduTwin.BLL.Seeding;
using EduTwin.DAL.Seeding;

namespace EduTwin.BLL.Tests.Seeding;

internal class TestAsyncQueryProvider<TEntity> : IAsyncQueryProvider
{
    private readonly IQueryProvider _inner;
    internal TestAsyncQueryProvider(IQueryProvider inner) => _inner = inner;

    public IQueryable CreateQuery(Expression expression)
        => new TestAsyncEnumerable<TEntity>(expression);

    public IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        => new TestAsyncEnumerable<TElement>(expression);

    public object? Execute(Expression expression)
        => _inner.Execute(expression);

    public TResult Execute<TResult>(Expression expression)
        => _inner.Execute<TResult>(expression);

    public TResult ExecuteAsync<TResult>(Expression expression, CancellationToken cancellationToken)
    {
        var expectedResultType = typeof(TResult).GetGenericArguments()[0];
        var executionResult = typeof(IQueryProvider)
            .GetMethod(
                name: nameof(IQueryProvider.Execute),
                genericParameterCount: 1,
                types: new[] { typeof(Expression) })!
            .MakeGenericMethod(expectedResultType)
            .Invoke(this, new[] { expression });

        return (TResult)typeof(Task).GetMethod(nameof(Task.FromResult))!
            .MakeGenericMethod(expectedResultType)
            .Invoke(null, new[] { executionResult })!;
    }
}

internal class TestAsyncEnumerable<T> : EnumerableQuery<T>, IAsyncEnumerable<T>, IQueryable<T>
{
    public TestAsyncEnumerable(IEnumerable<T> enumerable) : base(enumerable) { }
    public TestAsyncEnumerable(Expression expression) : base(expression) { }

    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default)
        => new TestAsyncEnumerator<T>(this.AsEnumerable().GetEnumerator());

    IQueryProvider IQueryable.Provider => new TestAsyncQueryProvider<T>(this);
}

internal class TestAsyncEnumerator<T> : IAsyncEnumerator<T>
{
    private readonly IEnumerator<T> _inner;
    public TestAsyncEnumerator(IEnumerator<T> inner) => _inner = inner;
    public ValueTask DisposeAsync() { _inner.Dispose(); return new ValueTask(); }
    public ValueTask<bool> MoveNextAsync() => new ValueTask<bool>(_inner.MoveNext());
    public T Current => _inner.Current;
}

public class EduTwinRuntimeSeederTests
{
    private static Mock<DbSet<T>> MockDbSet<T>(List<T> data) where T : class
    {
        var queryable = data.AsQueryable();
        var mockSet = new Mock<DbSet<T>>();
        mockSet.As<IAsyncEnumerable<T>>().Setup(m => m.GetAsyncEnumerator(It.IsAny<CancellationToken>())).Returns(new TestAsyncEnumerator<T>(queryable.GetEnumerator()));
        mockSet.As<IQueryable<T>>().Setup(m => m.Provider).Returns(new TestAsyncQueryProvider<T>(queryable.Provider));
        mockSet.As<IQueryable<T>>().Setup(m => m.Expression).Returns(queryable.Expression);
        mockSet.As<IQueryable<T>>().Setup(m => m.ElementType).Returns(queryable.ElementType);
        mockSet.As<IQueryable<T>>().Setup(m => m.GetEnumerator()).Returns(queryable.GetEnumerator());
        return mockSet;
    }

    private static EduTwinDbContext CreateMockContext(SeedDataContainer? dataA = null, SeedDataContainer? dataB = null, bool includeThirdTenant = false)
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>().Options;
        var mockContext = new Mock<EduTwinDbContext>(options);

        var centers = new List<Center>();
        var users = new List<User>();
        var teachers = new List<Teacher>();
        var students = new List<Student>();
        var subjects = new List<Subject>();
        var classes = new List<Class>();
        var classStudents = new List<ClassStudent>();
        var topics = new List<KnowledgeNode>();
        var edges = new List<KnowledgeEdge>();
        var curriculums = new List<Curriculum>();
        var curriculumClasses = new List<CurriculumClass>();
        var curriculumNodes = new List<CurriculumNode>();
        var questions = new List<Question>();
        var qOptions = new List<QuestionOption>();
        var qNodes = new List<QuestionKnowledgeNode>();
        var goals = new List<StudentSubjectGoal>();

        if (dataA != null)
        {
            centers.Add(dataA.Center);
            users.AddRange(dataA.Users);
            teachers.AddRange(dataA.Teachers);
            students.AddRange(dataA.Students);
            subjects.AddRange(dataA.Subjects);
            classes.AddRange(dataA.Classes);
            classStudents.AddRange(dataA.ClassStudents);
            topics.AddRange(dataA.Topics);
            edges.AddRange(dataA.Edges);
            curriculums.AddRange(dataA.Curriculums);
            curriculumClasses.AddRange(dataA.CurriculumClasses);
            curriculumNodes.AddRange(dataA.CurriculumNodes);
            questions.AddRange(dataA.Questions);
            qOptions.AddRange(dataA.QuestionOptions);
            qNodes.AddRange(dataA.QuestionNodes);
            goals.AddRange(dataA.Goals);
        }

        if (dataB != null)
        {
            centers.Add(dataB.Center);
            users.AddRange(dataB.Users);
            teachers.AddRange(dataB.Teachers);
            students.AddRange(dataB.Students);
            subjects.AddRange(dataB.Subjects);
            classes.AddRange(dataB.Classes);
            classStudents.AddRange(dataB.ClassStudents);
            topics.AddRange(dataB.Topics);
            edges.AddRange(dataB.Edges);
            curriculums.AddRange(dataB.Curriculums);
            curriculumClasses.AddRange(dataB.CurriculumClasses);
            curriculumNodes.AddRange(dataB.CurriculumNodes);
            questions.AddRange(dataB.Questions);
            qOptions.AddRange(dataB.QuestionOptions);
            qNodes.AddRange(dataB.QuestionNodes);
            goals.AddRange(dataB.Goals);
        }

        if (includeThirdTenant)
        {
            var thirdCenterId = Guid.NewGuid();
            centers.Add(new Center { CenterId = thirdCenterId, CenterCode = "C_TENANT", CenterName = "Third Tenant", Timezone = "UTC", Status = EduTwin.Contracts.Organization.CenterStatus.Active });
            users.Add(new User { CenterId = thirdCenterId, UserId = Guid.NewGuid(), Username = "c_user", RoleName = EduTwin.Contracts.IdentityAndTenancy.UserRole.Teacher, Status = EduTwin.Contracts.IdentityAndTenancy.UserStatus.Active });
        }

        mockContext.Setup(c => c.Set<Center>()).Returns(MockDbSet(centers).Object);
        mockContext.Setup(c => c.Set<User>()).Returns(MockDbSet(users).Object);
        mockContext.Setup(c => c.Set<Teacher>()).Returns(MockDbSet(teachers).Object);
        mockContext.Setup(c => c.Set<Student>()).Returns(MockDbSet(students).Object);
        mockContext.Setup(c => c.Set<Subject>()).Returns(MockDbSet(subjects).Object);
        mockContext.Setup(c => c.Set<Class>()).Returns(MockDbSet(classes).Object);
        mockContext.Setup(c => c.Set<ClassStudent>()).Returns(MockDbSet(classStudents).Object);
        mockContext.Setup(c => c.Set<KnowledgeNode>()).Returns(MockDbSet(topics).Object);
        mockContext.Setup(c => c.Set<KnowledgeEdge>()).Returns(MockDbSet(edges).Object);
        mockContext.Setup(c => c.Set<Curriculum>()).Returns(MockDbSet(curriculums).Object);
        mockContext.Setup(c => c.Set<CurriculumClass>()).Returns(MockDbSet(curriculumClasses).Object);
        mockContext.Setup(c => c.Set<CurriculumNode>()).Returns(MockDbSet(curriculumNodes).Object);
        mockContext.Setup(c => c.Set<Question>()).Returns(MockDbSet(questions).Object);
        mockContext.Setup(c => c.Set<QuestionOption>()).Returns(MockDbSet(qOptions).Object);
        mockContext.Setup(c => c.Set<QuestionKnowledgeNode>()).Returns(MockDbSet(qNodes).Object);
        mockContext.Setup(c => c.Set<StudentSubjectGoal>()).Returns(MockDbSet(goals).Object);

        return mockContext.Object;
    }

    [Fact]
    public async Task Manifest_Empty_ShouldReturnMissing()
    {
        var db = CreateMockContext();
        var evaluator = new ManifestEvaluator(db);
        var status = await evaluator.EvaluateTenantAsync(true);
        Assert.Equal(TenantSeedStatus.Missing, status);
    }

    [Fact]
    public async Task Manifest_Exact_ShouldReturnComplete()
    {
        var factory = new EduTwinSeedFactory(true);
        var db = CreateMockContext(dataA: factory.CreateData());
        var evaluator = new ManifestEvaluator(db);
        var status = await evaluator.EvaluateTenantAsync(true);
        Assert.Equal(TenantSeedStatus.Complete, status);
    }

    [Fact]
    public async Task Manifest_MissingOneExpectedId_ShouldReturnConflict()
    {
        var factory = new EduTwinSeedFactory(true);
        var data = factory.CreateData();
        data.Users.RemoveAt(0); // Introduce mismatch

        var db = CreateMockContext(dataA: data);
        var evaluator = new ManifestEvaluator(db);
        var status = await evaluator.EvaluateTenantAsync(true);
        Assert.Equal(TenantSeedStatus.Conflict, status);
    }

    [Fact]
    public async Task Manifest_WrongCenterCode_ShouldReturnConflict()
    {
        var factory = new EduTwinSeedFactory(true);
        var data = factory.CreateData();
        data.Center.CenterCode = "WRONG";

        var db = CreateMockContext(dataA: data);
        var evaluator = new ManifestEvaluator(db);
        var status = await evaluator.EvaluateTenantAsync(true);
        Assert.Equal(TenantSeedStatus.Conflict, status);
    }

    [Fact]
    public async Task Manifest_ThirdTenant_ShouldNotAffectAB()
    {
        var db = CreateMockContext(includeThirdTenant: true);
        var evaluator = new ManifestEvaluator(db);
        
        var thirdCenterExists = await db.Set<Center>().AnyAsync(c => c.CenterCode == "C_TENANT");
        Assert.True(thirdCenterExists); // Confirming third tenant is in the mock data

        var statusA = await evaluator.EvaluateTenantAsync(true);
        var statusB = await evaluator.EvaluateTenantAsync(false);
        
        Assert.Equal(TenantSeedStatus.Missing, statusA);
        Assert.Equal(TenantSeedStatus.Missing, statusB);
    }

    [Fact]
    public void SeedDataContainer_DoesNotExposeForbiddenCollections()
    {
        var factory = new EduTwinSeedFactory(true);
        var data = factory.CreateData();
        var properties = data.GetType().GetProperties();
        Assert.DoesNotContain(properties, p => p.PropertyType.Name.Contains("Attempt") || (p.PropertyType.Name.Contains("Twin") && !p.PropertyType.Name.Contains("StudentTwin")));
    }

    [Theory]
    [InlineData(TenantSeedStatus.Complete, TenantSeedStatus.Complete, 0, true, false, false)]
    [InlineData(TenantSeedStatus.Missing, TenantSeedStatus.Missing, 16, false, true, true)]
    [InlineData(TenantSeedStatus.Complete, TenantSeedStatus.Missing, 8, false, false, true)]
    [InlineData(TenantSeedStatus.Missing, TenantSeedStatus.Complete, 8, false, true, false)]
    [InlineData(TenantSeedStatus.Conflict, TenantSeedStatus.Missing, 0, false, false, false)]
    [InlineData(TenantSeedStatus.Missing, TenantSeedStatus.Conflict, 0, false, false, false)]
    public void SeedExecutionPlan_CreatesCorrectTruthTable(
        TenantSeedStatus statusA, TenantSeedStatus statusB, int expectedHashes, bool expectedNoOp, bool expectedSeedA, bool expectedSeedB)
    {
        var plan = SeedExecutionPlan.Create(statusA, statusB);
        Assert.Equal(expectedHashes, plan.ExpectedUsersToHash);
        Assert.Equal(expectedNoOp, plan.IsNoOp);
        Assert.Equal(expectedSeedA, plan.ShouldSeedA);
        Assert.Equal(expectedSeedB, plan.ShouldSeedB);
        Assert.Equal(statusA == TenantSeedStatus.Conflict || statusB == TenantSeedStatus.Conflict, plan.HasConflict);
    }

    [Fact]
    public async Task Manifest_WrongTeacherIdButCountCorrect_ShouldReturnConflict()
    {
        var factory = new EduTwinSeedFactory(true);
        var data = factory.CreateData();
        data.Teachers[0].TeacherId = Guid.NewGuid(); // Mutate teacher ID

        var db = CreateMockContext(dataA: data);
        var evaluator = new ManifestEvaluator(db);
        var status = await evaluator.EvaluateTenantAsync(true);
        Assert.Equal(TenantSeedStatus.Conflict, status);
    }

    [Fact]
    public async Task Manifest_ExtraRow_ShouldReturnConflict()
    {
        var factory = new EduTwinSeedFactory(true);
        var data = factory.CreateData();
        data.Classes.Add(new Class { ClassId = Guid.NewGuid(), CenterId = data.Center.CenterId, Status = EduTwin.Contracts.Organization.ClassStatus.Active }); // Add extra row

        var db = CreateMockContext(dataA: data);
        var evaluator = new ManifestEvaluator(db);
        var status = await evaluator.EvaluateTenantAsync(true);
        Assert.Equal(TenantSeedStatus.Conflict, status);
    }

    [Fact]
    public async Task Manifest_SoftDeletedRow_ShouldReturnConflict()
    {
        var factory = new EduTwinSeedFactory(true);
        var data = factory.CreateData();
        data.Students[0].IsDeleted = true; // Mutate to soft-deleted

        var db = CreateMockContext(dataA: data);
        var evaluator = new ManifestEvaluator(db);
        var status = await evaluator.EvaluateTenantAsync(true);
        Assert.Equal(TenantSeedStatus.Conflict, status);
    }

    [Fact]
    public async Task Manifest_CenterCodeCollision_ShouldReturnConflict()
    {
        var factory = new EduTwinSeedFactory(true);
        var data = factory.CreateData();
        
        // Setup a database where another center has taken Center A's code
        var db = CreateMockContext();
        var anotherCenter = new Center { CenterId = Guid.NewGuid(), CenterCode = data.Center.CenterCode, CenterName = "Imposter", Timezone = "UTC" };
        var centers = new List<Center> { anotherCenter };
        var mockContext = Mock.Get(db);
        mockContext.Setup(c => c.Set<Center>()).Returns(MockDbSet(centers).Object);

        var evaluator = new ManifestEvaluator(db);
        var status = await evaluator.EvaluateTenantAsync(true);
        Assert.Equal(TenantSeedStatus.Conflict, status);
    }

    [Fact]
    public async Task Manifest_SoftDeletedCenter_ShouldReturnConflict()
    {
        var factory = new EduTwinSeedFactory(true);
        var data = factory.CreateData();
        data.Center.IsDeleted = true; // Soft-delete the center itself

        var db = CreateMockContext(dataA: data);
        var evaluator = new ManifestEvaluator(db);
        var status = await evaluator.EvaluateTenantAsync(true);
        Assert.Equal(TenantSeedStatus.Conflict, status);
    }

    [Fact]
    public async Task Manifest_EdgeIdBelongingToAnotherTenant_ShouldReturnConflict()
    {
        var factoryA = new EduTwinSeedFactory(true);
        var dataA = factoryA.CreateData();
        
        var db = CreateMockContext();
        var mockContext = Mock.Get(db);
        var edge = dataA.Edges[0];
        // Create an edge with same EdgeId but different CenterId
        var edges = new List<KnowledgeEdge> { new KnowledgeEdge { EdgeId = edge.EdgeId, CenterId = Guid.NewGuid(), SourceNodeId = edge.SourceNodeId, TargetNodeId = edge.TargetNodeId } };
        mockContext.Setup(c => c.Set<KnowledgeEdge>()).Returns(MockDbSet(edges).Object);

        var evaluator = new ManifestEvaluator(db);
        var status = await evaluator.EvaluateTenantAsync(true);
        Assert.Equal(TenantSeedStatus.Conflict, status);
    }

    [Fact]
    public async Task Manifest_OptionIdBelongingToAnotherTenant_ShouldReturnConflict()
    {
        var factoryA = new EduTwinSeedFactory(true);
        var dataA = factoryA.CreateData();
        
        var db = CreateMockContext();
        var mockContext = Mock.Get(db);
        var option = dataA.QuestionOptions[0];
        var options = new List<QuestionOption> { new QuestionOption { OptionId = option.OptionId, CenterId = Guid.NewGuid(), QuestionId = option.QuestionId, OptionLabel = option.OptionLabel } };
        mockContext.Setup(c => c.Set<QuestionOption>()).Returns(MockDbSet(options).Object);

        var evaluator = new ManifestEvaluator(db);
        var status = await evaluator.EvaluateTenantAsync(true);
        Assert.Equal(TenantSeedStatus.Conflict, status);
    }

    [Fact]
    public async Task Manifest_GoalIdBelongingToAnotherTenant_ShouldReturnConflict()
    {
        var factoryA = new EduTwinSeedFactory(true);
        var dataA = factoryA.CreateData();
        
        var db = CreateMockContext();
        var mockContext = Mock.Get(db);
        var goal = dataA.Goals[0];
        var goals = new List<StudentSubjectGoal> { new StudentSubjectGoal { GoalId = goal.GoalId, CenterId = Guid.NewGuid(), StudentId = goal.StudentId, SubjectId = goal.SubjectId } };
        mockContext.Setup(c => c.Set<StudentSubjectGoal>()).Returns(MockDbSet(goals).Object);

        var evaluator = new ManifestEvaluator(db);
        var status = await evaluator.EvaluateTenantAsync(true);
        Assert.Equal(TenantSeedStatus.Conflict, status);
    }

    [Fact]
    public async Task Manifest_CenterAbsentButSubjectOrphan_ShouldReturnConflict()
    {
        var factoryA = new EduTwinSeedFactory(true);
        var dataA = factoryA.CreateData();
        
        var db = CreateMockContext();
        var mockContext = Mock.Get(db);
        // DB has no Center, but has a Subject belonging to Center A
        var subjects = new List<Subject> { new Subject { SubjectId = Guid.NewGuid(), CenterId = dataA.Center.CenterId, SubjectCode = "ORPHAN" } };
        mockContext.Setup(c => c.Set<Subject>()).Returns(MockDbSet(subjects).Object);

        var evaluator = new ManifestEvaluator(db);
        var status = await evaluator.EvaluateTenantAsync(true);
        Assert.Equal(TenantSeedStatus.Conflict, status);
    }

    [Fact]
    public async Task Manifest_CenterAbsentButGoalOrphan_ShouldReturnConflict()
    {
        var factoryA = new EduTwinSeedFactory(true);
        var dataA = factoryA.CreateData();
        
        var db = CreateMockContext();
        var mockContext = Mock.Get(db);
        // DB has no Center, but has a Goal belonging to Center A
        var goals = new List<StudentSubjectGoal> { new StudentSubjectGoal { GoalId = 999999UL, CenterId = dataA.Center.CenterId } };
        mockContext.Setup(c => c.Set<StudentSubjectGoal>()).Returns(MockDbSet(goals).Object);

        var evaluator = new ManifestEvaluator(db);
        var status = await evaluator.EvaluateTenantAsync(true);
        Assert.Equal(TenantSeedStatus.Conflict, status);
    }

    [Fact]
    public async Task Manifest_WrongClassIdButCountCorrect_ShouldReturnConflict()
    {
        var factory = new EduTwinSeedFactory(true);
        var data = factory.CreateData();
        data.Classes[0].ClassId = Guid.NewGuid(); // Mutate ClassId

        var db = CreateMockContext(dataA: data);
        var evaluator = new ManifestEvaluator(db);
        var status = await evaluator.EvaluateTenantAsync(true);
        Assert.Equal(TenantSeedStatus.Conflict, status);
    }

    [Fact]
    public async Task Manifest_WrongQuestionKnowledgeNodeButCountCorrect_ShouldReturnConflict()
    {
        var factory = new EduTwinSeedFactory(true);
        var data = factory.CreateData();
        data.QuestionNodes[0].NodeId = 999999UL; // Mutate composite key part

        var db = CreateMockContext(dataA: data);
        var evaluator = new ManifestEvaluator(db);
        var status = await evaluator.EvaluateTenantAsync(true);
        Assert.Equal(TenantSeedStatus.Conflict, status);
    }

    [Theory]
    [InlineData(TenantSeedStatus.Complete, TenantSeedStatus.Complete, 0)]
    [InlineData(TenantSeedStatus.Complete, TenantSeedStatus.Missing, 8)]
    [InlineData(TenantSeedStatus.Missing, TenantSeedStatus.Complete, 8)]
    [InlineData(TenantSeedStatus.Missing, TenantSeedStatus.Missing, 16)]
    [InlineData(TenantSeedStatus.Conflict, TenantSeedStatus.Missing, 0)]
    public void SeedPasswordApplicator_ShouldCallHasherCorrectly(TenantSeedStatus statusA, TenantSeedStatus statusB, int expectedCalls)
    {
        var plan = SeedExecutionPlan.Create(statusA, statusB);
        var mockHasher = new Mock<Microsoft.AspNetCore.Identity.IPasswordHasher<User>>();
        mockHasher.Setup(h => h.HashPassword(It.IsAny<User>(), It.IsAny<string>())).Returns("hashed");

        var factoryA = new EduTwinSeedFactory(true);
        var dataA = plan.ShouldSeedA ? factoryA.CreateData() : null;
        var factoryB = new EduTwinSeedFactory(false);
        var dataB = plan.ShouldSeedB ? factoryB.CreateData() : null;

        var applicator = new SeedPasswordApplicator(mockHasher.Object);
        applicator.ApplyHashes(plan, dataA, dataB, "password");

        mockHasher.Verify(h => h.HashPassword(It.IsAny<User>(), It.IsAny<string>()), Times.Exactly(expectedCalls));
    }

    [Fact]
    public async Task Manifest_WrongDateOfBirthButCountCorrect_ShouldReturnConflict()
    {
        var factory = new EduTwinSeedFactory(true);
        var data = factory.CreateData();
        data.Students[0].DateOfBirth = new DateOnly(1990, 1, 1); // Mutate DateOfBirth

        var db = CreateMockContext(dataA: data);
        var evaluator = new ManifestEvaluator(db);
        var status = await evaluator.EvaluateTenantAsync(true);
        Assert.Equal(TenantSeedStatus.Conflict, status);
    }

    [Fact]
    public async Task EduTwinRuntimeSeeder_SeedAsync_WhenConflict_ThrowsAndNoHashCalls()
    {
        var db = CreateMockContext();
        var mockHasher = new Mock<Microsoft.AspNetCore.Identity.IPasswordHasher<User>>();
        var mockEvaluator = new Mock<IManifestEvaluator>();
        mockEvaluator.Setup(e => e.EvaluateTenantAsync(true)).ReturnsAsync(TenantSeedStatus.Conflict);
        mockEvaluator.Setup(e => e.EvaluateTenantAsync(false)).ReturnsAsync(TenantSeedStatus.Missing);
        
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { { "Seed:CenterManagerPassword", "VerySecurePassword123" } }).Build();
        var logger = new Mock<ILogger<EduTwinRuntimeSeeder>>();
        
        var seeder = new EduTwinRuntimeSeeder(db, logger.Object, config, mockHasher.Object, mockEvaluator.Object);
        
        await Assert.ThrowsAsync<InvalidOperationException>(() => seeder.SeedAsync());
        
        mockHasher.Verify(h => h.HashPassword(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task EduTwinRuntimeSeeder_SeedAsync_WhenNoOp_ReturnsAndNoHashCalls()
    {
        var db = CreateMockContext();
        var mockHasher = new Mock<Microsoft.AspNetCore.Identity.IPasswordHasher<User>>();
        var mockEvaluator = new Mock<IManifestEvaluator>();
        mockEvaluator.Setup(e => e.EvaluateTenantAsync(It.IsAny<bool>())).ReturnsAsync(TenantSeedStatus.Complete);
        
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { { "Seed:CenterManagerPassword", "VerySecurePassword123" } }).Build();
        var logger = new Mock<ILogger<EduTwinRuntimeSeeder>>();
        
        var seeder = new EduTwinRuntimeSeeder(db, logger.Object, config, mockHasher.Object, mockEvaluator.Object);
        
        await seeder.SeedAsync();
        
        mockHasher.Verify(h => h.HashPassword(It.IsAny<User>(), It.IsAny<string>()), Times.Never);
    }
}
