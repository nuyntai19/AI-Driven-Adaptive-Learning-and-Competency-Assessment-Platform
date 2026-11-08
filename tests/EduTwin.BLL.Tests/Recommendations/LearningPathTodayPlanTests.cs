using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using EduTwin.BLL.Recommendations.Planning;
using EduTwin.Contracts.Recommendations;
using Xunit;

namespace EduTwin.BLL.Tests.Recommendations;

public sealed class LearningPathTodayPlanTests
{
    private static AgendaItemDto Item(string purpose, string skillId) => new()
    {
        Purpose = purpose,
        SkillId = skillId,
        QuestionCount = 5,
        EstimatedSecondsPerQuestion = 120
    };

    [Fact]
    public void ReviewThatNoLongerFitsTheReviewLimit_IsLeftOutOfTheDay()
    {
        var result = new LearningAgendaDto { AvailableMinutes = 15 };
        var options = new List<AgendaItemDto> { Item("Review", "a"), Item("Review", "b"), Item("Practice", "c") };

        typeof(LearningPathV4Planner)
            .GetMethod("BuildTodayPlan", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, [result, options, new Dictionary<string, decimal>(), new Dictionary<string, int>()]);

        Assert.DoesNotContain(result.TodayPlan, item => item.QuestionCount == 0);
        Assert.Equal(new[] { "a", "c" }, result.TodayPlan.Select(item => item.SkillId));
    }
}
