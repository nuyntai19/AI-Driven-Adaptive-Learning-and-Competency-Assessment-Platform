using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.Dashboards;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.Organization;
using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Dashboards;

public sealed class GetCenterDashboardUseCase : IGetCenterDashboardUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly TimeProvider _timeProvider;

    public GetCenterDashboardUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<CenterDashboardResult> ExecuteAsync(
        Guid? subjectId,
        decimal riskThreshold,
        CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsResolved ||
            !_tenantContext.CenterId.HasValue ||
            _tenantContext.CenterId.Value == Guid.Empty)
        {
            return CenterDashboardResult.NotFound();
        }

        var centerId = _tenantContext.CenterId.Value;

        if (subjectId.HasValue && subjectId.Value == Guid.Empty)
        {
            return CenterDashboardResult.ValidationFailed();
        }

        if (riskThreshold < 0m || riskThreshold > 100m)
        {
            return CenterDashboardResult.ValidationFailed();
        }

        var centerExists = await _dbContext.Centers.AsNoTracking()
            .AnyAsync(c => c.CenterId == centerId, cancellationToken);
        if (!centerExists)
        {
            return CenterDashboardResult.NotFound();
        }

        if (subjectId.HasValue)
        {
            var subjectExists = await _dbContext.Subjects.AsNoTracking()
                .AnyAsync(s => s.CenterId == centerId && s.SubjectId == subjectId.Value && !s.IsDeleted, cancellationToken);
            if (!subjectExists)
            {
                return CenterDashboardResult.NotFound();
            }
        }

        // 1. Summary counts
        var teacherCount = await _dbContext.Teachers.AsNoTracking()
            .Where(t => t.CenterId == centerId && !t.IsDeleted)
            .Where(t => t.User == null || (!t.User.IsDeleted && t.User.Status == UserStatus.Active))
            .CountAsync(cancellationToken);

        var activeStudentsQuery = _dbContext.Students.AsNoTracking()
            .Where(s => s.CenterId == centerId && !s.IsDeleted)
            .Where(s => s.User == null || (!s.User.IsDeleted && s.User.Status == UserStatus.Active));

        var studentCount = await activeStudentsQuery.CountAsync(cancellationToken);

        var classCount = await _dbContext.Classes.AsNoTracking()
            .CountAsync(c => c.CenterId == centerId && c.Status == ClassStatus.Active && !c.IsDeleted, cancellationToken);

        // 2. Mastery by Subject
        var subjectsQuery = _dbContext.Subjects.AsNoTracking()
            .Where(s => s.CenterId == centerId && !s.IsDeleted);

        if (subjectId.HasValue)
        {
            subjectsQuery = subjectsQuery.Where(s => s.SubjectId == subjectId.Value);
        }

        var subjects = await subjectsQuery
            .Select(s => new { s.SubjectId, s.SubjectName })
            .ToListAsync(cancellationToken);

        var masteryBySubject = new List<SubjectMasterySummaryDto>();

        foreach (var subject in subjects)
        {
            var publishedCurriculumExists = await _dbContext.Curriculums.AsNoTracking()
                .AnyAsync(c => c.CenterId == centerId &&
                               c.SubjectId == subject.SubjectId &&
                               c.ReviewStatus == ReviewStatus.Published &&
                               !c.IsDeleted,
                    cancellationToken);

            var activeTopicsQuery = _dbContext.KnowledgeNodes.AsNoTracking()
                .Where(n => n.CenterId == centerId &&
                            n.SubjectId == subject.SubjectId &&
                            n.NodeType == NodeType.Topic &&
                            n.IsActive &&
                            !n.IsDeleted);

            if (publishedCurriculumExists)
            {
                activeTopicsQuery = activeTopicsQuery.Where(n =>
                    _dbContext.CurriculumNodes.Any(cn =>
                        cn.CenterId == centerId &&
                        cn.NodeId == n.NodeId &&
                        cn.Curriculum != null &&
                        cn.Curriculum.ReviewStatus == ReviewStatus.Published &&
                        !cn.Curriculum.IsDeleted));
            }

            var activeTopics = await activeTopicsQuery
                .Select(n => new { n.NodeId, n.ExamImportance })
                .ToListAsync(cancellationToken);

            var totalWeight = activeTopics.Sum(t => t.ExamImportance);

            if (studentCount == 0 || activeTopics.Count == 0 || totalWeight <= 0m)
            {
                masteryBySubject.Add(new SubjectMasterySummaryDto
                {
                    SubjectId = subject.SubjectId,
                    SubjectName = subject.SubjectName,
                    AverageMastery = 0m
                });
                continue;
            }

            var activeTopicNodeIdSet = activeTopics.Select(t => t.NodeId).ToHashSet();

            var allSubjectTwins = await _dbContext.KnowledgeTwins.AsNoTracking()
                .Where(kt => kt.CenterId == centerId &&
                             kt.SubjectId == subject.SubjectId &&
                             !kt.IsDeleted &&
                             activeStudentsQuery.Any(s => s.StudentId == kt.StudentId))
                .Select(kt => new { kt.TopicNodeId, kt.MasteryPercentage })
                .ToListAsync(cancellationToken);

            var twins = allSubjectTwins.Where(kt => activeTopicNodeIdSet.Contains(kt.TopicNodeId)).ToList();

            // Zero-fill denominator: Every active student x active topic is in denominator
            var weightedSum = activeTopics.Sum(t =>
            {
                var sumTopicMastery = twins.Where(kt => kt.TopicNodeId == t.NodeId).Sum(kt => kt.MasteryPercentage);
                var avgTopicMastery = sumTopicMastery / studentCount;
                return avgTopicMastery * t.ExamImportance;
            });

            var avgSubjectMastery = Math.Round(weightedSum / totalWeight, 2, MidpointRounding.AwayFromZero);

            masteryBySubject.Add(new SubjectMasterySummaryDto
            {
                SubjectId = subject.SubjectId,
                SubjectName = subject.SubjectName,
                AverageMastery = avgSubjectMastery
            });
        }

        // 3. High Risk by Class
        var classesQuery = _dbContext.Classes.AsNoTracking()
            .Where(c => c.CenterId == centerId && c.Status == ClassStatus.Active && !c.IsDeleted);

        if (subjectId.HasValue)
        {
            classesQuery = classesQuery.Where(c => c.SubjectId == subjectId.Value);
        }

        var activeClasses = await classesQuery
            .Select(c => new
            {
                c.ClassId,
                c.ClassName,
                c.SubjectId,
                SubjectName = c.Subject.SubjectName,
                EnrolledStudentCount = c.ClassStudents.Count(cs => cs.Status == ClassStudentStatus.Active)
            })
            .ToListAsync(cancellationToken);

        var highRiskByClass = new List<ClassHighRiskSummaryDto>();
        var classRanking = new List<ClassRankingItemDto>();

        foreach (var cls in activeClasses)
        {
            var enrolledCount = cls.EnrolledStudentCount;
            var highRiskCount = 0;

            if (enrolledCount > 0)
            {
                highRiskCount = await _dbContext.StudentSubjectGoals.AsNoTracking()
                    .Where(g => g.CenterId == centerId &&
                                g.SubjectId == cls.SubjectId &&
                                g.RiskScore >= riskThreshold &&
                                !g.IsDeleted &&
                                _dbContext.ClassStudents.Any(cs => cs.CenterId == centerId && cs.ClassId == cls.ClassId && cs.StudentId == g.StudentId && cs.Status == ClassStudentStatus.Active))
                    .CountAsync(cancellationToken);
            }

            highRiskByClass.Add(new ClassHighRiskSummaryDto
            {
                ClassId = cls.ClassId,
                ClassName = cls.ClassName,
                HighRiskStudentCount = highRiskCount,
                TotalStudentCount = enrolledCount
            });

            // Class ranking calculation
            decimal classAverageMastery = 0m;
            if (enrolledCount > 0)
            {
                // Find applicable topics from assigned curriculum or fallback to subject active topics
                var anyClassCurriculumQuery = _dbContext.CurriculumClasses.AsNoTracking()
                    .Where(cc => cc.CenterId == centerId && cc.ClassId == cls.ClassId);

                var classCurriculumQuery = anyClassCurriculumQuery
                    .Where(cc => cc.Curriculum != null &&
                                 !cc.Curriculum.IsDeleted &&
                                 cc.Curriculum.ReviewStatus == ReviewStatus.Published);

                var hasAnyCurriculumAssigned = await anyClassCurriculumQuery.AnyAsync(cancellationToken);
                var hasCurriculumAssigned = await classCurriculumQuery.AnyAsync(cancellationToken);

                var applicableTopicsQuery = _dbContext.KnowledgeNodes.AsNoTracking()
                    .Where(n => n.CenterId == centerId &&
                                n.SubjectId == cls.SubjectId &&
                                n.NodeType == NodeType.Topic &&
                                n.IsActive &&
                                !n.IsDeleted);

                if (hasAnyCurriculumAssigned)
                {
                    applicableTopicsQuery = applicableTopicsQuery.Where(n =>
                        hasCurriculumAssigned &&
                        _dbContext.CurriculumNodes.AsNoTracking().Any(cn =>
                            cn.CenterId == centerId &&
                            cn.NodeId == n.NodeId &&
                            classCurriculumQuery.Any(cc => cc.CurriculumId == cn.CurriculumId)));
                }

                var classTopics = await applicableTopicsQuery
                    .Select(n => new { n.NodeId, n.ExamImportance })
                    .ToListAsync(cancellationToken);

                var classTotalWeight = classTopics.Sum(t => t.ExamImportance);
                if (classTopics.Count > 0 && classTotalWeight > 0m)
                {
                    var classTopicNodeIdSet = classTopics.Select(t => t.NodeId).ToHashSet();

                    var allClassesTwins = await _dbContext.KnowledgeTwins.AsNoTracking()
                        .Where(kt => kt.CenterId == centerId &&
                                     kt.SubjectId == cls.SubjectId &&
                                     !kt.IsDeleted &&
                                     _dbContext.ClassStudents.Any(cs => cs.CenterId == centerId && cs.ClassId == cls.ClassId && cs.StudentId == kt.StudentId && cs.Status == ClassStudentStatus.Active))
                        .Select(kt => new { kt.TopicNodeId, kt.MasteryPercentage })
                        .ToListAsync(cancellationToken);

                    var classTwins = allClassesTwins.Where(kt => classTopicNodeIdSet.Contains(kt.TopicNodeId)).ToList();

                    var classWeightedSum = classTopics.Sum(t =>
                    {
                        var sumTopicMastery = classTwins.Where(kt => kt.TopicNodeId == t.NodeId).Sum(kt => kt.MasteryPercentage);
                        var avgTopicMastery = sumTopicMastery / enrolledCount;
                        return avgTopicMastery * t.ExamImportance;
                    });

                    classAverageMastery = Math.Round(classWeightedSum / classTotalWeight, 2, MidpointRounding.AwayFromZero);
                }
            }

            // Assignment completion rate
            decimal assignmentCompletionRate = 0m;
            var classPublishedAssignmentsQuery = _dbContext.Assignments.AsNoTracking()
                .Where(a => a.CenterId == centerId && a.ClassId == cls.ClassId && a.Status == AssignmentStatus.Published && !a.IsDeleted);

            var hasPublishedAssignments = await classPublishedAssignmentsQuery.AnyAsync(cancellationToken);

            if (hasPublishedAssignments)
            {
                var totalTargets = await _dbContext.AssignmentTargets.AsNoTracking()
                    .Where(t => t.CenterId == centerId && classPublishedAssignmentsQuery.Any(a => a.AssignmentId == t.AssignmentId))
                    .CountAsync(cancellationToken);

                var completedTargets = await _dbContext.StudentAssignmentProgresses.AsNoTracking()
                    .Where(p => p.CenterId == centerId &&
                                p.Status == ProgressStatus.Completed &&
                                !p.IsDeleted &&
                                classPublishedAssignmentsQuery.Any(a => a.AssignmentId == p.AssignmentId))
                    .CountAsync(cancellationToken);

                assignmentCompletionRate = totalTargets > 0
                    ? Math.Round((decimal)completedTargets / totalTargets * 100m, 2, MidpointRounding.AwayFromZero)
                    : 0m;
            }

            classRanking.Add(new ClassRankingItemDto
            {
                Rank = 0, // Assigned below
                ClassId = cls.ClassId,
                ClassName = cls.ClassName,
                SubjectName = cls.SubjectName,
                AverageMastery = classAverageMastery,
                AssignmentCompletionRate = assignmentCompletionRate
            });
        }

        // Rank ordering: averageMastery desc, assignmentCompletionRate desc
        classRanking = classRanking
            .OrderByDescending(c => c.AverageMastery)
            .ThenByDescending(c => c.AssignmentCompletionRate)
            .Select((item, index) =>
            {
                item.Rank = index + 1;
                return item;
            })
            .ToList();

        var resultDto = new CenterDashboardDataDto
        {
            Summary = new CenterDashboardSummaryDto
            {
                TeacherCount = teacherCount,
                StudentCount = studentCount,
                ClassCount = classCount
            },
            MasteryBySubject = masteryBySubject,
            HighRiskByClass = highRiskByClass,
            ClassRanking = classRanking,
            GeneratedAt = _timeProvider.GetUtcNow().UtcDateTime
        };

        return CenterDashboardResult.Success(resultDto);
    }
}
