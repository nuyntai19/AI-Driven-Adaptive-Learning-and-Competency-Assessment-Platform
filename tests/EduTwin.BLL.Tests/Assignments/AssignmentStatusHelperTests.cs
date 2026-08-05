using System;
using EduTwin.BLL.Assignments;
using EduTwin.Contracts.Assignments;
using Xunit;

namespace EduTwin.BLL.Tests.Assignments;

public class AssignmentStatusHelperTests
{
    [Fact]
    public void GetEffectiveProgressStatus_WhenStatusIsCompleted_ReturnsCompleted_EvenIfOverdue()
    {
        // Arrange
        var dueAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var utcNow = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc); // Past due
        var dbStatus = ProgressStatus.Completed;

        // Act
        var result = AssignmentStatusHelper.GetEffectiveProgressStatus(dbStatus, dueAt, utcNow);

        // Assert
        Assert.Equal(ProgressStatus.Completed, result);
    }

    [Fact]
    public void GetEffectiveProgressStatus_WhenStatusIsNotCompleted_AndPastDueAt_ReturnsOverdue()
    {
        // Arrange
        var dueAt = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var utcNow = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc); // Past due
        var dbStatus = ProgressStatus.InProgress;

        // Act
        var result = AssignmentStatusHelper.GetEffectiveProgressStatus(dbStatus, dueAt, utcNow);

        // Assert
        Assert.Equal(ProgressStatus.Overdue, result);
    }

    [Fact]
    public void GetEffectiveProgressStatus_WhenStatusIsNotCompleted_AndNotPastDueAt_ReturnsOriginalStatus()
    {
        // Arrange
        var dueAt = new DateTime(2026, 1, 3, 0, 0, 0, DateTimeKind.Utc);
        var utcNow = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc); // Before due
        var dbStatus = ProgressStatus.InProgress;

        // Act
        var result = AssignmentStatusHelper.GetEffectiveProgressStatus(dbStatus, dueAt, utcNow);

        // Assert
        Assert.Equal(ProgressStatus.InProgress, result);
    }

    [Fact]
    public void GetEffectiveProgressStatus_WhenDueAtIsNull_ReturnsOriginalStatus()
    {
        // Arrange
        DateTime? dueAt = null;
        var utcNow = new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc);
        var dbStatus = ProgressStatus.NotStarted;

        // Act
        var result = AssignmentStatusHelper.GetEffectiveProgressStatus(dbStatus, dueAt, utcNow);

        // Assert
        Assert.Equal(ProgressStatus.NotStarted, result);
    }
}
