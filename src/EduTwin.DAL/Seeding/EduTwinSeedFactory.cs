using System;
using System.Collections.Generic;
using System.Linq;
using Bogus;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Organization;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.DigitalTwin;

namespace EduTwin.DAL.Seeding;

public partial class EduTwinSeedFactory
{
    private readonly bool _isCenterA;
    private readonly Guid _centerId;

    // Counters
    private ulong _nodeId;
    private ulong _edgeId;
    private ulong _questionId;
    private ulong _optionId;
    private ulong _goalId;

    public EduTwinSeedFactory(bool isCenterA)
    {
        _isCenterA = isCenterA;

        _centerId = isCenterA ? DeterministicSeedIds.CenterAId : DeterministicSeedIds.CenterBId;
        _nodeId = isCenterA ? DeterministicSeedIds.CenterANodeIdBase : DeterministicSeedIds.CenterBNodeIdBase;
        _edgeId = isCenterA ? DeterministicSeedIds.CenterAEdgeIdBase : DeterministicSeedIds.CenterBEdgeIdBase;
        _questionId = isCenterA ? DeterministicSeedIds.CenterAQuestionIdBase : DeterministicSeedIds.CenterBQuestionIdBase;
        _optionId = isCenterA ? DeterministicSeedIds.CenterAOptionIdBase : DeterministicSeedIds.CenterBOptionIdBase;
        _goalId = isCenterA ? DeterministicSeedIds.CenterAGoalIdBase : DeterministicSeedIds.CenterBGoalIdBase;
    }

