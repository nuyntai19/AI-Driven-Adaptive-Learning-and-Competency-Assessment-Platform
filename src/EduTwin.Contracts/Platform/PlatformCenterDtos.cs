using System;
using System.Collections.Generic;

namespace EduTwin.Contracts.Platform;

public record PlatformCenterListItemDto
{
    public Guid CenterId { get; init; }
    public string CenterCode { get; init; } = string.Empty;
    public string CenterName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string Timezone { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public string RowVersion { get; init; } = string.Empty;
    public Guid? PrimaryManagerUserId { get; init; }
    public string? PrimaryManagerUsername { get; init; }
    public string? PrimaryManagerDisplayName { get; init; }
    public string? PrimaryManagerUserRowVersion { get; init; }
    public Guid? InitialManagerUserId { get; init; }
    public string? InitialManagerUsername { get; init; }
    public string? InitialManagerDisplayName { get; init; }
    public string? InitialManagerUserRowVersion { get; init; }
    public int ActiveStudentCount { get; init; }
    public int ActiveTeacherCount { get; init; }
    public int ClassCount { get; init; }
    public int ActiveManagerCount { get; init; }
    public bool HasActivePrimaryManager { get; init; }
}

public record UpdateCenterMetadataRequest
{
    public string? CenterName { get; init; }
    public string? Timezone { get; init; }
    public string ExpectedRowVersion { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
}

public record CreatePlatformCenterRequest
{
    public string CenterCode { get; init; } = string.Empty;
    public string CenterName { get; init; } = string.Empty;
    public string Timezone { get; init; } = string.Empty;
    public string InitialManagerUsername { get; init; } = string.Empty;
    public string InitialManagerDisplayName { get; init; } = string.Empty;
    public string InitialManagerPassword { get; init; } = string.Empty;
}

public record UpdatePlatformCenterStatusRequest
{
    public string Status { get; init; } = string.Empty;
    public string? RowVersion { get; init; }
    public string? ExpectedRowVersion { get; init; }
    public string? Reason { get; init; }

    public string EffectiveRowVersion => !string.IsNullOrWhiteSpace(RowVersion) ? RowVersion : (ExpectedRowVersion ?? string.Empty);
}

public record ResetCenterManagerPasswordRequest
{
    public string NewPassword { get; init; } = string.Empty;
    public string ExpectedUserRowVersion { get; init; } = string.Empty;
    public string? Reason { get; init; }
}

public record ResetCenterManagerPasswordData
{
    public Guid CenterId { get; init; }
    public Guid ManagerUserId { get; init; }
    public string NewUserRowVersion { get; init; } = string.Empty;
    public DateTime ResetAtUtc { get; init; }
    public bool Success { get; init; } = true;
}

public record PlatformCentersListData
{
    public IReadOnlyList<PlatformCenterListItemDto> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public record PlatformCenterManagerListItemDto
{
    public Guid UserId { get; init; }
    public string Username { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public bool IsPrimary { get; init; }
    public DateTime CreatedAt { get; init; }
    public string RowVersion { get; init; } = string.Empty;
    public uint AuthVersion { get; init; }
}

public record PlatformCenterManagersListData
{
    public IReadOnlyList<PlatformCenterManagerListItemDto> Items { get; init; } = [];
    public int TotalCount { get; init; }
    public Guid? PrimaryManagerUserId { get; init; }
    public int Page { get; init; }
    public int PageSize { get; init; }
}

public record CreateCenterManagerRequest
{
    public string Username { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Password { get; init; } = string.Empty;
    public string ExpectedCenterRowVersion { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
}

public record CreateCenterManagerResponseData
{
    public Guid UserId { get; init; }
    public Guid CenterId { get; init; }
    public string Username { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public bool IsPrimary { get; init; }
    public DateTime CreatedAt { get; init; }
    public string RowVersion { get; init; } = string.Empty;
    public string CenterRowVersion { get; init; } = string.Empty;
}

public record UpdateCenterManagerStatusRequest
{
    public string Status { get; init; } = string.Empty;
    public string ExpectedUserRowVersion { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
}

public record UpdateCenterManagerStatusData
{
    public Guid UserId { get; init; }
    public Guid CenterId { get; init; }
    public string Status { get; init; } = string.Empty;
    public bool IsPrimary { get; init; }
    public string RowVersion { get; init; } = string.Empty;
    public DateTime UpdatedAtUtc { get; init; }
}

public record MakePrimaryCenterManagerRequest
{
    public string ExpectedCenterRowVersion { get; init; } = string.Empty;
    public string ExpectedManagerUserRowVersion { get; init; } = string.Empty;
    public bool DisablePreviousPrimary { get; init; }
    public string? ExpectedPreviousPrimaryUserRowVersion { get; init; }
    public string Reason { get; init; } = string.Empty;
}

public record MakePrimaryCenterManagerData
{
    public Guid CenterId { get; init; }
    public Guid PrimaryManagerUserId { get; init; }
    public string NewCenterRowVersion { get; init; } = string.Empty;
    public bool PreviousPrimaryDisabled { get; init; }
    public DateTime UpdatedAtUtc { get; init; }
}
