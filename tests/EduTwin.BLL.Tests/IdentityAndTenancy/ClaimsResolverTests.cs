using System;
using System.Security.Claims;
using Xunit;
using EduTwin.BLL.IdentityAndTenancy;
using Moq;

namespace EduTwin.BLL.Tests.IdentityAndTenancy;

public class ClaimsResolverTests
{
    private ClaimsPrincipal CreatePrincipal(string? sub, string? centerId, string? role, string? authVersion, bool isAuthenticated = true)
    {
        var identity = new ClaimsIdentity(isAuthenticated ? "TestAuth" : null);
        if (sub != null) identity.AddClaim(new Claim("sub", sub));
        if (centerId != null) identity.AddClaim(new Claim("center_id", centerId));
        if (role != null) identity.AddClaim(new Claim("role", role));
        if (authVersion != null) identity.AddClaim(new Claim("auth_version", authVersion));

        return new ClaimsPrincipal(identity);
    }

    [Fact]
    public void AuthenticatedPrincipal_WithValidClaims_ResolvesCorrectly()
    {
        var centerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var principal = CreatePrincipal(userId.ToString(), centerId.ToString(), "Teacher", "1");

        var mockInitializer = new Mock<ITenantContextInitializer>();
        var resolver = new ClaimsResolver();

        resolver.Resolve(principal, mockInitializer.Object);

        mockInitializer.Verify(i => i.Initialize(centerId, userId, "Teacher", 1), Times.Once);
    }

    [Fact]
    public void MissingCenterId_Fails()
    {
        var principal = CreatePrincipal(Guid.NewGuid().ToString(), null, "Teacher", "1");

        var resolver = new ClaimsResolver();
        var mockInitializer = new Mock<ITenantContextInitializer>();

        Assert.Throws<UnauthorizedAccessException>(() => resolver.Resolve(principal, mockInitializer.Object));
    }

    [Fact]
    public void MalformedSub_Fails()
    {
        var principal = CreatePrincipal("invalid-guid", Guid.NewGuid().ToString(), "Teacher", "1");

        var resolver = new ClaimsResolver();
        var mockInitializer = new Mock<ITenantContextInitializer>();

        Assert.Throws<UnauthorizedAccessException>(() => resolver.Resolve(principal, mockInitializer.Object));
    }

    [Fact]
    public void MissingOrInvalidAuthVersion_Fails()
    {
        var principal1 = CreatePrincipal(Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), "Teacher", null);
        var principal2 = CreatePrincipal(Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), "Teacher", "invalid");

        var resolver = new ClaimsResolver();
        var mockInitializer = new Mock<ITenantContextInitializer>();

        Assert.Throws<UnauthorizedAccessException>(() => resolver.Resolve(principal1, mockInitializer.Object));
        Assert.Throws<UnauthorizedAccessException>(() => resolver.Resolve(principal2, mockInitializer.Object));
    }

    [Fact]
    public void AnonymousPrincipal_DoesNotCreateAuthenticatedContext()
    {
        var centerId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var principal = CreatePrincipal(userId.ToString(), centerId.ToString(), "Teacher", "1", isAuthenticated: false);

        var mockInitializer = new Mock<ITenantContextInitializer>();
        var resolver = new ClaimsResolver();

        resolver.Resolve(principal, mockInitializer.Object);

        mockInitializer.Verify(i => i.Initialize(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void NameIdentifierWithoutSub_Fails()
    {
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim(ClaimTypes.NameIdentifier, Guid.NewGuid().ToString()));
        identity.AddClaim(new Claim("center_id", Guid.NewGuid().ToString()));
        identity.AddClaim(new Claim("role", "Teacher"));
        identity.AddClaim(new Claim("auth_version", "1"));
        var principal = new ClaimsPrincipal(identity);

        var resolver = new ClaimsResolver();
        var mockInitializer = new Mock<ITenantContextInitializer>();

        Assert.Throws<UnauthorizedAccessException>(() => resolver.Resolve(principal, mockInitializer.Object));
    }

    [Fact]
    public void ClaimTypesRoleWithoutExactRole_Fails()
    {
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim("sub", Guid.NewGuid().ToString()));
        identity.AddClaim(new Claim("center_id", Guid.NewGuid().ToString()));
        identity.AddClaim(new Claim(ClaimTypes.Role, "Teacher"));
        identity.AddClaim(new Claim("auth_version", "1"));
        var principal = new ClaimsPrincipal(identity);

        var resolver = new ClaimsResolver();
        var mockInitializer = new Mock<ITenantContextInitializer>();

        Assert.Throws<UnauthorizedAccessException>(() => resolver.Resolve(principal, mockInitializer.Object));
    }

    [Fact]
    public void DuplicateCenterIdClaims_Fail()
    {
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim("sub", Guid.NewGuid().ToString()));
        identity.AddClaim(new Claim("center_id", Guid.NewGuid().ToString()));
        identity.AddClaim(new Claim("center_id", Guid.NewGuid().ToString())); // Duplicate
        identity.AddClaim(new Claim("role", "Teacher"));
        identity.AddClaim(new Claim("auth_version", "1"));
        var principal = new ClaimsPrincipal(identity);

        var resolver = new ClaimsResolver();
        var mockInitializer = new Mock<ITenantContextInitializer>();

        Assert.Throws<UnauthorizedAccessException>(() => resolver.Resolve(principal, mockInitializer.Object));
    }

    [Fact]
    public void DuplicateRoleClaims_Fail()
    {
        var identity = new ClaimsIdentity("TestAuth");
        identity.AddClaim(new Claim("sub", Guid.NewGuid().ToString()));
        identity.AddClaim(new Claim("center_id", Guid.NewGuid().ToString()));
        identity.AddClaim(new Claim("role", "Teacher"));
        identity.AddClaim(new Claim("role", "Student")); // Duplicate
        identity.AddClaim(new Claim("auth_version", "1"));
        var principal = new ClaimsPrincipal(identity);

        var resolver = new ClaimsResolver();
        var mockInitializer = new Mock<ITenantContextInitializer>();

        Assert.Throws<UnauthorizedAccessException>(() => resolver.Resolve(principal, mockInitializer.Object));
    }
}
