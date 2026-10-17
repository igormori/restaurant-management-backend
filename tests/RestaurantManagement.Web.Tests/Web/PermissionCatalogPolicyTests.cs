using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using RestaurantManagement.Shared.Auth;
using Shouldly;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// Every permission in the catalog can be required by an endpoint, because each has a policy.
/// </summary>
public class PermissionCatalogPolicyTests : IDisposable
{
    private readonly PermissionProbeApiFactory _factory = new();

    public void Dispose()
    {
        _factory.Dispose();
    }

    [Fact]
    [Trait("Rule", "W10")]
    public async Task PolicyProvider_EveryPermissionInTheCatalog_HasAPolicyNamedAfterIt()
    {
        // Arrange
        var provider = _factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();

        // Act
        var policies = await Task.WhenAll(Permissions.All.Select(permission => provider.GetPolicyAsync(permission)));

        // Assert
        Permissions.All.ShouldNotBeEmpty();
        policies.ShouldAllBe(policy => policy != null, "every permission in the catalog needs a policy, or an endpoint requiring it would fail");
    }
}
