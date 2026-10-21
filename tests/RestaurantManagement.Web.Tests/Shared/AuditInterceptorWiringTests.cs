using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Menu.Data;
using RestaurantManagement.Modules.Organization.Data;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Data.AuditLog;
using RestaurantManagement.Shared.Data.Tenancy;
using RestaurantManagement.Web.Auth;
using Shouldly;

namespace RestaurantManagement.Web.Tests;

public class AuditInterceptorWiringTests : IDisposable
{
    private readonly AuthApiFactory _factory = new();

    public void Dispose() => _factory.Dispose();

    private bool HasAuditInterceptor<TContext>() where TContext : DbContext
    {
        using var scope = _factory.Services.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<TContext>();
        var coreOptions = context.GetService<IDbContextOptions>().FindExtension<CoreOptionsExtension>();
        return coreOptions?.Interceptors?.OfType<AuditSaveChangesInterceptor>().Any() ?? false;
    }

    // Scenario 42
    [Fact]
    [Trait("Rule", "SHD12")]
    public void IdentityDbContext_Resolved_HasAuditInterceptorAttached()
    {
        // Arrange / Act
        var hasInterceptor = HasAuditInterceptor<IdentityDbContext>();

        // Assert
        hasInterceptor.ShouldBeTrue();
    }

    // Scenario 43
    [Fact]
    [Trait("Rule", "SHD13")]
    public void MenuDbContext_Resolved_HasAuditInterceptorAttached()
    {
        // Arrange / Act
        var hasInterceptor = HasAuditInterceptor<MenuDbContext>();

        // Assert
        hasInterceptor.ShouldBeTrue();
    }

    // Scenario 44
    [Fact]
    [Trait("Rule", "SHD14")]
    public void OrganizationDbContext_Resolved_HasAuditInterceptorAttached()
    {
        // Arrange / Act
        var hasInterceptor = HasAuditInterceptor<OrganizationDbContext>();

        // Assert
        hasInterceptor.ShouldBeTrue();
    }

    // Scenario 45
    [Fact]
    [Trait("Rule", "SHD15")]
    public void ICurrentUser_Resolved_IsClaimsCurrentUser()
    {
        // Arrange
        using var scope = _factory.Services.CreateScope();

        // Act
        var currentUser = scope.ServiceProvider.GetRequiredService<ICurrentUser>();

        // Assert
        currentUser.ShouldBeOfType<ClaimsCurrentUser>();
    }

    // Scenario 46
    [Fact]
    [Trait("Rule", "SHD16")]
    public void ITenantScope_ResolvedFromTwoRequestScopes_IsTheSameInstance()
    {
        // Arrange
        using var firstScope = _factory.Services.CreateScope();
        using var secondScope = _factory.Services.CreateScope();

        // Act
        var first = firstScope.ServiceProvider.GetRequiredService<ITenantScope>();
        var second = secondScope.ServiceProvider.GetRequiredService<ITenantScope>();

        // Assert
        first.ShouldBeSameAs(second);
    }
}
