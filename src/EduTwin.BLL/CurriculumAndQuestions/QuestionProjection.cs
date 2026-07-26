using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.DAL.CurriculumAndQuestions;

namespace EduTwin.BLL.CurriculumAndQuestions;

/// <summary>
/// Shared projection helpers for Question aggregate — ensures teacher-facing DTO is the single source of truth.
/// </summary>
internal static class QuestionProjection
{
    internal static QuestionDto ToDto(
        Question q,
        IEnumerable<QuestionOption> options,
        IEnumerable<QuestionKnowledgeNode> mappings)
    {
        return new QuestionDto
        {
            QuestionId = q.QuestionId.ToString(CultureInfo.InvariantCulture),
            SubjectId = q.SubjectId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(),
            PrimaryTopicNodeId = q.PrimaryTopicNodeId.ToString(CultureInfo.InvariantCulture),
            CreatedByTeacherId = q.CreatedByTeacherId.ToString("D", CultureInfo.InvariantCulture).ToLowerInvariant(),
            QuestionType = q.QuestionType.ToString(),
            Difficulty = q.Difficulty,
            QuestionText = q.QuestionText,
            CorrectAnswer = q.CorrectAnswer,
            Solution = q.Solution,
            ExpectedReasoning = q.ExpectedReasoning,
            GradingCriteria = q.GradingCriteria,
            MaxScore = q.MaxScore,
            EstimatedTimeSeconds = q.EstimatedTimeSeconds,
            ReasoningRequired = q.ReasoningRequired,
            LanguageCode = q.LanguageCode,
            Status = q.Status.ToString(),
            Options = options
                .OrderBy(o => o.OrderIndex)
                .ThenBy(o => o.OptionId)
                .Select(o => new QuestionOptionDto
                {
                    OptionId = o.OptionId.ToString(CultureInfo.InvariantCulture),
                    Label = o.OptionLabel,
                    Text = o.OptionText,
                    IsCorrect = o.IsCorrect,
                    OrderIndex = o.OrderIndex
                })
                .ToList(),
            KnowledgeMappings = mappings
                .OrderBy(m => m.NodeId)
                .Select(m => new KnowledgeMappingDto
                {
                    NodeId = m.NodeId.ToString(CultureInfo.InvariantCulture),
                    MappingRole = m.MappingRole.ToString()
                })
                .ToList(),
            RowVersion = q.RowVersion.ToString(CultureInfo.InvariantCulture)
        };
    }
}
