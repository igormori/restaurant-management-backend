using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Modules.Identity.Data;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Shared.Auth;
using Shouldly;
using Currency = RestaurantManagement.Modules.Organization.Entities.Currency;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// Location and organization endpoints on a real PostgreSQL database, decided by the stored access
/// grid. Cantina da Nonna has Batel and Centro. Ana is its Owner; Joao is an Admin with Manage on
/// Locations, Menus and Members at both locations; Caio is a new Admin with nothing granted yet;
/// Eva is the Owner of Trattoria do Sol only.
/// </summary>
public class OrganizationGridEndpointTests : IClassFixture<PostgresDatabaseFixture>
{
    private static readonly Guid AnaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
    private static readonly Guid JoaoId = Guid.Parse("22222222-bbbb-4bbb-8bbb-00000000000b");
    private static readonly Guid CaioId = Guid.Parse("33333333-cccc-4ccc-8ccc-00000000000c");
    private static readonly Guid EvaId = Guid.Parse("44444444-dddd-4ddd-8ddd-00000000000d");
    private static readonly Guid RitaId = Guid.Parse("55555555-eeee-4eee-8eee-00000000000e");
    private static readonly Guid CantinaId = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");
    private static readonly Guid TrattoriaId = Guid.Parse("b2b2b2b2-0000-4000-8000-000000000002");
    private static readonly Guid BatelId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000001");
    private static readonly Guid CentroId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000002");

    // Carries the location and the organization settings, so one body passes validation on every endpoint below.
    private static object LocationBody(string name, string code) => new
    {
        name,
        address = "Av. Sete de Setembro, 1865",
        city = "Curitiba",
        state = "PR",
        postalCode = "80060-070",
        country = "Brasil",
        phoneNumber = "+55 41 3000-3000",
        status = "Active",
        timeZone = "America/Sao_Paulo",
        code,
        description = "Cozinha italiana de família",
        serviceChargeRate = 10m,
        discountApprovalLimit = 5m,
        businessDayCutoff = "04:00"
    };

    private readonly PostgresDatabaseFixture _postgres;

    public OrganizationGridEndpointTests(PostgresDatabaseFixture postgres) => _postgres = postgres;

    private async Task<TenantApiFactory> CreateFactoryAsync()
    {
        var factory = await TenantApiFactory.CreateAsync(_postgres);
        await factory.SeedOrganizationAsync(CantinaId, "Cantina da Nonna", Currency.BRL);
        await factory.SeedOrganizationAsync(TrattoriaId, "Trattoria do Sol", Currency.BRL);
        await factory.SeedLocationAsync(CantinaId, BatelId, "Unidade Batel", "BATEL", "America/Sao_Paulo");
        await factory.SeedLocationAsync(CantinaId, CentroId, "Unidade Centro", "CENTRO", "America/Sao_Paulo");
        await factory.SeedUserAsync(AnaId, "ana@cantinadanonna.com.br", "Ana", TenantApiFactory.OwnerOf(CantinaId));
        await factory.SeedUserAsync(JoaoId, "joao@cantinadanonna.com.br", "João", TenantApiFactory.MemberOf(CantinaId));
        await factory.SeedUserAsync(CaioId, "caio@cantinadanonna.com.br", "Caio", TenantApiFactory.MemberOf(CantinaId));
        await factory.SeedUserAsync(EvaId, "eva@trattoriadosol.com.br", "Eva", TenantApiFactory.OwnerOf(TrattoriaId));
        foreach (var location in new[] { BatelId, CentroId })
            await factory.GrantAsync(JoaoId, CantinaId, location,
                (AccessModule.Locations, AccessLevel.Manage), (AccessModule.Menus, AccessLevel.Manage), (AccessModule.Members, AccessLevel.Manage));
        return factory;
    }

    [Fact]
    [Trait("Rule", "OR39")]
    public async Task EditLocation_AdminManagingBatelAndCentroAfterTheOwnerAddsANewLocation_IsRefusedAtTheNewLocation()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync();
        var created = await factory.ClientFor(AnaId, CantinaId).PostAsJsonAsync("/api/locations", LocationBody("Unidade Barigui", "BARIGUI"));
        var newLocationId = (await created.Content.ReadFromJsonAsync<LocationRow>())!.Id;
        var admin = factory.ClientFor(JoaoId, CantinaId);

