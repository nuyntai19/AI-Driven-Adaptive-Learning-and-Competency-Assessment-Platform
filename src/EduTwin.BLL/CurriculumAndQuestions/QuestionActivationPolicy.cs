using System;
using System.Collections.Generic;
using System.Linq;
using EduTwin.BLL.AssessmentAndReasoning.PreliminaryGrading;
using EduTwin.Contracts.CurriculumAndQuestions;

namespace EduTwin.BLL.CurriculumAndQuestions;

public sealed class QuestionActivationPolicy : IQuestionActivationPolicy
{
    private readonly IMathAnswerNormalizer _mathNormalizer;
    private readonly ICoordinateAnswerNormalizer _coordinateNormalizer;

    public QuestionActivationPolicy(
        IMathAnswerNormalizer? mathNormalizer = null,
        ICoordinateAnswerNormalizer? coordinateNormalizer = null)
    {
        _mathNormalizer = mathNormalizer ?? new MathAnswerNormalizer();
        _coordinateNormalizer = coordinateNormalizer ?? new CoordinateAnswerNormalizer(_mathNormalizer);
    }

    public bool Validate(
        QuestionType questionType,
        QuestionAnswerEvaluationMode evaluationMode,
        string? correctAnswer,
        IReadOnlyList<QuestionOptionValidationItem>? options,
        out string? validationError)
    {
        validationError = null;

        if (questionType == QuestionType.MultipleChoice)
        {
            if (evaluationMode != QuestionAnswerEvaluationMode.TextExact)
            {
                validationError = "Câu hỏi trắc nghiệm bắt buộc phải có chế độ so khớp là TextExact.";
                return false;
            }

            if (options == null || options.Count < 2)
            {
                validationError = "Câu hỏi trắc nghiệm phải có ít nhất 2 lựa chọn.";
                return false;
            }

            var correctCount = options.Count(o => o.IsCorrect);
            if (correctCount != 1)
            {
                validationError = "Câu hỏi trắc nghiệm phải có đúng 1 đáp án đúng.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(correctAnswer))
            {
                validationError = "Đáp án đúng không được để trống.";
                return false;
            }

            return true;
        }

        if (questionType == QuestionType.Essay)
        {
            if (evaluationMode != QuestionAnswerEvaluationMode.Manual)
            {
                validationError = "Câu hỏi tự luận bắt buộc phải có chế độ so khớp là Manual.";
                return false;
            }

            if (string.IsNullOrWhiteSpace(correctAnswer))
            {
                validationError = "Đáp án đúng hoặc hướng dẫn giải không được để trống.";
                return false;
            }

            return true;
        }

        if (questionType == QuestionType.ShortAnswer)
        {
            if (string.IsNullOrWhiteSpace(correctAnswer))
            {
                validationError = "Đáp án đúng không được để trống.";
                return false;
            }

            switch (evaluationMode)
            {
                case QuestionAnswerEvaluationMode.NumericRational:
                    if (!_mathNormalizer.TryNormalize(correctAnswer, out _))
                    {
                        validationError = "Đáp án đúng không hợp lệ cho chế độ Điền số / Số hữu tỉ (NumericRational).";
                        return false;
                    }
                    return true;

                case QuestionAnswerEvaluationMode.Coordinate2D:
                    if (!_coordinateNormalizer.TryNormalize(correctAnswer, out _))
                    {
                        validationError = "Đáp án đúng không hợp lệ cho chế độ Tọa độ 2D (Coordinate2D).";
                        return false;
                    }
                    return true;

                case QuestionAnswerEvaluationMode.TextExact:
                case QuestionAnswerEvaluationMode.Manual:
                    return true;

                default:
                    validationError = $"Chế độ so khớp {evaluationMode} không hợp lệ cho câu hỏi trả lời ngắn.";
                    return false;
            }
        }

        validationError = $"Loại câu hỏi {questionType} không được hỗ trợ.";
        return false;
    }

    public bool ValidateCompleteQuestion(
        QuestionType questionType,
        QuestionAnswerEvaluationMode evaluationMode,
        byte difficulty,
        string? questionText,
        string? correctAnswer,
        string? solution,
        decimal maxScore,
        uint estimatedTimeSeconds,
        IReadOnlyList<QuestionOptionValidationItem>? options,
        out string? validationError)
    {
        validationError = null;

        if (string.IsNullOrWhiteSpace(questionText))
        {
            validationError = "Nội dung câu hỏi không được để trống.";
            return false;
        }

        if (difficulty < 1 || difficulty > 5)
        {
            validationError = "Độ khó câu hỏi phải nằm trong khoảng từ 1 đến 5.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(solution))
        {
            validationError = "Lời giải / hướng dẫn giải không được để trống.";
            return false;
        }

        if (maxScore <= 0)
        {
            validationError = "Điểm tối đa phải lớn hơn 0.";
            return false;
        }

        if (estimatedTimeSeconds == 0)
        {
            validationError = "Thời gian ước tính phải là số nguyên dương.";
            return false;
        }

        if (questionType == QuestionType.MultipleChoice)
        {
            if (options == null || options.Count < 2)
            {
                validationError = "Câu hỏi trắc nghiệm phải có ít nhất 2 lựa chọn.";
                return false;
            }

            var seenLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var opt in options)
            {
                if (string.IsNullOrWhiteSpace(opt.OptionLabel))
                {
                    validationError = "Nhãn lựa chọn không được để trống.";
                    return false;
                }

                if (string.IsNullOrWhiteSpace(opt.OptionText))
                {
                    validationError = $"Nội dung lựa chọn {opt.OptionLabel} không được để trống.";
                    return false;
                }

                if (!seenLabels.Add(opt.OptionLabel.Trim()))
                {
                    validationError = $"Nhãn lựa chọn '{opt.OptionLabel}' bị trùng lặp.";
                    return false;
                }
            }
        }

        return Validate(questionType, evaluationMode, correctAnswer, options, out validationError);
    }
}
