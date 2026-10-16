using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using RestaurantManagement.Modules.Identity.Entities;
using RestaurantManagement.Modules.Organization.Data;
using RestaurantManagement.Modules.Organization.Entities;
using RestaurantManagement.Shared;
using Shouldly;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// Location and organization endpoints on a real PostgreSQL database: the organization comes only from
/// the sign-in token (Rule 3) and another organization's location answers 404 (Rule 5).
/// Ana is Owner of both Cantina da Nonna and Trattoria do Sol, and is signed in to Cantina.
/// </summary>
public class OrganizationTenantEndpointTests : IClassFixture<PostgresDatabaseFixture>
{
    private const string Cantina = "a1a1a1a1-0000-4000-8000-000000000001";

    private static readonly Guid AnaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
    private static readonly Guid CantinaId = Guid.Parse(Cantina);
    private static readonly Guid TrattoriaId = Guid.Parse("b2b2b2b2-0000-4000-8000-000000000002");
    private static readonly Guid CantinaBatelId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000001");
    private static readonly Guid TrattoriaCentroId = Guid.Parse("44444444-dddd-4ddd-8ddd-000000000003");

    // Also carries the organization settings, so the same body passes validation on PUT api/organizations/edit.
    private static readonly object LocationBody = new
    {
        organizationId = TrattoriaId,
        name = "Unidade Mercado Municipal",
        address = "Av. Sete de Setembro, 1865",
        city = "Curitiba",
        state = "PR",
        postalCode = "80060-070",
        country = "Brasil",
        phoneNumber = "+55 41 3000-3000",
        status = "Closed",
        timeZone = "America/Sao_Paulo",
        code = "MERCADO",
        serviceChargeRate = 10m,
        discountApprovalLimit = 5m,
        businessDayCutoff = "04:00"
    };

    private static readonly object OrganizationBody = new
    {
        organizationId = TrattoriaId,
        name = "Cantina da Nonna Batel",
        description = "Cozinha italiana de família",
        serviceChargeRate = 10m,
        discountApprovalLimit = 5m,
        businessDayCutoff = "04:00"
    };

    private readonly PostgresDatabaseFixture _postgres;

    public OrganizationTenantEndpointTests(PostgresDatabaseFixture postgres) => _postgres = postgres;

    private sealed record Row(Guid Id, Guid OrganizationId);

    private sealed record ErrorBody(string Message);

    private async Task<TenantApiFactory> CreateFactoryWithTwoOrganizationsAsync()
    {
        var factory = await TenantApiFactory.CreateAsync(_postgres);
        await factory.SeedUserAsync(AnaId, "ana@cantinadanonna.com.br", "Ana",
            new UserRole { OrganizationId = CantinaId, Role = Roles.Owner },
            new UserRole { OrganizationId = TrattoriaId, Role = Roles.Owner });
        await factory.SeedOrganizationAsync(CantinaId, "Cantina da Nonna", Currency.BRL);
        await factory.SeedOrganizationAsync(TrattoriaId, "Trattoria do Sol", Currency.BRL);
        await factory.SeedLocationAsync(CantinaId, CantinaBatelId, "Unidade Batel", "BATEL", "America/Sao_Paulo");
        await factory.SeedLocationAsync(TrattoriaId, TrattoriaCentroId, "Unidade Centro", "CENTRO", "America/Sao_Paulo");
        return factory;
    }

    /// <summary>Every request carries a JSON body; endpoints that take no body ignore it.</summary>
    private static HttpRequestMessage Request(string method, string url, object body)
        => new(new HttpMethod(method), url) { Content = JsonContent.Create(body) };

