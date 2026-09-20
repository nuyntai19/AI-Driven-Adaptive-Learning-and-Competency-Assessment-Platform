using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.CurriculumAndQuestions.Import;

public sealed class QuestionImportUseCase : IQuestionImportUseCase
{
    private static readonly ConcurrentDictionary<string, (DateTime CreatedAt, List<QuestionImportItemDto> Questions)> PreviewCache = new();

    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public QuestionImportUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<QuestionImportPreviewResult> PreviewAsync(
        Stream fileStream,
        string fileName,
        CancellationToken cancellationToken)
    {
        if (fileStream == null || fileStream.Length == 0)
        {
            return QuestionImportPreviewResult.ValidationFailed("Tệp tải lên không được rỗng.");
        }

        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        List<Dictionary<string, string>> rawRows;

        try
        {
            if (ext == ".csv" || ext == ".txt")
            {
                rawRows = await ParseCsvAsync(fileStream, cancellationToken);
            }
            else if (ext == ".xlsx")
            {
                rawRows = await ParseXlsxAsync(fileStream, cancellationToken);
            }
            else
            {
                return QuestionImportPreviewResult.ValidationFailed("Định dạng tệp không được hỗ trợ. Vui lòng tải lên tệp .csv hoặc .xlsx.");
            }
        }
        catch (Exception ex)
        {
            return QuestionImportPreviewResult.ValidationFailed($"Lỗi khi đọc tệp dữ liệu: {ex.Message}");
        }

        if (rawRows.Count == 0)
        {
            return QuestionImportPreviewResult.ValidationFailed("Tệp không có dòng dữ liệu hợp lệ nào.");
        }

        var validQuestions = new List<QuestionImportItemDto>();
        var errors = new List<QuestionImportRowErrorDto>();

        for (int i = 0; i < rawRows.Count; i++)
        {
            var rowIndex = i + 2; // Row 1 is header
            var row = rawRows[i];
            var rowErrors = new List<QuestionImportRowErrorDto>();

            // 1. Question Text
            var questionText = GetValue(row, "questiontext", "cauhoi", "noidung", "noidungcauhoi", "content");
            if (string.IsNullOrWhiteSpace(questionText))
            {
                rowErrors.Add(new QuestionImportRowErrorDto
                {
                    RowIndex = rowIndex,
                    Field = "QuestionText",
                    ErrorMessage = "Nội dung câu hỏi không được để trống.",
                    RawValue = questionText
                });
            }

            // 2. Question Type
            var typeStr = GetValue(row, "questiontype", "loaicauhoi", "type");
            var questionType = QuestionType.MultipleChoice;
            if (!string.IsNullOrWhiteSpace(typeStr))
            {
                if (typeStr.Equals("ShortAnswer", StringComparison.OrdinalIgnoreCase) ||
                    typeStr.Equals("Tuluanngan", StringComparison.OrdinalIgnoreCase) ||
                    typeStr.Equals("TuLuan", StringComparison.OrdinalIgnoreCase))
                {
                    questionType = QuestionType.ShortAnswer;
                }
                else if (typeStr.Equals("Essay", StringComparison.OrdinalIgnoreCase) ||
                         typeStr.Equals("Tuluan", StringComparison.OrdinalIgnoreCase))
                {
                    questionType = QuestionType.Essay;
                }
                else if (!typeStr.Equals("MultipleChoice", StringComparison.OrdinalIgnoreCase) &&
                         !typeStr.Equals("TracNghiem", StringComparison.OrdinalIgnoreCase) &&
                         !typeStr.Equals("TN", StringComparison.OrdinalIgnoreCase))
                {
                    rowErrors.Add(new QuestionImportRowErrorDto
                    {
                        RowIndex = rowIndex,
                        Field = "QuestionType",
                        ErrorMessage = "Loại câu hỏi không hợp lệ (hỗ trợ MultipleChoice, ShortAnswer, Essay).",
                        RawValue = typeStr
                    });
                }
            }

            // 3. Difficulty
            var diffStr = GetValue(row, "difficulty", "dokho", "level");
            byte difficulty = 3;
            if (!string.IsNullOrWhiteSpace(diffStr))
            {
                if (byte.TryParse(diffStr, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedDiff) &&
                    parsedDiff >= 1 && parsedDiff <= 5)
                {
                    difficulty = parsedDiff;
                }
                else
                {
                    rowErrors.Add(new QuestionImportRowErrorDto
                    {
                        RowIndex = rowIndex,
                        Field = "Difficulty",
                        ErrorMessage = "Độ khó phải là số nguyên từ 1 đến 5.",
                        RawValue = diffStr
                    });
                }
            }

            // 4. Correct Answer
            var correctAnswer = GetValue(row, "correctanswer", "dapandung", "answer");
            if (string.IsNullOrWhiteSpace(correctAnswer))
            {
                rowErrors.Add(new QuestionImportRowErrorDto
                {
                    RowIndex = rowIndex,
                    Field = "CorrectAnswer",
                    ErrorMessage = "Đáp án đúng không được để trống.",
                    RawValue = correctAnswer
                });
            }

            // 5. Solution
            var solution = GetValue(row, "solution", "loigiai", "giaithich", "explanation");
            if (string.IsNullOrWhiteSpace(solution))
            {
                rowErrors.Add(new QuestionImportRowErrorDto
                {
                    RowIndex = rowIndex,
                    Field = "Solution",
                    ErrorMessage = "Lời giải chi tiết không được để trống.",
                    RawValue = solution
                });
            }

            // 6. Expected Reasoning
            var expectedReasoning = GetValue(row, "expectedreasoning", "lapluanmongdoi");

            // 7. Max Score
            var maxScoreStr = GetValue(row, "maxscore", "diemtoida", "score", "diem");
            decimal maxScore = 10m;
            if (!string.IsNullOrWhiteSpace(maxScoreStr))
            {
                if (decimal.TryParse(maxScoreStr.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedScore) && parsedScore > 0)
                {
                    maxScore = parsedScore;
                }
                else
                {
                    rowErrors.Add(new QuestionImportRowErrorDto
                    {
                        RowIndex = rowIndex,
                        Field = "MaxScore",
                        ErrorMessage = "Điểm tối đa phải là số lớn hơn 0.",
                        RawValue = maxScoreStr
                    });
                }
            }

            // 8. Estimated Time
            var timeStr = GetValue(row, "estimatedtimeseconds", "thoigianuoctinh", "time");
            uint estimatedTimeSeconds = 120;
            if (!string.IsNullOrWhiteSpace(timeStr))
            {
                if (uint.TryParse(timeStr, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedTime) && parsedTime > 0)
                {
                    estimatedTimeSeconds = parsedTime;
                }
                else
                {
                    rowErrors.Add(new QuestionImportRowErrorDto
                    {
                        RowIndex = rowIndex,
                        Field = "EstimatedTimeSeconds",
                        ErrorMessage = "Thời gian ước tính phải là số nguyên dương.",
                        RawValue = timeStr
                    });
                }
            }

            // 9. Reasoning Required
            var reasoningReqStr = GetValue(row, "reasoningrequired", "yeucaubienluan", "batbuoclapluan");
            bool reasoningRequired = true;
            if (!string.IsNullOrWhiteSpace(reasoningReqStr))
            {
                if (reasoningReqStr.Equals("0", StringComparison.Ordinal) ||
                    reasoningReqStr.Equals("false", StringComparison.OrdinalIgnoreCase) ||
                    reasoningReqStr.Equals("khong", StringComparison.OrdinalIgnoreCase) ||
                    reasoningReqStr.Equals("no", StringComparison.OrdinalIgnoreCase))
                {
                    reasoningRequired = false;
                }
            }

            // 10. Options (for MultipleChoice)
            var options = new List<QuestionOptionInput>();
            if (questionType == QuestionType.MultipleChoice)
            {
                var optA = GetValue(row, "optiona", "luachona", "dapana", "a");
                var optB = GetValue(row, "optionb", "luachonb", "dapanb", "b");
                var optC = GetValue(row, "optionc", "luachonc", "dapanc", "c");
                var optD = GetValue(row, "optiond", "luachond", "dapand", "d");

                var miscA = GetValue(row, "misconceptiona", "sailama", "misconception_a");
                var miscB = GetValue(row, "misconceptionb", "sailamb", "misconception_b");
                var miscC = GetValue(row, "misconceptionc", "sailamc", "misconception_c");
                var miscD = GetValue(row, "misconceptiond", "sailamd", "misconception_d");

                var candidateOptions = new[]
                {
                    (Label: "A", Text: optA, Misc: miscA),
                    (Label: "B", Text: optB, Misc: miscB),
                    (Label: "C", Text: optC, Misc: miscC),
                    (Label: "D", Text: optD, Misc: miscD)
                };

                uint order = 1;
                foreach (var cand in candidateOptions)
                {
                    if (!string.IsNullOrWhiteSpace(cand.Text))
                    {
                        var isCorrect = IsAnswerMatch(correctAnswer, cand.Label, cand.Text);
                        options.Add(new QuestionOptionInput
                        {
                            OptionLabel = cand.Label,
                            OptionText = cand.Text.Trim(),
                            IsCorrect = isCorrect,
                            OrderIndex = order++,
                            Misconception = isCorrect ? null : (!string.IsNullOrWhiteSpace(cand.Misc) ? cand.Misc.Trim() : null)
                        });
                    }
                }

                if (options.Count < 2)
                {
                    rowErrors.Add(new QuestionImportRowErrorDto
                    {
                        RowIndex = rowIndex,
                        Field = "Options",
                        ErrorMessage = "Câu hỏi trắc nghiệm phải có ít nhất 2 lựa chọn (OptionA, OptionB).",
                        RawValue = $"Found {options.Count} options"
                    });
                }
                else
                {
                    var correctCount = options.Count(o => o.IsCorrect);
                    if (correctCount != 1)
                    {
                        rowErrors.Add(new QuestionImportRowErrorDto
                        {
                            RowIndex = rowIndex,
                            Field = "CorrectAnswer",
                            ErrorMessage = $"Câu hỏi trắc nghiệm phải có đúng 1 đáp án đúng (hiện phát hiện {correctCount} đáp án khớp với '{correctAnswer}').",
                            RawValue = correctAnswer
                        });
                    }
                }
            }

            // 11. Rubrics (RequiredIdeas, CommonErrors)
            var requiredIdeasStr = GetValue(row, "requiredideas", "ytuongbatbuoc", "rubric");
            var requiredIdeas = !string.IsNullOrWhiteSpace(requiredIdeasStr)
                ? requiredIdeasStr.Split(new[] { ';', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
                : new List<string>();

            var commonErrorsStr = GetValue(row, "commonerrors", "loithuonggap", "sailam");
            var commonErrors = !string.IsNullOrWhiteSpace(commonErrorsStr)
                ? commonErrorsStr.Split(new[] { ';', '|' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
                : new List<string>();

            if (rowErrors.Count > 0)
            {
                errors.AddRange(rowErrors);
            }
            else
            {
                validQuestions.Add(new QuestionImportItemDto
                {
                    RowIndex = rowIndex,
                    QuestionType = questionType,
                    Difficulty = difficulty,
                    QuestionText = questionText!.Trim(),
                    CorrectAnswer = correctAnswer!.Trim(),
                    Solution = solution!.Trim(),
                    ExpectedReasoning = !string.IsNullOrWhiteSpace(expectedReasoning) ? expectedReasoning.Trim() : null,
                    MaxScore = maxScore,
                    EstimatedTimeSeconds = estimatedTimeSeconds,
                    ReasoningRequired = reasoningRequired,
                    Options = options,
                    RequiredIdeas = requiredIdeas,
                    CommonErrors = commonErrors
                });
            }
        }

        var previewToken = Guid.NewGuid().ToString("N");
        PreviewCache[previewToken] = (DateTime.UtcNow, validQuestions);

        // Clean up old cache entries (> 1 hour)
        var threshold = DateTime.UtcNow.AddHours(-1);
        foreach (var key in PreviewCache.Keys)
        {
            if (PreviewCache.TryGetValue(key, out var cached) && cached.CreatedAt < threshold)
            {
                PreviewCache.TryRemove(key, out _);
            }
        }

        var previewData = new QuestionImportPreviewDataDto
        {
            PreviewToken = previewToken,
            TotalRows = rawRows.Count,
            ValidCount = validQuestions.Count,
            InvalidCount = errors.Count,
            ValidQuestions = validQuestions,
            Errors = errors
        };

        return QuestionImportPreviewResult.Success(previewData);
    }

    public async Task<QuestionImportConfirmResult> ConfirmAsync(
        QuestionImportConfirmRequest request,
        CancellationToken cancellationToken)
    {
        if (request == null)
        {
            return QuestionImportConfirmResult.ValidationFailed("Dữ liệu xác nhận không được để trống.");
        }

        if (!_tenantContext.IsResolved ||
            !_tenantContext.CenterId.HasValue ||
            _tenantContext.CenterId.Value == Guid.Empty ||
            !_tenantContext.UserId.HasValue ||
            _tenantContext.UserId.Value == Guid.Empty)
        {
            return QuestionImportConfirmResult.ValidationFailed("Không xác định được ngữ cảnh trung tâm hoặc người dùng.");
        }

        var centerId = _tenantContext.CenterId.Value;
        var actorId = _tenantContext.UserId.Value;

        if (request.SubjectId == Guid.Empty)
        {
            return QuestionImportConfirmResult.ValidationFailed("Môn học (SubjectId) là bắt buộc.");
        }

        if (request.PrimaryTopicNodeId == 0)
        {
            return QuestionImportConfirmResult.ValidationFailed("Chủ đề kiến thức (PrimaryTopicNodeId) là bắt buộc.");
        }

        // Verify subject and topic node exist
        var subjectExists = await _dbContext.Subjects.AsNoTracking()
            .AnyAsync(s => s.CenterId == centerId && s.SubjectId == request.SubjectId && !s.IsDeleted, cancellationToken);
        if (!subjectExists)
        {
            return QuestionImportConfirmResult.NotFound("Môn học không tồn tại trong trung tâm.");
        }

        var nodeExists = await _dbContext.KnowledgeNodes.AsNoTracking()
            .AnyAsync(n => n.CenterId == centerId && n.SubjectId == request.SubjectId && n.NodeId == request.PrimaryTopicNodeId && !n.IsDeleted, cancellationToken);
        if (!nodeExists)
        {
            return QuestionImportConfirmResult.NotFound("Chủ đề kiến thức không tồn tại trong môn học.");
        }

        // Retrieve questions from request payload or preview cache
        List<QuestionImportItemDto>? questionsToImport = request.Questions;
        if (questionsToImport == null || questionsToImport.Count == 0)
        {
            if (!string.IsNullOrWhiteSpace(request.PreviewToken) && PreviewCache.TryGetValue(request.PreviewToken, out var cached))
            {
                questionsToImport = cached.Questions;
            }
        }

        if (questionsToImport == null || questionsToImport.Count == 0)
        {
            return QuestionImportConfirmResult.ValidationFailed("Không có câu hỏi hợp lệ nào để nhập.");
        }

        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var importedCount = 0;

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var item in questionsToImport)
            {
                var criteria = new Contracts.CurriculumAndQuestions.GradingCriteria
                {
                    RequiredIdeas = item.RequiredIdeas,
                    CommonErrors = item.CommonErrors,
                    ScoringNotes = item.Solution
                };

                var evalMode = item.QuestionType switch
                {
                    QuestionType.MultipleChoice => QuestionAnswerEvaluationMode.TextExact,
                    QuestionType.Essay => QuestionAnswerEvaluationMode.Manual,
                    _ => QuestionAnswerEvaluationMode.TextExact
                };

                var question = new Question
                {
                    CenterId = centerId,
                    SubjectId = request.SubjectId,
                    PrimaryTopicNodeId = request.PrimaryTopicNodeId,
                    CreatedByTeacherId = actorId,
                    QuestionType = item.QuestionType,
                    Difficulty = item.Difficulty,
                    QuestionText = item.QuestionText,
                    CorrectAnswer = item.CorrectAnswer,
                    Solution = item.Solution,
                    ExpectedReasoning = item.ExpectedReasoning,
                    GradingCriteria = criteria,
                    MaxScore = item.MaxScore,
                    EstimatedTimeSeconds = item.EstimatedTimeSeconds,
                    ReasoningRequired = item.ReasoningRequired,
                    LanguageCode = "vi",
                    Status = QuestionStatus.Active,
                    AnswerEvaluationMode = evalMode,
                    CreatedAt = now,
                    CreatedBy = actorId,
                    UpdatedAt = now,
                    UpdatedBy = actorId
                };

                _dbContext.Questions.Add(question);
                await _dbContext.SaveChangesAsync(cancellationToken);

                if (item.Options != null && item.Options.Count > 0)
                {
                    ulong optIndex = 1;
                    foreach (var optInput in item.Options)
                    {
                        var option = new QuestionOption
                        {
                            OptionId = question.QuestionId * 1000 + optIndex,
                            CenterId = centerId,
                            QuestionId = question.QuestionId,
                            OptionLabel = optInput.OptionLabel,
                            OptionText = optInput.OptionText,
                            IsCorrect = optInput.IsCorrect,
                            OrderIndex = optInput.OrderIndex > 0 ? optInput.OrderIndex : (uint)optIndex,
                            Misconception = optInput.Misconception,
                            CreatedAt = now,
                            CreatedBy = actorId,
                            UpdatedAt = now,
                            UpdatedBy = actorId
                        };
                        _dbContext.QuestionOptions.Add(option);
                        optIndex++;
                    }
                    await _dbContext.SaveChangesAsync(cancellationToken);
                }

                importedCount++;
            }

            await transaction.CommitAsync(cancellationToken);

            if (!string.IsNullOrWhiteSpace(request.PreviewToken))
            {
                PreviewCache.TryRemove(request.PreviewToken, out _);
            }

            return QuestionImportConfirmResult.Success(new QuestionImportConfirmDataDto
            {
                ImportedCount = importedCount,
                Message = $"Đã nhập thành công {importedCount} câu hỏi vào ngân hàng đề."
            });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            return QuestionImportConfirmResult.Failure("IMPORT_FAILED", $"Lỗi trong quá trình nhập dữ liệu: {ex.Message}");
        }
    }

    private static bool IsAnswerMatch(string? correctAnswer, string optionLabel, string optionText)
    {
        if (string.IsNullOrWhiteSpace(correctAnswer))
            return false;

        var ca = correctAnswer.Trim();
        if (ca.Equals(optionLabel.Trim(), StringComparison.OrdinalIgnoreCase))
            return true;
        if (ca.Equals(optionText.Trim(), StringComparison.OrdinalIgnoreCase))
            return true;
        return false;
    }

    private static string? GetValue(Dictionary<string, string> row, params string[] fieldAliases)
    {
        foreach (var alias in fieldAliases)
        {
            if (row.TryGetValue(alias, out var val) && !string.IsNullOrWhiteSpace(val))
            {
                return val;
            }
        }
        return null;
    }

    private static async Task<List<Dictionary<string, string>>> ParseCsvAsync(Stream stream, CancellationToken cancellationToken)
    {
        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        var rows = new List<Dictionary<string, string>>();
        string? headerLine = await reader.ReadLineAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(headerLine)) return rows;

        var delimiter = DetectDelimiter(headerLine);
        var headers = SplitCsvLine(headerLine, delimiter).Select(NormalizeHeader).ToList();

        string? line;
        while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;

            var values = SplitCsvLine(line, delimiter);
            var rowDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < headers.Count && i < values.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(headers[i]))
                {
                    rowDict[headers[i]] = values[i];
                }
            }

