using System;
using EduTwin.DAL.IdentityAndTenancy;

namespace EduTwin.BLL.IdentityAndTenancy;

public interface IJwtTokenGenerator
{
    string GenerateToken(User user, Guid centerId);
}
