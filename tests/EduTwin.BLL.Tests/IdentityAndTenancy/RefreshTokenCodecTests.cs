using System;
using System.Linq;
using System.Text;
using System.Security.Cryptography;
using Xunit;
using EduTwin.BLL.IdentityAndTenancy;

namespace EduTwin.BLL.Tests.IdentityAndTenancy;

public class RefreshTokenCodecTests
{
    private readonly RefreshTokenCodec _sut = new();

    [Fact]
    public void GenerateRawToken_ReturnsUrlSafeBase64String()
    {
        var rawToken = _sut.GenerateRawToken();

        Assert.NotNull(rawToken);
        Assert.DoesNotContain("+", rawToken);
        Assert.DoesNotContain("/", rawToken);
        Assert.DoesNotContain("=", rawToken);
    }

    [Fact]
    public void HashToken_ReturnsLowercaseSha256Hex()
    {
        var rawToken = "my-test-token-123";
        var hash = _sut.HashToken(rawToken);

        var rawBytes = Encoding.UTF8.GetBytes(rawToken);
        var expectedHash = Convert.ToHexString(SHA256.HashData(rawBytes)).ToLowerInvariant();

        Assert.Equal(expectedHash, hash);
        Assert.Equal(64, hash.Length);
        Assert.Equal(hash, hash.ToLowerInvariant());
        Assert.True(hash.All(c => "0123456789abcdef".Contains(c)));
    }

    [Fact]
    public void IsValidRawToken_GeneratedToken_IsValid()
    {
        var rawToken = _sut.GenerateRawToken();
        Assert.Equal(86, rawToken.Length);
        Assert.True(_sut.IsValidRawToken(rawToken));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("1234567890123456789012345678901234567890123456789012345678901234567890123456789012345")] // 85 chars
    [InlineData("123456789012345678901234567890123456789012345678901234567890123456789012345678901234567")] // 87 chars
    public void IsValidRawToken_InvalidLength_ReturnsFalse(string? token)
    {
        Assert.False(_sut.IsValidRawToken(token));
    }

    [Theory]
    [InlineData("1234567890123456789012345678901234567890123456789012345678901234567890123456789012345 ")] // Space
    [InlineData("1234567890123456789012345678901234567890123456789012345678901234567890123456789012345=")] // =
    [InlineData("1234567890123456789012345678901234567890123456789012345678901234567890123456789012345+")] // +
    [InlineData("1234567890123456789012345678901234567890123456789012345678901234567890123456789012345/")] // /
    [InlineData("1234567890123456789012345678901234567890123456789012345678901234567890123456789012345\n")] // newline
    [InlineData("1234567890123456789012345678901234567890123456789012345678901234567890123456789012345é")] // unicode
    public void IsValidRawToken_InvalidCharacters_ReturnsFalse(string token)
    {
        Assert.False(_sut.IsValidRawToken(token));
    }
}
