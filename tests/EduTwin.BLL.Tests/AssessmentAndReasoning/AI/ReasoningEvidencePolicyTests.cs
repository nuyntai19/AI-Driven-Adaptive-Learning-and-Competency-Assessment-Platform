using EduTwin.BLL.AssessmentAndReasoning;
using EduTwin.BLL.AssessmentAndReasoning.AI;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.CurriculumAndQuestions;
using Xunit;

namespace EduTwin.BLL.Tests.AssessmentAndReasoning.AI;

public sealed class ReasoningEvidencePolicyTests
{
    [Theory]
    [InlineData(null)] [InlineData("")] [InlineData("...")] [InlineData("khum")]
    [InlineData(" KHUM!! ")] [InlineData("Em không biết.")] [InlineData("I don't know")]
    public void MissingOrExplicitNonAnswerCannotSupportPerfectReasoning(string? text)
    {
        var request = Request(text);
        Assert.False(ReasoningEvidencePolicy.HasEvidence(request));
        var response = Response() with { ReasoningQuality = 100, ReasoningVerdict = "Valid", MethodDetected = "Thay số" };
        var error = Assert.Throws<AIAnalysisValidationException>(() => new AnalyzeReasoningResponseValidator().Validate(request, response));
        Assert.Equal(AIResponseValidationRule.ReasoningConsistency, error.ValidationRule);

        var honest = Response();
        new AnalyzeReasoningResponseValidator().Validate(request, honest);
        Assert.Equal(10m, honest.SuggestedScore); // Correct answer points are independent from missing reasoning.
    }

    [Theory]
    [InlineData("x=2")] [InlineData("Vì x>0")] [InlineData("2×3+1=7")]
    [InlineData("Không biết cách khác, em thay x=3 được 7.")]
    public void ConciseActualReasoningIsNotRejectedByTextLengthOrKeywords(string text) =>
        Assert.True(ReasoningEvidencePolicy.HasEvidence(Request(text)));

    [Fact]
    public void StudentImagesAndEssayAnswerCanProvideEvidenceButQuestionImagesCannot()
    {
        var request = Request("khum");
        Assert.True(ReasoningEvidencePolicy.HasEvidence(request with { StudentSubmission = request.StudentSubmission with {
            ImageParts = [new([1], "image/png")] } }));
        Assert.False(ReasoningEvidencePolicy.HasEvidence(request with { Question = request.Question with {
            ImageParts = [new([1], "image/png")] } }));
        Assert.True(ReasoningEvidencePolicy.HasEvidence(request with { Question = request.Question with { QuestionType = QuestionType.Essay },
            StudentSubmission = request.StudentSubmission with { FinalAnswer = "Thay x=3 vào y=2x+1 ta được y=7." } }));
    }

    private static AnalyzeReasoningRequest Request(string? text) => new() {
        SchemaVersion = AIAnalysisContract.SchemaVersion, Language = "vi", AllowedKnowledgeNodes = [],
        Question = new() { QuestionType = QuestionType.MultipleChoice, QuestionText = "Tính y khi x=3", CorrectAnswer = "A.7", Solution = "2*3+1=7",
            GradingCriteria = new() { SchemaVersion = "1", RequiredIdeas = [], CommonErrors = [], ScoringNotes = "Chọn đúng được điểm tối đa." } },
        StudentSubmission = new() { FinalAnswer = "A.7", ReasoningText = text, PreliminaryIsCorrect = true } };

    private static AnalyzeReasoningResponse Response() => new() {
        SchemaVersion = AIAnalysisContract.SchemaVersion, Language = "vi", Feedback = "Đáp án đúng. Chưa có nội dung lập luận để đánh giá.",
        ReasoningQuality = 0, ReasoningVerdict = "Uncertain", AnswerAssessment = "Correct", Confidence = 100, ErrorType = ErrorType.None,
        MissingSteps = [], RootCauseNodeIds = [], ReasoningIssues = [], SuggestedScore = 10 };
}
