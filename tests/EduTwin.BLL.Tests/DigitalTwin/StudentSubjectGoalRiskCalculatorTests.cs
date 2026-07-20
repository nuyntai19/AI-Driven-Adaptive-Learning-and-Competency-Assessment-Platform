using System;
using Xunit;
using EduTwin.BLL.DigitalTwin;

namespace EduTwin.BLL.Tests.DigitalTwin;

public class StudentSubjectGoalRiskCalculatorTests
{
    [Fact]
    public void CalculateRisk_Target85_Predicted0_Remaining120_Returns68()
    {
        var risk = StudentSubjectGoalRiskCalculator.CalculateRisk(8.5m, 0m, 120);
        Assert.Equal(68.00m, risk);
    }

    [Fact]
    public void CalculateRisk_RemainingDays0_MaxTimePressure()
    {
        var risk = StudentSubjectGoalRiskCalculator.CalculateRisk(10m, 0m, 0);
        Assert.Equal(100.00m, risk);
    }

    [Fact]
    public void CalculateRisk_RemainingDays180_MinTimePressure()
    {
        var risk = StudentSubjectGoalRiskCalculator.CalculateRisk(10m, 0m, 180);
        Assert.Equal(70.00m, risk);
    }

    [Fact]
    public void CalculateRisk_RemainingDaysGreaterThan180_ClampedToMinTimePressure()
    {
        var risk = StudentSubjectGoalRiskCalculator.CalculateRisk(10m, 0m, 365);
        Assert.Equal(70.00m, risk);
    }

    [Theory]
    [InlineData(5, 6, 100)]
    [InlineData(8, 8, 50)]
    public void CalculateRisk_TargetScoreLessOrEqualPredicted_Returns0(double target, double predicted, int remaining)
    {
        var risk = StudentSubjectGoalRiskCalculator.CalculateRisk((decimal)target, (decimal)predicted, remaining);
        Assert.Equal(0.00m, risk);
    }

    [Fact]
    public void CalculateRisk_TargetScore0_Returns0()
    {
        var risk = StudentSubjectGoalRiskCalculator.CalculateRisk(0m, 0m, 100);
        Assert.Equal(0.00m, risk);
    }

    [Fact]
    public void CalculateRisk_PredictedScore10_Returns0()
    {
        var risk = StudentSubjectGoalRiskCalculator.CalculateRisk(10m, 10m, 100);
        Assert.Equal(0.00m, risk);
    }

    [Fact]
    public void CalculateRisk_IntermediateValues_NotRoundedPrematurely()
    {
        var risk = StudentSubjectGoalRiskCalculator.CalculateRisk(9.99m, 0m, 90);
        // scoreGap = 0.999
        // timePressure = 1 - 0.5 = 0.5
        // rawRisk = 100 * 0.999 * (0.7 + 0.15) = 99.9 * 0.85 = 84.915
        // rounded to 84.92 (AwayFromZero)
        Assert.Equal(84.92m, risk);
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(10.1)]
    public void CalculateRisk_InvalidTargetScore_ThrowsArgumentOutOfRangeException(double target)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StudentSubjectGoalRiskCalculator.CalculateRisk((decimal)target, 0m, 100));
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(10.1)]
    public void CalculateRisk_InvalidPredictedScore_ThrowsArgumentOutOfRangeException(double predicted)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StudentSubjectGoalRiskCalculator.CalculateRisk(10m, (decimal)predicted, 100));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3651)]
    public void CalculateRisk_InvalidRemainingDays_ThrowsArgumentOutOfRangeException(int remaining)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => StudentSubjectGoalRiskCalculator.CalculateRisk(10m, 0m, remaining));
    }
}
