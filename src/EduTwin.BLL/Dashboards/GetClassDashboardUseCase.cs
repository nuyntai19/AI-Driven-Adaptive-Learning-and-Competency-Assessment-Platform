using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Assignments;
using EduTwin.Contracts.Dashboards;
using EduTwin.Contracts.Organization;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.Dashboards;

public sealed class GetClassDashboardUseCase : IGetClassDashboardUseCase
{
    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IClassOwnershipGuard _classOwnershipGuard;
    private readonly TimeProvider _timeProvider;

    public GetClassDashboardUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        IClassOwnershipGuard classOwnershipGuard,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
        _classOwnershipGuard = classOwnershipGuard ?? throw new ArgumentNullException(nameof(classOwnershipGuard));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<ClassDashboardResult> ExecuteAsync(
        Guid classId,
        decimal riskThreshold,
        CancellationToken cancellationToken)
    {
        if (classId == Guid.Empty || riskThreshold < 0m || riskThreshold > 100m)
        {
            return ClassDashboardResult.ValidationFailed();
        }

        if (!_tenantContext.IsResolved ||
            !_tenantContext.CenterId.HasValue ||
            _tenantContext.CenterId.Value == Guid.Empty)
        {
            return ClassDashboardResult.NotFound();
        }

        var centerId = _tenantContext.CenterId.Value;

        // Verify access via ClassOwnershipGuard (Teacher ownership vs CenterManager)
        var accessDecision = await _classOwnershipGuard.CheckClassAccessAsync(classId, cancellationToken);
        if (accessDecision == OwnershipDecision.NotFound)
        {
            return ClassDashboardResult.NotFound();
        }
        if (accessDecision == OwnershipDecision.Forbidden)
        {
            return ClassDashboardResult.Forbidden();
        }

        var classEntity = await _dbContext.Classes.AsNoTracking()
            .Where(c => c.CenterId == centerId && c.ClassId == classId && c.Status == ClassStatus.Active && !c.IsDeleted)
            .Select(c => new
            {
                c.ClassId,
                c.ClassName,
                c.SubjectId,
                SubjectName = c.Subject.SubjectName,
                EnrolledStudents = c.ClassStudents
                    .Where(cs => cs.Status == ClassStudentStatus.Active && !cs.Student.IsDeleted)
                    .Select(cs => new
                    {
                        cs.Student.StudentId,
                        cs.Student.FullName
                    })
                    .ToList()
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (classEntity == null)
        {
            return ClassDashboardResult.NotFound();
        }

        var enrolledStudents = classEntity.EnrolledStudents;
        var enrolledStudentIds = enrolledStudents.Select(s => s.StudentId).ToList();
        var studentCount = enrolledStudents.Count;

        // 1. High Risk Students
        var highRiskStudents = new List<ClassHighRiskStudentDto>();
        decimal averagePredictedScore = 0m;

        if (studentCount > 0)
        {
            var studentGoals = await _dbContext.StudentSubjectGoals.AsNoTracking()
                .Where(g => g.CenterId == centerId &&
                            g.SubjectId == classEntity.SubjectId &&
                            !g.IsDeleted &&
                            _dbContext.ClassStudents.Any(cs => cs.CenterId == centerId && cs.ClassId == classId && cs.StudentId == g.StudentId && cs.Status == ClassStudentStatus.Active))
                .ToListAsync(cancellationToken);

            var studentGoalMap = studentGoals.ToDictionary(g => g.StudentId);

            foreach (var student in enrolledStudents)
            {
                if (studentGoalMap.TryGetValue(student.StudentId, out var goal))
                {
                    if (goal.RiskScore >= riskThreshold)
                    {
                        highRiskStudents.Add(new ClassHighRiskStudentDto
                        {
                            StudentId = student.StudentId,
                            FullName = student.FullName,
                            TargetScore = goal.TargetScore,
                            PredictedScore = goal.CurrentPredictedScore,
                            RemainingDays = goal.RemainingDays,
                            RiskScore = goal.RiskScore
                        });
                    }
                }
            }

            // Average predicted score across all enrolled students (missing goal = 0)
            var totalPredicted = enrolledStudents.Sum(s => studentGoalMap.TryGetValue(s.StudentId, out var g) ? g.CurrentPredictedScore : 0m);
            averagePredictedScore = Math.Round(totalPredicted / studentCount, 2, MidpointRounding.AwayFromZero);
        }

        highRiskStudents = highRiskStudents.OrderByDescending(s => s.RiskScore).ToList();

        // 2. Applicable Topics (curriculum assigned to class or subject active topics)
        var classCurriculumQuery = _dbContext.CurriculumClasses.AsNoTracking()
            .Where(cc => cc.CenterId == centerId && cc.ClassId == classEntity.ClassId);

        var hasCurriculumAssigned = await classCurriculumQuery.AnyAsync(cancellationToken);

        var applicableTopicsQuery = _dbContext.KnowledgeNodes.AsNoTracking()
            .Where(n => n.CenterId == centerId && n.SubjectId == classEntity.SubjectId && n.IsActive && !n.IsDeleted);

        if (hasCurriculumAssigned)
        {
            applicableTopicsQuery = applicableTopicsQuery.Where(n =>
                _dbContext.CurriculumNodes.AsNoTracking().Any(cn =>
                    cn.CenterId == centerId &&
                    cn.NodeId == n.NodeId &&
                    classCurriculumQuery.Any(cc => cc.CurriculumId == cn.CurriculumId)));
        }

        var applicableTopics = await applicableTopicsQuery
            .OrderBy(n => n.OrderIndex)
            .Select(n => new { n.NodeId, n.NodeName, n.ExamImportance })
            .ToListAsync(cancellationToken);

        var weakTopics = new List<ClassWeakTopicDto>();
        var gapGroups = new List<ClassGapGroupDto>();
        decimal averageMastery = 0m;

        if (studentCount > 0 && applicableTopics.Count > 0)
        {
            var applicableTopicNodeIdSet = applicableTopics.Select(t => t.NodeId).ToHashSet();

            var allClassTwins = await _dbContext.KnowledgeTwins.AsNoTracking()
                .Where(kt => kt.CenterId == centerId &&
                             kt.SubjectId == classEntity.SubjectId &&
                             !kt.IsDeleted &&
                             _dbContext.ClassStudents.Any(cs => cs.CenterId == centerId && cs.ClassId == classId && cs.StudentId == kt.StudentId && cs.Status == ClassStudentStatus.Active))
                .Select(kt => new { kt.StudentId, kt.TopicNodeId, kt.MasteryPercentage })
                .ToListAsync(cancellationToken);

            var twins = allClassTwins.Where(kt => applicableTopicNodeIdSet.Contains(kt.TopicNodeId)).ToList();

            var totalWeight = applicableTopics.Sum(t => t.ExamImportance);
            if (totalWeight > 0m)
            {
                var weightedSum = applicableTopics.Sum(t =>
                {
                    var sumTopic = twins.Where(kt => kt.TopicNodeId == t.NodeId).Sum(kt => kt.MasteryPercentage);
                    var avgTopic = sumTopic / studentCount;
                    return avgTopic * t.ExamImportance;
                });
                averageMastery = Math.Round(weightedSum / totalWeight, 2, MidpointRounding.AwayFromZero);
            }

            // Find weak topics (< 60% class average mastery)
            foreach (var topic in applicableTopics)
            {
                var topicTwins = twins.Where(kt => kt.TopicNodeId == topic.NodeId).ToDictionary(kt => kt.StudentId, kt => kt.MasteryPercentage);
                var topicSum = topicTwins.Values.Sum();
                var topicAvg = Math.Round(topicSum / studentCount, 2, MidpointRounding.AwayFromZero);

                // Students with mastery < 60 on this topic (missing twin has mastery 0, so < 60)
                var affectedStudentIds = enrolledStudents
                    .Where(s => !topicTwins.TryGetValue(s.StudentId, out var m) || m < 60m)
                    .Select(s => s.StudentId)
                    .ToList();

                if (topicAvg < 60m)
                {
                    weakTopics.Add(new ClassWeakTopicDto
                    {
                        TopicNodeId = topic.NodeId.ToString(CultureInfo.InvariantCulture),
                        TopicName = topic.NodeName,
                        AverageMastery = topicAvg,
                        AffectedStudentCount = affectedStudentIds.Count
                    });

                    if (affectedStudentIds.Count > 0)
                    {
                        gapGroups.Add(new ClassGapGroupDto
                        {
                            GroupKey = $"topic-{topic.NodeId}-below-60",
                            TopicNodeId = topic.NodeId.ToString(CultureInfo.InvariantCulture),
                            TopicName = topic.NodeName,
                            Threshold = 60m,
                            StudentCount = affectedStudentIds.Count,
                            StudentIds = affectedStudentIds,
                            SuggestedAction = $"Giao bài luyện {topic.NodeName}."
                        });
                    }
                }
            }
        }

        // 3. Assignment completion rate
        decimal assignmentCompletionRate = 0m;
        var publishedAssignmentsQuery = _dbContext.Assignments.AsNoTracking()
            .Where(a => a.CenterId == centerId && a.ClassId == classEntity.ClassId && a.Status == AssignmentStatus.Published && !a.IsDeleted);

        var hasPublishedAssignments = await publishedAssignmentsQuery.AnyAsync(cancellationToken);

        if (hasPublishedAssignments)
        {
            var totalTargets = await _dbContext.AssignmentTargets.AsNoTracking()
                .Where(t => t.CenterId == centerId && publishedAssignmentsQuery.Any(a => a.AssignmentId == t.AssignmentId))
                .CountAsync(cancellationToken);

            var completedTargets = await _dbContext.StudentAssignmentProgresses.AsNoTracking()
                .Where(p => p.CenterId == centerId &&
                            p.Status == ProgressStatus.Completed &&
                            !p.IsDeleted &&
                            publishedAssignmentsQuery.Any(a => a.AssignmentId == p.AssignmentId))
                .CountAsync(cancellationToken);

            assignmentCompletionRate = totalTargets > 0
                ? Math.Round((decimal)completedTargets / totalTargets * 100m, 2, MidpointRounding.AwayFromZero)
                : 0m;
        }

        var resultDto = new ClassDashboardDataDto
        {
            Class = new ClassBasicInfoDto
            {
                ClassId = classEntity.ClassId,
                ClassName = classEntity.ClassName,
                SubjectId = classEntity.SubjectId,
                SubjectName = classEntity.SubjectName
            },
            Overview = new ClassOverviewDto
            {
                StudentCount = studentCount,
                AveragePredictedScore = averagePredictedScore,
                AverageMastery = averageMastery,
                AssignmentCompletionRate = assignmentCompletionRate
            },
            HighRiskStudents = highRiskStudents,
            WeakTopics = weakTopics,
            GapGroups = gapGroups,
            GeneratedAt = _timeProvider.GetUtcNow().UtcDateTime
        };

        return ClassDashboardResult.Success(resultDto);
    }
}
