using System.ComponentModel.DataAnnotations;

namespace EduTwin.Contracts.AssessmentAndReasoning;

public sealed class ListAttemptsQuery
{
    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? StudentId { get; set; }

    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? SubjectId { get; set; }

    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? QuestionId { get; set; }

    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? AssignmentId { get; set; }

    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? Status { get; set; }

    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? From { get; set; }

    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? To { get; set; }

    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? Page { get; set; }

    [DisplayFormat(ConvertEmptyStringToNull = false)]
    public string? PageSize { get; set; }
}