    // Scenario 18
    [Theory]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    public async Task LocationById_LocationOfAnotherOrganization_ReturnsNotFoundAndLeavesItUnchanged(string method)
    {
        // Arrange
        await using var factory = await CreateFactoryWithTwoOrganizationsAsync();
        var client = factory.ClientFor(AnaId, CantinaId, Roles.Owner);

        // Act
        var response = await client.SendAsync(Request(method, $"/api/locations/{TrattoriaCentroId}", LocationBody));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var stored = await factory.InOrganizationAsync<OrganizationDbContext, Location?>(TrattoriaId,
            db => db.Locations.SingleOrDefaultAsync(l => l.Id == TrattoriaCentroId));
        stored.ShouldNotBeNull();
        stored.Name.ShouldBe("Unidade Centro");
        stored.Status.ShouldBe(LocationStatus.Active);
    }

    // Scenario 22
    [Fact]
    public async Task GetLocations_LocationsInTwoOrganizations_ReturnsOnlyTokenOrganizationsLocations()
    {
        // Arrange
        await using var factory = await CreateFactoryWithTwoOrganizationsAsync();
        var client = factory.ClientFor(AnaId, CantinaId, Roles.Owner);

        // Act
        var response = await client.GetAsync("/api/locations");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var locations = await response.Content.ReadFromJsonAsync<List<Row>>();
        locations!.Select(l => l.Id).ShouldBe(new[] { CantinaBatelId });
    }

    // Scenario 23
    [Fact]
    public async Task CreateLocation_BodyNamesAnotherOrganization_StoresLocationInTokenOrganization()
    {
        // Arrange
        await using var factory = await CreateFactoryWithTwoOrganizationsAsync();
        var client = factory.ClientFor(AnaId, CantinaId, Roles.Owner);

        // Act
        var response = await client.PostAsJsonAsync("/api/locations", LocationBody);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<Row>())!.OrganizationId.ShouldBe(CantinaId);
    }

    // Scenario 24
    [Fact]
    public async Task EditOrganization_SignedInToCantina_EditsCantinaAndLeavesTrattoriaUnchanged()
    {
        // Arrange
        await using var factory = await CreateFactoryWithTwoOrganizationsAsync();
        var client = factory.ClientFor(AnaId, CantinaId, Roles.Owner);

        // Act
        var response = await client.PutAsJsonAsync("/api/organizations/edit", OrganizationBody);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var names = await factory.InOrganizationAsync<OrganizationDbContext, Dictionary<Guid, string>>(CantinaId,
            db => db.Organizations.ToDictionaryAsync(o => o.Id, o => o.Name));
        names[CantinaId].ShouldBe("Cantina da Nonna Batel");
        names[TrattoriaId].ShouldBe("Trattoria do Sol");
    }

    // Scenario 25
    [Theory]
    [InlineData("GET", "/api/locations/" + Cantina)]
    [InlineData("POST", "/api/locations/" + Cantina)]
    [InlineData("PUT", "/api/organizations/edit/" + Cantina)]
    public async Task OldOrganizationInUrlRoute_Called_IsNotRouted(string method, string url)
    {
        // Arrange
        await using var factory = await CreateFactoryWithTwoOrganizationsAsync();
        var client = factory.ClientFor(AnaId, CantinaId, Roles.Owner);

        // Act
        var response = await client.SendAsync(Request(method, url, LocationBody));

        // Assert
        ((int)response.StatusCode).ShouldBeOneOf(404, 405);
    }

    // Scenario 26
    [Theory]
    [InlineData("GET", "/api/locations")]
    [InlineData("POST", "/api/locations")]
    [InlineData("PUT", "/api/organizations/edit")]
    public async Task OrganizationEndpoints_NoOrganizationInToken_ReturnsForbiddenOrganizationRequired(string method, string url)
    {
        // Arrange
        await using var factory = await CreateFactoryWithTwoOrganizationsAsync();
        var client = factory.ClientFor(AnaId, organizationId: null, Roles.Owner);

        // Act
        var response = await client.SendAsync(Request(method, url, LocationBody));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.Content.ReadFromJsonAsync<ErrorBody>())!.Message.ShouldBe(factory.Message("OrganizationRequired"));
    }
}
