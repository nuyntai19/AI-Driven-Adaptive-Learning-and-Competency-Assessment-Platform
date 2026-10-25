using System.Linq;
using Xunit;
using EduTwin.DAL.Seeding;

namespace EduTwin.BLL.Tests.Seeding;

public class EduTwinSeedFactoryTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void GradeSpecificSeed_HasSixClassesAndNoMixedMembershipOrCurriculum(bool centerA)
    {
        var data = new EduTwinSeedFactory(centerA).CreateData();
        Assert.Equal(6, data.Classes.Count);
        Assert.Equal(12, data.Curriculums.Count);
        foreach (var subject in data.Subjects)
            Assert.Equal(new byte?[] { 10, 11, 12 }, data.Classes.Where(c => c.SubjectId == subject.SubjectId)
                .OrderBy(c => c.GradeLevel).Select(c => c.GradeLevel));
        foreach (var enrollment in data.ClassStudents)
        {
            var student = data.Students.Single(s => s.StudentId == enrollment.StudentId);
            var cls = data.Classes.Single(c => c.ClassId == enrollment.ClassId);
            Assert.Equal(student.GradeLevel, cls.GradeLevel);
            Assert.Equal(student.GradeLevel, enrollment.GradeLevelAtEnrollment);
        }
        Assert.All(data.Students, student => Assert.Equal(2, data.ClassStudents.Count(e => e.StudentId == student.StudentId)));
        foreach (var link in data.CurriculumClasses)
            Assert.Equal(data.Curriculums.Single(c => c.CurriculumId == link.CurriculumId).GradeLevel,
                data.Classes.Single(c => c.ClassId == link.ClassId).GradeLevel);
        var legacy = new[] { DeterministicSeedIds.CenterAMathClassId, DeterministicSeedIds.CenterAEnglishClassId,
            DeterministicSeedIds.CenterBMathClassId, DeterministicSeedIds.CenterBEnglishClassId };
        Assert.DoesNotContain(data.Classes, c => legacy.Contains(c.ClassId));
    }

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
    public void CreateData_ExcludesUnapprovedQuestionScopeWithoutRegrading()
    {
        var factory = new EduTwinSeedFactory(true);
        var data = factory.CreateData();

        Assert.Equal(41, data.Questions.Count);
        Assert.Equal(["MATH-FUNC-05"], data.SkippedQuestionTemplates);
        Assert.DoesNotContain(data.Questions, q => q.QuestionId == DeterministicSeedIds.CenterAQuestionIdBase + 4);
        foreach (var q in data.Questions)
        {
            var versions = data.GraphVersions.Where(v => v.GradeLevel == q.GradeLevel && v.SubjectId == q.SubjectId).Select(v => v.GraphVersionId).ToHashSet();
            Assert.Contains(data.NodeGrades, n => n.NodeId == q.PrimaryTopicNodeId && versions.Contains(n.GraphVersionId));
        }
    }

    [Fact]
    public void CreateData_Questions_ShouldHaveCorrectDistributionAndOptions()
    {
        var factory = new EduTwinSeedFactory(true);
        var data = factory.CreateData();

        // One legacy template needs academic review; its grade is not silently rewritten.
        var mcQuestions = data.Questions.Where(q => q.QuestionType == EduTwin.Contracts.CurriculumAndQuestions.QuestionType.MultipleChoice).ToList();
        var saQuestions = data.Questions.Where(q => q.QuestionType == EduTwin.Contracts.CurriculumAndQuestions.QuestionType.ShortAnswer).ToList();
        var essayQuestions = data.Questions.Where(q => q.QuestionType == EduTwin.Contracts.CurriculumAndQuestions.QuestionType.Essay).ToList();

        Assert.Equal(12, mcQuestions.Count);
        Assert.Equal(23, saQuestions.Count);
        Assert.Equal(6, essayQuestions.Count);

        foreach (var topic in data.Topics)
        {
            var topicQuestions = data.Questions.Where(q => q.PrimaryTopicNodeId == topic.NodeId).OrderBy(q => q.Difficulty).ToList();
            if (topic.NodeId >= DeterministicSeedIds.CenterANodeIdBase + 5000)
            {
                Assert.Single(topicQuestions);
                continue;
            }
            Assert.Equal(topic.NodeCode == "MATH-FUNCTIONS" ? 4 : 5, topicQuestions.Count);
            Assert.Equal(EduTwin.Contracts.CurriculumAndQuestions.QuestionType.MultipleChoice, topicQuestions[0].QuestionType); // Diff 1
            Assert.Equal(EduTwin.Contracts.CurriculumAndQuestions.QuestionType.ShortAnswer, topicQuestions[1].QuestionType);    // Diff 2
            Assert.Equal(EduTwin.Contracts.CurriculumAndQuestions.QuestionType.MultipleChoice, topicQuestions[2].QuestionType); // Diff 3
            Assert.Equal(EduTwin.Contracts.CurriculumAndQuestions.QuestionType.ShortAnswer, topicQuestions[3].QuestionType);    // Diff 4
            if (topic.NodeCode != "MATH-FUNCTIONS")
                Assert.Equal(EduTwin.Contracts.CurriculumAndQuestions.QuestionType.Essay, topicQuestions[4].QuestionType); // Diff 5
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ExpandedSamples_MapEveryCurriculumAndQuestionToItsCurrentGradeGraph(bool centerA)
    {
        var data = new EduTwinSeedFactory(centerA).CreateData();
        foreach (var curriculum in data.Curriculums.Where(c => c.GraphVersionId.HasValue))
        {
            var version = data.GraphVersions.Single(v => v.GraphVersionId == curriculum.GraphVersionId);
            Assert.Equal(curriculum.SubjectId, version.SubjectId);
            Assert.Equal(curriculum.GradeLevel, version.GradeLevel);
            var mapped = data.NodeGrades.Where(m => m.GraphVersionId == version.GraphVersionId).Select(m => m.NodeId).ToHashSet();
            Assert.All(data.CurriculumNodes.Where(n => n.CurriculumId == curriculum.CurriculumId), n => Assert.Contains(n.NodeId, mapped));
        }
        foreach (var version in data.GraphVersions.Where(v => v.IsCurrent))
        {
            var mapped = data.NodeGrades.Where(m => m.GraphVersionId == version.GraphVersionId).Select(m => m.NodeId).ToHashSet();
            Assert.All(data.EdgeGrades.Where(e => e.GraphVersionId == version.GraphVersionId), m =>
            {
                var edge = data.Edges.Single(e => e.EdgeId == m.EdgeId);
                Assert.Contains(edge.SourceNodeId, mapped);
                Assert.Contains(edge.TargetNodeId, mapped);
                Assert.Null(m.SourceGraphVersionId);
            });
        }
        Assert.Equal(6, data.GraphVersions.Count(v => v.IsCurrent));
        Assert.All(data.Questions.Where(q => q.QuestionId >= (centerA ? 15000UL : 25000UL)), q =>
            Assert.Contains(data.GraphVersions.Where(v => v.IsCurrent && v.SubjectId == q.SubjectId && v.GradeLevel == q.GradeLevel),
                v => data.NodeGrades.Any(m => m.GraphVersionId == v.GraphVersionId && m.NodeId == q.PrimaryTopicNodeId)));
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

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void CreateData_DomainSample_UsesBoundedMathEvaluationInBothCenters(bool centerA)
    {
        var data = new EduTwinSeedFactory(centerA).CreateData();
        var mathSubject = data.Subjects.Single(s => s.SubjectCode == "MATH");
        var question = data.Questions.Single(q => q.SubjectId == mathSubject.SubjectId &&
            q.QuestionText == "Tìm tập xác định của hàm số y = 1 / (x - 2).");
        Assert.Equal(EduTwin.Contracts.CurriculumAndQuestions.QuestionAnswerEvaluationMode.MathEquivalent, question.AnswerEvaluationMode);
        var result = new EduTwin.BLL.AssessmentAndReasoning.PreliminaryGrading.ShortAnswerGrader().Grade(
            "D=R\\{2}", question.CorrectAnswer,
            new EduTwin.BLL.AssessmentAndReasoning.PreliminaryGrading.QuestionGradingContext { EvaluationMode=question.AnswerEvaluationMode,MaxScore=question.MaxScore });
        Assert.True(result.IsCorrect);
        Assert.Equal(question.MaxScore,result.Score);
        Assert.Contains(data.Questions, q => q.QuestionType == EduTwin.Contracts.CurriculumAndQuestions.QuestionType.ShortAnswer &&
            q.AnswerEvaluationMode == EduTwin.Contracts.CurriculumAndQuestions.QuestionAnswerEvaluationMode.TextExact);
    }

    [Fact]
    public void CreateData_MathFunc04_UsesCoordinate2DEvaluation()
    {
        var factory = new EduTwinSeedFactory(true);
        var data = factory.CreateData();
        var mathSubject = data.Subjects.Single(s => s.SubjectCode == "MATH");
        var question = data.Questions.Single(q =>
            q.SubjectId == mathSubject.SubjectId &&
            q.QuestionText == "Tìm tọa độ đỉnh của parabol y = -2x^2 + 4x - 1.");

        Assert.Equal(
            EduTwin.Contracts.CurriculumAndQuestions.QuestionAnswerEvaluationMode.Coordinate2D,
            question.AnswerEvaluationMode);
        Assert.Equal("(1, 1)", question.CorrectAnswer);
    }
}
