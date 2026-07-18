using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.DAL.IdentityAndTenancy;

namespace EduTwin.API.Security;

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtOptions _options;
    private readonly TimeProvider _timeProvider;

    public JwtTokenGenerator(IOptions<JwtOptions> options, TimeProvider timeProvider)
    {
        _options = options.Value;
        _timeProvider = timeProvider;
        _options.Validate();
    }

    public string GenerateToken(User user, Guid centerId)
    {
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.UserId.ToString("D").ToLowerInvariant()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("D").ToLowerInvariant()),
            new Claim(JwtRegisteredClaimNames.Iat, _timeProvider.GetUtcNow().ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new Claim("center_id", centerId.ToString("D").ToLowerInvariant()),
            new Claim("role", user.RoleName.ToString()),
            new Claim("auth_version", user.AuthVersion.ToString())
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: _timeProvider.GetUtcNow().UtcDateTime.AddSeconds(900), // 900 seconds lifetime
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
