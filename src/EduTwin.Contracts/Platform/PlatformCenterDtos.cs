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
    public Guid? InitialManagerUserId { get; init; }
    public string? InitialManagerUsername { get; init; }
    public string? InitialManagerDisplayName { get; init; }
    public string? InitialManagerUserRowVersion { get; init; }
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
