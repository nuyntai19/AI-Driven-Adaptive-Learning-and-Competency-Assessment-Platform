using System.Collections.Generic;
using EduTwin.Contracts.Common;

namespace EduTwin.Contracts.CurriculumAndQuestions;

public class QuestionListResponse
{
    public List<QuestionDto> Data { get; set; } = new();
    public PagedMetaDto Meta { get; set; } = null!;
}
