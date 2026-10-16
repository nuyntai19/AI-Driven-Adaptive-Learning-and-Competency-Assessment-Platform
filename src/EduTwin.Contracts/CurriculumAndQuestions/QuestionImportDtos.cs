using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.CurriculumAndQuestions;

/// <summary>Immutable context bound to the preview token. Multipart form fields use these names.</summary>
public sealed class QuestionImportPreviewRequest
{
    public Guid SubjectId { get; set; }
    public Guid? TeacherId { get; set; }
    public string TemplateVersion { get; set; } = "1.0";
    public bool LegacyMode { get; set; }
    public ulong? DefaultTopicNodeId { get; set; }
    public byte? GradeLevel { get; set; }
    public byte Difficulty { get; set; } = 3;
    public decimal MaxScore { get; set; } = 10m;
    public uint EstimatedTimeSeconds { get; set; } = 120;
    public bool ReasoningRequired { get; set; }
    public string Visibility { get; set; } = "Private";
    public string LanguageCode { get; set; } = "vi";
    public string ShortAnswerEvaluationMode { get; set; } = "Manual";
}

public sealed class QuestionImportItemDto
{
    public int RowIndex { get; set; }
    public string SheetName { get; set; } = string.Empty;
    public string QuestionKey { get; set; } = string.Empty;
    public string TopicCode { get; set; } = string.Empty;
    public string TopicName { get; set; } = string.Empty;
    public string PrimaryTopicNodeId { get; set; } = string.Empty;
    public byte GradeLevel { get; set; }
    public string Visibility { get; set; } = "Private";
    public string LanguageCode { get; set; } = "vi";
    public string ScoringNotes { get; set; } = string.Empty;
    public List<string> DefaultedFields { get; set; } = new();
    public QuestionType QuestionType { get; set; } = QuestionType.MultipleChoice;
    public byte Difficulty { get; set; } = 3;
    public string QuestionText { get; set; } = string.Empty;
    public string CorrectAnswer { get; set; } = string.Empty;
    public string Solution { get; set; } = string.Empty;
    public string? ExpectedReasoning { get; set; }
    public decimal MaxScore { get; set; } = 10m;
    public uint EstimatedTimeSeconds { get; set; } = 120;
    public bool ReasoningRequired { get; set; } = false;
    public QuestionAnswerEvaluationMode AnswerEvaluationMode { get; set; } = QuestionAnswerEvaluationMode.TextExact;
    public List<QuestionOptionInput> Options { get; set; } = new();
    public List<string> RequiredIdeas { get; set; } = new();
    public List<string> CommonErrors { get; set; } = new();
}

public sealed class QuestionImportRowErrorDto
{
    public int RowIndex { get; set; }
    public string SheetName { get; set; } = string.Empty;
    public string QuestionKey { get; set; } = string.Empty;
    public string ColumnName { get; set; } = string.Empty;
    public string Code { get; set; } = "VALIDATION_FAILED";
    public string Field { get; set; } = string.Empty;
    public string ErrorMessage { get; set; } = string.Empty;
    public string? RawValue { get; set; }
}

public sealed class QuestionImportPreviewResponse
{
    public QuestionImportPreviewDataDto Data { get; set; } = null!;
    public MetaDto Meta { get; set; } = null!;
}

public sealed class QuestionImportPreviewDataDto
{
    public string PreviewToken { get; set; } = string.Empty;
    public int Revision { get; set; } = 1;
    public string SubjectId { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public string TemplateVersion { get; set; } = "1.0";
    public int TotalRows { get; set; }
    public int ValidCount { get; set; }
    public int InvalidCount { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public List<QuestionImportItemDto> ValidQuestions { get; set; } = new();
    public List<QuestionImportRowErrorDto> Errors { get; set; } = new();
    public List<QuestionImportRowErrorDto> Warnings { get; set; } = new();
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed class QuestionImportConfirmRequest
{
    public string PreviewToken { get; set; } = string.Empty;
    public int Revision { get; set; } = 1;
    public string ImportMode { get; set; } = "AllValid";
    public string TargetStatus { get; set; } = "Draft";
    public List<string> SelectedQuestionKeys { get; set; } = new();
}

public sealed class QuestionImportConfirmResponse
{
    public QuestionImportConfirmDataDto Data { get; set; } = null!;
    public MetaDto Meta { get; set; } = null!;
}

public sealed class QuestionImportConfirmDataDto
{
    public int ImportedCount { get; set; }
    public int SkippedCount { get; set; }
    public List<string> QuestionIds { get; set; } = new();
    public string Message { get; set; } = string.Empty;
}
