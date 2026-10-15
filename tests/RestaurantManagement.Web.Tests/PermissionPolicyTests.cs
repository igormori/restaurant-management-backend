using System.Net;
using System.Net.Http.Headers;
using RestaurantManagement.Shared;
using Shouldly;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// One access policy per permission: met only when the pass names the selected organization and
/// holds at least one role that grants the permission (spec Outcome #2).
/// </summary>
public class PermissionPolicyTests : IDisposable
{
    private const string MembersUrl = "/api/test-probe/members";
    private const string MenuUrl = "/api/test-probe/menu";
    private const string ReportsUrl = "/api/test-probe/reports";

    private static readonly Guid MariaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
    private static readonly Guid CantinaId = Guid.Parse("c1c1c1c1-0000-4000-8000-000000000001");

    private readonly PermissionProbeApiFactory _factory = new();

    public void Dispose()
    {
        _factory.Dispose();
    }

    private HttpRequestMessage SignedInGet(string url, Guid? organizationId, params string[] roles)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestAccessTokens.Create(_factory.Services, MariaId, organizationId, roles));
        return request;
    }

    // Scenario 19
    [Fact]
    public async Task MembersManagePolicy_NoAccessToken_ReturnsUnauthorized()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync(MembersUrl);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // Scenario 20
    [Theory]
    [InlineData(Roles.Manager)]
    [InlineData(Roles.Cashier)]
    [InlineData(Roles.Waiter)]
    [InlineData(Roles.Kitchen)]
    public async Task MembersManagePolicy_RoleWithoutMembersManage_ReturnsForbidden(string role)
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.SendAsync(SignedInGet(MembersUrl, CantinaId, role));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // Scenario 21
    [Fact]
    public async Task MembersManagePolicy_OwnerRolesButNoOrganizationSelected_ReturnsForbidden()
    {
        // Arrange: a user of two organizations who has not selected one gets roles from both, no organization claim
        var client = _factory.CreateClient();

        // Act
        var response = await client.SendAsync(SignedInGet(MembersUrl, organizationId: null, Roles.Owner, Roles.Waiter));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // Scenario 22
    [Fact]
    public async Task MembersManagePolicy_OrganizationSelectedButNoRoleInIt_ReturnsForbidden()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.SendAsync(SignedInGet(MembersUrl, CantinaId));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // Scenario 23
    [Theory]
    [InlineData(Roles.Owner)]
    [InlineData(Roles.Admin)]
    public async Task MembersManagePolicy_OwnerOrAdminOfSelectedOrganization_ReturnsOk(string role)
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.SendAsync(SignedInGet(MembersUrl, CantinaId, role));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // Scenario 24
    [Fact]
    public async Task MenuManagePolicy_Manager_ReturnsOk()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.SendAsync(SignedInGet(MenuUrl, CantinaId, Roles.Manager));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // Scenario 25
    [Fact]
    public async Task ReportsViewPolicy_Manager_ReturnsOk()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.SendAsync(SignedInGet(ReportsUrl, CantinaId, Roles.Manager));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // Scenario 26
    [Fact]
    public async Task MenuManagePolicy_WaiterAndManagerInSameOrganization_ReturnsOk()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.SendAsync(SignedInGet(MenuUrl, CantinaId, Roles.Waiter, Roles.Manager));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}
