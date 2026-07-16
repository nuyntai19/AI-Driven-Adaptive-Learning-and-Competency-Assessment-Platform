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

public class EduTwinSeedFactory
{
    private readonly bool _isCenterA;
    private readonly Faker _faker;
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
        int seedValue = isCenterA ? 202601 : 202602;
        Randomizer.Seed = new Random(seedValue);
        _faker = new Faker("vi");

        _centerId = isCenterA ? DeterministicSeedIds.CenterAId : DeterministicSeedIds.CenterBId;
        _nodeId = isCenterA ? DeterministicSeedIds.CenterANodeIdBase : DeterministicSeedIds.CenterBNodeIdBase;
        _edgeId = isCenterA ? DeterministicSeedIds.CenterAEdgeIdBase : DeterministicSeedIds.CenterBEdgeIdBase;
        _questionId = isCenterA ? DeterministicSeedIds.CenterAQuestionIdBase : DeterministicSeedIds.CenterBQuestionIdBase;
        _optionId = isCenterA ? DeterministicSeedIds.CenterAOptionIdBase : DeterministicSeedIds.CenterBOptionIdBase;
        _goalId = isCenterA ? DeterministicSeedIds.CenterAGoalIdBase : DeterministicSeedIds.CenterBGoalIdBase;
    }

    public SeedDataContainer CreateData(string passwordHash)
    {
        var container = new SeedDataContainer();

        // 1. Center
        container.Center = new Center
        {
            CenterId = _centerId,
            CenterCode = _isCenterA ? "EDUTWIN_A" : "EDUTWIN_B",
            CenterName = _isCenterA ? "Trung tâm EduTwin A" : "Trung tâm EduTwin B",
            Timezone = "Asia/Bangkok",
            Status = EduTwin.Contracts.Organization.CenterStatus.Active,
            CreatedAt = DeterministicSeedIds.AuditTimeUtc,
            UpdatedAt = DeterministicSeedIds.AuditTimeUtc
        };

        // 2. Users
        var managerId = _isCenterA ? DeterministicSeedIds.CenterAManagerId : DeterministicSeedIds.CenterBManagerId;
        var teacherMathId = _isCenterA ? DeterministicSeedIds.CenterATeacherMathId : DeterministicSeedIds.CenterBTeacherMathId;
        var teacherEnglishId = _isCenterA ? DeterministicSeedIds.CenterATeacherEnglishId : DeterministicSeedIds.CenterBTeacherEnglishId;
        var studentIds = _isCenterA 
            ? new[] { DeterministicSeedIds.CenterAStudent01Id, DeterministicSeedIds.CenterAStudent02Id, DeterministicSeedIds.CenterAStudent03Id, DeterministicSeedIds.CenterAStudent04Id, DeterministicSeedIds.CenterAStudent05Id }
            : new[] { DeterministicSeedIds.CenterBStudent01Id, DeterministicSeedIds.CenterBStudent02Id, DeterministicSeedIds.CenterBStudent03Id, DeterministicSeedIds.CenterBStudent04Id, DeterministicSeedIds.CenterBStudent05Id };

        var users = new List<User>
        {
            CreateUser(managerId, "manager", "Center Manager", "CenterManager", passwordHash),
            CreateUser(teacherMathId, "teacher.math", "Teacher Math", "Teacher", passwordHash),
            CreateUser(teacherEnglishId, "teacher.english", "Teacher English", "Teacher", passwordHash)
        };
        for (int i = 0; i < 5; i++)
        {
            users.Add(CreateUser(studentIds[i], $"student{i+1:D2}", _faker.Name.FullName(), "Student", passwordHash));
        }
        container.Users = users;

        // 3. Teachers & Students Profiles
        container.Teachers.Add(new Teacher { CenterId = _centerId, TeacherId = teacherMathId, CreatedAt = DeterministicSeedIds.AuditTimeUtc, UpdatedAt = DeterministicSeedIds.AuditTimeUtc });
        container.Teachers.Add(new Teacher { CenterId = _centerId, TeacherId = teacherEnglishId, CreatedAt = DeterministicSeedIds.AuditTimeUtc, UpdatedAt = DeterministicSeedIds.AuditTimeUtc });
        
        foreach (var sId in studentIds)
        {
            container.Students.Add(new Student 
            { 
                CenterId = _centerId, 
                StudentId = sId, 
                FullName = _faker.Name.FullName(),
                GradeLevel = 10,
                DateOfBirth = new DateOnly(2010, 1, 1).AddDays(_faker.Random.Int(0, 365)),
                CreatedAt = DeterministicSeedIds.AuditTimeUtc,
                UpdatedAt = DeterministicSeedIds.AuditTimeUtc
            });
        }

        // 4. Subjects
        var mathSubId = _isCenterA ? DeterministicSeedIds.SubjectMathId : new Guid("30000000-0000-0000-0000-000000000009");
        var engSubId = _isCenterA ? DeterministicSeedIds.SubjectEnglishId : new Guid("40000000-0000-0000-0000-000000000009");
        container.Subjects.Add(new Subject { CenterId = _centerId, SubjectId = mathSubId, SubjectCode = "MATH", SubjectName = "Toán", IsActive = true, CreatedAt = DeterministicSeedIds.AuditTimeUtc, UpdatedAt = DeterministicSeedIds.AuditTimeUtc });
        container.Subjects.Add(new Subject { CenterId = _centerId, SubjectId = engSubId, SubjectCode = "ENGLISH", SubjectName = "Tiếng Anh", IsActive = true, CreatedAt = DeterministicSeedIds.AuditTimeUtc, UpdatedAt = DeterministicSeedIds.AuditTimeUtc });

        // 5. Classes
        var mathClassId = _isCenterA ? DeterministicSeedIds.CenterAMathClassId : DeterministicSeedIds.CenterBMathClassId;
        var engClassId = _isCenterA ? DeterministicSeedIds.CenterAEnglishClassId : DeterministicSeedIds.CenterBEnglishClassId;
        container.Classes.Add(new Class { CenterId = _centerId, ClassId = mathClassId, TeacherId = teacherMathId, SubjectId = mathSubId, ClassName = "Lớp Toán", AcademicYear = "2026-2027", Status = EduTwin.Contracts.Organization.ClassStatus.Active, CreatedAt = DeterministicSeedIds.AuditTimeUtc, UpdatedAt = DeterministicSeedIds.AuditTimeUtc });
        container.Classes.Add(new Class { CenterId = _centerId, ClassId = engClassId, TeacherId = teacherEnglishId, SubjectId = engSubId, ClassName = "Lớp Tiếng Anh", AcademicYear = "2026-2027", Status = EduTwin.Contracts.Organization.ClassStatus.Active, CreatedAt = DeterministicSeedIds.AuditTimeUtc, UpdatedAt = DeterministicSeedIds.AuditTimeUtc });

        // 6. Class Students
        foreach (var sId in studentIds)
        {
            container.ClassStudents.Add(new ClassStudent { CenterId = _centerId, ClassId = mathClassId, StudentId = sId, JoinedAt = DeterministicSeedIds.AuditTimeUtc });
            container.ClassStudents.Add(new ClassStudent { CenterId = _centerId, ClassId = engClassId, StudentId = sId, JoinedAt = DeterministicSeedIds.AuditTimeUtc });
        }

        // 7. Topics and Edges
        var mathTopics = new[]
        {
            CreateTopic(mathSubId, "MATH-FUNCTIONS", "Hàm số", 1),
            CreateTopic(mathSubId, "MATH-EXP-LOG", "Mũ–Logarit", 2),
            CreateTopic(mathSubId, "MATH-ANTIDERIVATIVE", "Nguyên hàm", 3)
        };
        var engTopics = new[]
        {
            CreateTopic(engSubId, "ENG-TENSES", "Thì", 1),
            CreateTopic(engSubId, "ENG-RELATIVE-CLAUSES", "Mệnh đề quan hệ", 2),
            CreateTopic(engSubId, "ENG-CONTEXT-VOCAB", "Từ vựng theo ngữ cảnh", 3)
        };
        container.Topics.AddRange(mathTopics);
        container.Topics.AddRange(engTopics);

        container.Edges.Add(CreateEdge(mathSubId, mathTopics[0].NodeId, mathTopics[1].NodeId));
        container.Edges.Add(CreateEdge(mathSubId, mathTopics[1].NodeId, mathTopics[2].NodeId));
        container.Edges.Add(CreateEdge(engSubId, engTopics[0].NodeId, engTopics[1].NodeId));
        container.Edges.Add(CreateEdge(engSubId, engTopics[1].NodeId, engTopics[2].NodeId));

        // 8. Curriculums
        var mathCurriculumId = _isCenterA ? DeterministicSeedIds.CenterAMathCurriculumId : DeterministicSeedIds.CenterBMathCurriculumId;
        var engCurriculumId = _isCenterA ? DeterministicSeedIds.CenterAEnglishCurriculumId : DeterministicSeedIds.CenterBEnglishCurriculumId;
        
        container.Curriculums.Add(new Curriculum { CenterId = _centerId, CurriculumId = mathCurriculumId, SubjectId = mathSubId, TeacherId = teacherMathId, Title = "Giáo trình Toán", ReviewStatus = EduTwin.Contracts.CurriculumAndQuestions.ReviewStatus.Published, CreatedAt = DeterministicSeedIds.AuditTimeUtc, UpdatedAt = DeterministicSeedIds.AuditTimeUtc });
        container.Curriculums.Add(new Curriculum { CenterId = _centerId, CurriculumId = engCurriculumId, SubjectId = engSubId, TeacherId = teacherEnglishId, Title = "Giáo trình Tiếng Anh", ReviewStatus = EduTwin.Contracts.CurriculumAndQuestions.ReviewStatus.Published, CreatedAt = DeterministicSeedIds.AuditTimeUtc, UpdatedAt = DeterministicSeedIds.AuditTimeUtc });

        container.CurriculumClasses.Add(new CurriculumClass { CenterId = _centerId, CurriculumId = mathCurriculumId, ClassId = mathClassId, AssignedAt = DeterministicSeedIds.AuditTimeUtc });
        container.CurriculumClasses.Add(new CurriculumClass { CenterId = _centerId, CurriculumId = engCurriculumId, ClassId = engClassId, AssignedAt = DeterministicSeedIds.AuditTimeUtc });

        for (int i = 0; i < mathTopics.Length; i++) container.CurriculumNodes.Add(new CurriculumNode { CenterId = _centerId, CurriculumId = mathCurriculumId, NodeId = mathTopics[i].NodeId, OrderIndex = (uint)i, CreatedAt = DeterministicSeedIds.AuditTimeUtc });
        for (int i = 0; i < engTopics.Length; i++) container.CurriculumNodes.Add(new CurriculumNode { CenterId = _centerId, CurriculumId = engCurriculumId, NodeId = engTopics[i].NodeId, OrderIndex = (uint)i, CreatedAt = DeterministicSeedIds.AuditTimeUtc });

        // 9. Questions
        GenerateQuestionsForTopic(container, mathTopics[0], mathSubId, teacherMathId, "vi");
        GenerateQuestionsForTopic(container, mathTopics[1], mathSubId, teacherMathId, "vi");
        GenerateQuestionsForTopic(container, mathTopics[2], mathSubId, teacherMathId, "vi");
        GenerateQuestionsForTopic(container, engTopics[0], engSubId, teacherEnglishId, "en");
        GenerateQuestionsForTopic(container, engTopics[1], engSubId, teacherEnglishId, "en");
        GenerateQuestionsForTopic(container, engTopics[2], engSubId, teacherEnglishId, "en");

        // 10. Student Goals
        foreach (var sId in studentIds)
        {
            container.Goals.Add(CreateGoal(sId, mathSubId));
            container.Goals.Add(CreateGoal(sId, engSubId));
        }

        return container;
    }

    private User CreateUser(Guid id, string username, string fullName, string role, string passwordHash)
    {
        var roleEnum = role == "CenterManager" ? EduTwin.Contracts.IdentityAndTenancy.UserRole.CenterManager 
                     : role == "Teacher" ? EduTwin.Contracts.IdentityAndTenancy.UserRole.Teacher 
                     : EduTwin.Contracts.IdentityAndTenancy.UserRole.Student;

        return new User
        {
            CenterId = _centerId,
            UserId = id,
            Username = username,
            PasswordHash = passwordHash,
            DisplayName = fullName,
            RoleName = roleEnum,
            Status = EduTwin.Contracts.IdentityAndTenancy.UserStatus.Active,
            AuthVersion = 1,
            CreatedAt = DeterministicSeedIds.AuditTimeUtc,
            UpdatedAt = DeterministicSeedIds.AuditTimeUtc
        };
    }

    private KnowledgeNode CreateTopic(Guid subjectId, string code, string name, int order)
    {
        return new KnowledgeNode
        {
            CenterId = _centerId,
            NodeId = _nodeId++,
            SubjectId = subjectId,
            NodeType = EduTwin.Contracts.KnowledgeGraph.NodeType.Topic,
            NodeCode = code,
            NodeName = name,
            ExamImportance = _faker.Random.Decimal(0, 100),
            EstimatedLearningMinutes = (uint)_faker.Random.Int(30, 120),
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
            CreatedAt = DeterministicSeedIds.AuditTimeUtc,
            UpdatedAt = DeterministicSeedIds.AuditTimeUtc
        };
    }

    private void GenerateQuestionsForTopic(SeedDataContainer c, KnowledgeNode topic, Guid subjectId, Guid teacherId, string lang)
    {
        // 5 questions: 1 MC, 2 SA, 3 MC, 4 SA, 5 Essay
        c.Questions.Add(CreateQuestion(topic.NodeId, subjectId, teacherId, 1, EduTwin.Contracts.CurriculumAndQuestions.QuestionType.MultipleChoice, lang, c));
        c.Questions.Add(CreateQuestion(topic.NodeId, subjectId, teacherId, 2, EduTwin.Contracts.CurriculumAndQuestions.QuestionType.ShortAnswer, lang, c));
        c.Questions.Add(CreateQuestion(topic.NodeId, subjectId, teacherId, 3, EduTwin.Contracts.CurriculumAndQuestions.QuestionType.MultipleChoice, lang, c));
        c.Questions.Add(CreateQuestion(topic.NodeId, subjectId, teacherId, 4, EduTwin.Contracts.CurriculumAndQuestions.QuestionType.ShortAnswer, lang, c));
        c.Questions.Add(CreateQuestion(topic.NodeId, subjectId, teacherId, 5, EduTwin.Contracts.CurriculumAndQuestions.QuestionType.Essay, lang, c));
    }

    private Question CreateQuestion(ulong topicId, Guid subjectId, Guid teacherId, int difficulty, EduTwin.Contracts.CurriculumAndQuestions.QuestionType type, string lang, SeedDataContainer c)
    {
        var qId = _questionId++;
        
        string qText = lang == "vi" ? $"Câu hỏi {type} mức {difficulty} cho {_faker.Lorem.Sentence()}" : $"Question {type} diff {difficulty} for {_faker.Lorem.Sentence()}";
        string solution = lang == "vi" ? "Lời giải chi tiết" : "Detailed solution";
        string reasoning = lang == "vi" ? "Lập luận kì vọng" : "Expected reasoning";
        
        var q = new Question
        {
            CenterId = _centerId,
            QuestionId = qId,
            SubjectId = subjectId,
            PrimaryTopicNodeId = topicId,
            CreatedByTeacherId = teacherId,
            QuestionType = type,
            Difficulty = (byte)difficulty,
            Status = EduTwin.Contracts.CurriculumAndQuestions.QuestionStatus.Active,
            ReasoningRequired = true,
            MaxScore = (uint)(difficulty * 10),
            EstimatedTimeSeconds = (uint)(difficulty * 60),
            QuestionText = qText,
            LanguageCode = lang,
            Solution = solution,
            ExpectedReasoning = reasoning,
            GradingCriteria = new EduTwin.Contracts.CurriculumAndQuestions.GradingCriteria { SchemaVersion = "1.0", RequiredIdeas = new System.Collections.Generic.List<string> { "idea1" }, CommonErrors = new System.Collections.Generic.List<string> { "error1" }, ScoringNotes = "note1" },
            CreatedAt = DeterministicSeedIds.AuditTimeUtc,
            UpdatedAt = DeterministicSeedIds.AuditTimeUtc
        };

        if (type == EduTwin.Contracts.CurriculumAndQuestions.QuestionType.MultipleChoice)
        {
            var correctIndex = _faker.Random.Int(0, 3);
            for (int i = 0; i < 4; i++)
            {
                var label = ((char)('A' + i)).ToString();
                var isCorrect = i == correctIndex;
                c.QuestionOptions.Add(new QuestionOption
                {
                    CenterId = _centerId,
                    OptionId = _optionId++,
                    QuestionId = qId,
                    OptionLabel = label,
                    OptionText = $"Option {label}",
                    IsCorrect = isCorrect,
                    OrderIndex = (uint)i,
                    CreatedAt = DeterministicSeedIds.AuditTimeUtc,
                    UpdatedAt = DeterministicSeedIds.AuditTimeUtc
                });
                if (isCorrect) q.CorrectAnswer = label;
            }
        }
        else
        {
            q.CorrectAnswer = "Answer";
        }

        c.QuestionNodes.Add(new QuestionKnowledgeNode
        {
            CenterId = _centerId,
            QuestionId = qId,
            NodeId = topicId,
            MappingRole = EduTwin.Contracts.CurriculumAndQuestions.MappingRole.Primary,
            CreatedAt = DeterministicSeedIds.AuditTimeUtc
        });

        return q;
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
