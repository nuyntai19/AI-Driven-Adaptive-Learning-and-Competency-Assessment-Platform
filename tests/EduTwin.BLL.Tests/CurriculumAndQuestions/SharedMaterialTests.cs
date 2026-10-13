using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.BLL.CurriculumAndQuestions;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Xunit;

namespace EduTwin.BLL.Tests.CurriculumAndQuestions;

public sealed class SharedMaterialTests : IDisposable
{
    private readonly Guid _center = Guid.NewGuid();
    private readonly Guid _actor = Guid.NewGuid();
    private readonly Guid _other = Guid.NewGuid();
    private readonly Guid _subject = Guid.NewGuid();
    private readonly TenantContext _tenant = new();
    private readonly EduTwinDbContext _db;
    public SharedMaterialTests()
    {
        _tenant.Initialize(_center, _actor, nameof(UserRole.Teacher), 1);
        _db = new EduTwinDbContext(new DbContextOptionsBuilder<EduTwinDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options, _tenant);
    }
    public void Dispose() => _db.Dispose();
    private Question Question(ulong id, Guid owner, MaterialVisibility visibility = MaterialVisibility.Private,
        QuestionStatus status = QuestionStatus.Active, Guid? center = null) => new() {
        QuestionId = id, CenterId = center ?? _center, CreatedByTeacherId = owner, SubjectId = _subject, PrimaryTopicNodeId = 5,
        Visibility = visibility, Status = status, QuestionType = QuestionType.ShortAnswer, AnswerEvaluationMode = QuestionAnswerEvaluationMode.TextExact,
        Difficulty = 2, GradeLevel = 10, QuestionText = "Question", CorrectAnswer = "2", Solution = "Solution", LanguageCode = "vi",
        EstimatedTimeSeconds = 120, MaxScore = 10, RowVersion = 1, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow };
    private UpdateQuestionRequest Update() => new() { PrimaryTopicNodeId = "5", QuestionType = "ShortAnswer", Difficulty = 2,
        GradeLevel = 10, QuestionText = "Changed text", CorrectAnswer = "2", Solution = "Solution", LanguageCode = "vi",
        EstimatedTimeSeconds = 120, MaxScore = 10, RowVersion = "1" };

    [Fact]
    public async Task Question_ReadListIsTenantBoundAndSharingDoesNotGrantWriteRights()
    {
        _db.Questions.AddRange(Question(1, _actor), Question(2, _other), Question(3, _other, MaterialVisibility.Shared),
            Question(4, _other, MaterialVisibility.Shared, QuestionStatus.Draft), Question(5, _other, MaterialVisibility.Shared, center: Guid.NewGuid()));
        await _db.SaveChangesAsync();
        var list = await new ListQuestionsUseCase(_db, _tenant).ExecuteAsync(new());
        Assert.True(list.IsSuccess); Assert.Equal(2, list.Data!.Count);
        var get = new GetQuestionUseCase(_db, _tenant);
        Assert.True((await get.ExecuteAsync("3")).IsSuccess);
        Assert.False((await get.ExecuteAsync("2")).IsSuccess);
        Assert.False((await get.ExecuteAsync("4")).IsSuccess);
        Assert.False((await get.ExecuteAsync("5")).IsSuccess);
        var update = await new UpdateQuestionUseCase(_db, _tenant, TimeProvider.System).ExecuteAsync("3", Update());
        Assert.Equal(ErrorCodes.ForbiddenResource, update.ErrorCode);
        var own = await new ListQuestionsUseCase(_db, _tenant).ExecuteAsync(new() { OwnedOnly = true });
        Assert.Single(own.Data!);
    }

    [Fact]
    public async Task Question_OmittedCriteriaArePreservedAndUsedGradingBasisCannotBeRewritten()
    {
        var question = Question(1, _actor);
        question.GradingCriteria = new() { RequiredIdeas = ["first", "second"], CommonErrors = ["invalid division"] };
        _db.Questions.Add(question);
        _db.KnowledgeNodes.Add(new() { CenterId = _center, SubjectId = _subject, NodeId = 5, NodeCode = "topic", NodeName = "Topic", IsActive = true, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();
        var useCase = new UpdateQuestionUseCase(_db, _tenant, TimeProvider.System);
        var saved = await useCase.ExecuteAsync("1", Update());
        Assert.True(saved.IsSuccess);
        Assert.Equal(2, saved.Data!.GradingCriteria!.RequiredIdeas.Count);
        Assert.Single(saved.Data.GradingCriteria.CommonErrors);
        _db.Attempts.Add(new() { CenterId = _center, AttemptId = 1, StudentId = Guid.NewGuid(), QuestionId = 1,
            FinalAnswer = "2", ReasoningLanguage = "vi", ClientSubmissionId = Guid.NewGuid(), CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();
        var request = Update(); request.RowVersion = saved.Data.RowVersion; request.MaxScore = 2;
        var rejected = await useCase.ExecuteAsync("1", request);
        Assert.Equal(ErrorCodes.InvalidStateTransition, rejected.ErrorCode);
        Assert.Equal(10, question.MaxScore);
    }

    [Fact]
    public async Task SharedCurriculumCanBeReadAndClonedButNotEditedOrExposeClassTargets()
    {
        var id = Guid.NewGuid();
        _db.Curriculums.Add(new() { CurriculumId = id, CenterId = _center, TeacherId = _other, SubjectId = _subject,
            Title = "Shared curriculum", GradeLevel = 11, Visibility = MaterialVisibility.Shared, ReviewStatus = ReviewStatus.Published, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        _db.CurriculumClasses.Add(new() { CenterId = _center, CurriculumId = id, ClassId = Guid.NewGuid(), AssignedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();
        var read = await new GetCurriculumUseCase(_db, _tenant).ExecuteAsync(new() { CurriculumId = id });
        Assert.True(read.IsSuccess); Assert.Empty(read.Data!.ClassIds); Assert.Equal("Shared", read.Data.Visibility);
        var clone = await new CloneCurriculumUseCase(_db, _tenant, TimeProvider.System).ExecuteAsync(id, new());
        Assert.True(clone.IsSuccess); Assert.Equal(_actor.ToString("D"), clone.Data!.TeacherId);
        Assert.Equal("Private", clone.Data.Visibility); Assert.Equal((byte)11, clone.Data.GradeLevel!.Value);
        var update = await new UpdateCurriculumUseCase(_db, _tenant, TimeProvider.System).ExecuteAsync(id,
            new() { Title = "Forbidden edit", RowVersion = read.Data.RowVersion });
        Assert.False(update.IsSuccess);
    }

    [Fact]
    public async Task PrivateCurriculumCannotBeReadOrClonedByOtherTeacher()
    {
        var id = Guid.NewGuid();
        _db.Curriculums.Add(new() { CurriculumId = id, CenterId = _center, TeacherId = _other, SubjectId = _subject,
            Title = "Private curriculum", ReviewStatus = ReviewStatus.Published, CreatedAt = DateTime.UtcNow, UpdatedAt = DateTime.UtcNow });
        await _db.SaveChangesAsync();
        Assert.False((await new GetCurriculumUseCase(_db, _tenant).ExecuteAsync(new() { CurriculumId = id })).IsSuccess);
        Assert.False((await new CloneCurriculumUseCase(_db, _tenant, TimeProvider.System).ExecuteAsync(id, new())).IsSuccess);
    }
}
