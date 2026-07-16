using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Seeding;

namespace EduTwin.BLL.Seeding;

public enum TenantSeedStatus
{
    Missing,
    Complete,
    Conflict
}

public class SeedExecutionPlan
{
    public bool ShouldSeedA { get; set; }
    public bool ShouldSeedB { get; set; }
    public bool IsNoOp { get; set; }
    public bool HasConflict { get; set; }
    public int ExpectedUsersToHash { get; set; }

    public static SeedExecutionPlan Create(TenantSeedStatus statusA, TenantSeedStatus statusB)
    {
        if (statusA == TenantSeedStatus.Conflict || statusB == TenantSeedStatus.Conflict)
        {
            return new SeedExecutionPlan { HasConflict = true, ExpectedUsersToHash = 0 };
        }

        if (statusA == TenantSeedStatus.Complete && statusB == TenantSeedStatus.Complete)
        {
            return new SeedExecutionPlan { IsNoOp = true, ExpectedUsersToHash = 0 };
        }

        bool seedA = statusA == TenantSeedStatus.Missing;
        bool seedB = statusB == TenantSeedStatus.Missing;
        
        int hashes = 0;
        if (seedA) hashes += 8;
        if (seedB) hashes += 8;

        return new SeedExecutionPlan
        {
            ShouldSeedA = seedA,
            ShouldSeedB = seedB,
            ExpectedUsersToHash = hashes
        };
    }
}

public interface IManifestEvaluator
{
    Task<TenantSeedStatus> EvaluateTenantAsync(bool isCenterA);
}

public class ManifestEvaluator : IManifestEvaluator
{
    private readonly EduTwinDbContext _dbContext;

