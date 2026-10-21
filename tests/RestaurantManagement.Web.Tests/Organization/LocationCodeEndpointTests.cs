using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using RestaurantManagement.Modules.Organization.Entities;
using RestaurantManagement.Shared;
using Shouldly;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// Location codes through the real endpoints on PostgreSQL (spec Rule 4): unique within an organization,
/// closed locations included and case ignored, reusable by another organization; backed by a unique index.
/// Also: a location edit that leaves out an optional field keeps that field's stored value.
/// Ana owns Cantina da Nonna and Trattoria do Sol.
/// </summary>
public class LocationCodeEndpointTests : IClassFixture<PostgresDatabaseFixture>
{
    private static readonly Guid AnaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
    private static readonly Guid CantinaId = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");
    private static readonly Guid TrattoriaId = Guid.Parse("b2b2b2b2-0000-4000-8000-000000000002");

    private readonly PostgresDatabaseFixture _postgres;

    public LocationCodeEndpointTests(PostgresDatabaseFixture postgres) => _postgres = postgres;

    private async Task<TenantApiFactory> CreateFactoryAsync()
    {
        var factory = await TenantApiFactory.CreateAsync(_postgres);
        await factory.SeedOrganizationAsync(CantinaId, "Cantina da Nonna", Currency.BRL);
        await factory.SeedOrganizationAsync(TrattoriaId, "Trattoria do Sol", Currency.BRL);
        await factory.SeedUserAsync(AnaId, "ana@cantinadanonna.com.br", "Ana",
            TenantApiFactory.OwnerOf(CantinaId),
            TenantApiFactory.OwnerOf(TrattoriaId));
        return factory;
    }

    private static object LocationBody(string name, string code) => new
    {
        name,
        address = "Rua Augusta, 1200",
        city = "São Paulo",
        state = "SP",
        postalCode = "01304-001",
        country = "Brasil",
        phoneNumber = "+55 11 3000-1000",
        status = "Active",
        timeZone = "America/Sao_Paulo",
        code
    };

    private static async Task<Guid> CreateLocationAsync(HttpClient client, string name, string code)
    {
        var response = await client.PostAsJsonAsync("/api/locations", LocationBody(name, code));
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return JsonNode.Parse(await response.Content.ReadAsStringAsync())!["id"]!.GetValue<Guid>();
    }

    /// <summary>GET api/locations, as location name to code.</summary>
    private static async Task<Dictionary<string, string?>> ListCodesAsync(HttpClient client)
    {
        var locations = JsonNode.Parse(await client.GetStringAsync("/api/locations"))!.AsArray();
        return locations.ToDictionary(l => l!["name"]!.GetValue<string>(), l => l!["code"]?.ToString());
    }

