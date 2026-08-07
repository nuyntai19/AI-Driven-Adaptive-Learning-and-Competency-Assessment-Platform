using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using Moq;
using EduTwin.BLL.Assignments;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.Common;
using EduTwin.DAL;
using EduTwin.DAL.Assignments;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.BLL.Tests.Assignments;

public class GetStudentAssignmentUseCaseTests
{
    [Fact]
    public async Task ExecuteAsync_ShouldReturnAssignmentDetail_WithoutLeakingAnswers()
    {
        // Arrange
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        var tenantAccessorMock = new Mock<ITenantIdAccessor>();
        tenantAccessorMock.Setup(x => x.CenterId).Returns(centerId);

        using var context = new EduTwinDbContext(options, tenantAccessorMock.Object);

        var assignment = new Assignment
        {
            AssignmentId = assignmentId,
            CenterId = centerId,
            Title = "Test Assignment",
            Status = AssignmentStatus.Published,
            DueAt = DateTime.UtcNow.AddDays(1),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var progress = new StudentAssignmentProgress
        {
            ProgressId = 1,
            CenterId = centerId,
            StudentId = studentId,
            AssignmentId = assignment.AssignmentId,
            Status = ProgressStatus.NotStarted,
            CompletedQuestionCount = 0,
            TotalQuestionCount = 1,
            Assignment = assignment,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        
        var question = new Question
        {
            QuestionId = 1001,
            CenterId = centerId,
            QuestionType = QuestionType.MultipleChoice,
            Difficulty = 3,
            QuestionText = "1 + 1 = ?",
            CorrectAnswer = "B",
            Solution = "Because 1 and 1 makes 2.",
            ExpectedReasoning = "Basic math",
            LanguageCode = "en",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
        
        var optionA = new QuestionOption { OptionId = 1, QuestionId = 1001, CenterId = centerId, OptionLabel = "A", OptionText = "1", IsCorrect = false, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        var optionB = new QuestionOption { OptionId = 2, QuestionId = 1001, CenterId = centerId, OptionLabel = "B", OptionText = "2", IsCorrect = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
        
        var aq = new AssignmentQuestion
        {
            CenterId = centerId,
            AssignmentId = assignmentId,
            QuestionId = 1001,
            OrderIndex = 1,
            Points = 1,
            Question = question,
            CreatedAt = DateTime.UtcNow
        };

        context.Assignments.Add(assignment);
        context.StudentAssignmentProgresses.Add(progress);
        context.Questions.Add(question);
        context.QuestionOptions.AddRange(optionA, optionB);
        context.AssignmentQuestions.Add(aq);
        await context.SaveChangesAsync();

        var tenantContextMock = new Mock<ITenantContext>();
        tenantContextMock.Setup(x => x.CenterId).Returns(centerId);
        tenantContextMock.Setup(x => x.UserId).Returns(studentId);
        tenantContextMock.Setup(x => x.Role).Returns("Student");
        tenantContextMock.Setup(x => x.IsResolved).Returns(true);

        var useCase = new GetStudentAssignmentUseCase(context, tenantContextMock.Object, TimeProvider.System);

        // Act
        var result = await useCase.ExecuteAsync(assignmentId, CancellationToken.None);

        // Assert
        Assert.True(result.IsSuccess);
        var detail = result.Data!.Data;
        Assert.Equal("Test Assignment", detail.Title);
        Assert.Single(detail.Questions);
        
        var studentQuestion = detail.Questions[0];
        Assert.Equal("1 + 1 = ?", studentQuestion.QuestionText);
        // Ensure no correct answer or solution is leaked
        var properties = studentQuestion.GetType().GetProperties();
        Assert.Null(properties.FirstOrDefault(p => p.Name == "CorrectAnswer"));
        Assert.Null(properties.FirstOrDefault(p => p.Name == "Solution"));
        
        Assert.Equal(2, studentQuestion.Options.Count);
        var optionProperties = studentQuestion.Options[0].GetType().GetProperties();
        Assert.Null(optionProperties.FirstOrDefault(p => p.Name == "IsCorrect"));
    }

    [Fact]
    public async Task ExecuteAsync_ShouldReturnForbiddenOrNotFound_WhenCrossTenant()
    {
        // Arrange
        var centerId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        
        var options = new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        using var context = new EduTwinDbContext(options);

        var assignment = new Assignment
        {
            AssignmentId = assignmentId,
            CenterId = Guid.NewGuid(), // Different center
            Title = "Test Assignment",
            Status = AssignmentStatus.Published,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var progress = new StudentAssignmentProgress
        {
            ProgressId = 1,
            CenterId = assignment.CenterId,
            StudentId = studentId,
            AssignmentId = assignment.AssignmentId,
            Status = ProgressStatus.NotStarted,
            Assignment = assignment,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        context.Assignments.Add(assignment);
        context.StudentAssignmentProgresses.Add(progress);
        await context.SaveChangesAsync();

        var tenantContextMock = new Mock<ITenantContext>();
        tenantContextMock.Setup(x => x.CenterId).Returns(centerId);
        tenantContextMock.Setup(x => x.UserId).Returns(studentId);

        var useCase = new GetStudentAssignmentUseCase(context, tenantContextMock.Object, TimeProvider.System);

        // Act
        var result = await useCase.ExecuteAsync(assignmentId, CancellationToken.None);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(ErrorCodes.ResourceNotFound, result.ErrorCode);
    }
}
