using System;
using System.Security.Cryptography;
using System.Text;

namespace EduTwin.BLL.IdentityAndTenancy;

public interface IRefreshTokenCodec
{
    string GenerateRawToken();
    string HashToken(string rawToken);
    bool IsValidRawToken(string? rawToken);
}

public class RefreshTokenCodec : IRefreshTokenCodec
{
    public string GenerateRawToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(64);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    public string HashToken(string rawToken)
    {
        var bytes = Encoding.UTF8.GetBytes(rawToken);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public bool IsValidRawToken(string? rawToken)
    {
        if (string.IsNullOrEmpty(rawToken) || rawToken.Length != 86)
        {
            return false;
        }

        foreach (var c in rawToken)
        {
            if (!(c >= 'a' && c <= 'z') &&
                !(c >= 'A' && c <= 'Z') &&
                !(c >= '0' && c <= '9') &&
                c != '-' && c != '_')
            {
                return false;
            }
        }

        return true;
    }
}