    public SeedDataContainer CreateData()
    {
        var container = new SeedDataContainer();

        // 1. Center
        container.Center = new Center
        {
            CenterId = _centerId,
            CenterCode = _isCenterA ? "EDUTWIN_A" : "EDUTWIN_B",
            CenterName = _isCenterA ? "Trung tâm EduTwin A" : "Trung tâm EduTwin B",
            Timezone = "Asia/Ho_Chi_Minh",
            Status = EduTwin.Contracts.Organization.CenterStatus.Active,
            CreatedAt = DeterministicSeedIds.AuditTimeUtc,
            UpdatedAt = DeterministicSeedIds.AuditTimeUtc
        };

        // 2. Users & Profiles
        var managerId = _isCenterA ? DeterministicSeedIds.CenterAManagerId : DeterministicSeedIds.CenterBManagerId;
        var teacherMathId = _isCenterA ? DeterministicSeedIds.CenterATeacherMathId : DeterministicSeedIds.CenterBTeacherMathId;
        var teacherEnglishId = _isCenterA ? DeterministicSeedIds.CenterATeacherEnglishId : DeterministicSeedIds.CenterBTeacherEnglishId;
        var studentIds = _isCenterA
            ? new[] { DeterministicSeedIds.CenterAStudent01Id, DeterministicSeedIds.CenterAStudent02Id, DeterministicSeedIds.CenterAStudent03Id, DeterministicSeedIds.CenterAStudent04Id, DeterministicSeedIds.CenterAStudent05Id }
            : new[] { DeterministicSeedIds.CenterBStudent01Id, DeterministicSeedIds.CenterBStudent02Id, DeterministicSeedIds.CenterBStudent03Id, DeterministicSeedIds.CenterBStudent04Id, DeterministicSeedIds.CenterBStudent05Id };

        container.Users.Add(CreateUser(managerId, "manager", "Center Manager", EduTwin.Contracts.IdentityAndTenancy.UserRole.CenterManager));
        container.Users.Add(CreateUser(teacherMathId, "teacher.math", "Teacher Math", EduTwin.Contracts.IdentityAndTenancy.UserRole.Teacher));
        container.Users.Add(CreateUser(teacherEnglishId, "teacher.english", "Teacher English", EduTwin.Contracts.IdentityAndTenancy.UserRole.Teacher));

        container.Teachers.Add(new Teacher { CenterId = _centerId, TeacherId = teacherMathId, CreatedAt = DeterministicSeedIds.AuditTimeUtc, UpdatedAt = DeterministicSeedIds.AuditTimeUtc });
        container.Teachers.Add(new Teacher { CenterId = _centerId, TeacherId = teacherEnglishId, CreatedAt = DeterministicSeedIds.AuditTimeUtc, UpdatedAt = DeterministicSeedIds.AuditTimeUtc });

        var faker = new Faker("vi");
        faker.Random = new Bogus.Randomizer(_isCenterA ? 202601 : 202602);

        for (int i = 0; i < 5; i++)
        {
            var studentId = studentIds[i];
            var fullName = faker.Name.FullName();
            var dob = DateOnly.FromDateTime(faker.Date.Between(new DateTime(2010, 1, 1), new DateTime(2010, 12, 31)));
            var grade = (byte)faker.Random.Int(10, 12);

            container.Users.Add(CreateUser(studentId, $"student{i+1:D2}", fullName, EduTwin.Contracts.IdentityAndTenancy.UserRole.Student));

            container.Students.Add(new Student
            {
                CenterId = _centerId,
                StudentId = studentId,
                FullName = fullName,
                GradeLevel = grade,
                DateOfBirth = dob,
                CreatedAt = DeterministicSeedIds.AuditTimeUtc,
                UpdatedAt = DeterministicSeedIds.AuditTimeUtc
            });
        }

        // 3. Subjects
        var mathSubId = _isCenterA ? DeterministicSeedIds.CenterAMathSubjectId : DeterministicSeedIds.CenterBMathSubjectId;
        var engSubId = _isCenterA ? DeterministicSeedIds.CenterAEnglishSubjectId : DeterministicSeedIds.CenterBEnglishSubjectId;
        container.Subjects.Add(new Subject { CenterId = _centerId, SubjectId = mathSubId, SubjectCode = "MATH", SubjectName = "Toán", IsActive = true, CreatedAt = DeterministicSeedIds.AuditTimeUtc, UpdatedAt = DeterministicSeedIds.AuditTimeUtc });
        container.Subjects.Add(new Subject { CenterId = _centerId, SubjectId = engSubId, SubjectCode = "ENGLISH", SubjectName = "Tiếng Anh", IsActive = true, CreatedAt = DeterministicSeedIds.AuditTimeUtc, UpdatedAt = DeterministicSeedIds.AuditTimeUtc });

        // 4. Six grade-specific classes. Preserve legacy IDs above for existing
        // databases; fresh seed data never creates an unclassified mixed class.
        foreach (byte grade in new byte[] { 10, 11, 12 })
        foreach (var math in new[] { true, false })
            container.Classes.Add(new Class
            {
                CenterId = _centerId, ClassId = DeterministicSeedIds.GradeClassId(_isCenterA, math, grade),
                TeacherId = math ? teacherMathId : teacherEnglishId, SubjectId = math ? mathSubId : engSubId,
                ClassName = $"Lớp {(math ? "Toán" : "Tiếng Anh")} {grade}", GradeLevel = grade,
                AcademicYear = "2026-2027", Status = EduTwin.Contracts.Organization.ClassStatus.Active,
                CreatedAt = DeterministicSeedIds.AuditTimeUtc, UpdatedAt = DeterministicSeedIds.AuditTimeUtc,
                RowVersion = DeterministicSeedIds.InitialRowVersion
            });

        // 5. Class Students
        foreach (var student in container.Students)
        foreach (var math in new[] { true, false })
            container.ClassStudents.Add(new ClassStudent
            {
                CenterId = _centerId, ClassId = DeterministicSeedIds.GradeClassId(_isCenterA, math, student.GradeLevel),
                StudentId = student.StudentId, GradeLevelAtEnrollment = student.GradeLevel,
                Status = EduTwin.Contracts.Organization.ClassStudentStatus.Active,
                JoinedAt = DeterministicSeedIds.AuditTimeUtc
            });

        // 6. Topics and Edges
        var mathTopics = new[]
        {
            CreateTopic(mathSubId, "MATH-FUNCTIONS", "Hàm số", 1, 80m, 120),
            CreateTopic(mathSubId, "MATH-EXP-LOG", "Mũ–Logarit", 2, 75m, 150),
            CreateTopic(mathSubId, "MATH-ANTIDERIVATIVE", "Nguyên hàm", 3, 90m, 180)
        };
        var engTopics = new[]
        {
            CreateTopic(engSubId, "ENG-TENSES", "Thì", 1, 85m, 100),
            CreateTopic(engSubId, "ENG-RELATIVE-CLAUSES", "Mệnh đề quan hệ", 2, 70m, 90),
            CreateTopic(engSubId, "ENG-CONTEXT-VOCAB", "Từ vựng theo ngữ cảnh", 3, 60m, 60)
        };
        container.Topics.AddRange(mathTopics);
        container.Topics.AddRange(engTopics);

        container.Edges.Add(CreateEdge(mathSubId, mathTopics[0].NodeId, mathTopics[1].NodeId));
        container.Edges.Add(CreateEdge(mathSubId, mathTopics[1].NodeId, mathTopics[2].NodeId));
        container.Edges.Add(CreateEdge(engSubId, engTopics[0].NodeId, engTopics[1].NodeId));
        container.Edges.Add(CreateEdge(engSubId, engTopics[1].NodeId, engTopics[2].NodeId));

        // 7. Curriculums
        foreach (byte grade in new byte[] { 10, 11, 12 })
        foreach (var math in new[] { true, false })
        {
            var curriculumId = DeterministicSeedIds.GradeCurriculumId(_isCenterA, math, grade);
            var teacherId = math ? teacherMathId : teacherEnglishId;
            container.Curriculums.Add(new Curriculum
            {
                CenterId = _centerId, CurriculumId = curriculumId, SubjectId = math ? mathSubId : engSubId,
                TeacherId = teacherId, Title = $"Khung mẫu {(math ? "Toán" : "Tiếng Anh")} {grade}", GradeLevel = grade,
                GraphVersionId = DeterministicSeedIds.GradeGraphVersionId(_isCenterA, math, grade),
                Description = "Dữ liệu minh họa một số chủ đề; chưa phải giáo trình đầy đủ cho cả năm học.",
                ReviewStatus = EduTwin.Contracts.CurriculumAndQuestions.ReviewStatus.Published, SourceFile = null,
                CreatedAt = DeterministicSeedIds.AuditTimeUtc, UpdatedAt = DeterministicSeedIds.AuditTimeUtc
            });
            container.CurriculumClasses.Add(new CurriculumClass
            {
                CenterId = _centerId, CurriculumId = curriculumId,
                ClassId = DeterministicSeedIds.GradeClassId(_isCenterA, math, grade),
                AssignedBy = teacherId, AssignedAt = DeterministicSeedIds.AuditTimeUtc
            });
            container.CurriculumApplications.Add(new ClassCurriculumApplication
            {
                ApplicationId = DeterministicSeedIds.GradeClassId(_isCenterA, math, grade), CenterId = _centerId,
                ClassId = DeterministicSeedIds.GradeClassId(_isCenterA, math, grade), CurriculumId = curriculumId,
                SubjectId = math ? mathSubId : engSubId, ApplicationRole = "Primary", AssignedBy = teacherId,
                StartedAt = DeterministicSeedIds.AuditTimeUtc, ClassGradeAtStart = grade, CurriculumGradeAtStart = grade
            });
            var topics = math ? new[] { mathTopics[grade - 10] } : engTopics;
            for (int i = 0; i < topics.Length; i++)
                container.CurriculumNodes.Add(new CurriculumNode
                {
                    CenterId = _centerId, CurriculumId = curriculumId, NodeId = topics[i].NodeId,
                    OrderIndex = (uint)(i + 1), CreatedAt = DeterministicSeedIds.AuditTimeUtc
                });
        }

        // Explicit grade assignments approved in KNOWLEDGE-GRADE-PROPOSALS-2026-10-22.
        // Do not infer scope from topic names/codes or a draft curriculum's composition.
        foreach (byte grade in new byte[] { 10, 11, 12 })
        foreach (var math in new[] { true, false })
        {
            var graphId = DeterministicSeedIds.GradeGraphVersionId(_isCenterA, math, grade);
            var actor = math ? teacherMathId : teacherEnglishId;
            container.GraphVersions.Add(new KnowledgeGraphVersion
            {
                CenterId = _centerId, GraphVersionId = graphId, SubjectId = math ? mathSubId : engSubId,
                GradeLevel = grade, Status = "Published", IsCurrent = true,
                Title = $"Khung tri thức {(math ? "Toán" : "Tiếng Anh")} {grade}",
                CreatedAt = DeterministicSeedIds.AuditTimeUtc, UpdatedAt = DeterministicSeedIds.AuditTimeUtc,
                CreatedBy = actor, UpdatedBy = actor
            });
            var gradeTopics = math ? new[] { mathTopics[grade - 10] } : engTopics;
            foreach (var topic in gradeTopics)
                container.NodeGrades.Add(new KnowledgeNodeGrade { CenterId = _centerId, GraphVersionId = graphId,
                    NodeId = topic.NodeId, CreatedAt = DeterministicSeedIds.AuditTimeUtc, CreatedBy = actor });
            var gradeEdges = math
                ? (grade == 10 ? Array.Empty<KnowledgeEdge>() : new[] { container.Edges[grade - 11] })
                : container.Edges.Where(e => e.SubjectId == engSubId).ToArray();
            foreach (var edge in gradeEdges)
                container.EdgeGrades.Add(new KnowledgeEdgeGrade { CenterId = _centerId, GraphVersionId = graphId,
                    EdgeId = edge.EdgeId, RelationType = edge.RelationType, Weight = edge.Weight,
                    SourceGraphVersionId = math ? DeterministicSeedIds.GradeGraphVersionId(_isCenterA, true, (byte)(grade - 1)) : null,
                    CreatedAt = DeterministicSeedIds.AuditTimeUtc, CreatedBy = actor });
        }

        // 8. Questions
        GenerateQuestionsFromTemplates(container, QuestionSeedTemplates.GetTemplates(), teacherMathId, teacherEnglishId);
        foreach (var version in container.GraphVersions) version.IsCurrent = false;
        AddExpandedGradeSamples(container, mathSubId, engSubId, teacherMathId, teacherEnglishId);

        // 9. Student Goals
        foreach (var sId in studentIds)
        {
            container.Goals.Add(CreateGoal(sId, mathSubId));
            container.Goals.Add(CreateGoal(sId, engSubId));
        }

        return container;
    }

