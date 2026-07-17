using System;
using EduTwin.Contracts.Organization;

namespace EduTwin.BLL.IdentityAndTenancy;

public record CenterResolutionDto(Guid CenterId, string CenterCode, CenterStatus Status);
