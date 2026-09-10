using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.DAL.DigitalTwin;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.DigitalTwin;

public sealed class StudentTwinUpdater : IStudentTwinUpdater
{
    private readonly EduTwinDbContext _dbContext;

    public StudentTwinUpdater(EduTwinDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public async Task<StudentTwin> UpdateAsync(
        Guid centerId,
        Guid studentId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var twin = await _dbContext.StudentTwins
            .SingleOrDefaultAsync(
                st => st.CenterId == centerId
                    && st.StudentId == studentId
                    && !st.IsDeleted,
                cancellationToken);

        if (twin is null)
        {
            twin = new StudentTwin
            {
                TwinId = Guid.NewGuid(),
                CenterId = centerId,
                StudentId = studentId,
                OverallMastery = 0m,
                LastEvidenceAt = null,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            };
            _dbContext.StudentTwins.Add(twin);
        }

        var dbTwins = await _dbContext.KnowledgeTwins
            .Where(k => k.CenterId == centerId
                && k.StudentId == studentId
                && !k.IsDeleted)
            .ToListAsync(cancellationToken);

        var localTwins = _dbContext.KnowledgeTwins.Local
            .Where(k => k.CenterId == centerId
                && k.StudentId == studentId
                && !k.IsDeleted);

        var allTwins = dbTwins
            .UnionBy(localTwins, k => new { k.SubjectId, k.TopicNodeId })
            .ToList();

        if (allTwins.Count > 0)
        {
            var avgMastery = allTwins.Average(k => k.MasteryPercentage);
            twin.OverallMastery = Math.Round(Math.Clamp(avgMastery, 0m, 100m), 2, MidpointRounding.AwayFromZero);
        }
        else
        {
            twin.OverallMastery = 0m;
        }

        twin.LastEvidenceAt = utcNow;
        twin.UpdatedAt = utcNow;

        return twin;
    }
}