    private User CreateUser(Guid id, string username, string fullName, EduTwin.Contracts.IdentityAndTenancy.UserRole roleEnum)
    {
        return new User
        {
            CenterId = _centerId,
            UserId = id,
            Username = username,
            PasswordHash = "", // Replaced by Seeder
            DisplayName = fullName,
            RoleName = roleEnum,
            Status = EduTwin.Contracts.IdentityAndTenancy.UserStatus.Active,
            AuthVersion = 1,
            CreatedAt = DeterministicSeedIds.AuditTimeUtc,
            UpdatedAt = DeterministicSeedIds.AuditTimeUtc
        };
    }

    // Short core-idea briefs per seed topic. The Study ("Tìm hiểu") activity renders the
    // topic description as its lesson text; without one, a topic whose worked examples
    // are exhausted has nothing to teach and the session cannot start. Teachers can
    // replace these with full lessons in real deployments.
    private static readonly IReadOnlyDictionary<string, string> TopicDescriptions = new Dictionary<string, string>
    {
        ["MATH-FUNCTIONS"] = "Ý chính: hàm số là quy tắc cho mỗi giá trị x đúng một giá trị y = f(x). Khi làm bài, em cần tìm tập xác định (các giá trị x khiến biểu thức có nghĩa), tính f(a) bằng cách thay x = a, và đọc đồ thị để biết hàm tăng/giảm trên từng khoảng. Hầu hết lỗi sai nằm ở điều kiện xác định: mẫu khác 0, biểu thức trong căn không âm.",
        ["MATH-EXP-LOG"] = "Ý chính: lũy thừa và logarit là hai phép toán ngược nhau: a^x = b khi và chỉ khi x = log_a(b), với a > 0, a ≠ 1 và b > 0. Khi giải phương trình mũ/logarit, hãy đưa hai vế về cùng cơ số hoặc lấy logarit hai vế, và luôn kiểm tra điều kiện xác định của logarit trước khi kết luận nghiệm.",
        ["MATH-ANTIDERIVATIVE"] = "Ý chính: nguyên hàm là phép toán ngược của đạo hàm — F(x) là nguyên hàm của f(x) nếu F'(x) = f(x). Mọi nguyên hàm chỉ khác nhau một hằng số C, nên đáp án luôn có dạng F(x) + C. Cách làm: thuộc bảng nguyên hàm cơ bản (x^n, 1/x, e^x, sin, cos) và biến đổi biểu thức về các dạng trong bảng trước khi lấy nguyên hàm từng phần tử.",
        ["MATH10-QUADRATIC"] = "Ý chính: hàm bậc hai y = ax² + bx + c (a ≠ 0) có đồ thị là parabol với đỉnh tại x = -b/2a. Dấu của tam thức bậc hai phụ thuộc vào dấu của a và biệt thức Δ: ngoài khoảng hai nghiệm trái dấu với a, trong khoảng hai nghiệm cùng dấu với a. Khi giải bất phương trình bậc hai, em lập bảng xét dấu theo thứ tự: tìm nghiệm → xác định dấu a → kết luận khoảng nghiệm.",
        ["MATH10-COORDINATE"] = "Ý chính: vectơ được biểu diễn bằng cặp tọa độ hiệu của điểm cuối trừ điểm đầu. Các phép toán làm theo từng thành phần: cộng/trừ từng tọa độ, nhân vô hướng cho tổng x₁x₂ + y₁y₂. Độ dài vectơ và khoảng cách hai điểm đều từ định lý Pythagore: √(x² + y²). Hai vectơ vuông góc khi tích vô hướng bằng 0.",
        ["MATH11-TRIG"] = "Ý chính: sin, cos, tan của một góc được đọc trên đường tròn lượng giác; dấu của chúng thay đổi theo góc phần tư. Cần thuộc: sin²x + cos²x = 1, tan x = sin x / cos x và các giá trị đặc biệt tại 0, π/6, π/4, π/3, π/2. Khi tính giá trị biểu thức lượng giác, hãy rút gọn về sin/cos và dùng các công thức trên thay vì bấm máy từng bước.",
        ["MATH11-TRIG-EQUATIONS"] = "Ý chính: phương trình lượng giác cơ bản dựa trên các họ nghiệm mẫu: sin x = sin a cho hai họ x = a + k2π và x = π − a + k2π; cos x = cos a cho x = ±a + k2π; tan x = tan a cho x = a + kπ. Cách làm: biến đổi phương trình về đúng một hàm lượng giác của cùng một góc, đối chiếu dạng mẫu, rồi giới hạn họ nghiệm theo khoảng đề bài yêu cầu.",
        ["MATH12-DERIVATIVE"] = "Ý chính: dấu của đạo hàm quyết định sự biến thiên — f'(x) > 0 thì hàm đồng biến, f'(x) < 0 thì nghịch biến; cực trị nằm tại điểm đạo hàm đổi dấu. Quy trình khảo sát: tập xác định → tính f'(x) → tìm nghiệm của f' → lập bảng biến thiên → kết luận khoảng đơn điệu và cực trị. Đây là kỹ năng nền cho mọi bài toán ứng dụng đạo hàm.",
        ["MATH12-OPTIMIZATION"] = "Ý chính: bài toán cực trị thực tế gồm ba bước — chọn biến và lập hàm mục tiêu f(x) kèm điều kiện ràng buộc, tìm giá trị lớn nhất/nhỏ nhất của f trên miền xác định (xét cả điểm cực trị lẫn hai đầu mút nếu là đoạn kín), rồi đối chiếu đáp số với yêu cầu thực tế. Sai lầm thường gặp là quên xét đầu mút hoặc quên điều kiện của biến.",
        ["ENG-TENSES"] = "Core idea: each tense signals WHEN an action happens and whether it is finished. Present simple is for habits and facts (I walk to school every day); past simple for completed past actions with a time marker (yesterday, last week); present perfect for past actions connected to now (have/has + past participle, with since/for/just/already). Strategy: find the time marker in the sentence first — it usually decides the tense before you look at the verb.",
        ["ENG-RELATIVE-CLAUSES"] = "Core idea: a relative clause adds information about a noun, and starts with a relative pronoun — who for people, which for things, that for both, whose for possession, where for places. Ask: 'Which noun am I describing, and is it a person or a thing?' If the clause is essential, no commas; extra information goes between commas and usually takes who/which, never that.",
        ["ENG-CONTEXT-VOCAB"] = "Core idea: you can guess an unknown word from its context instead of a dictionary. Look for four clue types: a definition after a dash or 'is/are', an example after 'such as/for example', an opposite after 'but/however', and a cause–effect link. Strategy: read the whole sentence, decide the word's role (noun/verb/adjective) and whether it is positive or negative, then choose the option that keeps the sentence logical.",
        ["ENG10-COMMUNITY"] = "Core idea: this topic builds vocabulary about family and community life — volunteers, neighbours, charity, local events — and the verbs that naturally go with them (take part in, donate to, help out). Strategy: learn words in small groups with a collocation and one example sentence each, instead of single words; most test questions ask you to complete a sentence where only one word fits the meaning and the grammar.",
        ["ENG10-READING"] = "Core idea: short reading passages about community life are tested with three question types — main idea (what is the text mostly about?), detail (which fact is stated?), and inference (what can you conclude?). Strategy: read the questions first, underline the sentence in the passage that answers each one, and distrust options that copy words from the text but change the meaning.",
        ["ENG11-ENVIRONMENT"] = "Core idea: environment vocabulary clusters around problems (pollution, deforestation, global warming), causes (emissions, waste), and solutions (recycle, renewable energy, protect). Strategy: sort new words into these three groups; exam sentences usually place a word inside one group, so the group's logic helps you eliminate wrong options quickly.",
        ["ENG11-WRITING"] = "Core idea: a good paragraph has one topic sentence stating the main idea, supporting sentences with reasons or examples, and a short concluding sentence. Linking words show the logic: first, moreover, however, therefore, in conclusion. Strategy when writing about the environment: state your point in the first sentence, give two supports, and end by restating the point in different words.",
        ["ENG12-CAREERS"] = "Core idea: career topics use vocabulary about jobs, skills and future plans — career, qualification, experience, apply for, be responsible for — plus future forms (will, be going to) for plans and predictions. Strategy: connect each job word with its typical verbs and workplace; questions often test whether the word matches both the meaning and the verb pattern around it.",
        ["ENG12-SPEAKING"] = "Core idea: job-interview speaking follows a predictable pattern — greet politely, introduce yourself briefly (name, strengths, experience), answer with a short structure (point → example → result), and close with thanks. Useful frames: 'I am interested in… because…', 'My strength is… For example…'. Strategy: prepare two or three flexible answers you can adapt, instead of memorising long scripts.",
    };

