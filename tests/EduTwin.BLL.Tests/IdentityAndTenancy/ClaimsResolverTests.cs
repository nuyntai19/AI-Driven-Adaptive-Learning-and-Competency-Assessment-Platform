using System;
using System.Security.Claims;
using System.Collections.Generic;
using Xunit;
using Moq;
using EduTwin.BLL.IdentityAndTenancy;
using EduTwin.Contracts.IdentityAndTenancy;

namespace EduTwin.BLL.Tests.IdentityAndTenancy;

public class ClaimsResolverTests
{
    private readonly ClaimsResolver _resolver;
    private readonly Mock<ITenantContextInitializer> _mockInitializer;
    private readonly Guid _centerId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly string _authVersion = "1";

    public ClaimsResolverTests()
    {
        _resolver = new ClaimsResolver();
        _mockInitializer = new Mock<ITenantContextInitializer>();
    }

    private ClaimsPrincipal CreatePrincipal(string role, bool duplicateRole = false)
    {
        var claims = new List<Claim>
        {
            new Claim("sub", _userId.ToString()),
            new Claim("center_id", _centerId.ToString()),
            new Claim("auth_version", _authVersion)
        };

        claims.Add(new Claim("role", role));
        if (duplicateRole)
        {
            claims.Add(new Claim("role", role));
        }

        var identity = new ClaimsIdentity(claims, "TestAuth");
        return new ClaimsPrincipal(identity);
    }

    [Theory]
    [InlineData("Student")]
    [InlineData("Teacher")]
    [InlineData("CenterManager")]
    public void Resolve_Should_Accept_Valid_Roles(string role)
    {
        var principal = CreatePrincipal(role);
        _resolver.Resolve(principal, _mockInitializer.Object);

        _mockInitializer.Verify(x => x.Initialize(_centerId, _userId, role, int.Parse(_authVersion)), Times.Once);
    }

