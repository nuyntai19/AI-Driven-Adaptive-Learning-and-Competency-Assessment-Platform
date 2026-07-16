using System.Linq;
using Xunit;
using EduTwin.DAL.Seeding;

namespace EduTwin.BLL.Tests.Seeding;

public class EduTwinSeedFactoryTests
{
    [Fact]
    public void CreateData_TwoCallsForSameCenter_ShouldBeDeterministicAndIdentical()
    {
        var factory1 = new EduTwinSeedFactory(true);
        var data1 = factory1.CreateData();

        var factory2 = new EduTwinSeedFactory(true);
        var data2 = factory2.CreateData();

        Assert.Equal(data1.Center.CenterId, data2.Center.CenterId);
        Assert.Equal(data1.Users.First().Username, data2.Users.First().Username);
        Assert.Equal(data1.Students.First().DateOfBirth, data2.Students.First().DateOfBirth);
        Assert.Equal(data1.Questions.First().QuestionText, data2.Questions.First().QuestionText);

        // Ensure deterministic IDs
        Assert.Equal(DeterministicSeedIds.CenterAId, data1.Center.CenterId);
    }

    [Fact]
    public void CreateData_DifferentCenters_ShouldHaveDifferentIdsButIdenticalLogicalCounts()
    {
        var factoryA = new EduTwinSeedFactory(true);
        var dataA = factoryA.CreateData();

        var factoryB = new EduTwinSeedFactory(false);
        var dataB = factoryB.CreateData();

        // Distinct IDs
        Assert.NotEqual(dataA.Center.CenterId, dataB.Center.CenterId);
        Assert.NotEqual(
            dataA.Users.First(u => u.RoleName == EduTwin.Contracts.IdentityAndTenancy.UserRole.CenterManager).UserId,
            dataB.Users.First(u => u.RoleName == EduTwin.Contracts.IdentityAndTenancy.UserRole.CenterManager).UserId
        );

        // Identical structure sizes
        Assert.Equal(dataA.Users.Count, dataB.Users.Count);
        Assert.Equal(dataA.Questions.Count, dataB.Questions.Count);
        Assert.Equal(dataA.Topics.Count, dataB.Topics.Count);
    }

    [Fact]
    public void CreateData_ShouldHave30QuestionsPerCenter()
    {
        var factory = new EduTwinSeedFactory(true);
        var data = factory.CreateData();

        Assert.Equal(30, data.Questions.Count);
    }

    [Fact]
    public void CreateData_Questions_ShouldHaveCorrectDistributionAndOptions()
    {
        var factory = new EduTwinSeedFactory(true);
        var data = factory.CreateData();

        // 6 topics * 5 questions
        var mcQuestions = data.Questions.Where(q => q.QuestionType == EduTwin.Contracts.CurriculumAndQuestions.QuestionType.MultipleChoice).ToList();
        var saQuestions = data.Questions.Where(q => q.QuestionType == EduTwin.Contracts.CurriculumAndQuestions.QuestionType.ShortAnswer).ToList();
        var essayQuestions = data.Questions.Where(q => q.QuestionType == EduTwin.Contracts.CurriculumAndQuestions.QuestionType.Essay).ToList();

        Assert.Equal(12, mcQuestions.Count);
        Assert.Equal(12, saQuestions.Count);
        Assert.Equal(6, essayQuestions.Count);

        foreach (var topic in data.Topics)
        {
            var topicQuestions = data.Questions.Where(q => q.PrimaryTopicNodeId == topic.NodeId).OrderBy(q => q.Difficulty).ToList();
            Assert.Equal(5, topicQuestions.Count);
            Assert.Equal(EduTwin.Contracts.CurriculumAndQuestions.QuestionType.MultipleChoice, topicQuestions[0].QuestionType); // Diff 1
            Assert.Equal(EduTwin.Contracts.CurriculumAndQuestions.QuestionType.ShortAnswer, topicQuestions[1].QuestionType);    // Diff 2
            Assert.Equal(EduTwin.Contracts.CurriculumAndQuestions.QuestionType.MultipleChoice, topicQuestions[2].QuestionType); // Diff 3
            Assert.Equal(EduTwin.Contracts.CurriculumAndQuestions.QuestionType.ShortAnswer, topicQuestions[3].QuestionType);    // Diff 4
            Assert.Equal(EduTwin.Contracts.CurriculumAndQuestions.QuestionType.Essay, topicQuestions[4].QuestionType);          // Diff 5
        }

        foreach (var mc in mcQuestions)
        {
            var options = data.QuestionOptions.Where(o => o.QuestionId == mc.QuestionId).ToList();
            Assert.Equal(4, options.Count);
            Assert.Single(options, o => o.IsCorrect);
        }

        foreach (var other in saQuestions.Concat(essayQuestions))
        {
            var options = data.QuestionOptions.Where(o => o.QuestionId == other.QuestionId).ToList();
            Assert.Empty(options);
        }
    }

    [Fact]
    public void CreateData_LanguageCode_ShouldMatchSubject()
    {
        var factory = new EduTwinSeedFactory(true);
        var data = factory.CreateData();

        var engSubject = data.Subjects.First(s => s.SubjectCode == "ENGLISH");
        var mathSubject = data.Subjects.First(s => s.SubjectCode == "MATH");

        var engQuestions = data.Questions.Where(q => q.SubjectId == engSubject.SubjectId);
        var mathQuestions = data.Questions.Where(q => q.SubjectId == mathSubject.SubjectId);

        Assert.All(engQuestions, q => Assert.Equal("en", q.LanguageCode));
        Assert.All(mathQuestions, q => Assert.Equal("vi", q.LanguageCode));
    }

    [Fact]
    public void CreateData_StudentIdentity_Consistency()
    {
        var factory = new EduTwinSeedFactory(true);
        var data = factory.CreateData();

        // Verify User.DisplayName == Student.FullName for all students
        foreach (var student in data.Students)
        {
            var user = data.Users.First(u => u.UserId == student.StudentId);
            Assert.Equal(user.DisplayName, student.FullName);
            Assert.Contains(student.GradeLevel, new byte[] { 10, 11, 12 });
        }
    }

    [Fact]
    public void CreateData_CurriculumAndClass_SeedRequirementsMet()
    {
        var factory = new EduTwinSeedFactory(true);
        var data = factory.CreateData();

        // CurriculumClass.AssignedBy = Teacher
        Assert.All(data.CurriculumClasses, cc => Assert.NotEqual(Guid.Empty, cc.AssignedBy));

        // ClassStudent.Status = Active
        Assert.All(data.ClassStudents, cs => Assert.Equal(EduTwin.Contracts.Organization.ClassStudentStatus.Active, cs.Status));

        // KnowledgeEdge.Weight = 1.00m
        Assert.All(data.Edges, e => Assert.Equal(1.00m, e.Weight));

        // SourceFile = null
        Assert.All(data.Curriculums, c => Assert.Null(c.SourceFile));

        // Goals Score = 0
        Assert.All(data.Goals, g =>
        {
            Assert.Equal(0m, g.CurrentPredictedScore);
            Assert.Equal(0m, g.RiskScore);
        });
    }
}
