using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.Common;
using EduTwin.Contracts.IdentityAndTenancy;
using EduTwin.Contracts.KnowledgeGraph;
using EduTwin.DAL.Persistence;

namespace EduTwin.BLL.KnowledgeGraph;

public interface ISuggestKnowledgeNodeCodeUseCase
{
    Task<SuggestKnowledgeNodeCodeResult> ExecuteAsync(Guid subjectId, string? prefix, CancellationToken cancellationToken);
}

public sealed class SuggestKnowledgeNodeCodeResult
{
    public bool IsSuccess { get; private init; }
    public KnowledgeNodeCodeSuggestionDto? Data { get; private init; }
    public string? ErrorCode { get; private init; }

    public static SuggestKnowledgeNodeCodeResult Success(KnowledgeNodeCodeSuggestionDto data) => new() { IsSuccess = true, Data = data };
    public static SuggestKnowledgeNodeCodeResult Failure(string errorCode) => new() { IsSuccess = false, ErrorCode = errorCode };
}

/// <summary>
/// Suggests the next standard node code "PREFIX_NN" (NN = 01, 02, …) for a subject.
/// The sequence is max(existing suffix) + 1 over every code of the subject in the caller's center,
/// soft-deleted nodes included: creating a node with a deleted node's code restores that node,
/// so a suggestion must never point at one. The unique index (center, subject, code) remains the
/// final guard against concurrent creates.
/// </summary>
public sealed class SuggestKnowledgeNodeCodeUseCase : ISuggestKnowledgeNodeCodeUseCase
{
    /// <summary>Same limit as the authoring form.</summary>
    public const int MaxCodeLength = 50;
    private const int MaxSequenceDigits = 6;
    private static readonly Regex PrefixPattern = new("^[A-Z0-9]+(?:_[A-Z0-9]+)*$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));

    private readonly EduTwinDbContext _dbContext;
    private readonly ITenantContext _tenantContext;

    public SuggestKnowledgeNodeCodeUseCase(EduTwinDbContext dbContext, ITenantContext tenantContext)
    {
        _dbContext = dbContext;
        _tenantContext = tenantContext;
    }

    public async Task<SuggestKnowledgeNodeCodeResult> ExecuteAsync(Guid subjectId, string? prefix, CancellationToken cancellationToken)
    {
        if (!_tenantContext.IsResolved || _tenantContext.CenterId is not { } centerId || centerId == Guid.Empty ||
            _tenantContext.UserId is null || _tenantContext.UserId == Guid.Empty)
            return SuggestKnowledgeNodeCodeResult.Failure(ErrorCodes.ResourceNotFound);

        if (_tenantContext.Role != nameof(UserRole.Teacher) && _tenantContext.Role != nameof(UserRole.CenterManager))
            return SuggestKnowledgeNodeCodeResult.Failure(ErrorCodes.ResourceNotFound);

        var normalizedPrefix = prefix?.Trim();
        if (subjectId == Guid.Empty || string.IsNullOrEmpty(normalizedPrefix) ||
            normalizedPrefix.Length > MaxCodeLength - 3 || !PrefixPattern.IsMatch(normalizedPrefix))
            return SuggestKnowledgeNodeCodeResult.Failure(ErrorCodes.ValidationFailed);

        var subjectExists = await _dbContext.Subjects.AsNoTracking()
            .AnyAsync(s => s.SubjectId == subjectId && s.CenterId == centerId && !s.IsDeleted, cancellationToken);
        if (!subjectExists)
            return SuggestKnowledgeNodeCodeResult.Failure(ErrorCodes.ResourceNotFound);

        var head = normalizedPrefix + "_";
        // LIKE may treat "_" as a wildcard; the ordinal check below keeps only exact "PREFIX_<digits>" codes.
        var codes = await _dbContext.KnowledgeNodes.IgnoreQueryFilters().AsNoTracking()
            .Where(n => n.CenterId == centerId && n.SubjectId == subjectId && n.NodeCode.StartsWith(head))
            .Select(n => n.NodeCode)
            .ToListAsync(cancellationToken);

        var max = 0;
        foreach (var code in codes)
        {
            if (!code.StartsWith(head, StringComparison.OrdinalIgnoreCase)) continue;
            var suffix = code[head.Length..];
            if (suffix.Length == 0 || suffix.Length > MaxSequenceDigits || !suffix.All(char.IsAsciiDigit)) continue;
            max = Math.Max(max, int.Parse(suffix, System.Globalization.CultureInfo.InvariantCulture));
        }

        var sequence = max + 1;
        var suggestion = $"{normalizedPrefix}_{sequence.ToString("00", System.Globalization.CultureInfo.InvariantCulture)}";
        if (suggestion.Length > MaxCodeLength)
            return SuggestKnowledgeNodeCodeResult.Failure(ErrorCodes.ValidationFailed);

        return SuggestKnowledgeNodeCodeResult.Success(new KnowledgeNodeCodeSuggestionDto
        {
            Code = suggestion,
            Prefix = normalizedPrefix,
            Sequence = sequence,
        });
    }
}
