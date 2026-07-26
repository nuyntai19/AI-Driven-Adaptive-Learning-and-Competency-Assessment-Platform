using System;

namespace EduTwin.Contracts.CurriculumAndQuestions;

public class QuestionListQuery
{
    public Guid? SubjectId { get; set; }
    public string? TopicId { get; set; }
    public string? Type { get; set; }
    public byte? Difficulty { get; set; }
    public string? Status { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}