    private KnowledgeNode CreateTopic(Guid subjectId, string code, string name, int order, decimal examImportance, uint estimatedMins)
    {
        return new KnowledgeNode
        {
            CenterId = _centerId,
            NodeId = _nodeId++,
            SubjectId = subjectId,
            NodeType = EduTwin.Contracts.KnowledgeGraph.NodeType.Topic,
            NodeCode = code,
            NodeName = name,
            Description = TopicDescriptions.GetValueOrDefault(code),
            ExamImportance = examImportance,
            EstimatedLearningMinutes = estimatedMins,
            OrderIndex = (uint)order,
            IsActive = true,
            CreatedAt = DeterministicSeedIds.AuditTimeUtc,
            UpdatedAt = DeterministicSeedIds.AuditTimeUtc,
            RowVersion = DeterministicSeedIds.InitialRowVersion
        };
    }

    private KnowledgeEdge CreateEdge(Guid subjectId, ulong sourceId, ulong targetId)
    {
        return new KnowledgeEdge
        {
            CenterId = _centerId,
            SubjectId = subjectId,
            EdgeId = _edgeId++,
            SourceNodeId = sourceId,
            TargetNodeId = targetId,
            RelationType = EduTwin.Contracts.KnowledgeGraph.RelationType.PrerequisiteOf,
            Weight = 1.00m,
            CreatedAt = DeterministicSeedIds.AuditTimeUtc,
            UpdatedAt = DeterministicSeedIds.AuditTimeUtc
        };
    }

