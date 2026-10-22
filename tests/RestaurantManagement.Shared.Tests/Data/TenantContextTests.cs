using NSubstitute;
using RestaurantManagement.Shared.Auth;
using RestaurantManagement.Shared.Data.Tenancy;
using Shouldly;

namespace RestaurantManagement.Shared.Tests.Data;

/// <summary>
/// Where the current organization comes from: an open tenant scope first, then the sign-in token.
/// </summary>
public class TenantContextTests
{
    private static readonly Guid CantinaId = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");
    private static readonly Guid NewTrattoriaId = Guid.Parse("c3c3c3c3-0000-4000-8000-000000000003");

    private static ICurrentUser SignedInTo(Guid organizationId)
    {
        var currentUser = Substitute.For<ICurrentUser>();
        currentUser.OrganizationId.Returns(organizationId);
        return currentUser;
    }

    // Scenario 7
    [Fact]
    [Trait("Rule", "SH6")]
    public void OrganizationId_ScopeOpenedForAnotherOrganization_ReturnsTheScopedOrganization()
    {
        // Arrange
        var tenantScope = new TenantScope();
        var sut = new TenantContext(tenantScope, SignedInTo(CantinaId), new SelectedLocation());
        using var scope = tenantScope.Begin(NewTrattoriaId);

        // Act
        var organizationId = sut.OrganizationId;

        // Assert
        organizationId.ShouldBe(NewTrattoriaId, "an open tenant scope takes priority over the token organization");
    }

    // Scenario 8
    [Fact]
    [Trait("Rule", "SH6")]
    public void OrganizationId_NoScopeOpen_ReturnsTheTokenOrganization()
    {
        // Arrange
        var sut = new TenantContext(new TenantScope(), SignedInTo(CantinaId), new SelectedLocation());

        // Act
        var organizationId = sut.OrganizationId;

        // Assert
        organizationId.ShouldBe(CantinaId, "with no scope open the organization comes from the token");
    }
}