    public ManifestEvaluator(EduTwinDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<TenantSeedStatus> EvaluateTenantAsync(bool isCenterA)
    {
        var factory = new EduTwinSeedFactory(isCenterA);
        var expected = factory.CreateData();
        var centerId = expected.Center.CenterId;

        if (await CheckGlobalCollisionsAsync(expected, centerId))
        {
            return TenantSeedStatus.Conflict;
        }

        // Check if any center row exists for this ID, regardless of IsDeleted status
        var centerExists = await _dbContext.Centers.AnyAsync(c => c.CenterId == centerId);
        if (centerExists)
        {
            // Center is physically in the DB, check if it's soft-deleted
            var isSoftDeleted = await _dbContext.Centers.AnyAsync(c => c.CenterId == centerId && c.IsDeleted);
            if (isSoftDeleted) return TenantSeedStatus.Conflict;
        }
        else
        {
            if (await HasOrphanDataAsync(centerId)) return TenantSeedStatus.Conflict;
            return TenantSeedStatus.Missing;
        }

        if (!await IsMatchCenterAsync(expected, centerId)) return TenantSeedStatus.Conflict;
        if (!await IsMatchUsersAsync(expected, centerId)) return TenantSeedStatus.Conflict;
        if (!await IsMatchTeachersAsync(expected, centerId)) return TenantSeedStatus.Conflict;
        if (!await IsMatchStudentsAsync(expected, centerId)) return TenantSeedStatus.Conflict;
        if (!await IsMatchSubjectsAsync(expected, centerId)) return TenantSeedStatus.Conflict;
        if (!await IsMatchClassesAsync(expected, centerId)) return TenantSeedStatus.Conflict;
        if (!await IsMatchClassStudentsAsync(expected, centerId)) return TenantSeedStatus.Conflict;
        if (!await IsMatchTopicsAsync(expected, centerId)) return TenantSeedStatus.Conflict;
        if (!await IsMatchEdgesAsync(expected, centerId)) return TenantSeedStatus.Conflict;
        if (!await IsMatchCurriculumsAsync(expected, centerId)) return TenantSeedStatus.Conflict;
        if (!await IsMatchCurriculumClassesAsync(expected, centerId)) return TenantSeedStatus.Conflict;
        if (!await IsMatchCurriculumNodesAsync(expected, centerId)) return TenantSeedStatus.Conflict;
        if (!await IsMatchQuestionsAsync(expected, centerId)) return TenantSeedStatus.Conflict;
        if (!await IsMatchQuestionNodesAsync(expected, centerId)) return TenantSeedStatus.Conflict;
        if (!await IsMatchQuestionOptionsAsync(expected, centerId)) return TenantSeedStatus.Conflict;
        if (!await IsMatchGoalsAsync(expected, centerId)) return TenantSeedStatus.Conflict;

        return TenantSeedStatus.Complete;
    }

    private async Task<bool> CheckGlobalCollisionsAsync(SeedDataContainer expected, Guid centerId)
    {
        if (await _dbContext.Centers.AnyAsync(c => c.CenterCode == expected.Center.CenterCode && c.CenterId != centerId)) return true;
        
        foreach (var u in expected.Users)
            if (await _dbContext.Users.AnyAsync(x => x.UserId == u.UserId && x.CenterId != centerId)) return true;

        foreach (var s in expected.Subjects)
            if (await _dbContext.Subjects.AnyAsync(x => x.SubjectId == s.SubjectId && x.CenterId != centerId)) return true;

        foreach (var c in expected.Classes)
            if (await _dbContext.Classes.AnyAsync(x => x.ClassId == c.ClassId && x.CenterId != centerId)) return true;

        foreach (var c in expected.Curriculums)
            if (await _dbContext.Curriculums.AnyAsync(x => x.CurriculumId == c.CurriculumId && x.CenterId != centerId)) return true;

        foreach (var n in expected.Topics)
            if (await _dbContext.KnowledgeNodes.AnyAsync(x => x.NodeId == n.NodeId && x.CenterId != centerId)) return true;

        foreach (var q in expected.Questions)
            if (await _dbContext.Questions.AnyAsync(x => x.QuestionId == q.QuestionId && x.CenterId != centerId)) return true;

        foreach (var e in expected.Edges)
            if (await _dbContext.KnowledgeEdges.AnyAsync(x => x.EdgeId == e.EdgeId && x.CenterId != centerId)) return true;
        
        foreach (var o in expected.QuestionOptions)
            if (await _dbContext.QuestionOptions.AnyAsync(x => x.OptionId == o.OptionId && x.CenterId != centerId)) return true;
        
        foreach (var g in expected.Goals)
            if (await _dbContext.StudentSubjectGoals.AnyAsync(x => x.GoalId == g.GoalId && x.CenterId != centerId)) return true;

        return false;
    }

    private async Task<bool> HasOrphanDataAsync(Guid centerId)
    {
        return await _dbContext.Users.AnyAsync(u => u.CenterId == centerId)
            || await _dbContext.Teachers.AnyAsync(t => t.CenterId == centerId)
            || await _dbContext.Students.AnyAsync(s => s.CenterId == centerId)
            || await _dbContext.Subjects.AnyAsync(s => s.CenterId == centerId)
            || await _dbContext.Classes.AnyAsync(c => c.CenterId == centerId)
            || await _dbContext.ClassStudents.AnyAsync(cs => cs.CenterId == centerId)
            || await _dbContext.KnowledgeNodes.AnyAsync(n => n.CenterId == centerId)
            || await _dbContext.KnowledgeEdges.AnyAsync(e => e.CenterId == centerId)
            || await _dbContext.Curriculums.AnyAsync(c => c.CenterId == centerId)
            || await _dbContext.CurriculumClasses.AnyAsync(cc => cc.CenterId == centerId)
            || await _dbContext.CurriculumNodes.AnyAsync(cn => cn.CenterId == centerId)
            || await _dbContext.Questions.AnyAsync(q => q.CenterId == centerId)
            || await _dbContext.QuestionOptions.AnyAsync(qo => qo.CenterId == centerId)
            || await _dbContext.QuestionKnowledgeNodes.AnyAsync(qkn => qkn.CenterId == centerId)
            || await _dbContext.StudentSubjectGoals.AnyAsync(g => g.CenterId == centerId);
    }

    private async Task<bool> IsMatchCenterAsync(SeedDataContainer expected, Guid centerId)
    {
        var c = await _dbContext.Centers.FirstOrDefaultAsync(c => c.CenterId == centerId && !c.IsDeleted && c.Status == EduTwin.Contracts.Organization.CenterStatus.Active);
        return c != null && c.CenterCode == expected.Center.CenterCode;
    }

    private async Task<bool> IsMatchUsersAsync(SeedDataContainer expected, Guid centerId)
    {
        var expectedSet = expected.Users.Select(u => (u.UserId, u.Username, u.RoleName)).ToHashSet();
        var actualList = await _dbContext.Users.Where(u => u.CenterId == centerId && !u.IsDeleted && u.Status == EduTwin.Contracts.IdentityAndTenancy.UserStatus.Active)
            .Select(u => new { u.UserId, u.Username, u.RoleName }).ToListAsync();
        var actualSet = actualList.Select(u => (u.UserId, u.Username, u.RoleName)).ToHashSet();
        return expectedSet.SetEquals(actualSet);
    }

    private async Task<bool> IsMatchTeachersAsync(SeedDataContainer expected, Guid centerId)
    {
        var expectedSet = expected.Teachers.Select(t => t.TeacherId).ToHashSet();
        var actualList = await _dbContext.Teachers.Where(t => t.CenterId == centerId && !t.IsDeleted).Select(t => t.TeacherId).ToListAsync();
        return expectedSet.SetEquals(actualList);
    }

    private async Task<bool> IsMatchStudentsAsync(SeedDataContainer expected, Guid centerId)
    {
        var expectedSet = expected.Students.Select(s => (s.StudentId, s.FullName, s.GradeLevel, s.DateOfBirth)).ToHashSet();
        var actualList = await _dbContext.Students.Where(s => s.CenterId == centerId && !s.IsDeleted).ToListAsync();
        var actualSet = actualList.Select(s => (s.StudentId, s.FullName, s.GradeLevel, s.DateOfBirth)).ToHashSet();
        return expectedSet.SetEquals(actualSet);
    }

    private async Task<bool> IsMatchSubjectsAsync(SeedDataContainer expected, Guid centerId)
    {
        var expectedSet = expected.Subjects.Select(s => (s.SubjectId, s.SubjectCode)).ToHashSet();
        var actualList = await _dbContext.Subjects.Where(s => s.CenterId == centerId && !s.IsDeleted && s.IsActive).Select(s => new { s.SubjectId, s.SubjectCode }).ToListAsync();
        var actualSet = actualList.Select(s => (s.SubjectId, s.SubjectCode)).ToHashSet();
        return expectedSet.SetEquals(actualSet);
    }

    private async Task<bool> IsMatchClassesAsync(SeedDataContainer expected, Guid centerId)
    {
        var expectedSet = expected.Classes.Select(c => (c.ClassId, c.TeacherId, c.SubjectId, c.AcademicYear)).ToHashSet();
        var actualList = await _dbContext.Classes.Where(c => c.CenterId == centerId && !c.IsDeleted && c.Status == EduTwin.Contracts.Organization.ClassStatus.Active).Select(c => new { c.ClassId, c.TeacherId, c.SubjectId, c.AcademicYear }).ToListAsync();
        var actualSet = actualList.Select(c => (c.ClassId, c.TeacherId, c.SubjectId, c.AcademicYear)).ToHashSet();
        return expectedSet.SetEquals(actualSet);
    }

    private async Task<bool> IsMatchClassStudentsAsync(SeedDataContainer expected, Guid centerId)
    {
        var expectedSet = expected.ClassStudents.Select(cs => (cs.ClassId, cs.StudentId, cs.Status)).ToHashSet();
        var actualList = await _dbContext.ClassStudents.Where(cs => cs.CenterId == centerId).Select(cs => new { cs.ClassId, cs.StudentId, cs.Status }).ToListAsync();
        var actualSet = actualList.Select(cs => (cs.ClassId, cs.StudentId, cs.Status)).ToHashSet();
        return expectedSet.SetEquals(actualSet);
    }

    private async Task<bool> IsMatchTopicsAsync(SeedDataContainer expected, Guid centerId)
    {
        var expectedSet = expected.Topics.Select(t => (t.NodeId, t.SubjectId, t.NodeCode, t.NodeType, t.OrderIndex, t.ExamImportance, t.EstimatedLearningMinutes)).ToHashSet();
        var actualList = await _dbContext.KnowledgeNodes.Where(n => n.CenterId == centerId && !n.IsDeleted && n.IsActive && n.NodeType == EduTwin.Contracts.KnowledgeGraph.NodeType.Topic)
            .Select(n => new { n.NodeId, n.SubjectId, n.NodeCode, n.NodeType, n.OrderIndex, n.ExamImportance, n.EstimatedLearningMinutes }).ToListAsync();
        var actualSet = actualList.Select(n => (n.NodeId, n.SubjectId, n.NodeCode, n.NodeType, n.OrderIndex, n.ExamImportance, n.EstimatedLearningMinutes)).ToHashSet();
        return expectedSet.SetEquals(actualSet);
    }

    private async Task<bool> IsMatchEdgesAsync(SeedDataContainer expected, Guid centerId)
    {
        var expectedSet = expected.Edges.Select(e => (e.EdgeId, e.SubjectId, e.SourceNodeId, e.TargetNodeId, e.RelationType, e.Weight)).ToHashSet();
        var actualList = await _dbContext.KnowledgeEdges.Where(e => e.CenterId == centerId && !e.IsDeleted)
            .Select(e => new { e.EdgeId, e.SubjectId, e.SourceNodeId, e.TargetNodeId, e.RelationType, e.Weight }).ToListAsync();
        var actualSet = actualList.Select(e => (e.EdgeId, e.SubjectId, e.SourceNodeId, e.TargetNodeId, e.RelationType, e.Weight)).ToHashSet();
        return expectedSet.SetEquals(actualSet);
    }

    private async Task<bool> IsMatchCurriculumsAsync(SeedDataContainer expected, Guid centerId)
    {
        var expectedSet = expected.Curriculums.Select(c => (c.CurriculumId, c.TeacherId, c.SubjectId)).ToHashSet();
        var actualList = await _dbContext.Curriculums.Where(c => c.CenterId == centerId && !c.IsDeleted && c.ReviewStatus == EduTwin.Contracts.CurriculumAndQuestions.ReviewStatus.Published)
            .Select(c => new { c.CurriculumId, c.TeacherId, c.SubjectId }).ToListAsync();
        var actualSet = actualList.Select(c => (c.CurriculumId, c.TeacherId, c.SubjectId)).ToHashSet();
        return expectedSet.SetEquals(actualSet);
    }

    private async Task<bool> IsMatchCurriculumClassesAsync(SeedDataContainer expected, Guid centerId)
    {
        var expectedSet = expected.CurriculumClasses.Select(cc => (cc.CurriculumId, cc.ClassId, cc.AssignedBy)).ToHashSet();
        var actualList = await _dbContext.CurriculumClasses.Where(cc => cc.CenterId == centerId).Select(cc => new { cc.CurriculumId, cc.ClassId, cc.AssignedBy }).ToListAsync();
        var actualSet = actualList.Select(cc => (cc.CurriculumId, cc.ClassId, cc.AssignedBy)).ToHashSet();
        return expectedSet.SetEquals(actualSet);
    }

    private async Task<bool> IsMatchCurriculumNodesAsync(SeedDataContainer expected, Guid centerId)
    {
        var expectedSet = expected.CurriculumNodes.Select(cn => (cn.CurriculumId, cn.NodeId, cn.OrderIndex)).ToHashSet();
        var actualList = await _dbContext.CurriculumNodes.Where(cn => cn.CenterId == centerId).Select(cn => new { cn.CurriculumId, cn.NodeId, cn.OrderIndex }).ToListAsync();
        var actualSet = actualList.Select(cn => (cn.CurriculumId, cn.NodeId, cn.OrderIndex)).ToHashSet();
        return expectedSet.SetEquals(actualSet);
    }

    private async Task<bool> IsMatchQuestionsAsync(SeedDataContainer expected, Guid centerId)
    {
        var expectedSet = expected.Questions.Select(q => (q.QuestionId, q.SubjectId, q.PrimaryTopicNodeId, q.CreatedByTeacherId, q.QuestionType, q.Difficulty, q.LanguageCode)).ToHashSet();
        var actualList = await _dbContext.Questions.Where(q => q.CenterId == centerId && !q.IsDeleted && q.Status == EduTwin.Contracts.CurriculumAndQuestions.QuestionStatus.Active)
            .Select(q => new { q.QuestionId, q.SubjectId, q.PrimaryTopicNodeId, q.CreatedByTeacherId, q.QuestionType, q.Difficulty, q.LanguageCode }).ToListAsync();
        var actualSet = actualList.Select(q => (q.QuestionId, q.SubjectId, q.PrimaryTopicNodeId, q.CreatedByTeacherId, q.QuestionType, q.Difficulty, q.LanguageCode)).ToHashSet();
        return expectedSet.SetEquals(actualSet);
    }

    private async Task<bool> IsMatchQuestionNodesAsync(SeedDataContainer expected, Guid centerId)
    {
        var expectedSet = expected.QuestionNodes.Select(qn => (qn.QuestionId, qn.NodeId, qn.MappingRole)).ToHashSet();
        var actualList = await _dbContext.QuestionKnowledgeNodes.Where(qn => qn.CenterId == centerId).Select(qn => new { qn.QuestionId, qn.NodeId, qn.MappingRole }).ToListAsync();
        var actualSet = actualList.Select(qn => (qn.QuestionId, qn.NodeId, qn.MappingRole)).ToHashSet();
        return expectedSet.SetEquals(actualSet);
    }

    private async Task<bool> IsMatchQuestionOptionsAsync(SeedDataContainer expected, Guid centerId)
    {
        var expectedSet = expected.QuestionOptions.Select(o => (o.OptionId, o.QuestionId, o.OptionLabel, o.IsCorrect, o.OrderIndex)).ToHashSet();
        var actualList = await _dbContext.QuestionOptions.Where(o => o.CenterId == centerId && !o.IsDeleted)
            .Select(o => new { o.OptionId, o.QuestionId, o.OptionLabel, o.IsCorrect, o.OrderIndex }).ToListAsync();
        var actualSet = actualList.Select(o => (o.OptionId, o.QuestionId, o.OptionLabel, o.IsCorrect, o.OrderIndex)).ToHashSet();
        return expectedSet.SetEquals(actualSet);
    }

    private async Task<bool> IsMatchGoalsAsync(SeedDataContainer expected, Guid centerId)
    {
        var expectedSet = expected.Goals.Select(g => (g.GoalId, g.StudentId, g.SubjectId, g.TargetScore, g.RemainingDays, g.CurrentPredictedScore, g.RiskScore)).ToHashSet();
        var actualList = await _dbContext.StudentSubjectGoals.Where(g => g.CenterId == centerId && !g.IsDeleted)
            .Select(g => new { g.GoalId, g.StudentId, g.SubjectId, g.TargetScore, g.RemainingDays, g.CurrentPredictedScore, g.RiskScore }).ToListAsync();
        var actualSet = actualList.Select(g => (g.GoalId, g.StudentId, g.SubjectId, g.TargetScore, g.RemainingDays, g.CurrentPredictedScore, g.RiskScore)).ToHashSet();
        return expectedSet.SetEquals(actualSet);
    }
}
