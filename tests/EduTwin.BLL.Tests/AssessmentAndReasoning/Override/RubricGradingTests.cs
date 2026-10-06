using EduTwin.BLL.AssessmentAndReasoning.Override;
using EduTwin.BLL.CurriculumAndQuestions;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.CurriculumAndQuestions;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Override;

public class RubricGradingTests
{
    private static GradingCriteria Definition() => new() { Criteria = [
        new() { CriterionId = "method", Title = "Phương pháp", Description = "Accept equivalent methods", MaxScore = 0.5m },
        new() { CriterionId = "result", Title = "Kết quả", MaxScore = 1.5m }] };

    [Fact]
    public void Grade_ComputesTotalAndPreservesDefinitionSnapshot()
    {
        var definition = Definition();
        Assert.True(RubricGrading.TryGrade(definition, 2m, [new() { CriterionId = "method", AwardedScore = 0.5m },
            new() { CriterionId = "result", AwardedScore = 0m, Comment = "Incorrect constant" }], out var grade, out var error));
        Assert.Null(error); Assert.Equal(0.5m, grade!.AwardedScore);
        definition.Criteria[0].Title = "Changed later";
        Assert.Equal("Phương pháp", grade.Criteria[0].Title);
        Assert.Equal(0.5m, RubricGrade.Deserialize(RubricGrade.Serialize(grade))!.AwardedScore);
    }

    [Theory]
    [InlineData(-0.01)] [InlineData(0.51)] [InlineData(0.001)]
    public void Grade_RejectsInvalidPoints(double score) => Assert.False(RubricGrading.TryGrade(Definition(), 2m,
        [new() { CriterionId = "method", AwardedScore = (decimal)score }, new() { CriterionId = "result", AwardedScore = 1.5m }], out _, out _));

    [Fact]
    public void Grade_RejectsMissingUnknownDuplicateCriteriaAndWrongBudget()
    {
        Assert.False(RubricGrading.TryGrade(Definition(), 2m, null, out _, out _));
        Assert.False(RubricGrading.TryGrade(Definition(), 2m, [new() { CriterionId = "method" }], out _, out _));
        Assert.False(RubricGrading.TryGrade(Definition(), 2m, [new() { CriterionId = "method" }, new() { CriterionId = "method" }], out _, out _));
        Assert.False(RubricGrading.TryGrade(Definition(), 3m, [new() { CriterionId = "method" }, new() { CriterionId = "result" }], out _, out _));
        Assert.NotEmpty(GradingCriteriaValidator.Validate(Definition(), 3m));
    }

    [Fact]
    public void Grade_LegacyUnscoredRubricIsStillSupported()
    {
        Assert.True(RubricGrading.TryGrade(new(), 10m, null, out var grade, out _)); Assert.Null(grade);
        Assert.False(RubricGrading.TryGrade(new(), 10m, [new() { CriterionId = "unexpected" }], out _, out _));
    }
}
