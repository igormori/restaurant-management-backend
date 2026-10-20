using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using RestaurantManagement.Shared.Auth;
using Shouldly;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// Every module and level of the access grid can be required by an endpoint, because each has a policy.
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
    public async Task PolicyProvider_EveryModuleAndReadAndManage_HasAPolicyNamedModuleDotLevel()
    {
        // Arrange
        var provider = _factory.Services.GetRequiredService<IAuthorizationPolicyProvider>();
        var names = Enum.GetValues<AccessModule>()
            .SelectMany(module => new[] { AccessLevel.Read, AccessLevel.Manage }
                .Select(level => $"{module.ToString().ToLowerInvariant()}.{level.ToString().ToLowerInvariant()}"))
            .ToList();

        // Act
        var policies = await Task.WhenAll(names.Select(name => provider.GetPolicyAsync(name)));

        // Assert
        names.Count.ShouldBe(14, "seven modules with two levels each");
        policies.ShouldAllBe(policy => policy != null, "every module and level needs a policy, or an endpoint requiring it would fail");
    }
}
