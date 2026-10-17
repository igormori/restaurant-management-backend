using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Organization.Data;
using RestaurantManagement.Modules.Organization.Entities;
using RestaurantManagement.Shared;
using Shouldly;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// Organization currency and settings through the real endpoints on PostgreSQL (spec Rules 1, 5 and 7).
/// Ana owns Cantina da Nonna (BRL) and Maple Leaf Bistro (CAD); Liberty Diner (USD) belongs to someone else.
/// </summary>
public class OrganizationFieldsEndpointTests : IClassFixture<PostgresDatabaseFixture>
{
    private static readonly Guid AnaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
    private static readonly Guid CantinaId = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");
    private static readonly Guid MapleId = Guid.Parse("b2b2b2b2-0000-4000-8000-000000000002");
    private static readonly Guid LibertyId = Guid.Parse("c3c3c3c3-0000-4000-8000-000000000003");

    private readonly PostgresDatabaseFixture _postgres;

    public OrganizationFieldsEndpointTests(PostgresDatabaseFixture postgres) => _postgres = postgres;

    private async Task<TenantApiFactory> CreateFactoryAsync()
    {
        var factory = await TenantApiFactory.CreateAsync(_postgres);
        await factory.SeedOrganizationAsync(CantinaId, "Cantina da Nonna", Currency.BRL);
        await factory.SeedOrganizationAsync(MapleId, "Maple Leaf Bistro", Currency.CAD);
        await factory.SeedOrganizationAsync(LibertyId, "Liberty Diner", Currency.USD);
        await factory.SeedUserAsync(AnaId, "ana@cantinadanonna.com.br", "Ana",
            new UserRole { OrganizationId = CantinaId, Role = Roles.Owner },
            new UserRole { OrganizationId = MapleId, Role = Roles.Owner });
        return factory;
    }

    private static object EditBody(object? organizationId = null, string? currency = null) => new
    {
        organizationId,
        currency,
        name = "Cantina da Nonna",
        description = "Cozinha italiana de família",
        serviceChargeRate = 10m,
        discountApprovalLimit = 15.5m,
        businessDayCutoff = "05:30"
    };

