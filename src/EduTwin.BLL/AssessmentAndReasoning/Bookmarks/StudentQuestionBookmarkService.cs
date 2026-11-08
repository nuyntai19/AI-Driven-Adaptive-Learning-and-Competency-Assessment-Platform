using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EduTwin.Contracts.Assignments;
using EduTwin.DAL.AssessmentAndReasoning;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.AssessmentAndReasoning.Bookmarks;

public sealed record StudentQuestionBookmarkDto(
    string QuestionId, Guid SubjectId, bool Answered, string? QuestionText, string? TopicName, string? CorrectAnswer, string? Solution, DateTime CreatedAt);

/// <summary>Hard questions a student flagged. Content is only returned once the student has answered the question.</summary>
public sealed class StudentQuestionBookmarkService(EduTwinDbContext db, TimeProvider clock)
{
    public async Task<IReadOnlyList<StudentQuestionBookmarkDto>> ListAsync(Guid centerId, Guid studentId, CancellationToken ct)
    {
        var bookmarks = await db.StudentQuestionBookmarks.AsNoTracking()
            .Where(b => b.CenterId == centerId && b.StudentId == studentId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(ct);
        if (bookmarks.Count == 0) return [];

        var questionIds = bookmarks.Select(b => b.QuestionId).ToList();
        var questions = await db.Questions.AsNoTracking()
            .Where(q => q.CenterId == centerId && questionIds.Contains(q.QuestionId) && !q.IsDeleted)
            .Select(q => new { q.QuestionId, q.SubjectId, q.QuestionText, q.CorrectAnswer, q.Solution, TopicName = q.PrimaryTopicNode!.NodeName })
            .ToDictionaryAsync(q => q.QuestionId, ct);
        var answered = (await db.Attempts.AsNoTracking()
            .Where(a => a.CenterId == centerId && a.StudentId == studentId && !a.Skipped && questionIds.Contains(a.QuestionId))
            .Select(a => a.QuestionId).Distinct().ToListAsync(ct)).ToHashSet();

        // An assignment still running must not leak its answers through this list.
        var now = clock.GetUtcNow().UtcDateTime;
        var stillOpen = (await db.AssignmentQuestions.AsNoTracking()
            .Where(q => q.CenterId == centerId && questionIds.Contains(q.QuestionId) && q.Assignment != null && !q.Assignment.IsDeleted &&
                (q.Assignment.Status == AssignmentStatus.Draft || q.Assignment.Status == AssignmentStatus.Published && (!q.Assignment.DueAt.HasValue || q.Assignment.DueAt > now)))
            .Select(q => q.QuestionId).Distinct().ToListAsync(ct)).ToHashSet();

        return bookmarks.Where(b => questions.ContainsKey(b.QuestionId)).Select(b =>
        {
            var question = questions[b.QuestionId];
            var isAnswered = answered.Contains(b.QuestionId);
            var reveal = isAnswered && !stillOpen.Contains(b.QuestionId);
            return new StudentQuestionBookmarkDto(
                b.QuestionId.ToString(CultureInfo.InvariantCulture), question.SubjectId, isAnswered,
                isAnswered ? question.QuestionText : null, isAnswered ? question.TopicName : null,
                reveal ? question.CorrectAnswer : null, reveal ? question.Solution : null, b.CreatedAt);
        }).ToList();
    }

    /// <returns>False when the question does not exist in this center.</returns>
    public async Task<bool> AddAsync(Guid centerId, Guid studentId, ulong questionId, CancellationToken ct)
    {
        if (!await db.Questions.AsNoTracking().AnyAsync(q => q.CenterId == centerId && q.QuestionId == questionId && !q.IsDeleted, ct)) return false;
        if (await db.StudentQuestionBookmarks.AsNoTracking().AnyAsync(b => b.CenterId == centerId && b.StudentId == studentId && b.QuestionId == questionId, ct)) return true;

        db.StudentQuestionBookmarks.Add(new StudentQuestionBookmark
        {
            CenterId = centerId, StudentId = studentId, QuestionId = questionId,
            CreatedAt = clock.GetUtcNow().UtcDateTime, CreatedBy = studentId
        });
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException)
        {
            // A double click already saved it.
            db.ChangeTracker.Clear();
        }

        return true;
    }

    public async Task RemoveAsync(Guid centerId, Guid studentId, ulong questionId, CancellationToken ct)
    {
        var bookmark = await db.StudentQuestionBookmarks
            .SingleOrDefaultAsync(b => b.CenterId == centerId && b.StudentId == studentId && b.QuestionId == questionId, ct);
        if (bookmark == null) return;
        db.StudentQuestionBookmarks.Remove(bookmark);
        await db.SaveChangesAsync(ct);
    }
}
