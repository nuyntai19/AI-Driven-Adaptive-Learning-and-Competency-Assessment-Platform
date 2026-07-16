using System.Linq;
using Xunit;
using EduTwin.DAL.Seeding;

namespace EduTwin.BLL.Tests.Seeding;

public class EduTwinSeedFactoryTests
{
    [Fact]
    public void CreateData_TwoCallsForSameCenter_ShouldBeDeterministic()
    {
        var factory1 = new EduTwinSeedFactory(true);
        var data1 = factory1.CreateData("hash");

        var factory2 = new EduTwinSeedFactory(true);
        var data2 = factory2.CreateData("hash");

        Assert.Equal(data1.Center.CenterId, data2.Center.CenterId);
        Assert.Equal(data1.Users.First().Username, data2.Users.First().Username);
        Assert.Equal(data1.Students.First().DateOfBirth, data2.Students.First().DateOfBirth);
        Assert.Equal(data1.Questions.First().QuestionText, data2.Questions.First().QuestionText);
    }

    [Fact]
    public void CreateData_DifferentCenters_ShouldHaveDifferentIds()
    {
        var factoryA = new EduTwinSeedFactory(true);
        var dataA = factoryA.CreateData("hash");

        var factoryB = new EduTwinSeedFactory(false);
        var dataB = factoryB.CreateData("hash");

        Assert.NotEqual(dataA.Center.CenterId, dataB.Center.CenterId);
        Assert.NotEqual(dataA.Users.First(u => u.RoleName == EduTwin.Contracts.IdentityAndTenancy.UserRole.CenterManager).UserId, dataB.Users.First(u => u.RoleName == EduTwin.Contracts.IdentityAndTenancy.UserRole.CenterManager).UserId);
    }

    [Fact]
    public void CreateData_ShouldHave30QuestionsPerCenter()
    {
        var factory = new EduTwinSeedFactory(true);
        var data = factory.CreateData("hash");

        Assert.Equal(30, data.Questions.Count);
    }

    [Fact]
    public void CreateData_Questions_ShouldHaveCorrectDistributionAndOptions()
    {
        var factory = new EduTwinSeedFactory(true);
        var data = factory.CreateData("hash");

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
        var data = factory.CreateData("hash");

        var engSubject = data.Subjects.First(s => s.SubjectCode == "ENGLISH");
        var mathSubject = data.Subjects.First(s => s.SubjectCode == "MATH");

        var engQuestions = data.Questions.Where(q => q.SubjectId == engSubject.SubjectId);
        var mathQuestions = data.Questions.Where(q => q.SubjectId == mathSubject.SubjectId);

        Assert.All(engQuestions, q => Assert.Equal("en", q.LanguageCode));
        Assert.All(mathQuestions, q => Assert.Equal("vi", q.LanguageCode));
    }
}