            if (rowDict.Values.Any(v => !string.IsNullOrWhiteSpace(v)))
            {
                rows.Add(rowDict);
            }
        }

        return rows;
    }

    private static async Task<List<Dictionary<string, string>>> ParseXlsxAsync(Stream stream, CancellationToken cancellationToken)
    {
        var rows = new List<Dictionary<string, string>>();
        using var memory = new MemoryStream();
        await stream.CopyToAsync(memory, cancellationToken);
        memory.Position = 0;

        using var archive = new ZipArchive(memory, ZipArchiveMode.Read, leaveOpen: false);

        // 1. Read shared strings
        var sharedStrings = new List<string>();
        var sharedStringEntry = archive.GetEntry("xl/sharedStrings.xml");
        if (sharedStringEntry != null)
        {
            await using var ssStream = sharedStringEntry.Open();
            var ssDoc = await XDocument.LoadAsync(ssStream, LoadOptions.None, cancellationToken);
            XNamespace ns = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
            foreach (var si in ssDoc.Descendants(ns + "si"))
            {
                var text = string.Concat(si.Descendants(ns + "t").Select(t => t.Value));
                sharedStrings.Add(text);
            }
        }

        // 2. Read sheet1.xml
        var sheetEntry = archive.GetEntry("xl/worksheets/sheet1.xml");
        if (sheetEntry == null) return rows;

        await using var sheetStream = sheetEntry.Open();
        var sheetDoc = await XDocument.LoadAsync(sheetStream, LoadOptions.None, cancellationToken);
        XNamespace sheetNs = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";

        var sheetData = sheetDoc.Descendants(sheetNs + "sheetData").FirstOrDefault();
        if (sheetData == null) return rows;

        var headerRow = sheetData.Elements(sheetNs + "row").FirstOrDefault();
        if (headerRow == null) return rows;

        var headerMap = new Dictionary<string, string>(); // Column letter -> header name
        foreach (var c in headerRow.Elements(sheetNs + "c"))
        {
            var cellRef = (string?)c.Attribute("r") ?? "";
            var colLetters = Regex.Match(cellRef, "^[A-Z]+").Value;
            var val = GetCellValue(c, sharedStrings, sheetNs);
            if (!string.IsNullOrWhiteSpace(val))
            {
                headerMap[colLetters] = NormalizeHeader(val);
            }
        }

        foreach (var rowElem in sheetData.Elements(sheetNs + "row").Skip(1))
        {
            var rowDict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var c in rowElem.Elements(sheetNs + "c"))
            {
                var cellRef = (string?)c.Attribute("r") ?? "";
                var colLetters = Regex.Match(cellRef, "^[A-Z]+").Value;
                if (headerMap.TryGetValue(colLetters, out var headerName))
                {
                    var cellVal = GetCellValue(c, sharedStrings, sheetNs);
                    rowDict[headerName] = cellVal;
                }
            }

            if (rowDict.Values.Any(v => !string.IsNullOrWhiteSpace(v)))
            {
                rows.Add(rowDict);
            }
        }

        return rows;
    }

    private static string GetCellValue(XElement cell, List<string> sharedStrings, XNamespace ns)
    {
        var type = (string?)cell.Attribute("t");
        var valElem = cell.Element(ns + "v");
        if (valElem == null) return string.Empty;

        var val = valElem.Value;
        if (type == "s" && int.TryParse(val, out var strIndex) && strIndex >= 0 && strIndex < sharedStrings.Count)
        {
            return sharedStrings[strIndex];
        }

        return val;
    }

    private static string NormalizeHeader(string header)
    {
        var cleaned = Regex.Replace(header.Trim().ToLowerInvariant(), @"[^a-z0-9]", "");
        return cleaned;
    }

    private static char DetectDelimiter(string headerLine)
    {
        var commas = headerLine.Count(c => c == ',');
        var semicolons = headerLine.Count(c => c == ';');
        var tabs = headerLine.Count(c => c == '\t');
        if (semicolons > commas && semicolons > tabs) return ';';
        if (tabs > commas && tabs > semicolons) return '\t';
        return ',';
    }

    private static List<string> SplitCsvLine(string line, char delimiter)
    {
        var values = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (c == delimiter && !inQuotes)
            {
                values.Add(sb.ToString().Trim());
                sb.Clear();
            }
            else
            {
                sb.Append(c);
            }
        }
        values.Add(sb.ToString().Trim());
        return values;
    }
}
