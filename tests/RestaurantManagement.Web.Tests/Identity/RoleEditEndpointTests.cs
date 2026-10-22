using System.Net;
using System.Net.Http.Json;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities.Invitations;
using RestaurantManagement.Shared.Auth;
using Shouldly;
using static RestaurantManagement.Web.Tests.InvitationWebSupport;
using static RestaurantManagement.Web.Tests.RoleWebSupport;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// <c>PUT api/roles/{roleId}/levels</c> and <c>PUT api/roles/{roleId}/name</c> on a real PostgreSQL database.
/// Ana is the Owner of Cantina da Nonna; Bia holds the Waiter levels at Batel; Maria is an Admin with
/// Members = Manage at Batel and Mercado but no Cashier level; Trattoria do Sol has its own Sommelier role.
/// </summary>
public class RoleEditEndpointTests : IClassFixture<PostgresDatabaseFixture>
{
    private readonly PostgresDatabaseFixture _postgres;

    public RoleEditEndpointTests(PostgresDatabaseFixture postgres) => _postgres = postgres;

    private const string LevelsBody = """{"levels":{"Orders":"Read"}}""";
    private const string NameBody = """{"name":"Till"}""";

    [Fact]
    [Trait("Rule", "T121")]
    public async Task UpdateLevels_OwnerChangesWaitersLevels_EveryMembersStoredGridStaysIdentical()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync(_postgres);
        var waiterId = await RoleIdAsync(factory, CantinaId, "Waiter");
        var gridBefore = await CellsAsync(factory, CantinaId, BiaId, BatelId);

