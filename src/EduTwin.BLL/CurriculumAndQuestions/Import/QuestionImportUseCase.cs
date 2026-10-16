using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.BLL.AssessmentAndReasoning.PreliminaryGrading;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Logging;

namespace EduTwin.BLL.CurriculumAndQuestions.Import;

/// <summary>V1 text/inline math import. Client checks rendering syntax; persistence always creates Drafts.</summary>
public sealed class QuestionImportUseCase : IQuestionImportUseCase
{
    private const long MaxNormalizedDataBytes = 10 * 1024 * 1024;
    private const long MaxPreviewCacheBytes = 30 * 1024 * 1024;
    private static readonly ConcurrentDictionary<string, PreviewCacheEntry> PreviewCache = new(StringComparer.Ordinal);
    private static readonly object CacheGate = new();
    private static readonly SemaphoreSlim PreviewSlots = new(2, 2);
    private static readonly Timer CleanupTimer = new(_ => CleanupExpired(), null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
    private sealed class PreviewCacheEntry
    {
        public required Guid CenterId { get; init; }
        public required Guid ActorId { get; init; }
        public required Guid TeacherId { get; init; }
        public required Guid SubjectId { get; init; }
        public required string ActorRole { get; init; }
        public required DateTimeOffset ExpiresAt { get; init; }
        public required TimeProvider Clock { get; init; }
        public required string FileHash { get; init; }
        public required string DefaultsHash { get; init; }
        public required string TemplateVersion { get; init; }
        public required long CacheBytes { get; init; }
        public required int TotalRows { get; init; }
        public required int InvalidCount { get; init; }
        public required List<QuestionImportItemDto> Questions { get; init; }
        public int Revision { get; init; } = 1;
        private int _status;
        public bool TryBeginProcessing() => Interlocked.CompareExchange(ref _status, 1, 0) == 0;
        public void Reset() => Interlocked.CompareExchange(ref _status, 0, 1);
        public void Complete() => Interlocked.Exchange(ref _status, 2);
        public bool IsProcessing => Volatile.Read(ref _status) == 1;
    }
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;
    private readonly IQuestionActivationPolicy _activationPolicy;
    private readonly ILogger<QuestionImportUseCase>? _logger;
    public QuestionImportUseCase(EduTwinDbContext dbContext, ITenantContext tenantContext, TimeProvider? timeProvider = null,
        IMathAnswerNormalizer? mathNormalizer = null, ICoordinateAnswerNormalizer? coordinateNormalizer = null,
        IQuestionActivationPolicy? activationPolicy = null, ILogger<QuestionImportUseCase>? logger = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _timeProvider = timeProvider ?? TimeProvider.System;
        var math = mathNormalizer ?? new MathAnswerNormalizer();
        _activationPolicy = activationPolicy ?? new QuestionActivationPolicy(math, coordinateNormalizer ?? new CoordinateAnswerNormalizer(math));
        _logger = logger;
    }

    public async Task<QuestionImportPreviewResult> PreviewAsync(Stream fileStream, string fileName, QuestionImportPreviewRequest request, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryGetActor(out var centerId, out var actorId)) return QuestionImportPreviewResult.Failure("FORBIDDEN_RESOURCE", "Chỉ giáo viên hợp lệ trong trung tâm được nhập câu hỏi.");
        var defaultsError = ValidateDefaults(request);
        if (defaultsError is not null) return QuestionImportPreviewResult.ValidationFailed(defaultsError);
        lock (CacheGate)
        {
            CleanupExpired();
            if (PreviewCache.Count >= 256 || PreviewCache.Values.Sum(e => e.CacheBytes) >= MaxPreviewCacheBytes ||
                PreviewCache.Values.Count(e => e.CenterId == centerId && e.ActorId == actorId) >= 8)
                return QuestionImportPreviewResult.Failure("IMPORT_SESSION_LIMIT", "Đã đạt giới hạn phiên xem trước. Hoàn tất hoặc chờ phiên cũ hết hạn rồi thử lại.");
        }
        // Do not queue unbounded uploads while another workbook occupies memory.
        if (!PreviewSlots.Wait(0, cancellationToken))
            return QuestionImportPreviewResult.Failure("IMPORT_BUSY", "Hệ thống đang kiểm tra hai tệp khác. Vui lòng thử lại sau ít giây.");
        try { return await PreviewCoreAsync(fileStream, fileName, request, cancellationToken); }
        finally { PreviewSlots.Release(); }
    }

