using System;
using Xunit;
using EduTwin.BLL.IdentityAndTenancy;
using Microsoft.Extensions.DependencyInjection;

namespace EduTwin.BLL.Tests.IdentityAndTenancy;

public class TenantContextTests
{
    [Fact]
    public void NewScopedContext_StartsUnresolved()
    {
        var context = new TenantContext();
        Assert.False(context.IsResolved);
        Assert.Null(context.CenterId);
        Assert.Null(context.UserId);
        Assert.Null(context.Role);
        Assert.Null(context.AuthVersion);
    }

    [Fact]
    public void ValidIdentity_InitializesSuccessfully()
    {
        var context = new TenantContext();
        var centerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        context.Initialize(centerId, userId, "Teacher", 1);

        Assert.True(context.IsResolved);
        Assert.Equal(centerId, context.CenterId);
        Assert.Equal(userId, context.UserId);
        Assert.Equal("Teacher", context.Role);
        Assert.Equal(1, context.AuthVersion);
    }

    [Fact]
    public void EmptyCenterId_Rejected()
    {
        var context = new TenantContext();
        Assert.Throws<InvalidOperationException>(() => context.Initialize(Guid.Empty, Guid.NewGuid(), "Teacher", 1));
    }

    [Fact]
    public void EmptyUserId_Rejected()
    {
        var context = new TenantContext();
        Assert.Throws<InvalidOperationException>(() => context.Initialize(Guid.NewGuid(), Guid.Empty, "Teacher", 1));
    }

    [Fact]
    public void BlankRole_Rejected()
    {
        var context = new TenantContext();
        Assert.Throws<InvalidOperationException>(() => context.Initialize(Guid.NewGuid(), Guid.NewGuid(), " ", 1));
    }

    [Fact]
    public void InvalidAuthVersion_Rejected()
    {
        var context = new TenantContext();
        Assert.Throws<InvalidOperationException>(() => context.Initialize(Guid.NewGuid(), Guid.NewGuid(), "Teacher", 0));
    }

    [Fact]
    public void SameIdentityInitialization_IsIdempotent()
    {
        var context = new TenantContext();
        var centerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        context.Initialize(centerId, userId, "Teacher", 1);
        context.Initialize(centerId, userId, "Teacher", 1); // Should not throw

        Assert.True(context.IsResolved);
    }

    [Fact]
    public void ReinitializeWithDifferentCenterId_Rejected()
    {
        var context = new TenantContext();
        var centerId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        context.Initialize(centerId, userId, "Teacher", 1);

        Assert.Throws<InvalidOperationException>(() => context.Initialize(Guid.NewGuid(), userId, "Teacher", 1));
    }

    [Fact]
    public void BackgroundExecutionScope_SetsCenterId()
    {
        var context = new TenantContext();
        var backgroundCenterId = Guid.NewGuid();

        using (context.BeginScope(backgroundCenterId))
        {
            Assert.True(context.IsResolved);
            Assert.Equal(backgroundCenterId, context.CenterId);
            Assert.Null(context.UserId); // Background scope only sets CenterId
        }
    }

    [Fact]
    public void BackgroundScope_CannotOverrideAuthenticatedRequest()
    {
        var context = new TenantContext();
        context.Initialize(Guid.NewGuid(), Guid.NewGuid(), "Teacher", 1);

        Assert.Throws<InvalidOperationException>(() => context.BeginScope(Guid.NewGuid()));
    }

    [Fact]
    public void Dispose_RestoresPriorState()
    {
        var context = new TenantContext();
        var backgroundCenterId1 = Guid.NewGuid();
        var backgroundCenterId2 = Guid.NewGuid();

        using (context.BeginScope(backgroundCenterId1))
        {
            Assert.Equal(backgroundCenterId1, context.CenterId);

            using (context.BeginScope(backgroundCenterId2))
            {
                Assert.Equal(backgroundCenterId2, context.CenterId);
            }

            Assert.Equal(backgroundCenterId1, context.CenterId);
        }

        Assert.False(context.IsResolved);
    }