    [Theory]
    [InlineData("Admin")] // Không tồn tại
    [InlineData("teacher")] // Sai casing
    [InlineData("centermanager")] // Sai casing
    [InlineData("0")] // Numeric enum
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("")] // Rỗng
    [InlineData("   ")] // Whitespace
    public void Resolve_Should_Reject_Invalid_Roles(string invalidRole)
    {
        var principal = CreatePrincipal(invalidRole);

        var exception = Assert.Throws<UnauthorizedAccessException>(() =>
            _resolver.Resolve(principal, _mockInitializer.Object));

        Assert.Equal("Missing or invalid role claim.", exception.Message);

        // TenantContext không được initialize
        _mockInitializer.Verify(x => x.Initialize(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void Resolve_Should_Reject_Duplicate_Role_Claim()
    {
        var principal = CreatePrincipal("Student", duplicateRole: true);

        var exception = Assert.Throws<UnauthorizedAccessException>(() =>
            _resolver.Resolve(principal, _mockInitializer.Object));

        Assert.Equal("Missing or duplicate role claim.", exception.Message);

        _mockInitializer.Verify(x => x.Initialize(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    private ClaimsPrincipal CreateCustomPrincipal(IEnumerable<Claim> claims, bool isAuthenticated = true)
    {
        var identity = new ClaimsIdentity(claims, isAuthenticated ? "TestAuth" : null);
        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public void Resolve_MissingCenterId_ThrowsUnauthorizedAccessException()
    {
        var claims = new[] { new Claim("sub", _userId.ToString()), new Claim("role", "Student"), new Claim("auth_version", _authVersion) };
        var principal = CreateCustomPrincipal(claims);
        var exception = Assert.Throws<UnauthorizedAccessException>(() => _resolver.Resolve(principal, _mockInitializer.Object));
        Assert.Equal("Missing or duplicate center_id claim.", exception.Message);
        _mockInitializer.Verify(x => x.Initialize(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void Resolve_MalformedSub_ThrowsUnauthorizedAccessException()
    {
        var claims = new[] { new Claim("sub", "not-a-guid"), new Claim("center_id", _centerId.ToString()), new Claim("role", "Student"), new Claim("auth_version", _authVersion) };
        var principal = CreateCustomPrincipal(claims);
        var exception = Assert.Throws<UnauthorizedAccessException>(() => _resolver.Resolve(principal, _mockInitializer.Object));
        Assert.Equal("Missing or invalid sub claim.", exception.Message);
        _mockInitializer.Verify(x => x.Initialize(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void Resolve_MissingAuthVersion_ThrowsUnauthorizedAccessException()
    {
        var claims = new[] { new Claim("sub", _userId.ToString()), new Claim("center_id", _centerId.ToString()), new Claim("role", "Student") };
        var principal = CreateCustomPrincipal(claims);
        var exception = Assert.Throws<UnauthorizedAccessException>(() => _resolver.Resolve(principal, _mockInitializer.Object));
        Assert.Equal("Missing or duplicate auth_version claim.", exception.Message);
        _mockInitializer.Verify(x => x.Initialize(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void Resolve_InvalidAuthVersion_ThrowsUnauthorizedAccessException()
    {
        var claims = new[] { new Claim("sub", _userId.ToString()), new Claim("center_id", _centerId.ToString()), new Claim("role", "Student"), new Claim("auth_version", "invalid") };
        var principal = CreateCustomPrincipal(claims);
        var exception = Assert.Throws<UnauthorizedAccessException>(() => _resolver.Resolve(principal, _mockInitializer.Object));
        Assert.Equal("Missing or invalid auth_version claim.", exception.Message);
        _mockInitializer.Verify(x => x.Initialize(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void Resolve_AnonymousPrincipal_DoesNothing()
    {
        var claims = new[] { new Claim("sub", _userId.ToString()), new Claim("center_id", _centerId.ToString()), new Claim("role", "Student"), new Claim("auth_version", _authVersion) };
        var principal = CreateCustomPrincipal(claims, isAuthenticated: false);
        _resolver.Resolve(principal, _mockInitializer.Object);
        _mockInitializer.Verify(x => x.Initialize(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void Resolve_UsesNameIdentifierInsteadOfSub_ThrowsUnauthorizedAccessException()
    {
        var claims = new[] { new Claim(ClaimTypes.NameIdentifier, _userId.ToString()), new Claim("center_id", _centerId.ToString()), new Claim("role", "Student"), new Claim("auth_version", _authVersion) };
        var principal = CreateCustomPrincipal(claims);
        var exception = Assert.Throws<UnauthorizedAccessException>(() => _resolver.Resolve(principal, _mockInitializer.Object));
        Assert.Equal("Missing or duplicate sub claim.", exception.Message);
        _mockInitializer.Verify(x => x.Initialize(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void Resolve_UsesClaimTypesRoleInsteadOfRole_ThrowsUnauthorizedAccessException()
    {
        var claims = new[] { new Claim("sub", _userId.ToString()), new Claim("center_id", _centerId.ToString()), new Claim(ClaimTypes.Role, "Student"), new Claim("auth_version", _authVersion) };
        var principal = CreateCustomPrincipal(claims);
        var exception = Assert.Throws<UnauthorizedAccessException>(() => _resolver.Resolve(principal, _mockInitializer.Object));
        Assert.Equal("Missing or duplicate role claim.", exception.Message);
        _mockInitializer.Verify(x => x.Initialize(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void Resolve_DuplicateCenterId_ThrowsUnauthorizedAccessException()
    {
        var claims = new[] { new Claim("sub", _userId.ToString()), new Claim("center_id", _centerId.ToString()), new Claim("center_id", Guid.NewGuid().ToString()), new Claim("role", "Student"), new Claim("auth_version", _authVersion) };
        var principal = CreateCustomPrincipal(claims);
        var exception = Assert.Throws<UnauthorizedAccessException>(() => _resolver.Resolve(principal, _mockInitializer.Object));
        Assert.Equal("Missing or duplicate center_id claim.", exception.Message);
        _mockInitializer.Verify(x => x.Initialize(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }
}
