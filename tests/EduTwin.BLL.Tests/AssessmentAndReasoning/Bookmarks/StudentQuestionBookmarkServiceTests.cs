using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.BLL.AssessmentAndReasoning.Bookmarks;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Assignments;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.Bookmarks;

public sealed class StudentQuestionBookmarkServiceTests : IDisposable
{
    private const ulong QuestionId = 7;
    private static readonly DateTime UtcNow = new(2026, 11, 9, 8, 0, 0, DateTimeKind.Utc);
    private readonly Guid _centerId = Guid.NewGuid();
    private readonly Guid _studentId = Guid.NewGuid();
    private readonly Guid _subjectId = Guid.NewGuid();
    private readonly EduTwinDbContext _db;
    private readonly StudentQuestionBookmarkService _service;

    public StudentQuestionBookmarkServiceTests()
    {
        var options = new DbContextOptionsBuilder<EduTwinDbContext>().UseInMemoryDatabase($"Bookmarks_{Guid.NewGuid():N}").Options;
        var tenant = new TenantContext();
        tenant.Initialize(_centerId, _studentId, "Student", 1);
        _db = new TestDbContext(options, tenant);
        _service = new StudentQuestionBookmarkService(_db, new Clock());
        _db.KnowledgeNodes.Add(new KnowledgeNode { NodeType = NodeType.Topic, CenterId = _centerId, SubjectId = _subjectId, NodeId = 1, NodeName = "Đạo hàm", NodeCode = "DH", IsActive = true, CreatedAt = UtcNow, UpdatedAt = UtcNow });
        _db.Questions.Add(new Question
        {
            GradeLevel = 12, CenterId = _centerId, SubjectId = _subjectId, PrimaryTopicNodeId = 1, QuestionId = QuestionId, Difficulty = 3,
            Status = QuestionStatus.Active, QuestionText = "Tính đạo hàm của x^2", QuestionType = QuestionType.ShortAnswer, CorrectAnswer = "2x",
            Solution = "Áp dụng công thức.", ExpectedReasoning = "Công thức.", LanguageCode = "vi", MaxScore = 10m, EstimatedTimeSeconds = 60, CreatedAt = UtcNow, UpdatedAt = UtcNow
        });
        _db.SaveChanges();
    }

    public void Dispose() => _db.Dispose();

    [Fact]
    public async Task A_mark_on_an_unanswered_question_shows_no_content()
    {
        Assert.True(await _service.AddAsync(_centerId, _studentId, QuestionId, CancellationToken.None));

        var mark = Assert.Single(await _service.ListAsync(_centerId, _studentId, CancellationToken.None));

        Assert.False(mark.Answered);
        Assert.Null(mark.QuestionText);
        Assert.Null(mark.CorrectAnswer);
    }

    [Fact]
    public async Task An_answered_question_comes_back_with_its_solution()
    {
        await _service.AddAsync(_centerId, _studentId, QuestionId, CancellationToken.None);
        await AnswerAsync();

        var mark = Assert.Single(await _service.ListAsync(_centerId, _studentId, CancellationToken.None));

        Assert.True(mark.Answered);
        Assert.Equal("7", mark.QuestionId);
        Assert.Equal("2x", mark.CorrectAnswer);
        Assert.Equal("Áp dụng công thức.", mark.Solution);
    }

    [Fact]
    public async Task An_assignment_still_running_keeps_the_answer_hidden()
    {
        await _service.AddAsync(_centerId, _studentId, QuestionId, CancellationToken.None);
        await AnswerAsync();
        var assignment = new Assignment { AssignmentId = Guid.NewGuid(), CenterId = _centerId, Title = "Bài kiểm tra", Status = AssignmentStatus.Published, DueAt = UtcNow.AddDays(1), CreatedAt = UtcNow, UpdatedAt = UtcNow };
        _db.Assignments.Add(assignment);
        _db.AssignmentQuestions.Add(new AssignmentQuestion { CenterId = _centerId, AssignmentId = assignment.AssignmentId, QuestionId = QuestionId, OrderIndex = 1, CreatedAt = UtcNow });
        await _db.SaveChangesAsync();

        var mark = Assert.Single(await _service.ListAsync(_centerId, _studentId, CancellationToken.None));

        Assert.NotNull(mark.QuestionText);
        Assert.Null(mark.CorrectAnswer);
        Assert.Null(mark.Solution);
    }