    /// <summary>GET api/organizations, keyed by organization name.</summary>
    private static async Task<Dictionary<string, JsonNode>> ListOrganizationsAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/organizations");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var organizations = JsonNode.Parse(await response.Content.ReadAsStringAsync())!.AsArray();
        return organizations.ToDictionary(o => o!["name"]!.GetValue<string>(), o => o!);
    }

    private static string? Text(JsonNode organization, string property) => organization[property]?.ToString();

    private static StringContent Json(string body) => new(body, Encoding.UTF8, "application/json");

    // Scenario 2
    [Fact]
    [Trait("Rule", "OR21")]
    public async Task CreateOrganization_CurrencyMissing_ReturnsBadRequestAndStoresNothing()
    {
        // Arrange
        await using var factory = await TenantApiFactory.CreateAsync(_postgres);
        await factory.SeedUserAsync(AnaId, "ana@cantinadanonna.com.br", "Ana");
        var client = factory.ClientFor(AnaId, organizationId: null, Roles.Owner);

        // Act
        var response = await client.PostAsync("/api/organizations/create", Json("""{ "name": "Pizzaria Vila Madalena" }"""));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, "an organization without a currency is refused");
        (await response.Content.ReadAsStringAsync()).ShouldContain("currency", Case.Insensitive, customMessage: "the error says the currency is the problem");
        var stored = await factory.InOrganizationAsync<OrganizationDbContext, int>(CantinaId,
            db => db.Organizations.IgnoreQueryFilters().CountAsync());
        stored.ShouldBe(0, "a refused organization is not stored");
    }

    // Scenario 23
    [Fact]
    [Trait("Rule", "OR21")]
    public async Task CreateOrganization_CurrencyAsNumber_ReturnsBadRequestAndStoresNothing()
    {
        // Arrange
        await using var factory = await TenantApiFactory.CreateAsync(_postgres);
        await factory.SeedUserAsync(AnaId, "ana@cantinadanonna.com.br", "Ana");
        var client = factory.ClientFor(AnaId, organizationId: null, Roles.Owner);

        // Act
        var response = await client.PostAsync("/api/organizations/create", Json("""{ "name": "Pizzaria Vila Madalena", "currency": 1 }"""));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, "a currency sent as a number is refused");
        (await response.Content.ReadAsStringAsync()).ShouldContain("currency", Case.Insensitive, customMessage: "the error says the currency is the problem");
        var stored = await factory.InOrganizationAsync<OrganizationDbContext, int>(CantinaId,
            db => db.Organizations.IgnoreQueryFilters().CountAsync());
        stored.ShouldBe(0, "a refused organization is not stored");
    }

    // Scenario 3
    [Fact]
    [Trait("Rule", "OR1")]
    public async Task CreateOrganization_CurrencyCad_ReturnsAndStoresCad()
    {
        // Arrange
        await using var factory = await TenantApiFactory.CreateAsync(_postgres);
        await factory.SeedUserAsync(AnaId, "ana@cantinadanonna.com.br", "Ana");
        var client = factory.ClientFor(AnaId, organizationId: null, Roles.Owner);

        // Act
        var response = await client.PostAsJsonAsync("/api/organizations/create", new { name = "Poutine Palace", currency = "CAD" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK, "an organization may be created in Canadian dollars");
        Text(JsonNode.Parse(await response.Content.ReadAsStringAsync())!, "currency").ShouldBe("CAD", "the organization is returned with the currency chosen");
        Text((await ListOrganizationsAsync(client))["Poutine Palace"], "currency").ShouldBe("CAD", "the organization is stored with the currency chosen");
    }

    // Scenario 4
    [Fact]
    [Trait("Rule", "OR22")]
    public async Task EditOrganization_BodyIncludesAnotherCurrency_KeepsStoredCurrency()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync();
        var client = factory.ClientFor(AnaId, CantinaId, Roles.Owner);

        // Act
        var response = await client.PutAsJsonAsync("/api/organizations/edit", EditBody(currency: "USD"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK, "an edit is accepted even when the body carries a currency");
        Text((await ListOrganizationsAsync(client))["Cantina da Nonna"], "currency").ShouldBe("BRL", "an organization's currency cannot be changed by editing it");
    }

    // Scenario 5
    [Fact]
    [Trait("Rule", "OR17")]
    public async Task GetOrganizations_MemberOfTwoOrganizations_ReturnsOnlyThoseWithTheirOwnCurrency()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync();
        var client = factory.ClientFor(AnaId, CantinaId, Roles.Owner);

        // Act
        var organizations = await ListOrganizationsAsync(client);

        // Assert
        organizations.ToDictionary(o => o.Key, o => Text(o.Value, "currency")).ShouldBe(new Dictionary<string, string?>
        {
            ["Cantina da Nonna"] = "BRL",
            ["Maple Leaf Bistro"] = "CAD"
        }, ignoreOrder: true, customMessage: "each organization shows its own currency, and only the user's organizations are listed");
    }

    // Scenario 9
    [Fact]
    [Trait("Rule", "OR7")]
    public async Task EditOrganization_ValidSettings_StoresAndReturnsThem()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync();
        var client = factory.ClientFor(AnaId, CantinaId, Roles.Owner);

        // Act
        var response = await client.PutAsJsonAsync("/api/organizations/edit", EditBody());

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK, "an Owner may edit the organization settings");
        var cantina = (await ListOrganizationsAsync(client))["Cantina da Nonna"];
        cantina["serviceChargeRate"]!.GetValue<decimal>().ShouldBe(10m, "the new service charge is stored");
        cantina["discountApprovalLimit"]!.GetValue<decimal>().ShouldBe(15.5m, "the new discount approval limit is stored");
        Text(cantina, "businessDayCutoff").ShouldBe("05:30", "the new business day cutoff is stored");
    }

    // Scenario 10
    [Fact]
    [Trait("Rule", "OR16")]
    public async Task EditOrganization_BodyNamesAnotherOrganization_ChangesOnlyTokenOrganizationSettings()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync();
        var client = factory.ClientFor(AnaId, CantinaId, Roles.Owner);

        // Act
        var response = await client.PutAsJsonAsync("/api/organizations/edit", EditBody(organizationId: MapleId));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK, "the edit is accepted for the token organization");
        var organizations = await ListOrganizationsAsync(client);
        organizations["Cantina da Nonna"]["serviceChargeRate"]!.GetValue<decimal>().ShouldBe(10m, "the token organization's settings are the ones changed");
        organizations["Maple Leaf Bistro"]["serviceChargeRate"]!.GetValue<decimal>().ShouldBe(0m, "another organization named in the body keeps its own service charge");
        organizations["Maple Leaf Bistro"]["discountApprovalLimit"]!.GetValue<decimal>().ShouldBe(0m, "another organization named in the body keeps its own discount limit");
        organizations["Maple Leaf Bistro"]["name"]!.GetValue<string>().ShouldBe("Maple Leaf Bistro", "another organization named in the body keeps its own name");
    }
}