        // Act
        var response = await factory.ClientFor(AnaId, CantinaId).PutAsync($"/api/roles/{waiterId}/levels", Json(LevelsBody));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await RoleLevelsAsync(factory, CantinaId, waiterId)).ShouldBe(new[] { "Orders:Read" }.ToList(), ignoreOrder: false, customMessage: "the Waiter defaults are replaced");
        (await CellsAsync(factory, CantinaId, BiaId, BatelId)).ShouldBe(gridBefore, ignoreOrder: false, customMessage: "editing a role's defaults never touches a member's grid");
    }

    [Fact]
    [Trait("Rule", "T122")]
    public async Task CreateInvitation_WaiterAfterItsLevelsWereEdited_GetsTheNewLevels()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync(_postgres);
        var waiterId = await RoleIdAsync(factory, CantinaId, "Waiter");
        var owner = factory.ClientFor(AnaId, CantinaId);
        (await owner.PutAsync($"/api/roles/{waiterId}/levels", Json(LevelsBody))).EnsureSuccessStatusCode();

        // Act
        var invitation = await owner.PostAsJsonAsync("/api/invitations", InviteBody(BrunoEmail, MercadoId, new[] { "Waiter" }));

        // Assert
        invitation.StatusCode.ShouldBe(HttpStatusCode.OK);
        var stored = await factory.InOrganizationAsync<IdentityDbContext, List<string>>(CantinaId, async db =>
            db.Set<OrganizationInvitationLevel>().Where(l => l.LocationId == MercadoId).AsEnumerable()
                .Select(l => $"{l.Module}:{l.Level}").ToList());
        stored.ShouldBe(new[] { "Orders:Read" }.ToList(), ignoreOrder: false, customMessage: "a later invitation uses the new defaults");
    }

    [Fact]
    [Trait("Rule", "T123")]
    public async Task RenameRole_OwnerRenamesWaiterToServer_TheRoleListShowsServerAndNoWaiter()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync(_postgres);
        var waiterId = await RoleIdAsync(factory, CantinaId, "Waiter");
        var client = factory.ClientFor(AnaId, CantinaId);

        // Act
        var response = await client.PutAsync($"/api/roles/{waiterId}/name", Json("""{"name":"Server"}"""));
        var names = (await ListRolesAsync(client)).Select(r => r.Name).ToList();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        names.ShouldContain("Server", "the role now carries the new name");
        names.ShouldNotContain("Waiter", "the old name is gone");
    }

    [Theory]
    [InlineData("levels", LevelsBody)]
    [InlineData("name", NameBody)]
    [Trait("Rule", "T124")]
    public async Task EditRole_AdminWithoutCashierEditsOrRenamesTheCashierRole_ReturnsForbiddenAndLeavesItUnchanged(string route, string body)
    {
        // Arrange
        await using var factory = await CreateFactoryAsync(_postgres);
        await MakeMariaAdminAtBothLocationsAsync(factory);
        var cashierId = await RoleIdAsync(factory, CantinaId, "Cashier");
        var levelsBefore = await RoleLevelsAsync(factory, CantinaId, cashierId);

        // Act
        var response = await factory.ClientFor(MariaId, CantinaId).PutAsync($"/api/roles/{cashierId}/{route}", Json(body));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "a role holding a level above the Admin's own cannot be edited or renamed by her");
        (await RoleNamesAsync(factory, CantinaId)).ShouldContain("Cashier", "the name is unchanged");
        (await RoleLevelsAsync(factory, CantinaId, cashierId)).ShouldBe(levelsBefore, ignoreOrder: false, customMessage: "the levels are unchanged");
    }

    [Theory]
    [InlineData("Manager", "levels", LevelsBody)]
    [InlineData("Cashier", "levels", LevelsBody)]
    [InlineData("Waiter", "levels", LevelsBody)]
    [InlineData("Kitchen", "levels", LevelsBody)]
    [InlineData("Manager", "name", NameBody)]
    [InlineData("Cashier", "name", NameBody)]
    [InlineData("Waiter", "name", NameBody)]
    [InlineData("Kitchen", "name", NameBody)]
    [Trait("Rule", "T125")]
    public async Task EditRole_CallerWithADefaultRoleThatLacksMembersManage_ReturnsForbiddenAndLeavesItUnchanged(string callerRole, string route, string body)
    {
        // Arrange
        await using var factory = await CreateFactoryAsync(_postgres);
        await SeedCaioWithRoleAsync(factory, callerRole);
        var waiterId = await RoleIdAsync(factory, CantinaId, "Waiter");
        var levelsBefore = await RoleLevelsAsync(factory, CantinaId, waiterId);

        // Act
        var response = await factory.ClientFor(CaioId, CantinaId).PutAsync($"/api/roles/{waiterId}/{route}", Json(body));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, $"a {callerRole} has no Members = Manage, so she cannot change roles");
        (await RoleNamesAsync(factory, CantinaId)).ShouldContain("Waiter", "the name is unchanged");
        (await RoleLevelsAsync(factory, CantinaId, waiterId)).ShouldBe(levelsBefore, ignoreOrder: false, customMessage: "the levels are unchanged");
    }

    [Theory]
    [InlineData("levels", LevelsBody)]
    [InlineData("name", NameBody)]
    [Trait("Rule", "T126")]
    public async Task EditRole_RoleOfAnotherOrganization_ReturnsNotFoundAndLeavesItUnchanged(string route, string body)
    {
        // Arrange
        await using var factory = await CreateFactoryAsync(_postgres);
        var sommelierId = await RoleIdAsync(factory, TrattoriaId, "Sommelier");

        // Act
        var response = await factory.ClientFor(AnaId, CantinaId).PutAsync($"/api/roles/{sommelierId}/{route}", Json(body));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound, "another organization's role is answered as if it did not exist");
        (await RoleNamesAsync(factory, TrattoriaId)).ShouldContain("Sommelier", "the other organization's role keeps its name");
        (await RoleLevelsAsync(factory, TrattoriaId, sommelierId)).ShouldBeEmpty("the other organization's role keeps its levels");
    }

    [Fact]
    [Trait("Rule", "T128")]
    public async Task UpdateLevels_AdminWithoutCashierRaisesWaiterToCashierManage_ReturnsForbiddenAndLeavesItUnchanged()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync(_postgres);
        await MakeMariaAdminAtBothLocationsAsync(factory);
        var waiterId = await RoleIdAsync(factory, CantinaId, "Waiter");
        var levelsBefore = await RoleLevelsAsync(factory, CantinaId, waiterId);

        // Act
        var response = await factory.ClientFor(MariaId, CantinaId)
            .PutAsync($"/api/roles/{waiterId}/levels", Json("""{"levels":{"Orders":"Manage","Cashier":"Manage"}}"""));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "an Admin cannot raise a role's default above her own level in a module");
        (await RoleLevelsAsync(factory, CantinaId, waiterId)).ShouldBe(levelsBefore, ignoreOrder: false, customMessage: "the levels are unchanged");
    }

    [Theory]
    [InlineData("levels", LevelsBody)]
    [InlineData("name", NameBody)]
    [Trait("Rule", "T129")]
    public async Task EditRole_UnknownRoleInTheOwnOrganization_ReturnsNotFound(string route, string body)
    {
        // Arrange
        await using var factory = await CreateFactoryAsync(_postgres);

        // Act
        var response = await factory.ClientFor(AnaId, CantinaId).PutAsync($"/api/roles/{Guid.NewGuid()}/{route}", Json(body));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound, "a role that does not exist is not found");
    }
}