    [Fact]
    public async Task A_closed_exam_keeps_bookmark_answers_hidden_until_publication()
    {
        await _service.AddAsync(_centerId, _studentId, QuestionId, CancellationToken.None);
        await AnswerAsync();
        var assignment = new Assignment { AssignmentId = Guid.NewGuid(), CenterId = _centerId, Title = "Thi", Kind = AssignmentKind.Midterm,
            Status = AssignmentStatus.Closed, DueAt = UtcNow.AddDays(-1), CreatedAt = UtcNow, UpdatedAt = UtcNow };
        _db.Assignments.Add(assignment);
        _db.AssignmentQuestions.Add(new AssignmentQuestion { CenterId = _centerId, AssignmentId = assignment.AssignmentId, QuestionId = QuestionId, OrderIndex = 1, CreatedAt = UtcNow });
        await _db.SaveChangesAsync();
        var hidden = Assert.Single(await _service.ListAsync(_centerId, _studentId, CancellationToken.None));
        Assert.Null(hidden.CorrectAnswer); Assert.Null(hidden.Solution);
        assignment.ResultsPublishedAt = UtcNow;
        await _db.SaveChangesAsync();
        var published = Assert.Single(await _service.ListAsync(_centerId, _studentId, CancellationToken.None));
        Assert.Equal("2x", published.CorrectAnswer);
    }

    [Fact]
    public async Task Multiple_choice_bookmark_exposes_answer_content_instead_of_an_unusable_letter()
    {
        var question = await _db.Questions.SingleAsync();
        question.QuestionType = QuestionType.MultipleChoice; question.CorrectAnswer = "C";
        _db.QuestionOptions.Add(new QuestionOption { CenterId = _centerId, QuestionId = QuestionId, OptionId = 11, OptionLabel = "C", OptionText = "2x", IsCorrect = true, CreatedAt = UtcNow, UpdatedAt = UtcNow });
        await _db.SaveChangesAsync();
        await _service.AddAsync(_centerId, _studentId, QuestionId, CancellationToken.None);
        await AnswerAsync();
        var mark = Assert.Single(await _service.ListAsync(_centerId, _studentId, CancellationToken.None));
        Assert.Equal("C. 2x", mark.CorrectAnswer);
    }

    [Fact]
    public async Task Marks_belong_to_one_student_and_can_be_removed()
    {
        await _service.AddAsync(_centerId, _studentId, QuestionId, CancellationToken.None);
        await _service.AddAsync(_centerId, _studentId, QuestionId, CancellationToken.None);

        Assert.Empty(await _service.ListAsync(_centerId, Guid.NewGuid(), CancellationToken.None));
        Assert.Single(await _service.ListAsync(_centerId, _studentId, CancellationToken.None));

        await _service.RemoveAsync(_centerId, _studentId, QuestionId, CancellationToken.None);
        Assert.Empty(await _service.ListAsync(_centerId, _studentId, CancellationToken.None));
    }

    [Fact]
    public async Task A_question_from_another_center_cannot_be_marked()
    {
        Assert.False(await _service.AddAsync(Guid.NewGuid(), _studentId, QuestionId, CancellationToken.None));
    }

    private async Task AnswerAsync()
    {
        _db.Attempts.Add(new Attempt
        {
            AttemptId = 1, CenterId = _centerId, StudentId = _studentId, QuestionId = QuestionId, FinalAnswer = "2x", ReasoningLanguage = "vi",
            Status = AttemptStatus.Completed, ClientSubmissionId = Guid.NewGuid(), CreatedAt = UtcNow, UpdatedAt = UtcNow
        });
        await _db.SaveChangesAsync();
    }

    private sealed class Clock : TimeProvider { public override DateTimeOffset GetUtcNow() => UtcNow; }
    private sealed class TestDbContext(DbContextOptions<EduTwinDbContext> options, TenantContext tenant) : EduTwinDbContext(options, tenant);
}
