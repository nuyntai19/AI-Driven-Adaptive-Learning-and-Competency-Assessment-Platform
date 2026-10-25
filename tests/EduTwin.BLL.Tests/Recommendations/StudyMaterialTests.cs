using EduTwin.BLL.Recommendations.Planning;
using EduTwin.DAL.CurriculumAndQuestions;
using Xunit;

namespace EduTwin.BLL.Tests.Recommendations;

public sealed class StudyMaterialTests
{
    private static List<Question> Pool(int count) => Enumerable.Range(1, count)
        .Select(i => new Question { QuestionId = (ulong)i, Solution = "Lời giải đã biên soạn", Difficulty = 1 }).ToList();

    [Fact]
    public void AnUnseenQuestionBankAloneDoesNotMakeALessonAvailable()
    {
        var pool = Pool(4);
        Assert.False(StudyMaterial.Available(null, pool, []));
        Assert.Empty(StudyMaterial.Examples(pool, []));
        Assert.True(StudyMaterial.Available("Lý thuyết đã biên soạn", pool, []));
    }

    [Fact]
    public void PreviouslyAnsweredExamplesCanBeReusedWithoutExposingCheckQuestions()
    {
        var examples = StudyMaterial.Examples(Pool(4), [1]);
        Assert.Single(examples);
        Assert.Equal(1ul, examples[0].QuestionId);
    }

    [Fact]
    public void SpareExamplesKeepFiveCheckAndTwoPracticeQuestionsUnseen()
    {
        Assert.Empty(StudyMaterial.Examples(Pool(7), []));
        Assert.Single(StudyMaterial.Examples(Pool(8), []));
        Assert.Equal(2, StudyMaterial.Examples(Pool(10), []).Count);
        var pool = Pool(10);
        pool.ForEach(q => q.Solution = " ");
        Assert.False(StudyMaterial.Available(null, pool, []));
    }
}
