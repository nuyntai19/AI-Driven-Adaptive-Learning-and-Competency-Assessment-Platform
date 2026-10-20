using System.Text;
using System.Text.Json;
using EduTwin.BLL.CurriculumAndQuestions;
using EduTwin.BLL.CurriculumAndQuestions.Import;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Organization;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Persistence.Tenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Moq;
using Xunit;

namespace EduTwin.BLL.Tests.CurriculumAndQuestions;

[Collection("Question import use cases")]
public sealed class QuestionImportUseCaseTests : IDisposable
{
    private readonly Guid _centerId = Guid.NewGuid();
    private readonly Guid _teacherId = Guid.NewGuid();
    private readonly Guid _subjectId = Guid.NewGuid();
    private readonly Mock<ITenantContext> _tenant = new();
    private readonly Mock<ITenantIdAccessor> _accessor = new();
    private readonly Mock<TimeProvider> _clock = new();
    private readonly DbContextOptions<EduTwinDbContext> _options;
    private readonly EduTwinDbContext _db;
    private readonly DateTimeOffset _now = new(2026, 10, 12, 4, 0, 0, TimeSpan.Zero);
    private const string Header = "QuestionKey,TopicCode,QuestionType,QuestionText,CorrectAnswer,Solution";
    public QuestionImportUseCaseTests()
    {
        _tenant.Setup(t => t.CenterId).Returns(_centerId); _tenant.Setup(t => t.UserId).Returns(_teacherId);
        _tenant.Setup(t => t.IsResolved).Returns(true); _tenant.Setup(t => t.Role).Returns(nameof(UserRole.Teacher));
        _accessor.Setup(t => t.CenterId).Returns(_centerId); _clock.Setup(t => t.GetUtcNow()).Returns(_now);
        _options = new DbContextOptionsBuilder<EduTwinDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning)).Options;
        _db = new EduTwinDbContext(_options, _accessor.Object); _db.Database.EnsureCreated();
        var user = new User { UserId = _teacherId, CenterId = _centerId, Username = "teacher.import", DisplayName = "Teacher", PasswordHash = "hash", RoleName = UserRole.Teacher, Status = UserStatus.Active, CreatedAt = _now.UtcDateTime, UpdatedAt = _now.UtcDateTime };
        _db.Users.Add(user); _db.Teachers.Add(new Teacher { TeacherId = _teacherId, CenterId = _centerId, User = user, CreatedAt = _now.UtcDateTime, UpdatedAt = _now.UtcDateTime });
        _db.Subjects.Add(new Subject { SubjectId = _subjectId, CenterId = _centerId, SubjectCode = "MATH", SubjectName = "Toán", IsActive = true, CreatedAt = _now.UtcDateTime, UpdatedAt = _now.UtcDateTime });
        for (ulong i = 1; i <= 3; i++) _db.KnowledgeNodes.Add(new KnowledgeNode { NodeId = 200 + i, CenterId = _centerId, SubjectId = _subjectId, NodeType = NodeType.Topic, NodeCode = $"TOP-{i}", NodeName = $"Chủ đề {i}", IsActive = true, CreatedAt = _now.UtcDateTime, UpdatedAt = _now.UtcDateTime });
        _db.SaveChanges();
    }
    private QuestionImportUseCase Sut(EduTwinDbContext? db = null, IQuestionActivationPolicy? policy = null) => new(db ?? _db, _tenant.Object, _clock.Object, activationPolicy: policy);
    private QuestionImportPreviewRequest Defaults(bool legacy = false) => new() { SubjectId = _subjectId, GradeLevel = 12, LegacyMode = legacy, DefaultTopicNodeId = legacy ? 201UL : null };
    private async Task<QuestionImportPreviewResult> Preview(string csv, QuestionImportPreviewRequest? request = null, QuestionImportUseCase? sut = null)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csv));
        return await (sut ?? Sut()).PreviewAsync(stream, "questions.csv", request ?? Defaults(), default);
    }
    private static QuestionImportConfirmRequest ConfirmRequest(QuestionImportPreviewResult preview) => new() { PreviewToken = preview.Data!.PreviewToken, Revision = preview.Data.Revision };

    [Fact]
    public async Task V1_ThreeTopicsAndAllQuestionTypes_PersistFullScalarMetadataAsDraft()
    {
        var csv = Header + ",GradeLevel,Visibility,LanguageCode,ScoringNotes,ExpectedReasoning,AnswerEvaluationMode,OptionA,OptionB,OptionC,OptionD,MisconceptionB,RequiredIdeas,CommonErrors\n" +
            "q1,TOP-1,MultipleChoice,Chọn $x^2$,A,Giải $x^2$,10,Shared,en,Ghi chú,Rationale,TextExact,Đúng,Sai,Sai C,Sai D,Nhầm dấu,,\n" +
            "q2,TOP-2,ShortAnswer,Tính $\\frac{1}{2}$,1/2,Lời giải,11,Private,vi,Chấm riêng,,NumericRational,,,,,,\"[\"\"tọa độ (1;2)\"\"]\",\"[\"\"dấu | giữ nguyên\"\"]\"\n" +
            "q3,TOP-3,Essay,Chứng minh,Đáp án mẫu,Hướng dẫn,12,Shared,vi,,,Manual,,,,,,,\n";
        var preview = await Preview(csv);
        Assert.True(preview.IsSuccess, preview.ErrorMessage); Assert.Empty(preview.Data!.Errors); Assert.Equal(3, preview.Data.ValidCount);
        Assert.Equal(_subjectId.ToString(), preview.Data.SubjectId); Assert.Equal(_now.AddMinutes(30).UtcDateTime, preview.Data.ExpiresAt);
        var result = await Sut().ConfirmAsync(ConfirmRequest(preview), default);
        Assert.True(result.IsSuccess, result.ErrorMessage); Assert.Equal(3, result.Data!.ImportedCount); Assert.Equal(3, result.Data.QuestionIds.Count);
        _db.ChangeTracker.Clear();
        var questions = await _db.Questions.OrderBy(q => q.PrimaryTopicNodeId).ToListAsync();
        Assert.Equal(new ulong[] { 201, 202, 203 }, questions.Select(q => q.PrimaryTopicNodeId));
        Assert.All(questions, q => { Assert.Equal(QuestionStatus.Draft, q.Status); Assert.Equal(_teacherId, q.CreatedByTeacherId); });
        Assert.Equal("en", questions[0].LanguageCode); Assert.Equal((byte)10, questions[0].GradeLevel); Assert.Equal(MaterialVisibility.Shared, questions[0].Visibility);
        Assert.Equal("Ghi chú", questions[0].GradingCriteria.ScoringNotes); Assert.Equal("Nhầm dấu", questions[0].GradingCriteria.OptionMisconceptions["B"]);
        Assert.Equal("Chấm riêng", questions[1].GradingCriteria.ScoringNotes); Assert.Equal("tọa độ (1;2)", Assert.Single(questions[1].GradingCriteria.RequiredIdeas));
        Assert.Contains(@"\frac{1}{2}", questions[1].QuestionText); Assert.Equal(4, await _db.QuestionOptions.CountAsync());
    }

    [Fact]
    public async Task DefaultsAreShownAndShortAnswerDefaultsManual_NewProfileRequiresTopicAndType()
    {
        var valid = await Preview(Header + "\nq1,TOP-1,ShortAnswer,Câu hỏi,2,Lời giải");
        var item = Assert.Single(valid.Data!.ValidQuestions);
        Assert.Equal(QuestionAnswerEvaluationMode.Manual, item.AnswerEvaluationMode); Assert.Equal((byte)12, item.GradeLevel);
        Assert.Contains("GradeLevel", item.DefaultedFields); Assert.Contains("AnswerEvaluationMode", item.DefaultedFields);
        var invalid = await Preview(Header + "\nq1,,,Câu hỏi,2,Lời giải");
        Assert.Equal(1, invalid.Data!.InvalidCount); Assert.Contains(invalid.Data.Errors, e => e.Field == "TopicCode"); Assert.Contains(invalid.Data.Errors, e => e.Field == "QuestionType");
    }

    [Fact]
    public async Task LegacyModeIsExplicitAndRetainsTextExactShortAnswerDefault()
    {
        const string csv = "Nội dung câu hỏi,Loại câu hỏi,Đáp án đúng,Lời giải\nCâu hỏi,ShortAnswer,2,Lời giải";
        var legacy = await Preview(csv, Defaults(true)); Assert.True(legacy.IsSuccess); var item = Assert.Single(legacy.Data!.ValidQuestions);
        Assert.Equal("TOP-1", item.TopicCode); Assert.Equal(QuestionAnswerEvaluationMode.TextExact, item.AnswerEvaluationMode);
        Assert.Contains("TopicCode", item.DefaultedFields);
        var normal = await Preview(csv); Assert.Equal(1, normal.Data!.InvalidCount);
        var defaults = Defaults(); defaults.DefaultTopicNodeId = 201; Assert.False((await Preview(csv, defaults)).IsSuccess);
    }

    [Fact]
    public async Task LegacyListSeparatorsRejectedToPreserveCoordinatesAndSetNotation()
    {
        var preview = await Preview("QuestionType,QuestionText,CorrectAnswer,Solution,RequiredIdeas,CommonErrors\nShortAnswer,Câu hỏi,2,Lời giải,(1;2),A|B", Defaults(true));
        Assert.Equal(1, preview.Data!.InvalidCount); Assert.Equal(2, preview.Data.ErrorCount); Assert.All(preview.Data.Errors, e => Assert.Equal("INVALID_JSON_ARRAY", e.Code));
    }

    [Fact]
    public async Task NewMcqRequiresFourOptions_LegacyKeepsTwoAndCanonicalizesAnswerLabel()
    {
        var csv = Header + ",OptionA,OptionB\nq1,TOP-1,MultipleChoice,Câu hỏi, A ,Lời giải,Đúng,Sai";
        var current = await Preview(csv); Assert.Contains(current.Data!.Errors, e => e.Code == "INCOMPLETE_MCQ_OPTIONS");
        var legacy = await Preview(csv, Defaults(true)); Assert.Equal("A", Assert.Single(legacy.Data!.ValidQuestions).CorrectAnswer);
    }

    [Theory]
    [InlineData("GradeLevel", "9")]
    [InlineData("GradeLevel", "13")]
    [InlineData("GradeLevel", "not-a-grade")]
    [InlineData("Difficulty", "0")]
    [InlineData("Difficulty", "6")]
    [InlineData("MaxScore", "0")]
    [InlineData("MaxScore", "1000")]
    [InlineData("MaxScore", "1.001")]
    [InlineData("EstimatedTimeSeconds", "0")]
    [InlineData("ReasoningRequired", "tru")]
    [InlineData("Visibility", "shared")]
    [InlineData("LanguageCode", "fr")]
    [InlineData("AnswerEvaluationMode", "0")]
    public async Task InvalidRowValuesAreErrors_AndNeverReplacedByDefaults(string field, string value)
    {
        var result = await Preview(Header + $",{field}\nq1,TOP-1,ShortAnswer,Câu hỏi,2,Lời giải,{value}");
        Assert.True(result.IsSuccess); Assert.Equal(0, result.Data!.ValidCount); Assert.Equal(1, result.Data.InvalidCount); Assert.Contains(result.Data.Errors, e => e.Field == field);
    }

    [Fact]
    public async Task ZeroFalseAndUpperScoreBoundaryRemainValid()
    {
        var result = await Preview(Header + ",ReasoningRequired,MaxScore\nq1,TOP-1,ShortAnswer,Câu hỏi,2,Lời giải,0,999.99\nq2,TOP-1,ShortAnswer,Câu khác,2,Lời giải,FALSE,10");
        Assert.Equal(2, result.Data!.ValidCount); Assert.All(result.Data.ValidQuestions, q => Assert.False(q.ReasoningRequired));
    }

    [Theory]
    [InlineData("GradeLevel")]
    [InlineData("Difficulty")]
    [InlineData("MaxScore")]
    [InlineData("EstimatedTimeSeconds")]
    [InlineData("Visibility")]
    [InlineData("LanguageCode")]
    [InlineData("TemplateVersion")]
    [InlineData("ShortAnswerEvaluationMode")]
    [InlineData("TeacherId")]
    [InlineData("SubjectId")]
    public async Task InvalidDefaultsAreRejectedBeforeParsing(string field)
    {
        var defaults = Defaults();
        switch (field)
        {
            case "GradeLevel": defaults.GradeLevel = 9; break; case "Difficulty": defaults.Difficulty = 0; break; case "MaxScore": defaults.MaxScore = 1000; break;
            case "EstimatedTimeSeconds": defaults.EstimatedTimeSeconds = 0; break; case "Visibility": defaults.Visibility = "0"; break; case "LanguageCode": defaults.LanguageCode = "fr"; break;
            case "TemplateVersion": defaults.TemplateVersion = "2.0"; break; case "ShortAnswerEvaluationMode": defaults.ShortAnswerEvaluationMode = "Unknown"; break;
            case "TeacherId": defaults.TeacherId = _teacherId; break; case "SubjectId": defaults.SubjectId = Guid.Empty; break;
        }
        Assert.False((await Preview("not even valid CSV", defaults)).IsSuccess);
    }

    [Fact]
    public async Task InvalidCountCountsQuestionsInsteadOfMultipleErrors_AndDefaultModeRejectsPartialImport()
    {
        var preview = await Preview(Header + "\nq1,TOP-1,ShortAnswer,Câu hợp lệ,2,Lời giải\nq2,bad,Unknown,,,\n");
        Assert.Equal(2, preview.Data!.TotalRows); Assert.Equal(1, preview.Data.InvalidCount); Assert.Equal(1, preview.Data.ValidCount); Assert.True(preview.Data.ErrorCount > 1);
        Assert.Equal(preview.Data.Errors.Count, preview.Data.ErrorCount);
        var blocked = await Sut().ConfirmAsync(ConfirmRequest(preview), default); Assert.False(blocked.IsSuccess); Assert.Empty(await _db.Questions.ToListAsync());
        var request = ConfirmRequest(preview); request.ImportMode = "SelectedValid"; request.SelectedQuestionKeys = new() { "q1" };
        var selected = await Sut().ConfirmAsync(request, default); Assert.True(selected.IsSuccess); Assert.Equal(1, selected.Data!.ImportedCount); Assert.Equal(1, selected.Data.SkippedCount);
    }

    [Theory]
    [InlineData("Unknown")]
    [InlineData("")]
    [InlineData("q1,q1")]
    public async Task SelectedValidRejectsUnknownEmptyOrDuplicateKeys(string keys)
    {
        var preview = await Preview(Header + "\nq1,TOP-1,ShortAnswer,Câu hỏi,2,Lời giải");
        var request = ConfirmRequest(preview); request.ImportMode = "SelectedValid"; request.SelectedQuestionKeys = keys.Length == 0 ? new() : keys.Split(',').ToList();
        Assert.False((await Sut().ConfirmAsync(request, default)).IsSuccess);
        Assert.True((await Sut().ConfirmAsync(ConfirmRequest(preview), default)).IsSuccess);
    }

    [Fact]
    public async Task DirectActivationRevisionAndUnknownPayloadFieldsAreRejected()
    {
        var preview = await Preview(Header + "\nq1,TOP-1,ShortAnswer,Câu hỏi,2,Lời giải");
        var request = ConfirmRequest(preview); request.TargetStatus = "Active"; Assert.False((await Sut().ConfirmAsync(request, default)).IsSuccess);
        request.TargetStatus = "Draft"; request.Revision = 2; Assert.False((await Sut().ConfirmAsync(request, default)).IsSuccess);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<QuestionImportConfirmRequest>("{\"PreviewToken\":\"token\",\"SubjectId\":\"untrusted\"}"));
        Assert.True((await Sut().ConfirmAsync(ConfirmRequest(preview), default)).IsSuccess);
    }

    [Theory]
    [InlineData("teacher")]
    [InlineData("subject")]
    [InlineData("topic")]
    [InlineData("topic-code")]
    [InlineData("topic-type")]
    public async Task ConfirmRevalidatesOwnerSubjectAndTopicAfterPreview(string field)
    {
        var preview = await Preview(Header + "\nq1,TOP-1,ShortAnswer,Câu hỏi,2,Lời giải");
        var node = await _db.KnowledgeNodes.SingleAsync(n => n.NodeId == 201);
        switch (field) { case "teacher": (await _db.Users.SingleAsync()).Status = UserStatus.Disabled; break; case "subject": (await _db.Subjects.SingleAsync()).IsActive = false; break;
            case "topic": node.IsActive = false; break; case "topic-code": node.NodeCode = "Changed"; break; case "topic-type": node.NodeType = NodeType.Chapter; break; }
        await _db.SaveChangesAsync();
        var confirm = await Sut().ConfirmAsync(ConfirmRequest(preview), default); Assert.False(confirm.IsSuccess); Assert.Empty(await _db.Questions.ToListAsync());
    }

    [Fact]
    public async Task PreviewRejectsInactiveOrNonTopicAndCrossSubjectNodes()
    {
        var node = await _db.KnowledgeNodes.SingleAsync(n => n.NodeId == 201); node.NodeType = NodeType.Chapter;
        (await _db.KnowledgeNodes.SingleAsync(n => n.NodeId == 202)).IsActive = false;
        (await _db.KnowledgeNodes.SingleAsync(n => n.NodeId == 203)).SubjectId = Guid.NewGuid(); await _db.SaveChangesAsync();
        var preview = await Preview(Header + "\nq1,TOP-1,ShortAnswer,A,2,Lời giải\nq2,TOP-2,ShortAnswer,B,2,Lời giải\nq3,TOP-3,ShortAnswer,C,2,Lời giải");
        Assert.Equal(3, preview.Data!.InvalidCount); Assert.All(preview.Data.Errors, e => Assert.Equal("TopicCode", e.Field));
    }

    [Fact]
    public async Task TokenIsBoundToActorTenantAndExpires()
    {
        var preview = await Preview(Header + "\nq1,TOP-1,ShortAnswer,Câu hỏi,2,Lời giải"); var request = ConfirmRequest(preview);
        _tenant.Setup(t => t.UserId).Returns(Guid.NewGuid()); Assert.False((await Sut().ConfirmAsync(request, default)).IsSuccess);
        _tenant.Setup(t => t.UserId).Returns(_teacherId); _tenant.Setup(t => t.CenterId).Returns(Guid.NewGuid()); Assert.False((await Sut().ConfirmAsync(request, default)).IsSuccess);
        _tenant.Setup(t => t.CenterId).Returns(_centerId); _clock.Setup(t => t.GetUtcNow()).Returns(_now.AddMinutes(30));
        var expired = await Sut().ConfirmAsync(request, default); Assert.False(expired.IsSuccess); Assert.Contains("hết hạn", expired.ErrorMessage);
    }

    [Fact]
    public async Task UnresolvedAndManagerActorsFailClosed()
    {
        _tenant.Setup(t => t.IsResolved).Returns(false); Assert.False((await Preview("anything")).IsSuccess);
        _tenant.Setup(t => t.IsResolved).Returns(true); _tenant.Setup(t => t.Role).Returns(nameof(UserRole.CenterManager)); Assert.False((await Preview("anything")).IsSuccess);
    }

    [Fact]
    public async Task ClientMutationOfPreviewDataCannotModifyCachedPersistence()
    {
        var defaults = Defaults(); var preview = await Preview(Header + "\nq1,TOP-1,ShortAnswer,Câu hỏi,2,Lời giải", defaults);
        preview.Data!.ValidQuestions[0].CorrectAnswer = "tampered"; preview.Data.ValidQuestions[0].PrimaryTopicNodeId = "203"; defaults.SubjectId = Guid.NewGuid();
        Assert.True((await Sut().ConfirmAsync(ConfirmRequest(preview), default)).IsSuccess);
        var saved = await _db.Questions.SingleAsync(); Assert.Equal("2", saved.CorrectAnswer); Assert.Equal(201UL, saved.PrimaryTopicNodeId); Assert.Equal(_subjectId, saved.SubjectId);
    }

    [Fact]
    public async Task CancelledConfirmReleasesLockAndAllowsRetry_CompletedTokenCannotReplay()
    {
        var preview = await Preview(Header + "\nq1,TOP-1,ShortAnswer,Câu hỏi,2,Lời giải"); var request = ConfirmRequest(preview);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Sut().ConfirmAsync(request, cancelled.Token));
        Assert.True((await Sut().ConfirmAsync(request, default)).IsSuccess); Assert.False((await Sut().ConfirmAsync(request, default)).IsSuccess); Assert.Equal(1, await _db.Questions.CountAsync());
    }

    [Fact]
    public async Task ConcurrentConfirmsOnlyOneWins()
    {
        var preview = await Preview(Header + "\nq1,TOP-1,ShortAnswer,Câu hỏi,2,Lời giải"); var request = ConfirmRequest(preview);
        using var first = new EduTwinDbContext(_options, _accessor.Object); using var second = new EduTwinDbContext(_options, _accessor.Object);
        var results = await Task.WhenAll(Sut(first).ConfirmAsync(request, default), Sut(second).ConfirmAsync(request, default));
        Assert.Single(results, r => r.IsSuccess); Assert.Single(await _db.Questions.ToListAsync());
    }

    [Fact]
    public async Task ActivationPolicyIsRecheckedAndValidationFailureAllowsRetry()
    {
        var policy = new Mock<IQuestionActivationPolicy>(); string? message = "Chế độ chấm không phù hợp";
        policy.Setup(p => p.Validate(It.IsAny<QuestionType>(), It.IsAny<QuestionAnswerEvaluationMode>(), It.IsAny<string?>(), It.IsAny<IReadOnlyList<QuestionOptionValidationItem>>(), out message)).Returns(true);
        policy.Setup(p => p.ValidateCompleteQuestion(It.IsAny<QuestionType>(), It.IsAny<QuestionAnswerEvaluationMode>(), It.IsAny<byte>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<decimal>(), It.IsAny<uint>(), It.IsAny<IReadOnlyList<QuestionOptionValidationItem>>(), out message)).Returns(false);
        var preview = await Preview(Header + "\nq1,TOP-1,ShortAnswer,Câu hỏi,2,Lời giải", sut: Sut(policy: policy.Object));
        var blocked = await Sut(policy: policy.Object).ConfirmAsync(ConfirmRequest(preview), default); Assert.False(blocked.IsSuccess); Assert.Empty(await _db.Questions.ToListAsync());
        Assert.True((await Sut().ConfirmAsync(ConfirmRequest(preview), default)).IsSuccess);
    }

    [Theory]
    [InlineData("NumericRational", "(1;2)")]
    [InlineData("Coordinate2D", "one")]
    [InlineData("MathEquivalent", "sin(x)")]
    public async Task AnswerModesUseSharedGrammarValidation(string mode, string answer)
    {
        var preview = await Preview(Header + $",AnswerEvaluationMode\nq1,TOP-1,ShortAnswer,Câu hỏi,{answer},Lời giải,{mode}");
        Assert.Equal(1, preview.Data!.InvalidCount); Assert.Contains(preview.Data.Errors, e => e.Code == "INVALID_ANSWER");
    }

    [Theory]
    [InlineData("$x", "UNCLOSED_MATH_DELIMITER")]
    [InlineData("$\\frac{1}{2$", "UNBALANCED_MATH_BRACES")]
    [InlineData("$$x$$", "UNSUPPORTED_MATH_PROFILE")]
    [InlineData("\\(x\\)", "UNSUPPORTED_MATH_PROFILE")]
    [InlineData("<script>alert(1)</script>", "UNSAFE_CONTENT")]
    [InlineData("$\\href{url}{x}$", "UNSAFE_CONTENT")]
    [InlineData("$\\def\\foo{x}$", "UNSAFE_CONTENT")]
    public async Task StructuralAndSecurityMathErrorsHaveLocations(string text, string code)
    {
        var preview = await Preview(Header + $"\nq1,TOP-1,ShortAnswer,{text},2,Lời giải");
        Assert.Equal(1, preview.Data!.InvalidCount); var error = Assert.Single(preview.Data.Errors, e => e.Code == code);
        Assert.Equal(2, error.RowIndex); Assert.Equal("q1", error.QuestionKey); Assert.Equal("QuestionText", error.ColumnName);
    }

    [Fact]
    public async Task MathValidatesSolutionExpectedReasoningScoringNotesAndLists()
    {
        var preview = await Preview(Header + ",ExpectedReasoning,ScoringNotes,RequiredIdeas,CommonErrors\nq1,TOP-1,ShortAnswer,Câu hỏi,2,$x,$x,$x,\"[\"\"$x\"\"]\",\"[\"\"$x\"\"]\"");
        Assert.Equal(1, preview.Data!.InvalidCount);
        Assert.Equal(new[] { "CommonErrors", "ExpectedReasoning", "RequiredIdeas", "ScoringNotes", "Solution" }, preview.Data.Errors.Select(e => e.Field).OrderBy(f => f).ToArray());
    }

    [Fact]
    public async Task MathComparisonsEscapedBracesAndLiteralDollarRemainValid()
    {
        var preview = await Preview(Header + "\nq1,TOP-1,ShortAnswer,Tập $\\{x:a<b>c\\}$ và ký hiệu \\$ ngoài công thức,2,Lời giải");
        Assert.Equal(1, preview.Data!.ValidCount); Assert.Empty(preview.Data.Errors);
    }

    [Fact]
    public async Task DuplicateKeysInvalidateAllAffectedQuestions()
    {
        var preview = await Preview(Header + "\nq1,TOP-1,ShortAnswer,A,2,Lời giải\nq1,TOP-2,ShortAnswer,B,2,Lời giải");
        Assert.Equal(2, preview.Data!.InvalidCount); Assert.Equal(2, preview.Data.ErrorCount); Assert.All(preview.Data.Errors, e => Assert.Equal("DUPLICATE_QUESTION_KEY", e.Code));
    }

    [Fact]
    public async Task UnknownHeaderNonemptyContentNeverDropsSilently()
    {
        var preview = await Preview(Header + ",ImageRef\nq1,TOP-1,ShortAnswer,A,2,Lời giải,media/q1.png");
        Assert.Equal(1, preview.Data!.InvalidCount); Assert.Contains(preview.Data.Errors, e => e.ColumnName == "ImageRef");
    }

    [Theory]
    [InlineData("frontend-template-v1.xlsx")]
    [InlineData("frontend-template-v1-vi.xlsx")]
    public async Task SubjectBoundTemplateCannotPreviewInAnotherSubject(string fileName)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "CurriculumAndQuestions", "Fixtures", "Import", fileName);
        await using var file = File.OpenRead(path);
        var preview = await Sut().PreviewAsync(file, fileName, Defaults(), default);
        Assert.False(preview.IsSuccess); Assert.Equal("TEMPLATE_SUBJECT_MISMATCH", preview.ErrorCode);
        Assert.Empty(await _db.Questions.ToListAsync());
    }

    [Fact]
    public async Task SessionLimitAndExpiryCleanupBoundPerActor()
    {
        for (var i = 0; i < 8; i++) Assert.True((await Preview(Header + $"\nq{i},TOP-1,ShortAnswer,Câu hỏi,2,Lời giải")).IsSuccess);
        Assert.Equal("IMPORT_SESSION_LIMIT", (await Preview(Header + "\nq9,TOP-1,ShortAnswer,Câu hỏi,2,Lời giải")).ErrorCode);
        _clock.Setup(t => t.GetUtcNow()).Returns(_now.AddMinutes(31)); Assert.True((await Preview(Header + "\nq10,TOP-1,ShortAnswer,Câu hỏi,2,Lời giải")).IsSuccess);
    }

    private sealed class BlockingImportStream : MemoryStream
    {
        private readonly TaskCompletionSource<bool> _started = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public BlockingImportStream(string csv) : base(Encoding.UTF8.GetBytes(csv)) { }
        public Task Started => _started.Task;
        public void Release() => _release.TrySetResult(true);
        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            _started.TrySetResult(true);
            await _release.Task.WaitAsync(cancellationToken);
            return await base.ReadAsync(buffer, cancellationToken);
        }
    }

    [Fact]
    public async Task TwoPreviewWorkersAreBoundedAndCancellationReleasesCapacity()
    {
        var csv = Header + "\nq1,TOP-1,ShortAnswer,Câu hỏi,2,Lời giải";
        using var firstFile = new BlockingImportStream(csv); using var secondFile = new BlockingImportStream(csv);
        using var firstDb = new EduTwinDbContext(_options, _accessor.Object); using var secondDb = new EduTwinDbContext(_options, _accessor.Object);
        using var cancellation = new CancellationTokenSource();
        var first = Sut(firstDb).PreviewAsync(firstFile, "first.csv", Defaults(), cancellation.Token);
        var second = Sut(secondDb).PreviewAsync(secondFile, "second.csv", Defaults(), default);
        try
        {
            await Task.WhenAll(firstFile.Started, secondFile.Started).WaitAsync(TimeSpan.FromSeconds(10));
            var busy = await Preview(csv); Assert.Equal("IMPORT_BUSY", busy.ErrorCode);
            cancellation.Cancel(); await Assert.ThrowsAnyAsync<OperationCanceledException>(() => first);
            var retry = await Preview(csv); Assert.True(retry.IsSuccess, retry.ErrorMessage);
        }
        finally { firstFile.Release(); secondFile.Release(); }
        Assert.True((await second).IsSuccess);
    }

    [Fact]
    public async Task CancellationBeforePreviewDoesNotConsumeWorkerOrSessionCapacity()
    {
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel();
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(Header + "\nq1,TOP-1,ShortAnswer,Câu hỏi,2,Lời giải"));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Sut().PreviewAsync(input, "questions.csv", Defaults(), cancelled.Token));
        Assert.True((await Preview(Header + "\nq1,TOP-1,ShortAnswer,Câu hỏi,2,Lời giải")).IsSuccess);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SharedStringRepetitionIsChargedPerUsageBeforeValidOrInvalidRowNormalization(bool invalidRows)
    {
        const string ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        const string relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        var cells = new[] { "TopicCode", "QuestionType", "QuestionText", "CorrectAnswer", "Solution", "ExpectedReasoning" };
        var sheet = new StringBuilder($"<worksheet xmlns='{ns}'><sheetData><row r='1'>");
        for (var i = 0; i < cells.Length; i++) sheet.Append($"<c r='{(char)('A' + i)}1' t='inlineStr'><is><t>{cells[i]}</t></is></c>");
        sheet.Append("</row>");
        for (var row = 2; row <= 501; row++)
            sheet.Append($"<row r='{row}'><c r='A{row}' t='inlineStr'><is><t>TOP-1</t></is></c><c r='B{row}' t='inlineStr'><is><t>{(invalidRows ? "Unknown" : "ShortAnswer")}</t></is></c><c r='C{row}' t='s'><v>0</v></c><c r='D{row}' t='inlineStr'><is><t>2</t></is></c><c r='E{row}' t='s'><v>0</v></c><c r='F{row}' t='s'><v>0</v></c></row>");
        sheet.Append("</sheetData></worksheet>");
        using var file = new MemoryStream();
        using (var zip = new System.IO.Compression.ZipArchive(file, System.IO.Compression.ZipArchiveMode.Create, true))
        {
            void Part(string name, string xml)
            {
                using var output = new StreamWriter(zip.CreateEntry(name).Open(), Encoding.UTF8); output.Write(xml);
            }
            Part("xl/workbook.xml", $"<workbook xmlns='{ns}' xmlns:r='{relationships}'><sheets><sheet name='Questions' sheetId='1' r:id='rId1'/></sheets></workbook>");
            Part("xl/_rels/workbook.xml.rels", $"<Relationships xmlns='{relationships}'><Relationship Id='rId1' Target='worksheets/questions.xml'/></Relationships>");
            Part("xl/sharedStrings.xml", $"<sst xmlns='{ns}'><si><t>{new string('x', 20000)}</t></si></sst>");
            Part("xl/worksheets/questions.xml", sheet.ToString());
        }
        Assert.True(file.Length < 100 * 1024); file.Position = 0;
        // A strict policy ensures rejection occurs before per-row business normalization,
        // rather than after serializing the amplified dataset. No process-wide GC metric.
        var policy = new Mock<IQuestionActivationPolicy>(MockBehavior.Strict);
        var result = await Sut(policy: policy.Object).PreviewAsync(file, "repeated-shared-string.xlsx", Defaults(), default);
        Assert.False(result.IsSuccess); Assert.Equal("NORMALIZED_DATA_LIMIT", result.ErrorCode); Assert.Contains("10 MiB", result.ErrorMessage);
        policy.VerifyNoOtherCalls();
        Assert.Empty(await _db.Questions.ToListAsync());
        Assert.True((await Preview(Header + "\nq1,TOP-1,ShortAnswer,Câu hỏi,2,Lời giải")).IsSuccess);
    }

    [Fact]
    public async Task JsonEscapingExpansionIsBoundedBeforeCachingAnIndependentSnapshot()
    {
        // 5.5 MiB of one-byte control characters expands to more than 30 MiB in JSON.
        // Every cell is within the 20,000-character limit; aggregate UTF-8 source is under 10 MiB.
        var csv = new StringBuilder(Header).Append('\n');
        for (var i = 0; i < 500; i++) csv.Append($"q{i},TOP-1,ShortAnswer,").Append('\0', 11000).Append(",2,Lời giải\n");
        var result = await Preview(csv.ToString());
        Assert.False(result.IsSuccess); Assert.Equal("NORMALIZED_DATA_LIMIT", result.ErrorCode); Assert.Contains("30 MiB", result.ErrorMessage);
        Assert.Empty(await _db.Questions.ToListAsync());
        Assert.True((await Preview(Header + "\nq1,TOP-1,ShortAnswer,Câu hỏi,2,Lời giải")).IsSuccess);
    }

    [Fact]
    public async Task LongBackslashFormulasRejectWithinBoundedValidationTime()
    {
        var text = "$" + new string('\\', 19998) + "$";
        var csv = new StringBuilder(Header).Append('\n');
        for (var i = 0; i < 500; i++) csv.Append($"q{i},TOP-1,ShortAnswer,").Append(text).Append(",2,Lời giải\n");
        using var file = new MemoryStream(Encoding.UTF8.GetBytes(csv.ToString()));
        Assert.True(file.Length < 10 * 1024 * 1024);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var result = await Task.Run(() => Sut().PreviewAsync(file, "long-formulas.csv", Defaults(), timeout.Token), timeout.Token).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(result.IsSuccess, result.ErrorMessage); Assert.Equal(500, result.Data!.InvalidCount);
        Assert.Equal(500, result.Data.ErrorCount); Assert.All(result.Data.Errors, error => Assert.Equal("INVALID_MATH_LENGTH", error.Code));
    }

    [Fact]
    public async Task RepeatedUnclosedTagLikeProseRemainsPlainTextWithinBoundedValidationTime()
    {
        var text = string.Concat(Enumerable.Repeat("<a", 10000));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var file = new MemoryStream(Encoding.UTF8.GetBytes(Header + $"\nq1,TOP-1,ShortAnswer,{text},2,Lời giải"));
        var result = await Task.Run(() => Sut().PreviewAsync(file, "comparison-text.csv", Defaults(), timeout.Token), timeout.Token).WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(result.IsSuccess, result.ErrorMessage); Assert.Equal(1, result.Data!.ValidCount); Assert.Empty(result.Data.Errors);
    }

    [Fact]
    public async Task MultilineCsvPreservesUnicodeEscapedQuotesAndRowNumbers()
    {
        var preview = await Preview(Header + "\r\n\r\nq1,TOP-1,ShortAnswer,\"Dòng 1\r\nDòng 2 \"\"trích dẫn\"\" $x^2$\",2,Lời giải");
        var item = Assert.Single(preview.Data!.ValidQuestions); Assert.Equal(3, item.RowIndex); Assert.Equal("Dòng 1\nDòng 2 \"trích dẫn\" $x^2$", item.QuestionText);
    }
    public void Dispose() { _db.Database.EnsureDeleted(); _db.Dispose(); }
}
