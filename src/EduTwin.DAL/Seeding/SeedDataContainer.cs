using System;
using System.Collections.Generic;
using EduTwin.DAL.IdentityAndTenancy;
using EduTwin.DAL.Organization;
using EduTwin.DAL.KnowledgeGraph;
using EduTwin.DAL.CurriculumAndQuestions;
using EduTwin.DAL.DigitalTwin;

namespace EduTwin.DAL.Seeding;

public class SeedDataContainer
{
    public Center Center { get; set; } = null!;
    public List<User> Users { get; set; } = new();
    public List<Teacher> Teachers { get; set; } = new();
    public List<Student> Students { get; set; } = new();
    public List<Subject> Subjects { get; set; } = new();
    public List<Class> Classes { get; set; } = new();
    public List<ClassStudent> ClassStudents { get; set; } = new();
    public List<KnowledgeNode> Topics { get; set; } = new();
    public List<KnowledgeEdge> Edges { get; set; } = new();
    public List<Curriculum> Curriculums { get; set; } = new();
    public List<CurriculumClass> CurriculumClasses { get; set; } = new();
    public List<CurriculumNode> CurriculumNodes { get; set; } = new();
    public List<Question> Questions { get; set; } = new();
    public List<QuestionKnowledgeNode> QuestionNodes { get; set; } = new();
    public List<QuestionOption> QuestionOptions { get; set; } = new();
    public List<StudentSubjectGoal> Goals { get; set; } = new();
}
