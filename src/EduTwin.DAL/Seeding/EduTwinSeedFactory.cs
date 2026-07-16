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
            Timezone = "Asia/Bangkok",
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

        // 4. Classes
        var mathClassId = _isCenterA ? DeterministicSeedIds.CenterAMathClassId : DeterministicSeedIds.CenterBMathClassId;
        var engClassId = _isCenterA ? DeterministicSeedIds.CenterAEnglishClassId : DeterministicSeedIds.CenterBEnglishClassId;
        container.Classes.Add(new Class { CenterId = _centerId, ClassId = mathClassId, TeacherId = teacherMathId, SubjectId = mathSubId, ClassName = "Lớp Toán", AcademicYear = "2026-2027", Status = EduTwin.Contracts.Organization.ClassStatus.Active, CreatedAt = DeterministicSeedIds.AuditTimeUtc, UpdatedAt = DeterministicSeedIds.AuditTimeUtc });
        container.Classes.Add(new Class { CenterId = _centerId, ClassId = engClassId, TeacherId = teacherEnglishId, SubjectId = engSubId, ClassName = "Lớp Tiếng Anh", AcademicYear = "2026-2027", Status = EduTwin.Contracts.Organization.ClassStatus.Active, CreatedAt = DeterministicSeedIds.AuditTimeUtc, UpdatedAt = DeterministicSeedIds.AuditTimeUtc });

        // 5. Class Students
        foreach (var sId in studentIds)
        {
            container.ClassStudents.Add(new ClassStudent { CenterId = _centerId, ClassId = mathClassId, StudentId = sId, Status = EduTwin.Contracts.Organization.ClassStudentStatus.Active, JoinedAt = DeterministicSeedIds.AuditTimeUtc });
            container.ClassStudents.Add(new ClassStudent { CenterId = _centerId, ClassId = engClassId, StudentId = sId, Status = EduTwin.Contracts.Organization.ClassStudentStatus.Active, JoinedAt = DeterministicSeedIds.AuditTimeUtc });
        }

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
        var mathCurriculumId = _isCenterA ? DeterministicSeedIds.CenterAMathCurriculumId : DeterministicSeedIds.CenterBMathCurriculumId;
        var engCurriculumId = _isCenterA ? DeterministicSeedIds.CenterAEnglishCurriculumId : DeterministicSeedIds.CenterBEnglishCurriculumId;

        container.Curriculums.Add(new Curriculum { CenterId = _centerId, CurriculumId = mathCurriculumId, SubjectId = mathSubId, TeacherId = teacherMathId, Title = "Giáo trình Toán", ReviewStatus = EduTwin.Contracts.CurriculumAndQuestions.ReviewStatus.Published, SourceFile = null, CreatedAt = DeterministicSeedIds.AuditTimeUtc, UpdatedAt = DeterministicSeedIds.AuditTimeUtc });
        container.Curriculums.Add(new Curriculum { CenterId = _centerId, CurriculumId = engCurriculumId, SubjectId = engSubId, TeacherId = teacherEnglishId, Title = "Giáo trình Tiếng Anh", ReviewStatus = EduTwin.Contracts.CurriculumAndQuestions.ReviewStatus.Published, SourceFile = null, CreatedAt = DeterministicSeedIds.AuditTimeUtc, UpdatedAt = DeterministicSeedIds.AuditTimeUtc });

        container.CurriculumClasses.Add(new CurriculumClass { CenterId = _centerId, CurriculumId = mathCurriculumId, ClassId = mathClassId, AssignedBy = teacherMathId, AssignedAt = DeterministicSeedIds.AuditTimeUtc });
        container.CurriculumClasses.Add(new CurriculumClass { CenterId = _centerId, CurriculumId = engCurriculumId, ClassId = engClassId, AssignedBy = teacherEnglishId, AssignedAt = DeterministicSeedIds.AuditTimeUtc });

        for (int i = 0; i < mathTopics.Length; i++) container.CurriculumNodes.Add(new CurriculumNode { CenterId = _centerId, CurriculumId = mathCurriculumId, NodeId = mathTopics[i].NodeId, OrderIndex = (uint)(i + 1), CreatedAt = DeterministicSeedIds.AuditTimeUtc });
        for (int i = 0; i < engTopics.Length; i++) container.CurriculumNodes.Add(new CurriculumNode { CenterId = _centerId, CurriculumId = engCurriculumId, NodeId = engTopics[i].NodeId, OrderIndex = (uint)(i + 1), CreatedAt = DeterministicSeedIds.AuditTimeUtc });

        // 8. Questions
        GenerateQuestionsFromTemplates(container, QuestionSeedTemplates.GetTemplates(), teacherMathId, teacherEnglishId);

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

            var q = new Question
            {
                CenterId = _centerId,
                QuestionId = qId,
                SubjectId = topic.SubjectId,
                PrimaryTopicNodeId = topic.NodeId,
                CreatedByTeacherId = teacherId,
                QuestionType = t.QuestionType,
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
