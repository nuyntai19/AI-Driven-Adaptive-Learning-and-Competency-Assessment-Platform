using System.Globalization;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.AssessmentAndReasoning;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.DAL.Persistence;
using Microsoft.EntityFrameworkCore;

namespace EduTwin.BLL.AssessmentAndReasoning.AttemptSummaries;

public sealed class ListAttemptsUseCase : IListAttemptsUseCase
{
    private static readonly string[] Rfc3339Formats =
    [
        "yyyy-MM-dd'T'HH:mm:ss'Z'",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'",
        "yyyy-MM-dd'T'HH:mm:sszzz",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz"
    ];

    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;
    private readonly IStudentOwnershipGuard _studentOwnershipGuard;

    public ListAttemptsUseCase(
        EduTwinDbContext dbContext,
        ITenantContext tenantContext,
        IStudentOwnershipGuard studentOwnershipGuard)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
        _studentOwnershipGuard = studentOwnershipGuard;
    }

    public async Task<ListAttemptsResult> ExecuteAsync(
        ListAttemptsQuery query,
        CancellationToken cancellationToken)
    {
        if (!TryGetActor(out var centerId, out var userId, out var role))
        {
            return ListAttemptsResult.NotFound();
        }

        if (!TryParseQuery(query, out var parsed))
        {
            return ListAttemptsResult.ValidationFailed();
        }

        Guid? targetStudentId;
        switch (role)
        {
            case UserRole.Student:
                targetStudentId = parsed.StudentId ?? userId;
                break;
            case UserRole.Teacher when parsed.StudentId is null:
                return ListAttemptsResult.ValidationFailed();
            case UserRole.Teacher:
                targetStudentId = parsed.StudentId;
                break;
            case UserRole.CenterManager:
                targetStudentId = parsed.StudentId;
                break;
            default:
                return ListAttemptsResult.NotFound();
        }

        if (targetStudentId.HasValue)
        {
            var ownership = await _studentOwnershipGuard.CheckStudentAccessAsync(
                targetStudentId.Value,
                cancellationToken);
            var ownershipFailure = MapOwnershipFailure(ownership);
            if (ownershipFailure is not null)
            {
                return ownershipFailure;
            }
        }

        if (parsed.SubjectId.HasValue && !await _dbContext.Subjects
                .AsNoTracking()
                .AnyAsync(subject =>
                    subject.CenterId == centerId &&
                    subject.SubjectId == parsed.SubjectId.Value,
                    cancellationToken))
        {
            return ListAttemptsResult.NotFound();
        }

        if (parsed.QuestionId.HasValue && !await _dbContext.Questions
                .AsNoTracking()
                .AnyAsync(question =>
                    question.CenterId == centerId &&
                    question.QuestionId == parsed.QuestionId.Value,
                    cancellationToken))
        {
            return ListAttemptsResult.NotFound();
        }

        if (parsed.AssignmentId.HasValue && !await _dbContext.Assignments
                .AsNoTracking()
                .AnyAsync(assignment =>
                    assignment.CenterId == centerId &&
                    assignment.AssignmentId == parsed.AssignmentId.Value,
                    cancellationToken))
        {
            return ListAttemptsResult.NotFound();
        }

        var attempts = _dbContext.Attempts
            .AsNoTracking()
            .Where(attempt => attempt.CenterId == centerId);

        if (targetStudentId.HasValue)
        {
            attempts = attempts.Where(attempt => attempt.StudentId == targetStudentId.Value);
        }

        if (parsed.SubjectId.HasValue)
        {
            attempts = attempts.Where(attempt =>
                attempt.Question.SubjectId == parsed.SubjectId.Value);
        }

        if (parsed.QuestionId.HasValue)
        {
            attempts = attempts.Where(attempt => attempt.QuestionId == parsed.QuestionId.Value);
        }

        if (parsed.AssignmentId.HasValue)
        {
            attempts = attempts.Where(attempt => attempt.AssignmentId == parsed.AssignmentId.Value);
        }

        if (parsed.Status.HasValue)
        {
            attempts = attempts.Where(attempt => attempt.Status == parsed.Status.Value);
        }

        if (parsed.From.HasValue)
        {
            attempts = attempts.Where(attempt => attempt.CreatedAt >= parsed.From.Value);
        }

        if (parsed.To.HasValue)
        {
            attempts = attempts.Where(attempt => attempt.CreatedAt <= parsed.To.Value);
        }

        var hasInvalidJobCardinality = await attempts.AnyAsync(
            attempt => _dbContext.AIAnalysisJobs.Count(job =>
                job.CenterId == centerId &&
                job.AttemptId == attempt.AttemptId) != 1,
            cancellationToken);
        if (hasInvalidJobCardinality)
        {
            throw new InvalidOperationException(
                "Every attempt summary must have exactly one analysis job.");
        }

        var totalItems = await attempts.LongCountAsync(cancellationToken);
        var totalPages = totalItems == 0
            ? 0
            : checked((int)(1 + ((totalItems - 1) / parsed.PageSize)));
        var offset = ((long)parsed.Page - 1) * parsed.PageSize;

        if (offset > int.MaxValue || totalItems == 0)
        {
            return ListAttemptsResult.Success(
                [],
                parsed.Page,
                parsed.PageSize,
                totalItems,
                totalPages);
        }

        var pagedAttempts = attempts
            .OrderByDescending(attempt => attempt.CreatedAt)
            .ThenByDescending(attempt => attempt.AttemptId)
            .Skip((int)offset)
            .Take(parsed.PageSize);

        var rows = await (
                from attempt in pagedAttempts
                join job in _dbContext.AIAnalysisJobs.AsNoTracking()
                    on new { attempt.CenterId, attempt.AttemptId }
                    equals new { job.CenterId, job.AttemptId }
                select new AttemptSummaryRow(
                    attempt.AttemptId,
                    attempt.StudentId,
                    attempt.Student.FullName,
                    attempt.Question.SubjectId,
                    attempt.QuestionId,
                    attempt.Question.QuestionText,
                    attempt.AssignmentId,
                    attempt.Status,
                    attempt.IsCorrect,
                    attempt.AwardedScore,
                    attempt.Question.MaxScore,
                    attempt.Skipped,
                    job.AnalysisJobId,
                    job.Status,
                    attempt.CreatedAt,
                    attempt.UpdatedAt))
            .ToListAsync(cancellationToken);

        var data = rows.Select(MapRow).ToList();
        return ListAttemptsResult.Success(
            data,
            parsed.Page,
            parsed.PageSize,
            totalItems,
            totalPages);
    }

    private bool TryGetActor(out Guid centerId, out Guid userId, out UserRole role)
    {
        centerId = _tenantContext.CenterId ?? Guid.Empty;
        userId = _tenantContext.UserId ?? Guid.Empty;
        role = default;

        if (!_tenantContext.IsResolved || centerId == Guid.Empty || userId == Guid.Empty)
        {
            return false;
        }

        role = _tenantContext.Role switch
        {
            nameof(UserRole.Student) => UserRole.Student,
            nameof(UserRole.Teacher) => UserRole.Teacher,
            nameof(UserRole.CenterManager) => UserRole.CenterManager,
            _ => default
        };
        return _tenantContext.Role is nameof(UserRole.Student)
            or nameof(UserRole.Teacher)
            or nameof(UserRole.CenterManager);
    }

    private static ListAttemptsResult? MapOwnershipFailure(OwnershipDecision decision) =>
        decision switch
        {
            OwnershipDecision.Allowed => null,
            OwnershipDecision.Forbidden => ListAttemptsResult.Forbidden(),
            OwnershipDecision.NotFound => ListAttemptsResult.NotFound(),
            _ => ListAttemptsResult.NotFound()
        };

    private static bool TryParseQuery(
        ListAttemptsQuery? query,
        out ParsedListAttemptsQuery parsed)
    {
        parsed = default;
        if (query is null ||
            HasInvalidPresentValue(query.StudentId) ||
            HasInvalidPresentValue(query.SubjectId) ||
            HasInvalidPresentValue(query.QuestionId) ||
            HasInvalidPresentValue(query.AssignmentId) ||
            HasInvalidPresentValue(query.Status) ||
            HasInvalidPresentValue(query.From) ||
            HasInvalidPresentValue(query.To) ||
            HasInvalidPresentValue(query.Page) ||
            HasInvalidPresentValue(query.PageSize))
        {
            return false;
        }

        if (!TryParseGuid(query.StudentId, out var studentId) ||
            !TryParseGuid(query.SubjectId, out var subjectId) ||
            !TryParsePositiveUlong(query.QuestionId, out var questionId) ||
            !TryParseGuid(query.AssignmentId, out var assignmentId) ||
            !TryParseAttemptStatus(query.Status, out var status) ||
            !TryParseTimestamp(query.From, out var from) ||
            !TryParseTimestamp(query.To, out var to) ||
            !TryParsePositiveInt(query.Page, 1, int.MaxValue, 1, out var page) ||
            !TryParsePositiveInt(query.PageSize, 1, 100, 20, out var pageSize) ||
            (from.HasValue && to.HasValue && from.Value > to.Value))
        {
            return false;
        }

        parsed = new ParsedListAttemptsQuery(
            studentId,
            subjectId,
            questionId,
            assignmentId,
            status,
            from,
            to,
            page,
            pageSize);
        return true;
    }

    private static bool HasInvalidPresentValue(string? value) =>
        value is not null && (value.Length == 0 || value == "null");

    private static bool TryParseGuid(string? value, out Guid? parsed)
    {
        parsed = null;
        if (value is null)
        {
            return true;
        }

        if (!Guid.TryParseExact(value, "D", out var guid))
        {
            return false;
        }

        parsed = guid;
        return true;
    }

    private static bool TryParsePositiveUlong(string? value, out ulong? parsed)
    {
        parsed = null;
        if (value is null)
        {
            return true;
        }

        if (!IsAsciiDigits(value) ||
            !ulong.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) ||
            id == 0)
        {
            return false;
        }

        parsed = id;
        return true;
    }

    private static bool TryParseAttemptStatus(string? value, out AttemptStatus? parsed)
    {
        parsed = value switch
        {
            nameof(AttemptStatus.PendingAnalysis) => AttemptStatus.PendingAnalysis,
            nameof(AttemptStatus.Processing) => AttemptStatus.Processing,
            nameof(AttemptStatus.Completed) => AttemptStatus.Completed,
            nameof(AttemptStatus.NeedsTeacherReview) => AttemptStatus.NeedsTeacherReview,
            _ => null
        };
        return value is null || parsed.HasValue;
    }

    private static bool TryParseTimestamp(string? value, out DateTime? parsed)
    {
        parsed = null;
        if (value is null)
        {
            return true;
        }

        if (!DateTimeOffset.TryParseExact(
                value,
                Rfc3339Formats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var timestamp))
        {
            return false;
        }

        parsed = timestamp.UtcDateTime;
        return true;
    }

    private static bool TryParsePositiveInt(
        string? value,
        int minimum,
        int maximum,
        int defaultValue,
        out int parsed)
    {
        parsed = defaultValue;
        return value is null ||
            (IsAsciiDigits(value) &&
             int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out parsed) &&
             parsed >= minimum &&
             parsed <= maximum);
    }

    private static bool IsAsciiDigits(string value) =>
        value.Length > 0 && value.All(character => character is >= '0' and <= '9');

    private static AttemptSummaryDto MapRow(AttemptSummaryRow row)
    {
        var (jobStatus, terminal) = MapJobStatus(row.JobStatus);
        return new AttemptSummaryDto
        {
            AttemptId = row.AttemptId.ToString(CultureInfo.InvariantCulture),
            StudentId = row.StudentId.ToString("D").ToLowerInvariant(),
            StudentName = row.StudentName,
            SubjectId = row.SubjectId.ToString("D").ToLowerInvariant(),
            QuestionId = row.QuestionId.ToString(CultureInfo.InvariantCulture),
            QuestionText = row.QuestionText,
            AssignmentId = row.AssignmentId?.ToString("D").ToLowerInvariant(),
            AttemptStatus = MapAttemptStatus(row.AttemptStatus),
            Grading = new AttemptSummaryGradingDto
            {
                IsCorrect = row.IsCorrect,
                AwardedScore = row.AwardedScore,
                MaxScore = row.MaxScore,
                Skipped = row.Skipped
            },
            AnalysisJobId = row.AnalysisJobId.ToString(CultureInfo.InvariantCulture),
            JobStatus = jobStatus,
            Terminal = terminal,
            PollUrl = $"/api/v1/learning/analysis-jobs/{row.AnalysisJobId.ToString(CultureInfo.InvariantCulture)}",
            CreatedAt = NormalizeUtc(row.CreatedAt),
            UpdatedAt = NormalizeUtc(row.UpdatedAt)
        };
    }

    private static string MapAttemptStatus(AttemptStatus status) =>
        status switch
        {
            AttemptStatus.PendingAnalysis => nameof(AttemptStatus.PendingAnalysis),
            AttemptStatus.Processing => nameof(AttemptStatus.Processing),
            AttemptStatus.Completed => nameof(AttemptStatus.Completed),
            AttemptStatus.NeedsTeacherReview => nameof(AttemptStatus.NeedsTeacherReview),
            _ => throw new InvalidOperationException($"Unknown attempt status: {status}")
        };

    private static (string Status, bool Terminal) MapJobStatus(AIJobStatus status) =>
        status switch
        {
            AIJobStatus.Pending => (nameof(AIJobStatus.Pending), false),
            AIJobStatus.Processing => (nameof(AIJobStatus.Processing), false),
            AIJobStatus.Completed => (nameof(AIJobStatus.Completed), true),
            AIJobStatus.FallbackCompleted => (nameof(AIJobStatus.FallbackCompleted), true),
            AIJobStatus.FailedTerminal => (nameof(AIJobStatus.FailedTerminal), true),
            _ => throw new InvalidOperationException($"Unknown analysis job status: {status}")
        };

    private static DateTime NormalizeUtc(DateTime value) =>
        value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

    private readonly record struct ParsedListAttemptsQuery(
        Guid? StudentId,
        Guid? SubjectId,
        ulong? QuestionId,
        Guid? AssignmentId,
        AttemptStatus? Status,
        DateTime? From,
        DateTime? To,
        int Page,
        int PageSize);

    private sealed record AttemptSummaryRow(
        ulong AttemptId,
        Guid StudentId,
        string StudentName,
        Guid SubjectId,
        ulong QuestionId,
        string QuestionText,
        Guid? AssignmentId,
        AttemptStatus AttemptStatus,
        bool? IsCorrect,
        decimal? AwardedScore,
        decimal MaxScore,
        bool Skipped,
        ulong AnalysisJobId,
        AIJobStatus JobStatus,
        DateTime CreatedAt,
        DateTime UpdatedAt);
}