    private async Task<QuestionImportPreviewResult> PreviewCoreAsync(Stream fileStream, string fileName, QuestionImportPreviewRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var centerId, out var actorId)) return QuestionImportPreviewResult.Failure("FORBIDDEN_RESOURCE", "Chỉ giáo viên hợp lệ trong trung tâm được nhập câu hỏi.");
        var defaultsError = ValidateDefaults(request);
        if (defaultsError is not null) return QuestionImportPreviewResult.ValidationFailed(defaultsError);
        if (!await OwnerExistsAsync(centerId, actorId, cancellationToken)) return QuestionImportPreviewResult.Failure("NOT_FOUND", "Giáo viên sở hữu không còn hoạt động trong trung tâm.");
        if (!await SubjectExistsAsync(centerId, request.SubjectId, cancellationToken)) return QuestionImportPreviewResult.Failure("NOT_FOUND", "Môn học không tồn tại hoặc không hoạt động trong trung tâm.");
        var topics = await _dbContext.KnowledgeNodes.AsNoTracking().Where(n => n.CenterId == centerId && n.SubjectId == request.SubjectId && n.IsActive && !n.IsDeleted && n.NodeType == NodeType.Topic).ToListAsync(cancellationToken);
        var defaultTopic = request.DefaultTopicNodeId.HasValue ? topics.SingleOrDefault(n => n.NodeId == request.DefaultTopicNodeId) : null;
        if (request.DefaultTopicNodeId.HasValue && defaultTopic is null) return QuestionImportPreviewResult.ValidationFailed("Chủ đề mặc định phải là Topic đang hoạt động thuộc môn đã chọn.");
        ParsedQuestionImportFile parsed;
        try { parsed = await QuestionImportFileParser.ParseAsync(fileStream, fileName, request.LegacyMode, cancellationToken); }
        catch (OperationCanceledException) { throw; }
        catch (QuestionImportFileException ex) { return QuestionImportPreviewResult.Failure(ex.Code, ex.Message); }
        catch (Exception ex) when (ex is InvalidDataException or IOException or System.Xml.XmlException)
        {
            _logger?.LogWarning("Question import parse failed. ExceptionType={ExceptionType}", ex.GetType().Name);
            return QuestionImportPreviewResult.ValidationFailed("Không thể đọc tệp hoặc cấu trúc tệp không hợp lệ.");
        }
        if (parsed.SourceSubjectId.HasValue && parsed.SourceSubjectId.Value != request.SubjectId)
            return QuestionImportPreviewResult.Failure("TEMPLATE_SUBJECT_MISMATCH", "Môn học trong Guide của template khác môn đã chọn. Tải đúng template theo môn hoặc kiểm tra lại môn đích.");
        if (parsed.Rows.Count == 0) return QuestionImportPreviewResult.ValidationFailed("Tệp không có câu hỏi.");
        // Shared strings can repeat a large cell hundreds of times in a tiny workbook.
        // Charge each usage (including invalid cells), before normalization or JSON cloning.
        if (!FitsNormalizedBudget(parsed, cancellationToken))
            return QuestionImportPreviewResult.Failure("NORMALIZED_DATA_LIMIT", "Tổng nội dung câu hỏi sau mở rộng vượt 10 MiB. Chia thành nhiều tệp nhỏ hơn; mỗi ô shared string được tính theo số lần dùng.");
        if (parsed.Diagnostics.Count > 0) return QuestionImportPreviewResult.ValidationFailed(string.Join("; ", parsed.Diagnostics.Select(d => d.Message)));
        var errors = new List<QuestionImportRowErrorDto>();
        var warnings = new List<QuestionImportRowErrorDto>();
        var candidates = new List<QuestionImportItemDto>();
        foreach (var row in parsed.Rows) { cancellationToken.ThrowIfCancellationRequested(); candidates.Add(NormalizeRow(row, request, topics, defaultTopic, errors)); }
        foreach (var duplicate in candidates.GroupBy(q => q.QuestionKey, StringComparer.Ordinal).Where(g => g.Count() > 1))
            foreach (var item in duplicate) AddError(errors, item, "QuestionKey", "DUPLICATE_QUESTION_KEY", "QuestionKey phải duy nhất trong tệp.", item.QuestionKey);
        var invalidRows = errors.Select(e => (e.SheetName, e.RowIndex)).ToHashSet();
        var valid = candidates.Where(q => !invalidRows.Contains((q.SheetName, q.RowIndex))).ToList();
        foreach (var duplicate in valid.GroupBy(q => q.QuestionText, StringComparer.Ordinal).Where(g => g.Count() > 1))
            foreach (var item in duplicate) AddError(warnings, item, "QuestionText", "DUPLICATE_CONTENT", "Có câu cùng nội dung trong tệp; import sẽ tạo câu mới.");
        using var serialized = new BoundedPreviewStream(MaxPreviewCacheBytes);
        try { await JsonSerializer.SerializeAsync(serialized, valid, cancellationToken: cancellationToken); }
        catch (NormalizedDataLimitException)
        {
            return QuestionImportPreviewResult.Failure("NORMALIZED_DATA_LIMIT", "Dữ liệu JSON của phiên xem trước vượt 30 MiB. Chia câu hỏi thành nhiều tệp nhỏ hơn.");
        }
        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
        PreviewCacheEntry cached;
        lock (CacheGate)
        {
            CleanupExpired();
            if (PreviewCache.Count >= 256 || PreviewCache.Values.Sum(e => e.CacheBytes) + serialized.Length > MaxPreviewCacheBytes || PreviewCache.Values.Count(e => e.CenterId == centerId && e.ActorId == actorId) >= 8)
                return QuestionImportPreviewResult.Failure("IMPORT_SESSION_LIMIT", "Đã đạt giới hạn phiên xem trước. Hoàn tất hoặc chờ phiên cũ hết hạn rồi thử lại.");
            // Admit while holding the quota lock, before allocating an independent snapshot.
            // Deserialize from the bounded buffer directly, avoiding an additional ToArray copy.
            cancellationToken.ThrowIfCancellationRequested();
            cached = new PreviewCacheEntry
            {
                CenterId = centerId, ActorId = actorId, TeacherId = actorId, SubjectId = request.SubjectId, ActorRole = _tenantContext.Role!,
                ExpiresAt = _timeProvider.GetUtcNow().AddMinutes(30), Clock = _timeProvider, FileHash = parsed.FileHash,
                DefaultsHash = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(request))), TemplateVersion = request.TemplateVersion,
                TotalRows = parsed.Rows.Count, InvalidCount = invalidRows.Count,
                Questions = JsonSerializer.Deserialize<List<QuestionImportItemDto>>(serialized.GetBuffer().AsSpan(0, checked((int)serialized.Length)))!, CacheBytes = serialized.Length
            };
            PreviewCache[token] = cached;
        }
        return QuestionImportPreviewResult.Success(new QuestionImportPreviewDataDto
        {
            PreviewToken = token, Revision = 1, SubjectId = request.SubjectId.ToString(), TemplateVersion = request.TemplateVersion, ExpiresAt = cached.ExpiresAt.UtcDateTime,
            TotalRows = parsed.Rows.Count, ValidCount = valid.Count, InvalidCount = invalidRows.Count, ErrorCount = errors.Count, WarningCount = warnings.Count,
            ValidQuestions = valid, Errors = errors, Warnings = warnings
        });
    }

    private static bool FitsNormalizedBudget(ParsedQuestionImportFile parsed, CancellationToken cancellationToken)
    {
        long usedBytes = 0;
        bool Charge(string? text)
        {
            if (text is not null) usedBytes += Encoding.UTF8.GetByteCount(text);
            return usedBytes <= MaxNormalizedDataBytes;
        }
        foreach (var row in parsed.Rows)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var (field, value) in row.Values) if (!Charge(field) || !Charge(value)) return false;
            foreach (var diagnostic in row.Diagnostics)
                if (!Charge(diagnostic.RawValue) || !Charge(diagnostic.Message) || !Charge(diagnostic.ColumnName)) return false;
        }
        foreach (var diagnostic in parsed.Diagnostics)
            if (!Charge(diagnostic.RawValue) || !Charge(diagnostic.Message)) return false;
        return true;
    }

    private sealed class NormalizedDataLimitException : Exception { }
    private sealed class BoundedPreviewStream(long limit) : MemoryStream
    {
        private void CheckWrite(int count)
        {
            if (Position > limit - count) throw new NormalizedDataLimitException();
            GrowWithinLimit(Position + count);
        }
        private void GrowWithinLimit(long required)
        {
            if (required > Capacity)
                Capacity = checked((int)Math.Min(limit, Math.Max(required, Math.Max(256L, Capacity * 2L))));
        }
        public override void Write(byte[] buffer, int offset, int count) { CheckWrite(count); base.Write(buffer, offset, count); }
        public override void Write(ReadOnlySpan<byte> buffer) { CheckWrite(buffer.Length); base.Write(buffer); }
        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested(); CheckWrite(count); return base.WriteAsync(buffer, offset, count, cancellationToken);
        }
        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested(); CheckWrite(buffer.Length); return base.WriteAsync(buffer, cancellationToken);
        }
        public override void SetLength(long value)
        {
            if (value > limit) throw new NormalizedDataLimitException();
            GrowWithinLimit(value);
            base.SetLength(value);
        }
    }

    public async Task<QuestionImportConfirmResult> ConfirmAsync(QuestionImportConfirmRequest request, CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var centerId, out var actorId)) return QuestionImportConfirmResult.Failure("FORBIDDEN_RESOURCE", "Chỉ giáo viên hợp lệ trong trung tâm được nhập câu hỏi.");
        if (request is null || string.IsNullOrWhiteSpace(request.PreviewToken) || request.TargetStatus != "Draft" || request.Revision != 1 || (request.ImportMode != "AllValid" && request.ImportMode != "SelectedValid") || request.SelectedQuestionKeys is null)
            return QuestionImportConfirmResult.ValidationFailed("Confirm cần token, revision hợp lệ, ImportMode AllValid/SelectedValid và TargetStatus Draft.");
        if (!PreviewCache.TryGetValue(request.PreviewToken, out var cached) || cached.CenterId != centerId || cached.ActorId != actorId || cached.ActorRole != _tenantContext.Role || cached.Revision != request.Revision)
            return QuestionImportConfirmResult.ValidationFailed("Phiên xem trước không tồn tại hoặc không thuộc người dùng hiện tại. Vui lòng tải lại tệp.");
        if (cached.ExpiresAt <= _timeProvider.GetUtcNow())
        {
            if (!cached.IsProcessing) PreviewCache.TryRemove(request.PreviewToken, out _);
            return QuestionImportConfirmResult.ValidationFailed("Phiên xem trước đã hết hạn. Vui lòng tải lại tệp.");
        }
        if (!cached.TryBeginProcessing()) return QuestionImportConfirmResult.ValidationFailed("Phiên đang được nhập hoặc đã hoàn tất; không thể xác nhận đồng thời.");
        IDbContextTransaction? transaction = null;
        var completed = false;
        var commitStarted = false;
        var addedQuestions = new List<Question>();
        var addedOptions = new List<QuestionOption>();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            transaction = _dbContext.Database.IsRelational()
                ? await _dbContext.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken)
                : await _dbContext.Database.BeginTransactionAsync(cancellationToken);
            if (!await OwnerExistsAsync(centerId, cached.TeacherId, cancellationToken) || !await SubjectExistsAsync(centerId, cached.SubjectId, cancellationToken)) return QuestionImportConfirmResult.NotFound("Giáo viên hoặc môn học không còn hoạt động. Vui lòng xem trước lại.");
            List<QuestionImportItemDto> selected;
            if (request.ImportMode == "AllValid")
            {
                if (cached.InvalidCount > 0 || request.SelectedQuestionKeys.Count > 0) return QuestionImportConfirmResult.ValidationFailed("Tệp còn câu lỗi. Chọn rõ SelectedValid để chỉ nhập các câu hợp lệ.");
                selected = cached.Questions;
            }
            else
            {
                var keys = request.SelectedQuestionKeys.ToHashSet(StringComparer.Ordinal);
                if (keys.Count == 0 || keys.Count != request.SelectedQuestionKeys.Count || keys.Any(string.IsNullOrWhiteSpace) || keys.Any(k => !cached.Questions.Any(q => q.QuestionKey == k))) return QuestionImportConfirmResult.ValidationFailed("Danh sách câu được chọn rỗng, trùng hoặc chứa câu không hợp lệ trong phiên.");
                selected = cached.Questions.Where(q => keys.Contains(q.QuestionKey)).ToList();
            }
            if (selected.Count == 0) return QuestionImportConfirmResult.ValidationFailed("Không có câu hợp lệ để nhập.");
            var topicIds = selected.Select(q => ulong.Parse(q.PrimaryTopicNodeId, CultureInfo.InvariantCulture)).Distinct().ToList();
            var currentTopics = await _dbContext.KnowledgeNodes.AsNoTracking().Where(n => n.CenterId == centerId && n.SubjectId == cached.SubjectId && topicIds.Contains(n.NodeId) && n.IsActive && !n.IsDeleted && n.NodeType == NodeType.Topic).ToListAsync(cancellationToken);
            if (currentTopics.Count != topicIds.Count || selected.Any(q => !currentTopics.Any(n => n.NodeId.ToString(CultureInfo.InvariantCulture) == q.PrimaryTopicNodeId && n.NodeCode == q.TopicCode))) return QuestionImportConfirmResult.NotFound("Chủ đề đã đổi hoặc không còn hoạt động trong môn đã chọn. Vui lòng xem trước lại.");
            foreach (var item in selected) if (!ValidateCompleteItem(item, out var message)) return QuestionImportConfirmResult.ValidationFailed($"Dòng {item.RowIndex}: {message}");
            var now = _timeProvider.GetUtcNow().UtcDateTime;
            foreach (var item in selected)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var question = new Question
                {
                    CenterId = centerId, SubjectId = cached.SubjectId, PrimaryTopicNodeId = ulong.Parse(item.PrimaryTopicNodeId, CultureInfo.InvariantCulture), CreatedByTeacherId = cached.TeacherId,
                    Visibility = Enum.Parse<MaterialVisibility>(item.Visibility), GradeLevel = item.GradeLevel, QuestionType = item.QuestionType, Difficulty = item.Difficulty,
                    QuestionText = item.QuestionText, CorrectAnswer = item.CorrectAnswer, Solution = item.Solution, ExpectedReasoning = item.ExpectedReasoning, GradingCriteria = CriteriaFor(item),
                    MaxScore = item.MaxScore, EstimatedTimeSeconds = item.EstimatedTimeSeconds, ReasoningRequired = item.ReasoningRequired, LanguageCode = item.LanguageCode,
                    Status = QuestionStatus.Draft, AnswerEvaluationMode = item.AnswerEvaluationMode, CreatedAt = now, UpdatedAt = now, CreatedBy = actorId, UpdatedBy = actorId
                };
                _dbContext.Questions.Add(question); addedQuestions.Add(question);
            }
            // Assign all generated IDs in one save, then add options in a second save.
            // Saving inside the question loop repeatedly scans a growing change tracker.
            await _dbContext.SaveChangesAsync(cancellationToken);
            for (var i = 0; i < selected.Count; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var item = selected[i];
                var question = addedQuestions[i];
                foreach (var option in item.Options)
                {
                    var entity = new QuestionOption { OptionId = checked(question.QuestionId * 1000 + option.OrderIndex), CenterId = centerId, QuestionId = question.QuestionId, OptionLabel = option.OptionLabel,
                        OptionText = option.OptionText, IsCorrect = option.IsCorrect, OrderIndex = option.OrderIndex, CreatedAt = now, UpdatedAt = now, CreatedBy = actorId, UpdatedBy = actorId };
                    _dbContext.QuestionOptions.Add(entity); addedOptions.Add(entity);
                }
            }
            await _dbContext.SaveChangesAsync(cancellationToken);
            commitStarted = true;
            await transaction.CommitAsync(cancellationToken);
            completed = true; cached.Complete(); PreviewCache.TryRemove(request.PreviewToken, out _);
            return QuestionImportConfirmResult.Success(new QuestionImportConfirmDataDto { ImportedCount = selected.Count, SkippedCount = cached.TotalRows - selected.Count,
                QuestionIds = addedQuestions.Select(q => q.QuestionId.ToString(CultureInfo.InvariantCulture)).ToList(), Message = $"Đã nhập {selected.Count} câu hỏi ở trạng thái Draft. Rà soát trong editor trước khi kích hoạt." });
        }
        catch (OperationCanceledException)
        {
            await RollbackAsync(transaction);
            if (commitStarted) { cached.Complete(); PreviewCache.TryRemove(request.PreviewToken, out _); }
            throw;
        }
        catch (Exception ex)
        {
            await RollbackAsync(transaction);
            if (commitStarted)
            {
                cached.Complete(); PreviewCache.TryRemove(request.PreviewToken, out _);
                return QuestionImportConfirmResult.Failure("IMPORT_COMMIT_UNCERTAIN", "Không xác định được kết quả commit. Kiểm tra kho câu hỏi trước khi tải lại tệp để tránh nhập trùng.");
            }
            _logger?.LogError("Question import failed. CenterId={CenterId} SubjectId={SubjectId} ExceptionType={ExceptionType}", centerId, cached.SubjectId, ex.GetType().Name);
            return QuestionImportConfirmResult.Failure("IMPORT_FAILED", "Không thể lưu lô câu hỏi. Toàn bộ lô đã được rollback; có thể thử lại phiên xem trước.");
        }
        finally
        {
            try { if (transaction is not null) await transaction.DisposeAsync(); }
            catch (Exception ex) { _logger?.LogWarning("Import transaction disposal failed. ExceptionType={ExceptionType}", ex.GetType().Name); }
            finally
            {
                if (!completed)
                {
                    foreach (var option in addedOptions) _dbContext.Entry(option).State = EntityState.Detached;
                    foreach (var question in addedQuestions) _dbContext.Entry(question).State = EntityState.Detached;
                    cached.Reset();
                }
            }
        }
    }

    private QuestionImportItemDto NormalizeRow(RawImportRow row, QuestionImportPreviewRequest defaults, List<KnowledgeNode> topics, KnowledgeNode? defaultTopic, List<QuestionImportRowErrorDto> errors)
    {
        string Value(string field) => row.Values.TryGetValue(field, out var value) ? value : string.Empty;
        var item = new QuestionImportItemDto { RowIndex = row.RowIndex, SheetName = row.SheetName, QuestionKey = Value("QuestionKey").Trim(), TopicCode = Value("TopicCode").Trim(),
            QuestionText = Value("QuestionText"), CorrectAnswer = Value("CorrectAnswer"), Solution = Value("Solution"), ExpectedReasoning = NullIfEmpty(Value("ExpectedReasoning")), ScoringNotes = Value("ScoringNotes") };
        if (item.QuestionKey.Length == 0) { item.QuestionKey = $"{row.SheetName}:{row.RowIndex}"; item.DefaultedFields.Add("QuestionKey"); }
        if (item.QuestionKey.Length > 64) AddError(errors, item, "QuestionKey", "FIELD_TOO_LONG", "QuestionKey tối đa 64 ký tự.");
        foreach (var diagnostic in row.Diagnostics) AddError(errors, item, diagnostic.ColumnName ?? "File", diagnostic.Code, diagnostic.Message, diagnostic.RawValue);
        var type = Value("QuestionType").Trim();
        if (type.Length == 0 && defaults.LegacyMode) { type = "MultipleChoice"; item.DefaultedFields.Add("QuestionType"); }
        if (defaults.LegacyMode) type = LegacyType(type);
        if (!TryEnum(type, out QuestionType questionType)) AddError(errors, item, "QuestionType", "INVALID_ENUM", "QuestionType bắt buộc: MultipleChoice, ShortAnswer hoặc Essay.", type);
        item.QuestionType = questionType;
        if (item.TopicCode.Length == 0 && defaults.LegacyMode && defaultTopic is not null) { item.TopicCode = defaultTopic.NodeCode; item.DefaultedFields.Add("TopicCode"); }
        var matches = topics.Where(t => t.NodeCode == item.TopicCode).ToList();
        if (matches.Count != 1) AddError(errors, item, "TopicCode", "INVALID_TOPIC", "TopicCode phải khớp chính xác một Topic đang hoạt động thuộc môn đã chọn.", item.TopicCode);
        else { item.PrimaryTopicNodeId = matches[0].NodeId.ToString(CultureInfo.InvariantCulture); item.TopicName = matches[0].NodeName; }
        string DefaultValue(string field, string value)
        {
            var raw = Value(field).Trim();
            if (raw.Length > 0) return raw;
            item.DefaultedFields.Add(field); return value;
        }
        var grade = DefaultValue("GradeLevel", defaults.GradeLevel?.ToString(CultureInfo.InvariantCulture) ?? string.Empty);
        if (!byte.TryParse(grade, NumberStyles.None, CultureInfo.InvariantCulture, out var gradeLevel) || gradeLevel is < 10 or > 12) AddError(errors, item, "GradeLevel", "INVALID_GRADE", "GradeLevel phải từ 10 đến 12; điền theo câu hoặc chọn mặc định của lô.", grade);
        item.GradeLevel = gradeLevel;
        var difficulty = DefaultValue("Difficulty", defaults.Difficulty.ToString(CultureInfo.InvariantCulture));
        if (!byte.TryParse(difficulty, NumberStyles.None, CultureInfo.InvariantCulture, out var difficultyValue) || difficultyValue is < 1 or > 5) AddError(errors, item, "Difficulty", "INVALID_DIFFICULTY", "Difficulty phải là số nguyên từ 1 đến 5.", difficulty);
        item.Difficulty = difficultyValue;
        var score = DefaultValue("MaxScore", defaults.MaxScore.ToString(CultureInfo.InvariantCulture));
        if (!decimal.TryParse(score, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var scoreValue) || !QuestionScoreLimits.IsValid(scoreValue)) AddError(errors, item, "MaxScore", "INVALID_SCORE", "MaxScore phải lớn hơn 0, tối đa 999.99 và tối đa 2 chữ số thập phân; dùng dấu chấm.", score);
        item.MaxScore = scoreValue;
        var time = DefaultValue("EstimatedTimeSeconds", defaults.EstimatedTimeSeconds.ToString(CultureInfo.InvariantCulture));
        if (!uint.TryParse(time, NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) || seconds == 0) AddError(errors, item, "EstimatedTimeSeconds", "INVALID_TIME", "EstimatedTimeSeconds phải là số nguyên dương.", time);
        item.EstimatedTimeSeconds = seconds;
        var reasoning = DefaultValue("ReasoningRequired", defaults.ReasoningRequired ? "true" : "false");
        if (!TryBoolean(reasoning, defaults.LegacyMode, out var reasoningValue)) AddError(errors, item, "ReasoningRequired", "INVALID_BOOLEAN", "ReasoningRequired chỉ nhận true/false hoặc 1/0.", reasoning);
        item.ReasoningRequired = reasoningValue;
        item.Visibility = DefaultValue("Visibility", defaults.Visibility);
        if (!TryEnum(item.Visibility, out MaterialVisibility _)) AddError(errors, item, "Visibility", "INVALID_ENUM", "Visibility chỉ nhận Private hoặc Shared.", item.Visibility);
        item.LanguageCode = DefaultValue("LanguageCode", defaults.LanguageCode);
        if (item.LanguageCode is not ("vi" or "en")) AddError(errors, item, "LanguageCode", "INVALID_LANGUAGE", "LanguageCode chỉ nhận vi hoặc en.", item.LanguageCode);
        var defaultMode = questionType == QuestionType.MultipleChoice ? "TextExact" : questionType == QuestionType.Essay ? "Manual" : defaults.LegacyMode ? "TextExact" : defaults.ShortAnswerEvaluationMode;
        var mode = DefaultValue("AnswerEvaluationMode", defaultMode);
        if (defaults.LegacyMode) mode = LegacyMode(mode);
        if (!TryEnum(mode, out QuestionAnswerEvaluationMode evaluationMode)) AddError(errors, item, "AnswerEvaluationMode", "INVALID_ENUM", "Chế độ chấm không hợp lệ.", mode);
        item.AnswerEvaluationMode = evaluationMode;
        foreach (var label in new[] { "A", "B", "C", "D" })
        {
            var text = Value($"Option{label}"); var misconception = Value($"Misconception{label}");
            if (questionType != QuestionType.MultipleChoice && (!string.IsNullOrWhiteSpace(text) || !string.IsNullOrWhiteSpace(misconception))) AddError(errors, item, $"Option{label}", "UNEXPECTED_OPTION", "Lựa chọn chỉ được dùng cho MultipleChoice.");
            if (questionType == QuestionType.MultipleChoice && !string.IsNullOrWhiteSpace(text)) item.Options.Add(new QuestionOptionInput { OptionLabel = label, OptionText = text,
                IsCorrect = item.CorrectAnswer.Trim() == label || (defaults.LegacyMode && (item.CorrectAnswer.Trim().Equals(label, StringComparison.OrdinalIgnoreCase) || item.CorrectAnswer.Trim().Equals(text.Trim(), StringComparison.OrdinalIgnoreCase))), OrderIndex = (uint)item.Options.Count + 1, Misconception = NullIfEmpty(misconception) });
            if (!string.IsNullOrWhiteSpace(misconception) && (string.IsNullOrWhiteSpace(text) || item.Options.Any(o => o.OptionLabel == label && o.IsCorrect)))
                AddError(errors, item, $"Misconception{label}", "INVALID_MISCONCEPTION", "Misconception chỉ được ghi cho lựa chọn sai có nội dung.");
        }
        if (questionType == QuestionType.MultipleChoice && !defaults.LegacyMode && item.Options.Count != 4)
            AddError(errors, item, "Options", "INCOMPLETE_MCQ_OPTIONS", "Template V1 yêu cầu đầy đủ 4 lựa chọn A, B, C và D.");
        if (questionType == QuestionType.MultipleChoice && item.Options.Count(o => o.IsCorrect) == 1) item.CorrectAnswer = item.Options.Single(o => o.IsCorrect).OptionLabel;
        item.RequiredIdeas = ParseList(Value("RequiredIdeas"), "RequiredIdeas", item, errors);
        item.CommonErrors = ParseList(Value("CommonErrors"), "CommonErrors", item, errors);
        if (string.IsNullOrWhiteSpace(item.QuestionText)) AddError(errors, item, "QuestionText", "REQUIRED_FIELD", "QuestionText không được để trống trong import text-only V1.");
        if (string.IsNullOrWhiteSpace(item.CorrectAnswer)) AddError(errors, item, "CorrectAnswer", "REQUIRED_FIELD", "CorrectAnswer không được để trống.");
        if (string.IsNullOrWhiteSpace(item.Solution)) AddError(errors, item, "Solution", "REQUIRED_FIELD", "Solution không được để trống.");
        if (!_activationPolicy.Validate(questionType, evaluationMode, item.CorrectAnswer, item.Options.Select(o => new QuestionOptionValidationItem(o.OptionLabel, o.OptionText, o.IsCorrect)).ToList(), out var policyError))
            AddError(errors, item, questionType == QuestionType.MultipleChoice ? "Options" : "CorrectAnswer", "INVALID_ANSWER", policyError ?? "Câu hỏi không hợp lệ.");
        foreach (var message in GradingCriteriaValidator.Validate(CriteriaFor(item), item.MaxScore)) AddError(errors, item, "GradingCriteria", "INVALID_GRADING_CRITERIA", message);
        foreach (var (field, text) in MathFields(item)) ValidateMathContent(item, field, text, errors);
        return item;
    }
    private bool ValidateCompleteItem(QuestionImportItemDto item, out string? message)
    {
        message = null;
        if (item.GradeLevel is < 10 or > 12 || !QuestionScoreLimits.IsValid(item.MaxScore) || !TryEnum(item.Visibility, out MaterialVisibility _) || item.LanguageCode is not ("vi" or "en")) { message = "Metadata của câu hỏi không hợp lệ."; return false; }
        var errors = new List<QuestionImportRowErrorDto>();
        foreach (var (field, text) in MathFields(item)) ValidateMathContent(item, field, text, errors);
        if (errors.Count > 0) { message = errors[0].ErrorMessage; return false; }
        var criteriaErrors = GradingCriteriaValidator.Validate(CriteriaFor(item), item.MaxScore);
        if (criteriaErrors.Count > 0) { message = criteriaErrors[0]; return false; }
        return _activationPolicy.ValidateCompleteQuestion(item.QuestionType, item.AnswerEvaluationMode, item.Difficulty, item.QuestionText, item.CorrectAnswer, item.Solution, item.MaxScore,
            item.EstimatedTimeSeconds, item.Options.Select(o => new QuestionOptionValidationItem(o.OptionLabel, o.OptionText, o.IsCorrect)).ToList(), out message);
    }
    private static GradingCriteria CriteriaFor(QuestionImportItemDto item) => QuestionOptionMisconceptionStore.Apply(
        new GradingCriteria { RequiredIdeas = item.RequiredIdeas.ToList(), CommonErrors = item.CommonErrors.ToList(), ScoringNotes = item.ScoringNotes }, item.Options);
    private bool TryGetActor(out Guid centerId, out Guid actorId)
    {
        centerId = _tenantContext.CenterId ?? Guid.Empty; actorId = _tenantContext.UserId ?? Guid.Empty;
        return _tenantContext.IsResolved && centerId != Guid.Empty && actorId != Guid.Empty && _tenantContext.Role == nameof(UserRole.Teacher);
    }
    private Task<bool> OwnerExistsAsync(Guid centerId, Guid teacherId, CancellationToken ct) => _dbContext.Teachers.AsNoTracking().AnyAsync(t => t.CenterId == centerId && t.TeacherId == teacherId && !t.IsDeleted &&
        t.User != null && t.User.CenterId == centerId && !t.User.IsDeleted && t.User.RoleName == UserRole.Teacher && t.User.Status == UserStatus.Active, ct);
    private Task<bool> SubjectExistsAsync(Guid centerId, Guid subjectId, CancellationToken ct) => _dbContext.Subjects.AsNoTracking().AnyAsync(s => s.CenterId == centerId && s.SubjectId == subjectId && s.IsActive && !s.IsDeleted, ct);
    private static string? ValidateDefaults(QuestionImportPreviewRequest? request)
    {
        if (request is null || request.SubjectId == Guid.Empty || request.TeacherId.HasValue) return "SubjectId bắt buộc; giáo viên chỉ import cho mình và không được chỉ định TeacherId.";
        if (request.TemplateVersion != "1.0") return "Chỉ hỗ trợ TemplateVersion 1.0.";
        if (request.GradeLevel.HasValue && request.GradeLevel is < 10 or > 12) return "GradeLevel mặc định phải từ 10 đến 12.";
        if (request.Difficulty is < 1 or > 5 || !QuestionScoreLimits.IsValid(request.MaxScore) || request.EstimatedTimeSeconds == 0) return "Độ khó, điểm tối đa hoặc thời gian mặc định không hợp lệ.";
        if (!TryEnum(request.Visibility, out MaterialVisibility _) || request.LanguageCode is not ("vi" or "en")) return "Visibility hoặc LanguageCode mặc định không hợp lệ.";
        if (!TryEnum(request.ShortAnswerEvaluationMode, out QuestionAnswerEvaluationMode _)) return "ShortAnswerEvaluationMode mặc định không hợp lệ.";
        if (request.DefaultTopicNodeId.HasValue && (!request.LegacyMode || request.DefaultTopicNodeId == 0)) return "Chỉ LegacyMode được dùng DefaultTopicNodeId; ID phải lớn hơn 0.";
        return null;
    }
    private static void CleanupExpired()
    {
        foreach (var pair in PreviewCache) if (!pair.Value.IsProcessing && pair.Value.ExpiresAt <= pair.Value.Clock.GetUtcNow()) PreviewCache.TryRemove(pair.Key, out _);
        GC.KeepAlive(CleanupTimer);
    }
    private async Task RollbackAsync(IDbContextTransaction? transaction)
    {
        if (transaction is null) return;
        try { await transaction.RollbackAsync(CancellationToken.None); }
        catch (Exception ex) { _logger?.LogWarning("Import transaction rollback failed. ExceptionType={ExceptionType}", ex.GetType().Name); }
    }
    private static bool TryEnum<T>(string? value, out T result) where T : struct, Enum => Enum.TryParse(value, false, out result) && Enum.IsDefined(result) && value == result.ToString();
    private static string? NullIfEmpty(string value) => string.IsNullOrWhiteSpace(value) ? null : value;
    private static bool TryBoolean(string value, bool legacy, out bool result)
    {
        result = false;
        if (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1") { result = true; return true; }
        if (value.Equals("false", StringComparison.OrdinalIgnoreCase) || value == "0") return true;
        if (!legacy) return false;
        if (value.ToLowerInvariant() is "co" or "có" or "yes" or "batbuoc" or "bắt buộc") { result = true; return true; }
        return value.ToLowerInvariant() is "khong" or "không" or "no";
    }
    private static string LegacyType(string value) => value.ToLowerInvariant() switch { "shortanswer" or "tuluanngan" or "traloingan" or "dienkhuyet" => "ShortAnswer", "essay" or "tuluan" => "Essay", "multiplechoice" or "tracnghiem" or "tn" => "MultipleChoice", _ => value };
    private static string LegacyMode(string value) => value.ToLowerInvariant() switch { "textexact" or "exact" or "chinhxac" => "TextExact", "numericrational" or "numeric" or "sohuuti" or "so" => "NumericRational", "coordinate2d" or "coordinate" or "toado" => "Coordinate2D", "mathequivalent" => "MathEquivalent", "manual" or "thucong" or "giaoviencham" => "Manual", _ => value };
    private static List<string> ParseList(string raw, string field, QuestionImportItemDto item, List<QuestionImportRowErrorDto> errors)
    {
        if (string.IsNullOrWhiteSpace(raw)) return new();
        try
        {
            var result = JsonSerializer.Deserialize<List<string>>(raw);
            if (result is null || result.Count > 100 || result.Any(string.IsNullOrWhiteSpace)) throw new JsonException();
            return result;
        }
        catch (JsonException) { AddError(errors, item, field, "INVALID_JSON_ARRAY", $"{field} phải là JSON array gồm tối đa 100 chuỗi không rỗng, kể cả file cũ. Chuyển danh sách phân cách ; hoặc | sang JSON để giữ nguyên tọa độ/ký hiệu."); return new(); }
    }
    private static IEnumerable<(string Field, string Text)> MathFields(QuestionImportItemDto item)
    {
        yield return ("QuestionText", item.QuestionText); yield return ("CorrectAnswer", item.CorrectAnswer); yield return ("Solution", item.Solution);
        yield return ("ExpectedReasoning", item.ExpectedReasoning ?? string.Empty); yield return ("ScoringNotes", item.ScoringNotes);
        foreach (var o in item.Options) { yield return ($"Option{o.OptionLabel}", o.OptionText); yield return ($"Misconception{o.OptionLabel}", o.Misconception ?? string.Empty); }
        foreach (var idea in item.RequiredIdeas) yield return ("RequiredIdeas", idea);
        foreach (var error in item.CommonErrors) yield return ("CommonErrors", error);
    }
    private static void ValidateMathContent(QuestionImportItemDto item, string field, string text, List<QuestionImportRowErrorDto> errors)
    {
        if (text.Length > 20000 || ((field == "CorrectAnswer" || field.StartsWith("Option", StringComparison.Ordinal)) && Encoding.UTF8.GetByteCount(text) > 65535)) { AddError(errors, item, field, "FIELD_TOO_LONG", "Nội dung vượt giới hạn 20.000 ký tự hoặc 65.535 byte của cột text."); return; }
        // Comparisons such as $a<b>c$ are valid math, not HTML. General tags are checked in prose only.
        var prose = new StringBuilder(text.Length);
        var inMath = false;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] == '$' && !IsEscaped(text, i)) { inMath = !inMath; continue; }
            if (!inMath) prose.Append(text[i]);
        }
        if (Regex.IsMatch(prose.ToString(), @"<\s*/?\s*[A-Za-z][^>]*>", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking) ||
            Regex.IsMatch(text, @"<\s*/?\s*(?:script|iframe|img|svg|object|embed|style)\b[^>]*>", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase | RegexOptions.NonBacktracking) ||
            Regex.IsMatch(text, @"\\(?:href|url|includegraphics|html\w*|def|gdef|edef|xdef|newcommand|renewcommand|providecommand|let|futurelet|input|include|write|csname)\b", RegexOptions.CultureInvariant | RegexOptions.NonBacktracking))
            AddError(errors, item, field, "UNSAFE_CONTENT", "Không hỗ trợ HTML, URL hoặc macro LaTeX tùy ý trong import.");
        if (text.Contains("$$", StringComparison.Ordinal) || text.Contains(@"\(", StringComparison.Ordinal) || text.Contains(@"\)", StringComparison.Ordinal) || text.Contains(@"\[", StringComparison.Ordinal) || text.Contains(@"\]", StringComparison.Ordinal)) { AddError(errors, item, field, "UNSUPPORTED_MATH_PROFILE", "Template V1 chỉ hỗ trợ công thức inline $...$; chuyển công thức block rồi xem trước lại."); return; }
        var start = -1;
        for (var i = 0; i < text.Length; i++)
        {
            if (text[i] != '$' || IsEscaped(text, i)) continue;
            if (start < 0) { start = i; continue; }
            var formula = text.Substring(start + 1, i - start - 1);
            if (formula.Length == 0 || formula.Length > 2000)
            {
                AddError(errors, item, field, "INVALID_MATH_LENGTH", "Công thức không được rỗng và không vượt quá 2.000 ký tự.");
                start = -1;
                continue;
            }
            var depth = 0; var balanced = true;
            for (var j = 0; j < formula.Length; j++)
            {
                // Inspect preceding backslashes only for braces. Scanning backwards
                // at every backslash would make a long run quadratic.
                if (formula[j] is not ('{' or '}') || IsEscaped(formula, j)) continue;
                if (formula[j] == '{') depth++;
                if (formula[j] == '}' && --depth < 0) { balanced = false; break; }
            }
            if (!balanced || depth != 0) AddError(errors, item, field, "UNBALANCED_MATH_BRACES", "Công thức có ngoặc nhọn chưa cân bằng.");
            start = -1;
        }
        if (start >= 0) AddError(errors, item, field, "UNCLOSED_MATH_DELIMITER", "Công thức có dấu $ chưa đóng.");
    }
    private static bool IsEscaped(string text, int index)
    {
        var count = 0; for (var i = index - 1; i >= 0 && text[i] == '\\'; i--) count++;
        return count % 2 == 1;
    }
    private static void AddError(List<QuestionImportRowErrorDto> target, QuestionImportItemDto item, string field, string code, string message, string? raw = null) => target.Add(new QuestionImportRowErrorDto
    { RowIndex = item.RowIndex, SheetName = item.SheetName, QuestionKey = item.QuestionKey, Field = field, ColumnName = field, Code = code, ErrorMessage = message, RawValue = raw is { Length: > 200 } ? raw[..200] : raw });
}