    private void GenerateQuestionsFromTemplates(SeedDataContainer c, List<QuestionTemplate> templates, Guid teacherMathId, Guid teacherEnglishId)
    {
        foreach (var t in templates)
        {
            var topic = c.Topics.First(n => n.NodeCode == t.TopicCode);
            var teacherId = t.TopicCode.StartsWith("MATH") ? teacherMathId : teacherEnglishId;
            var qId = _questionId++;
            var approvedVersionIds = c.GraphVersions.Where(v => v.SubjectId == topic.SubjectId &&
                v.GradeLevel == t.GradeLevel && v.Status == "Published" && !v.IsLegacy && v.IsCurrent)
                .Select(v => v.GraphVersionId).ToHashSet();
            if (!c.NodeGrades.Any(n => n.NodeId == topic.NodeId && approvedVersionIds.Contains(n.GraphVersionId)))
            {
                // Reserve deterministic IDs; do not silently regrade a question or broaden approved node scope.
                c.SkippedQuestionTemplates.Add(t.LogicalCode);
                if (t.QuestionType == EduTwin.Contracts.CurriculumAndQuestions.QuestionType.MultipleChoice)
                    _optionId += (ulong)t.Options.Count;
                continue;
            }

            var q = new Question
            {
                CenterId = _centerId,
                QuestionId = qId,
                SubjectId = topic.SubjectId,
                PrimaryTopicNodeId = topic.NodeId,
                GradeLevel = t.GradeLevel,
                CreatedByTeacherId = teacherId,
                QuestionType = t.QuestionType,
                AnswerEvaluationMode = t.AnswerEvaluationMode ?? (t.QuestionType switch
                {
                    EduTwin.Contracts.CurriculumAndQuestions.QuestionType.MultipleChoice => EduTwin.Contracts.CurriculumAndQuestions.QuestionAnswerEvaluationMode.TextExact,
                    EduTwin.Contracts.CurriculumAndQuestions.QuestionType.Essay => EduTwin.Contracts.CurriculumAndQuestions.QuestionAnswerEvaluationMode.Manual,
                    EduTwin.Contracts.CurriculumAndQuestions.QuestionType.ShortAnswer => EduTwin.Contracts.CurriculumAndQuestions.QuestionAnswerEvaluationMode.TextExact,
                    _ => EduTwin.Contracts.CurriculumAndQuestions.QuestionAnswerEvaluationMode.TextExact
                }),
                Difficulty = t.Difficulty,
                Status = EduTwin.Contracts.CurriculumAndQuestions.QuestionStatus.Active,
                ReasoningRequired = true,
                MaxScore = t.MaxScore,
                EstimatedTimeSeconds = t.EstimatedTimeSeconds,
                QuestionText = t.QuestionText,
                LanguageCode = t.LanguageCode,
                CorrectAnswer = t.CorrectAnswer,
                Solution = t.Solution,
                ExpectedReasoning = t.ExpectedReasoning,
                GradingCriteria = t.GradingCriteria,
                CreatedAt = DeterministicSeedIds.AuditTimeUtc,
                UpdatedAt = DeterministicSeedIds.AuditTimeUtc
            };
            c.Questions.Add(q);

            if (t.QuestionType == EduTwin.Contracts.CurriculumAndQuestions.QuestionType.MultipleChoice)
            {
                uint optionOrder = 0;
                foreach (var optTpl in t.Options)
                {
                    c.QuestionOptions.Add(new QuestionOption
                    {
                        CenterId = _centerId,
                        OptionId = _optionId++,
                        QuestionId = qId,
                        OptionLabel = optTpl.Label,
                        OptionText = optTpl.Text,
                        IsCorrect = optTpl.IsCorrect,
                        OrderIndex = optionOrder++,
                        CreatedAt = DeterministicSeedIds.AuditTimeUtc,
                        UpdatedAt = DeterministicSeedIds.AuditTimeUtc
                    });
                }
            }

            c.QuestionNodes.Add(new QuestionKnowledgeNode
            {
                CenterId = _centerId,
                QuestionId = qId,
                NodeId = topic.NodeId,
                MappingRole = EduTwin.Contracts.CurriculumAndQuestions.MappingRole.Primary,
                CreatedAt = DeterministicSeedIds.AuditTimeUtc
            });
        }
    }

    private StudentSubjectGoal CreateGoal(Guid studentId, Guid subjectId)
    {
        return new StudentSubjectGoal
        {
            CenterId = _centerId,
            GoalId = _goalId++,
            StudentId = studentId,
            SubjectId = subjectId,
            TargetScore = 8.00m,
            RemainingDays = 90,
            CurrentPredictedScore = 0m,
            RiskScore = 0m,
            CreatedAt = DeterministicSeedIds.AuditTimeUtc,
            UpdatedAt = DeterministicSeedIds.AuditTimeUtc
        };
    }
}