        // Act
        var editExisting = await admin.PutAsJsonAsync($"/api/locations/{BatelId}", LocationBody("Unidade Batel Reformada", "BATEL"));
        var editNew = await admin.PutAsJsonAsync($"/api/locations/{newLocationId}", LocationBody("Unidade Barigui Nova", "BARIGUI"));

        // Assert
        created.StatusCode.ShouldBe(HttpStatusCode.OK, "the Owner creates a location");
        editExisting.StatusCode.ShouldBe(HttpStatusCode.OK, "the Admin manages Locations at the locations that existed when access was granted");
        editNew.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "a new location is Owner-only until access is granted: there is no 'all locations' grid");
    }

    private sealed record LocationRow(Guid Id);

    [Theory]
    [InlineData("POST", "/api/locations")]
    [InlineData("PUT", "/api/organizations/edit")]
    [InlineData("DELETE", "/api/locations/44444444-dddd-4ddd-8ddd-000000000002")]
    [Trait("Rule", "OR40")]
    public async Task OwnerOnlyEndpoints_OwnerWithNoStoredLevels_ReturnsSuccess(string method, string url)
    {
        // Arrange
        await using var factory = await CreateFactoryAsync();
        var client = factory.ClientFor(AnaId, CantinaId);
        var request = new HttpRequestMessage(new HttpMethod(method), url) { Content = JsonContent.Create(LocationBody("Unidade Barigui", "BARIGUI")) };

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.IsSuccessStatusCode.ShouldBeTrue("the Owner creates and closes locations and edits the organization with no stored level");
    }

    [Fact]
    [Trait("Rule", "OR42")]
    public async Task EditOrganization_OwnerOfAnotherOrganizationOnly_ReturnsForbiddenAndChangesNothing()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync();
        var client = factory.ClientFor(EvaId, CantinaId);
        var ownerHere = await factory.ClientFor(AnaId, CantinaId).PutAsJsonAsync("/api/organizations/edit", LocationBody("Cantina da Nonna", "X"));

        // Act
        var response = await client.PutAsJsonAsync("/api/organizations/edit", LocationBody("Cantina Invadida", "X"));

        // Assert
        ownerHere.StatusCode.ShouldBe(HttpStatusCode.OK, "the Owner of Cantina da Nonna may edit it");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "being the Owner of Trattoria do Sol gives nothing in Cantina da Nonna");
        var name = await factory.InOrganizationAsync<RestaurantManagement.Modules.Organization.Data.OrganizationDbContext, string>(CantinaId,
            async db => (await db.Organizations.SingleAsync(o => o.Id == CantinaId)).Name);
        name.ShouldBe("Cantina da Nonna", "a refused edit leaves the organization unchanged");
    }

    [Fact]
    [Trait("Rule", "OR43")]
    public async Task CreateOrganization_UserWithNoOrganization_BecomesTheOwnerAndTheFiveDefaultRolesExist()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync();
        await factory.SeedUserAsync(RitaId, "rita@exemplo.com.br", "Rita");
        var client = factory.ClientFor(RitaId, organizationId: null);

        // Act
        var response = await client.PostAsJsonAsync("/api/organizations/create", new { name = "Pizzaria da Rita", currency = "BRL" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK, "a user with no organization may create one");
        var organizationId = (await response.Content.ReadFromJsonAsync<LocationRow>())!.Id;
        var (ownerCount, roleNames) = await factory.InOrganizationAsync<IdentityDbContext, (int, List<string>)>(organizationId, async db =>
            (await db.Set<OrganizationMember>().CountAsync(m => m.UserId == RitaId && m.IsOwner),
             await db.Set<OrganizationRole>().Select(r => r.Name).ToListAsync()));
        ownerCount.ShouldBe(1, "the creator is the Owner of the new organization");
        roleNames.ShouldBe(new[] { "Admin", "Manager", "Cashier", "Waiter", "Kitchen" }, ignoreOrder: true, customMessage: "a new organization has the five default roles and no Owner role");
    }
}
