using Shouldly;
using RestaurantManagement.Shared.Data.Tenancy;

namespace RestaurantManagement.Shared.Tests;

public class TenantScopeTests
{
    private static readonly Guid OrgA = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");
    private static readonly Guid OrgC = Guid.Parse("c3c3c3c3-0000-4000-8000-000000000003");

    // Scenario 23
    [Fact]
    [Trait("Rule", "SH6")]
    public void OrganizationId_NoScopeBegun_IsNull()
    {
        // Arrange
        var tenantScope = new TenantScope();

        // Act
        var organizationId = tenantScope.OrganizationId;

        // Assert
        organizationId.ShouldBeNull("with no scope begun there is no active organization");
    }

    // Scenario 24
    [Fact]
    [Trait("Rule", "SH6")]
    public void Begin_Called_OrganizationIdIsTheScopedOrganization()
    {
        // Arrange
        var tenantScope = new TenantScope();

        // Act
        using var scope = tenantScope.Begin(OrgC);

        // Assert
        tenantScope.OrganizationId.ShouldBe(OrgC, "inside a scope the active organization is the one the scope was begun for");
    }

    // Scenario 25
    [Fact]
    [Trait("Rule", "SH6")]
    public void Dispose_ScopeDisposed_OrganizationIdIsNullAgain()
    {
        // Arrange
        var tenantScope = new TenantScope();
        var scope = tenantScope.Begin(OrgC);

        // Act
        scope.Dispose();

        // Assert
        tenantScope.OrganizationId.ShouldBeNull("after the scope is closed no organization stays active");
    }

    // Scenario 26
    [Fact]
    [Trait("Rule", "SH6")]
    public void Dispose_NestedScopeDisposed_RestoresOuterOrganization()
    {
        // Arrange
        var tenantScope = new TenantScope();
        using var outer = tenantScope.Begin(OrgA);
        var inner = tenantScope.Begin(OrgC);

        // Act
        inner.Dispose();

        // Assert
        tenantScope.OrganizationId.ShouldBe(OrgA, "closing an inner scope brings back the outer organization");
    }

    // Scenario 27
    [Fact]
    [Trait("Rule", "SH6")]
    public async Task OrganizationId_ReadAfterAwaits_StillTheScopedOrganization()
    {
        // Arrange
        var tenantScope = new TenantScope();
        using var scope = tenantScope.Begin(OrgC);

        // Act
        await Task.Delay(10);
        await Task.Yield();

        // Assert
        tenantScope.OrganizationId.ShouldBe(OrgC, "the scoped organization survives waiting on other work");
    }

    // Scenario 28
    [Fact]
    [Trait("Rule", "SH6")]
    public async Task Begin_CalledInAnotherAsyncFlow_DoesNotLeakIntoCallerFlow()
    {
        // Arrange
        var tenantScope = new TenantScope();

        // Act
        await Task.Run(() => tenantScope.Begin(OrgC));

        // Assert
        tenantScope.OrganizationId.ShouldBeNull("a scope begun in another flow must not leak into the caller");
    }
}
