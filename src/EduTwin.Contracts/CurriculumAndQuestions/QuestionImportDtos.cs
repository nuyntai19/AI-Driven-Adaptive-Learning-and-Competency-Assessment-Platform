using System;
using System.Collections.Generic;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.Contracts.CurriculumAndQuestions;

public sealed class QuestionImportItemDto
{
    public int RowIndex { get; set; }
    public QuestionType QuestionType { get; set; } = QuestionType.MultipleChoice;
    public byte Difficulty { get; set; } = 3;
    public string QuestionText { get; set; } = string.Empty;
    public string CorrectAnswer { get; set; } = string.Empty;
    public string Solution { get; set; } = string.Empty;
    public string? ExpectedReasoning { get; set; }
    public decimal MaxScore { get; set; } = 10m;
    public uint EstimatedTimeSeconds { get; set; } = 120;
    public bool ReasoningRequired { get; set; } = true;
    public List<QuestionOptionInput> Options { get; set; } = new();
    public List<string> RequiredIdeas { get; set; } = new();
    public List<string> CommonErrors { get; set; } = new();
}

public sealed class QuestionImportRowErrorDto
{
    public int RowIndex { get; set; }
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
    public int TotalRows { get; set; }
    public int ValidCount { get; set; }
    public int InvalidCount { get; set; }
    public List<QuestionImportItemDto> ValidQuestions { get; set; } = new();
    public List<QuestionImportRowErrorDto> Errors { get; set; } = new();
}

public sealed class QuestionImportConfirmRequest
{
    public string PreviewToken { get; set; } = string.Empty;
    public Guid SubjectId { get; set; }
    public ulong PrimaryTopicNodeId { get; set; }
    public List<QuestionImportItemDto>? Questions { get; set; }
}

public sealed class QuestionImportConfirmResponse
{
    public QuestionImportConfirmDataDto Data { get; set; } = null!;
    public MetaDto Meta { get; set; } = null!;
}

public sealed class QuestionImportConfirmDataDto
{
    public int ImportedCount { get; set; }
    public string Message { get; set; } = string.Empty;
}
