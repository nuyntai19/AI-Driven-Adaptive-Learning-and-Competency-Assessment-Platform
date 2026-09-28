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
}
