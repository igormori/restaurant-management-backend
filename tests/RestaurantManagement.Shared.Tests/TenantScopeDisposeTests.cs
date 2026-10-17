using Shouldly;
using RestaurantManagement.Shared.Data;

namespace RestaurantManagement.Shared.Tests;

public class TenantScopeDisposeTests
{
    private static readonly Guid OrgA = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");
    private static readonly Guid OrgB = Guid.Parse("b2b2b2b2-0000-4000-8000-000000000002");
    private static readonly Guid OrgC = Guid.Parse("c3c3c3c3-0000-4000-8000-000000000003");

    [Fact]
    [Trait("Rule", "SH6")]
    public void Dispose_CalledAgainAfterANewScopeBegan_KeepsTheNewScope()
    {
        // Arrange
        var tenantScope = new TenantScope();
        var firstScope = tenantScope.Begin(OrgC);
        firstScope.Dispose();
        using var secondScope = tenantScope.Begin(OrgB);

        // Act
        firstScope.Dispose();

        // Assert
        tenantScope.OrganizationId.ShouldBe(OrgB, "disposing an old scope twice must not end the scope that is open now");
    }

    [Fact]
    [Trait("Rule", "SH6")]
    public void Dispose_OuterScopeWhileInnerIsOpen_KeepsTheInnerOrganization()
    {
        // Arrange
        var tenantScope = new TenantScope();
        var outerScope = tenantScope.Begin(OrgA);
        using var innerScope = tenantScope.Begin(OrgB);

        // Act
        outerScope.Dispose();

        // Assert
        tenantScope.OrganizationId.ShouldBe(OrgB, "closing the outer scope while the inner one is open keeps the inner organization");
    }

    [Fact]
    [Trait("Rule", "SH6")]
    public void Dispose_OuterScopeThenInnerScope_LeavesNoScope()
    {
        // Arrange
        var tenantScope = new TenantScope();
        var outerScope = tenantScope.Begin(OrgA);
        var innerScope = tenantScope.Begin(OrgB);

        // Act
        outerScope.Dispose();
        innerScope.Dispose();

        // Assert
        tenantScope.OrganizationId.ShouldBeNull("once every scope is closed no organization is active");
    }
}
