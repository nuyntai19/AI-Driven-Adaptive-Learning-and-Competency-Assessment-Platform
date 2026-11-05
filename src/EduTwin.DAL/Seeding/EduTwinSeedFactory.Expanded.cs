using System;
using System.Collections.Generic;
using System.Linq;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.KnowledgeGraph;

namespace EduTwin.DAL.Seeding;

public partial class EduTwinSeedFactory
{
    private void AddExpandedGradeSamples(SeedDataContainer data, Guid mathSubject, Guid englishSubject,
        Guid mathTeacher, Guid englishTeacher)
    {
        // Reserved demo IDs do not alter the original sample identities or any Published topology.
        _nodeId = _isCenterA ? 15000UL : 25000UL;
        _edgeId = _isCenterA ? 15000UL : 25000UL;
        _questionId = _isCenterA ? 15000UL : 25000UL;
        _optionId = _isCenterA ? 15000UL : 25000UL;

        var additions = new (Guid Subject, byte Grade, string Code, string Name, int Minutes)[]
        {
            (mathSubject, 10, "MATH10-QUADRATIC", "Hàm số và phương trình bậc hai", 150),
            (mathSubject, 10, "MATH10-COORDINATE", "Vectơ và tọa độ trong mặt phẳng", 150),
            (mathSubject, 11, "MATH11-TRIG", "Hàm số lượng giác", 150),
            (mathSubject, 11, "MATH11-TRIG-EQUATIONS", "Phương trình lượng giác", 120),
            (mathSubject, 12, "MATH12-DERIVATIVE", "Ứng dụng đạo hàm khảo sát hàm số", 180),
            (mathSubject, 12, "MATH12-OPTIMIZATION", "Bài toán cực trị và tối ưu", 120),
            (englishSubject, 10, "ENG10-COMMUNITY", "Chủ điểm cộng đồng và gia đình", 105),
            (englishSubject, 10, "ENG10-READING", "Đọc hiểu chủ đề cộng đồng", 105),
            (englishSubject, 11, "ENG11-ENVIRONMENT", "Chủ điểm môi trường", 105),
            (englishSubject, 11, "ENG11-WRITING", "Viết đoạn văn về môi trường", 105),
            (englishSubject, 12, "ENG12-CAREERS", "Chủ điểm nghề nghiệp và tương lai", 105),
            (englishSubject, 12, "ENG12-SPEAKING", "Giao tiếp và phỏng vấn nghề nghiệp", 105)
        };
        foreach (var item in additions)
            data.Topics.Add(CreateTopic(item.Subject, item.Code, item.Name, data.Topics.Count + 1, 70m, (uint)item.Minutes));
        var byCode = data.Topics.ToDictionary(n => n.NodeCode);
        var gradeTopicCodes = new Dictionary<(bool Math, byte Grade), string[]>
        {
            [(true, 10)] = ["MATH-FUNCTIONS", "MATH10-QUADRATIC", "MATH10-COORDINATE"],
            [(true, 11)] = ["MATH-EXP-LOG", "MATH11-TRIG", "MATH11-TRIG-EQUATIONS"],
            [(true, 12)] = ["MATH12-DERIVATIVE", "MATH12-OPTIMIZATION", "MATH-ANTIDERIVATIVE"],
            [(false, 10)] = ["ENG-TENSES", "ENG-RELATIVE-CLAUSES", "ENG-CONTEXT-VOCAB", "ENG10-COMMUNITY", "ENG10-READING"],
            [(false, 11)] = ["ENG-TENSES", "ENG-RELATIVE-CLAUSES", "ENG-CONTEXT-VOCAB", "ENG11-ENVIRONMENT", "ENG11-WRITING"],
            [(false, 12)] = ["ENG-TENSES", "ENG-RELATIVE-CLAUSES", "ENG-CONTEXT-VOCAB", "ENG12-CAREERS", "ENG12-SPEAKING"]
        };
        var gradeEdgeCodes = new Dictionary<(bool Math, byte Grade), (string Source, string Target)[]>
        {
            [(true, 10)] = [("MATH-FUNCTIONS", "MATH10-QUADRATIC")],
            [(true, 11)] = [("MATH11-TRIG", "MATH11-TRIG-EQUATIONS")],
            [(true, 12)] = [("MATH12-DERIVATIVE", "MATH12-OPTIMIZATION"), ("MATH12-DERIVATIVE", "MATH-ANTIDERIVATIVE")],
            [(false, 10)] = [("ENG-CONTEXT-VOCAB", "ENG10-READING")],
            [(false, 11)] = [("ENG-CONTEXT-VOCAB", "ENG11-WRITING")],
            [(false, 12)] = [("ENG-CONTEXT-VOCAB", "ENG12-SPEAKING")]
        };
        var sampleTransition = DeterministicSeedIds.AuditTimeUtc.AddDays(1);
        foreach (byte grade in new byte[] { 10, 11, 12 })
        foreach (var math in new[] { true, false })
        {
            var subject = math ? mathSubject : englishSubject;
            var teacher = math ? mathTeacher : englishTeacher;
            var versionId = DeterministicSeedIds.ExpandedGradeGraphVersionId(_isCenterA, math, grade);
            var curriculumId = DeterministicSeedIds.ExpandedGradeCurriculumId(_isCenterA, math, grade);
            var classId = DeterministicSeedIds.GradeClassId(_isCenterA, math, grade);
            data.GraphVersions.Add(new KnowledgeGraphVersion
            {
                CenterId = _centerId, GraphVersionId = versionId, SubjectId = subject, GradeLevel = grade,
                Status = "Published", IsCurrent = true, SourceGraphVersionId = DeterministicSeedIds.GradeGraphVersionId(_isCenterA, math, grade),
                Title = $"Khung tri thức {(math ? "Toán" : "Tiếng Anh")} {grade} · mẫu mở rộng",
                CreatedAt = sampleTransition, UpdatedAt = sampleTransition, CreatedBy = teacher, UpdatedBy = teacher
            });
            var codes = gradeTopicCodes[(math, grade)];
            for (var index = 0; index < codes.Length; index++)
            {
                var node = byCode[codes[index]];
                data.NodeGrades.Add(new KnowledgeNodeGrade { CenterId = _centerId, GraphVersionId = versionId,
                    NodeId = node.NodeId, CreatedAt = sampleTransition, CreatedBy = teacher });
                data.CurriculumNodes.Add(new CurriculumNode { CenterId = _centerId, CurriculumId = curriculumId,
                    NodeId = node.NodeId, OrderIndex = (uint)(index + 1), CreatedAt = sampleTransition });
            }
            foreach (var (source, target) in gradeEdgeCodes[(math, grade)])
            {
                var edge = CreateEdge(subject, byCode[source].NodeId, byCode[target].NodeId);
                data.Edges.Add(edge);
                data.EdgeGrades.Add(new KnowledgeEdgeGrade { CenterId = _centerId, GraphVersionId = versionId,
                    EdgeId = edge.EdgeId, RelationType = edge.RelationType, Weight = edge.Weight,
                    CreatedAt = sampleTransition, CreatedBy = teacher });
            }
            data.Curriculums.Add(new Curriculum
            {
                CenterId = _centerId, CurriculumId = curriculumId, SubjectId = subject, TeacherId = teacher,
                GradeLevel = grade, GraphVersionId = versionId, Title = $"Giáo trình mẫu {(math ? "Toán" : "Tiếng Anh")} {grade} · mở rộng",
                Description = "Giáo trình minh họa theo khối, gồm chủ đề cốt lõi và câu hỏi gắn đúng khung tri thức; giáo viên cần bổ sung bài học theo kế hoạch của trường.",
                ReviewStatus = ReviewStatus.Published, CreatedAt = sampleTransition, UpdatedAt = sampleTransition
            });
            data.CurriculumClasses.Add(new CurriculumClass { CenterId = _centerId, CurriculumId = curriculumId,
                ClassId = classId, AssignedBy = teacher, AssignedAt = sampleTransition });
            data.CurriculumApplications.Add(new ClassCurriculumApplication { CenterId = _centerId,
                ApplicationId = curriculumId, ClassId = classId, CurriculumId = curriculumId, SubjectId = subject,
                ApplicationRole = "Primary", AssignedBy = teacher, StartedAt = sampleTransition,
                ClassGradeAtStart = grade, CurriculumGradeAtStart = grade });
            var oldApplication = data.CurriculumApplications.Single(a => a.ApplicationId == classId);
            oldApplication.EndedAt = sampleTransition;
            // ck_class_curriculum_application_dates requires EndedBy whenever EndedAt is set;
            // without it the whole seed transaction rolls back and the API cannot start on a fresh DB.
            oldApplication.EndedBy = teacher;
        }

        var criteria = new GradingCriteria { SchemaVersion = "1.0", RequiredIdeas = ["Đáp án đúng"],
            CommonErrors = ["Nhầm kiến thức nền"], ScoringNotes = "Chấm theo đáp án mẫu." };
        var templates = new List<QuestionTemplate>
        {
            Sample("MATH10-Q-01", "MATH10-QUADRATIC", 10, "Giải phương trình x² - 5x + 6 = 0. Viết hai nghiệm theo thứ tự tăng dần, cách nhau bằng dấu phẩy.", "2,3", "Phân tích (x-2)(x-3)=0."),
            Sample("MATH10-C-01", "MATH10-COORDINATE", 10, "Trong mặt phẳng tọa độ, tính khoảng cách giữa A(0;0) và B(3;4).", "5", "Áp dụng công thức khoảng cách: căn(3²+4²)=5.", true),
            Sample("MATH11-T-01", "MATH11-TRIG", 11, "Tính sin(π/2).", "1", "Giá trị sin tại góc π/2 bằng 1.", true),
            Sample("MATH11-TE-01", "MATH11-TRIG-EQUATIONS", 11, "Phương trình sin(x)=0 có bao nhiêu nghiệm trong [0;2π)?", "2", "Hai nghiệm là 0 và π.", true),
            Sample("MATH12-D-01", "MATH12-DERIVATIVE", 12, "Cho y=x³. Tính y' tại x=2.", "12", "y'=3x², thay x=2 được 12.", true),
            Sample("MATH12-O-01", "MATH12-OPTIMIZATION", 12, "Tìm giá trị lớn nhất của f(x)=-x²+4x+1 trên R.", "5", "Đỉnh parabol tại x=2 và f(2)=5.", true),
            Sample("ENG10-C-01", "ENG10-COMMUNITY", 10, "Complete: People who help their community without pay are called _____.", "volunteers", "The plural noun is volunteers."),
            Sample("ENG10-R-01", "ENG10-READING", 10, "Read: Lan plants trees with her classmates every Sunday. What do they plant?", "trees", "The passage says they plant trees."),
            Sample("ENG11-E-01", "ENG11-ENVIRONMENT", 11, "Complete: We can _____ paper instead of throwing it away.", "recycle", "Recycle means processing used material for reuse."),
            Sample("ENG11-W-01", "ENG11-WRITING", 11, "Complete the first conditional: If we plant more trees, the air _____ cleaner.", "will be", "Use will + base verb in the result clause."),
            Sample("ENG12-C-01", "ENG12-CAREERS", 12, "What does the abbreviation CV stand for in a job application?", "curriculum vitae", "CV stands for curriculum vitae."),
            Sample("ENG12-S-01", "ENG12-SPEAKING", 12, "In a job interview, how would you briefly explain one skill relevant to the position? Write two clear sentences.",
                "I communicate clearly and work well in a team. I used these skills during a school project.",
                "Give a relevant skill and a concrete example.", false, true)
        };
        GenerateQuestionsFromTemplates(data, templates, mathTeacher, englishTeacher);

        QuestionTemplate Sample(string code, string topic, byte grade, string prompt, string answer, string solution,
            bool numeric = false, bool essay = false) => new()
        {
            LogicalCode = code, TopicCode = topic, GradeLevel = grade, Difficulty = 2,
            QuestionType = essay ? QuestionType.Essay : QuestionType.ShortAnswer,
            AnswerEvaluationMode = essay ? QuestionAnswerEvaluationMode.Manual : numeric ? QuestionAnswerEvaluationMode.NumericRational : QuestionAnswerEvaluationMode.TextExact,
            LanguageCode = topic.StartsWith("MATH", StringComparison.Ordinal) ? "vi" : "en",
            QuestionText = prompt, CorrectAnswer = answer, Solution = solution,
            ExpectedReasoning = solution, GradingCriteria = criteria, MaxScore = 10, EstimatedTimeSeconds = essay ? 240U : 90U
        };
    }
}
