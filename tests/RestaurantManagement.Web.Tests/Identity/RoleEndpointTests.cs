using System.Net;
using System.Net.Http.Json;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Shared.Data;
using RestaurantManagement.Shared.Services.AuditLog;
using Shouldly;
using static RestaurantManagement.Web.Tests.InvitationWebSupport;
using static RestaurantManagement.Web.Tests.RoleWebSupport;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// <c>GET api/roles</c>, <c>GET api/roles/{roleId}</c> and <c>POST api/roles</c> on a real PostgreSQL database.
/// Ana is the Owner of Cantina da Nonna; Maria is an Admin with Members = Manage at Batel and Mercado but no
/// Cashier level; Trattoria do Sol has its own Sommelier role.
/// </summary>
public class RoleEndpointTests : IClassFixture<PostgresDatabaseFixture>
{
    private readonly PostgresDatabaseFixture _postgres;

    public RoleEndpointTests(PostgresDatabaseFixture postgres) => _postgres = postgres;

    private const string MarketingBody = """{"name":"Marketing","levels":{"Menus":"Manage","Reports":"Read"}}""";

    [Fact]
    [Trait("Rule", "T112")]
    public async Task CreateRole_OwnerCreatesMarketing_ReturnsItAndListsItWithItsLevels()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync(_postgres);
        var client = factory.ClientFor(AnaId, CantinaId);

        // Act
        var response = await client.PostAsync("/api/roles", Json(MarketingBody));
        var roles = await ListRolesAsync(client);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var marketing = roles.Single(r => r.Name == "Marketing");
        marketing.Levels["Menus"].ShouldBe("Manage", "the role list shows the levels the Owner chose");
        marketing.Levels["Cashier"].ShouldBe("None", "a module left out means None");
    }

    [Fact]
    [Trait("Rule", "T113")]
    public async Task CreateInvitation_WithTheNewMarketingRole_Succeeds()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync(_postgres);
        var client = factory.ClientFor(AnaId, CantinaId);
        (await client.PostAsync("/api/roles", Json(MarketingBody))).EnsureSuccessStatusCode();

        // Act
        var invitation = await client.PostAsJsonAsync("/api/invitations", InviteBody(BrunoEmail, BatelId, new[] { "Marketing" }));

        // Assert
        invitation.StatusCode.ShouldBe(HttpStatusCode.OK, "a role created by the Owner can be chosen when inviting");
    }

    [Fact]
    [Trait("Rule", "T114")]
    public async Task CreateRole_TwoCreatesOfOneNameAtTheSameMoment_OneSucceedsAndTheOtherIsRefusedNeverAServerError()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync(_postgres);
        var first = factory.ClientFor(AnaId, CantinaId);
        var second = factory.ClientFor(AnaId, CantinaId);

        // Act
        var responses = await Task.WhenAll(
            first.PostAsync("/api/roles", Json("""{"name":"Marketing"}""")),
            second.PostAsync("/api/roles", Json("""{"name":"marketing"}""")));

        // Assert
        responses.Select(r => (int)r.StatusCode).Order().ToList().ShouldBe(new List<int> { 200, 400 }, ignoreOrder: false, customMessage: "one request wins and the other gets the same 400 as a plain duplicate, never a 500");
        (await RoleNamesAsync(factory, CantinaId)).Count(n => n.Equals("marketing", StringComparison.OrdinalIgnoreCase))
            .ShouldBe(1, "only one role of that name exists afterwards");
    }

    [Fact]
    [Trait("Rule", "T115")]
    public async Task CreateRole_AdminWithoutCashierGivesTheRoleCashierManage_ReturnsForbiddenAndStoresNothing()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync(_postgres);
        await MakeMariaAdminAtBothLocationsAsync(factory);
        var client = factory.ClientFor(MariaId, CantinaId);

        // Act
        var response = await client.PostAsync("/api/roles", Json("""{"name":"Marketing","levels":{"Cashier":"Manage"}}"""));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "an Admin cannot create a role stronger than her own access");
        (await RoleNamesAsync(factory, CantinaId)).ShouldNotContain("Marketing", "the refused role is not saved");
    }

    [Fact]
    [Trait("Rule", "T130")]
    public async Task CreateRole_AdminWithMembersManageEverywhereCreatesARoleWithinHerLevels_ReturnsOkAndStoresIt()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync(_postgres);
        await MakeMariaAdminAtBothLocationsAsync(factory);

        // Act
        var response = await factory.ClientFor(MariaId, CantinaId)
            .PostAsync("/api/roles", Json("""{"name":"Marketing","levels":{"Menus":"Manage","Orders":"Read"}}"""));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK, "an Admin may create a role at or below her own levels");
        (await RoleNamesAsync(factory, CantinaId)).ShouldContain("Marketing", "the role is saved");
    }

    [Theory]
    [InlineData("Manager")]
    [InlineData("Cashier")]
    [InlineData("Waiter")]
    [InlineData("Kitchen")]
    [Trait("Rule", "T116")]
    public async Task CreateRole_CallerWithADefaultRoleThatLacksMembersManage_ReturnsForbiddenAndStoresNothing(string role)
    {
        // Arrange
        await using var factory = await CreateFactoryAsync(_postgres);
        await SeedCaioWithRoleAsync(factory, role);

        // Act
        var response = await factory.ClientFor(CaioId, CantinaId).PostAsync("/api/roles", Json("""{"name":"Marketing"}"""));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, $"a {role} has no Members = Manage, so she cannot create roles");
        (await RoleNamesAsync(factory, CantinaId)).ShouldNotContain("Marketing", "the refused role is not saved");
    }

    [Fact]
    [Trait("Rule", "T117")]
    public async Task GetRole_RoleOfAnotherOrganization_ReturnsNotFound()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync(_postgres);
        var sommelierId = await RoleIdAsync(factory, TrattoriaId, "Sommelier");

        // Act
        var response = await factory.ClientFor(AnaId, CantinaId).GetAsync($"/api/roles/{sommelierId}");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound, "another organization's role is answered as if it did not exist");
    }

    [Fact]
    [Trait("Rule", "T118")]
    public async Task ListRoles_TwoOrganizationsWithRoles_ShowsOnlyTheTokenOrganizationsRoles()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync(_postgres);

        // Act
        var roles = await ListRolesAsync(factory.ClientFor(AnaId, CantinaId));

        // Assert
        roles.Select(r => r.Name).ShouldBe(new[] { "Admin", "Cashier", "Kitchen", "Manager", "Waiter" }, ignoreOrder: true,
            customMessage: "Cantina sees its five roles and never Trattoria's Sommelier");
    }

    [Fact]
    [Trait("Rule", "T119")]
    public async Task CreateRole_OwnerCreatesMarketing_WritesOneAuditEntryWithTheCallerTheTimeAndTheLevelsAfter()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync(_postgres);
        var client = factory.ClientFor(AnaId, CantinaId);

        // Act
        (await client.PostAsync("/api/roles", Json(MarketingBody))).EnsureSuccessStatusCode();

        // Assert
        var entry = (await AuditEntriesAsync(factory, CantinaId, AuditActions.RoleCreated)).ShouldHaveSingleItem("one create writes exactly one audit entry");
        entry.CreatedByUserId.ShouldBe(AnaId, "the entry records who created the role");
        entry.CreatedAt.ShouldBeGreaterThan(DateTime.UtcNow.AddMinutes(-5), "the entry records when");
        entry.After.ShouldNotBeNull().ShouldContain("Marketing", customMessage: "the entry records the name after the change");
        entry.After.ShouldContain("Manage", customMessage: "the entry records the levels after the change");
    }
}
