using EduTwin.Contracts.CurriculumAndQuestions;
using EduTwin.DAL.Persistence;
using EduTwin.DAL.Seeding;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.Seeding;

/// <summary>
/// Adds a second edition only to the two deterministic Development demo tenants.
/// Original Published versions, curricula, questions and class history are retained.
/// </summary>
public sealed class ExpandedDemoSeedBackfill(EduTwinDbContext db, TimeProvider clock)
{
    public async Task EnsureAsync(CancellationToken ct = default)
    {
        foreach (var centerA in new[] { true, false })
        {
            var seed = new EduTwinSeedFactory(centerA).CreateData();
            var centerId = seed.Center.CenterId;
            if (!await db.Centers.IgnoreQueryFilters().AnyAsync(c => c.CenterId == centerId &&
                c.CenterCode == seed.Center.CenterCode && !c.IsDeleted, ct)) continue;
            var oldVersions = seed.GraphVersions.Where(v => v.GraphVersionId ==
                DeterministicSeedIds.GradeGraphVersionId(centerA, v.SubjectId ==
                    (centerA ? DeterministicSeedIds.CenterAMathSubjectId : DeterministicSeedIds.CenterBMathSubjectId), v.GradeLevel)).ToArray();
            var newVersions = seed.GraphVersions.Except(oldVersions).ToArray();
            var newVersionIds = newVersions.Select(v => v.GraphVersionId).ToHashSet();
            var newCurricula = seed.Curriculums.Where(c => c.GraphVersionId.HasValue &&
                newVersionIds.Contains(c.GraphVersionId.Value)).ToArray();
            var newCurriculumIds = newCurricula.Select(c => c.CurriculumId).ToHashSet();
            var existingVersions = await db.KnowledgeGraphVersions.IgnoreQueryFilters()
                .Where(v => v.CenterId == centerId).Select(v => v.GraphVersionId).ToListAsync(ct);
            var existingCurricula = await db.Curriculums.IgnoreQueryFilters()
                .Where(c => c.CenterId == centerId).Select(c => c.CurriculumId).ToListAsync(ct);
            if (newVersionIds.All(existingVersions.Contains) && newCurriculumIds.All(existingCurricula.Contains)) continue;
            if (newVersionIds.Any(existingVersions.Contains) || newCurriculumIds.Any(existingCurricula.Contains) ||
                oldVersions.Any(v => !existingVersions.Contains(v.GraphVersionId)))
                throw new InvalidOperationException($"Demo content for {seed.Center.CenterCode} is incomplete; inspect it before retrying the additive upgrade.");

            var newNodes = seed.Topics.Where(n => n.NodeId >= (centerA ? 15000UL : 25000UL)).ToArray();
            var newEdges = seed.Edges.Where(e => e.EdgeId >= (centerA ? 15000UL : 25000UL)).ToArray();
            var newQuestions = seed.Questions.Where(q => q.QuestionId >= (centerA ? 15000UL : 25000UL)).ToArray();
            var existingNodeIds = await db.KnowledgeNodes.IgnoreQueryFilters().Where(n => n.CenterId == centerId).Select(n => n.NodeId).ToListAsync(ct);
            var existingEdgeIds = await db.KnowledgeEdges.IgnoreQueryFilters().Where(e => e.CenterId == centerId).Select(e => e.EdgeId).ToListAsync(ct);
            var existingQuestionIds = await db.Questions.IgnoreQueryFilters().Where(q => q.CenterId == centerId).Select(q => q.QuestionId).ToListAsync(ct);
            if (newNodes.Any(n => existingNodeIds.Contains(n.NodeId)) || newEdges.Any(e => existingEdgeIds.Contains(e.EdgeId)) ||
                newQuestions.Any(q => existingQuestionIds.Contains(q.QuestionId)))
                throw new InvalidOperationException($"Reserved demo IDs collide with existing {seed.Center.CenterCode} content; no rows were changed.");

            var now = clock.GetUtcNow().UtcDateTime;
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            db.KnowledgeNodes.AddRange(newNodes);
            db.KnowledgeEdges.AddRange(newEdges);
            await db.SaveChangesAsync(ct);

            foreach (var version in newVersions.OrderBy(v => v.GradeLevel).ThenBy(v => v.GraphVersionId))
            {
                version.Status = "Draft"; version.IsCurrent = false;
                version.CreatedAt = now; version.UpdatedAt = now;
                db.KnowledgeGraphVersions.Add(version);
                await db.SaveChangesAsync(ct);
                db.KnowledgeNodeGrades.AddRange(seed.NodeGrades.Where(m => m.GraphVersionId == version.GraphVersionId));
                await db.SaveChangesAsync(ct);
                db.KnowledgeEdgeGrades.AddRange(seed.EdgeGrades.Where(m => m.GraphVersionId == version.GraphVersionId));
                await db.SaveChangesAsync(ct);
                var previous = await db.KnowledgeGraphVersions.IgnoreQueryFilters().SingleAsync(v => v.CenterId == centerId &&
                    v.GraphVersionId == version.SourceGraphVersionId, ct);
                previous.IsCurrent = false;
                version.Status = "Published"; version.IsCurrent = true;
                await db.SaveChangesAsync(ct);
            }

            foreach (var curriculum in newCurricula) { curriculum.ReviewStatus = ReviewStatus.Draft; curriculum.CreatedAt = now; curriculum.UpdatedAt = now; }
            db.Curriculums.AddRange(newCurricula);
            db.CurriculumClasses.AddRange(seed.CurriculumClasses.Where(c => newCurriculumIds.Contains(c.CurriculumId)));
            db.CurriculumNodes.AddRange(seed.CurriculumNodes.Where(c => newCurriculumIds.Contains(c.CurriculumId)));
            await db.SaveChangesAsync(ct);
            foreach (var curriculum in newCurricula) curriculum.ReviewStatus = ReviewStatus.Published;
            await db.SaveChangesAsync(ct);

            foreach (var application in seed.CurriculumApplications.Where(a => newCurriculumIds.Contains(a.CurriculumId)))
            {
                var active = await db.ClassCurriculumApplications.IgnoreQueryFilters().Where(a => a.CenterId == centerId &&
                    a.ClassId == application.ClassId && a.ApplicationRole == "Primary" && a.EndedAt == null).ToListAsync(ct);
                var oldSeedId = application.ClassId;
                if (active.Any(a => a.ApplicationId != oldSeedId)) continue;
                foreach (var previous in active)
                {
                    previous.EndedAt = now;
                    previous.EndedBy = application.AssignedBy;
                    previous.EndReason = "Đổi sang giáo trình mẫu mở rộng; giữ lịch sử áp dụng cũ.";
                }
                await db.SaveChangesAsync(ct);
                application.StartedAt = now;
                db.ClassCurriculumApplications.Add(application);
                await db.SaveChangesAsync(ct);
            }

            foreach (var question in newQuestions) { question.CreatedAt = now; question.UpdatedAt = now; }
            db.Questions.AddRange(newQuestions);
            db.QuestionOptions.AddRange(seed.QuestionOptions.Where(o => newQuestions.Any(q => q.QuestionId == o.QuestionId)));
            db.QuestionKnowledgeNodes.AddRange(seed.QuestionNodes.Where(m => newQuestions.Any(q => q.QuestionId == m.QuestionId)));
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
        }
    }
}