    [Fact]
    public void SequentialJobScopes_DoNotLeakTenant()
    {
        var context = new TenantContext();
        var center1 = Guid.NewGuid();
        var center2 = Guid.NewGuid();

        using (context.BeginScope(center1))
        {
            Assert.Equal(center1, context.CenterId);
        }

        Assert.False(context.IsResolved);

        using (context.BeginScope(center2))
        {
            Assert.Equal(center2, context.CenterId);
        }

        Assert.False(context.IsResolved);
    }

    [Fact]
    public void TwoSeparateDIScopes_DoNotShareTenantState()
    {
        var services = new ServiceCollection();
        services.AddIdentityAndTenancy();

        var serviceProvider = services.BuildServiceProvider();

        var centerId1 = Guid.NewGuid();
        var userId1 = Guid.NewGuid();

        using (var scope1 = serviceProvider.CreateScope())
        {
            var context1 = scope1.ServiceProvider.GetRequiredService<TenantContext>();
            context1.Initialize(centerId1, userId1, "Teacher", 1);
            Assert.Equal(centerId1, context1.CenterId);
        }

        using (var scope2 = serviceProvider.CreateScope())
        {
            var context2 = scope2.ServiceProvider.GetRequiredService<TenantContext>();
            Assert.False(context2.IsResolved);
            Assert.Null(context2.CenterId);
        }
    }

    [Fact]
    public void TenantInterfaces_InSameScopeShareOneInstance()
    {
        var services = new ServiceCollection();
        services.AddIdentityAndTenancy();
        var serviceProvider = services.BuildServiceProvider();

        using var scope = serviceProvider.CreateScope();
        var sp = scope.ServiceProvider;

        var ctx = sp.GetRequiredService<ITenantContext>();
        var initializer = sp.GetRequiredService<ITenantContextInitializer>();
        var backgroundFactory = sp.GetRequiredService<IBackgroundTenantScopeFactory>();
        var concrete = sp.GetRequiredService<TenantContext>();

        Assert.Same(ctx, initializer);
        Assert.Same(initializer, backgroundFactory);
        Assert.Same(backgroundFactory, concrete);
    }

    [Fact]
    public void DisposingScopeTwice_DoesNotPopParentState()
    {
        var context = new TenantContext();
        var scope1 = context.BeginScope(Guid.NewGuid());
        var scope2 = context.BeginScope(Guid.NewGuid());

        scope2.Dispose();
        scope2.Dispose(); // Second time should be no-op

        Assert.True(context.IsResolved); // scope1 is still active

        scope1.Dispose();
        Assert.False(context.IsResolved);
    }

    [Fact]
    public void DisposingNestedScopesOutOfOrder_IsRejectedWithoutStateCorruption()
    {
        var context = new TenantContext();
        var scope1CenterId = Guid.NewGuid();
        var scope1 = context.BeginScope(scope1CenterId);
        var scope2CenterId = Guid.NewGuid();
        var scope2 = context.BeginScope(scope2CenterId);

        // Disposing scope1 while scope2 is active (top of stack)
        Assert.Throws<InvalidOperationException>(() => scope1.Dispose());

        // Stack should not be corrupted
        Assert.Equal(scope2CenterId, context.CenterId);

        scope2.Dispose(); // Now it's valid
        Assert.Equal(scope1CenterId, context.CenterId);

        scope1.Dispose();
        Assert.False(context.IsResolved);
    }

    [Fact]
    public void NestedBackgroundScopes_RestoreInLifoOrder()
    {
        var context = new TenantContext();
        var center1 = Guid.NewGuid();
        var center2 = Guid.NewGuid();
        var center3 = Guid.NewGuid();

        using (context.BeginScope(center1))
        {
            Assert.Equal(center1, context.CenterId);
            using (context.BeginScope(center2))
            {
                Assert.Equal(center2, context.CenterId);
                using (context.BeginScope(center3))
                {
                    Assert.Equal(center3, context.CenterId);
                }
                Assert.Equal(center2, context.CenterId);
            }
            Assert.Equal(center1, context.CenterId);
        }
        Assert.False(context.IsResolved);
    }
}