    // Scenario 15
    [Theory]
    [InlineData("MM")]
    [InlineData("mm")]
    [InlineData("BT")]
    [Trait("Rule", "OR28")]
    public async Task CreateLocation_CodeAlreadyUsedInOrganization_ReturnsBadRequestAndStoresNothing(string code)
    {
        // Arrange: Cantina has Mercado Municipal (MM, active) and Batel (BT, closed)
        await using var factory = await CreateFactoryAsync();
        var client = factory.ClientFor(AnaId, CantinaId);
        await CreateLocationAsync(client, "Unidade Mercado Municipal", "MM");
        var batelId = await CreateLocationAsync(client, "Unidade Batel", "BT");
        (await client.DeleteAsync($"/api/locations/{batelId}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        // Act
        var response = await client.PostAsJsonAsync("/api/locations", LocationBody("Unidade Moema", code));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, "a location code already used in the organization is refused, even by a closed location");
        (await ListCodesAsync(client)).Keys.ShouldBe(new[] { "Unidade Batel", "Unidade Mercado Municipal" }, ignoreOrder: true, customMessage: "a refused location is not stored");
    }

    // Scenario 16
    [Fact]
    [Trait("Rule", "OR28")]
    public async Task CreateLocation_CodeUsedOnlyByAnotherOrganization_ReturnsOkWithCode()
    {
        // Arrange: Trattoria already has MM
        await using var factory = await CreateFactoryAsync();
        await CreateLocationAsync(factory.ClientFor(AnaId, TrattoriaId), "Unidade Mercado", "MM");
        var cantinaClient = factory.ClientFor(AnaId, CantinaId);

        // Act
        var response = await cantinaClient.PostAsJsonAsync("/api/locations", LocationBody("Unidade Mercado Municipal", "MM"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK, "a code used only by another organization is free to use");
        var code = JsonNode.Parse(await response.Content.ReadAsStringAsync())!["code"]?.ToString();
        code.ShouldBe("MM", "the location is stored with the code chosen");
    }

    // Scenario 17
    [Fact]
    [Trait("Rule", "OR28")]
    public async Task EditLocation_CodeUsedByAnotherLocationOfOrganization_ReturnsBadRequestAndKeepsCode()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync();
        var client = factory.ClientFor(AnaId, CantinaId);
        await CreateLocationAsync(client, "Unidade Mercado Municipal", "MM");
        var batelId = await CreateLocationAsync(client, "Unidade Batel", "BT");

        // Act
        var response = await client.PutAsJsonAsync($"/api/locations/{batelId}", LocationBody("Unidade Batel", "MM"));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, "a location cannot take a code another location of the organization uses");
        (await ListCodesAsync(client))["Unidade Batel"].ShouldBe("BT", "a refused edit keeps the location's own code");
    }

    // Scenario 18
    [Fact]
    [Trait("Rule", "OR28")]
    public async Task CreateLocation_SameCodeSentConcurrently_StoresOneAndRejectsOthersWithoutServerError()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync();
        var clients = Enumerable.Range(1, 5).Select(_ => factory.ClientFor(AnaId, CantinaId)).ToList();

        // Act
        var responses = await Task.WhenAll(clients.Select((client, i) =>
            client.PostAsJsonAsync("/api/locations", LocationBody($"Unidade Moema {i + 1}", "MOE"))));

        // Assert
        var statuses = responses.Select(r => r.StatusCode).ToList();
        statuses.ShouldNotContain(HttpStatusCode.InternalServerError, customMessage: "two requests for the same code at once never cause a server error");
        statuses.Count(s => s == HttpStatusCode.OK).ShouldBe(1, "when the same code is sent at once, exactly one location is created");
        statuses.Count(s => s == HttpStatusCode.BadRequest).ShouldBe(4, "when the same code is sent at once, the other requests are refused as duplicates");
    }

    // Scenario 25
    [Fact]
    [Trait("Rule", "OR27")]
    public async Task EditLocation_StatusOmitted_KeepsStoredStatus()
    {
        // Arrange: Batel is closed
        await using var factory = await CreateFactoryAsync();
        var client = factory.ClientFor(AnaId, CantinaId);
        var batelId = await CreateLocationAsync(client, "Unidade Batel", "BT");
        (await client.DeleteAsync($"/api/locations/{batelId}")).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var bodyWithoutStatus = new
        {
            name = "Unidade Batel",
            address = "Rua Augusta, 1200",
            city = "São Paulo",
            state = "SP",
            postalCode = "01304-001",
            country = "Brasil",
            phoneNumber = "+55 11 3000-2000",
            timeZone = "America/Sao_Paulo",
            code = "BT"
        };

        // Act
        var response = await client.PutAsJsonAsync($"/api/locations/{batelId}", bodyWithoutStatus);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK, "an edit that leaves the status out is accepted");
        var locations = JsonNode.Parse(await client.GetStringAsync("/api/locations"))!.AsArray();
        var status = locations.Single(l => l!["id"]!.GetValue<Guid>() == batelId)!["status"]?.ToString();
        status.ShouldBe("Closed", "an edit that leaves the status out keeps the status the location already had");
    }
}
