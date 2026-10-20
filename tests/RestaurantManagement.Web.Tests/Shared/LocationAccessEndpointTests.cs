using System.Net;
using RestaurantManagement.Shared.Auth;
using Shouldly;
using Currency = RestaurantManagement.Modules.Organization.Entities.Currency;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// The X-Location-Id header through the real pipeline, on GET /api/locations (any access lists the
/// locations, so the answer shows the header check and not a menu rule).
/// Cantina da Nonna has Batel and Mercado Municipal; Trattoria do Sol has Centro.
/// Bia is a Waiter with Menus Read at Batel only; Ana holds Manager levels at both Cantina
/// locations and Locations Manage at Trattoria's Centro; Caio is a new Admin with no access yet.
/// </summary>
public class LocationAccessEndpointTests : IClassFixture<PostgresDatabaseFixture>
{
    private const string Batel = "44444444-dddd-4ddd-8ddd-000000000001";
    private const string MercadoMunicipal = "44444444-dddd-4ddd-8ddd-000000000002";
    private const string TrattoriaCentro = "44444444-dddd-4ddd-8ddd-000000000003";
    private const string LocationsUrl = "/api/locations";
    private const string AnaText = "11111111-aaaa-4aaa-8aaa-00000000000a";
    private const string BiaText = "22222222-bbbb-4bbb-8bbb-00000000000b";

    private static readonly Guid AnaId = Guid.Parse(AnaText);
    private static readonly Guid BiaId = Guid.Parse(BiaText);
    private static readonly Guid CaioId = Guid.Parse("33333333-cccc-4ccc-8ccc-00000000000c");
    private static readonly Guid CantinaId = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");
    private static readonly Guid TrattoriaId = Guid.Parse("b2b2b2b2-0000-4000-8000-000000000002");

    private readonly PostgresDatabaseFixture _postgres;

    public LocationAccessEndpointTests(PostgresDatabaseFixture postgres) => _postgres = postgres;

    private async Task<TenantApiFactory> CreateFactoryAsync()
    {
        var factory = await TenantApiFactory.CreateAsync(_postgres);
        await factory.SeedOrganizationAsync(CantinaId, "Cantina da Nonna", Currency.BRL);
        await factory.SeedOrganizationAsync(TrattoriaId, "Trattoria do Sol", Currency.BRL);
        await factory.SeedLocationAsync(CantinaId, Guid.Parse(Batel), "Unidade Batel", "BATEL", "America/Sao_Paulo");
        await factory.SeedLocationAsync(CantinaId, Guid.Parse(MercadoMunicipal), "Unidade Mercado Municipal", "MERCADO", "America/Sao_Paulo");
        await factory.SeedLocationAsync(TrattoriaId, Guid.Parse(TrattoriaCentro), "Unidade Centro", "CENTRO", "America/Sao_Paulo");
        await factory.SeedUserAsync(BiaId, "bia@cantinadanonna.com.br", "Bia", TenantApiFactory.MemberOf(CantinaId));
        await factory.GrantAsync(BiaId, CantinaId, Guid.Parse(Batel), (AccessModule.Menus, AccessLevel.Read));
        await factory.SeedUserAsync(CaioId, "caio@cantinadanonna.com.br", "Caio", TenantApiFactory.MemberOf(CantinaId));
        await factory.SeedUserAsync(AnaId, "ana@cantinadanonna.com.br", "Ana",
            TenantApiFactory.MemberOf(CantinaId), TenantApiFactory.MemberOf(TrattoriaId));
        foreach (var location in new[] { Batel, MercadoMunicipal })
            await factory.GrantAsync(AnaId, CantinaId, Guid.Parse(location),
                (AccessModule.Locations, AccessLevel.Read), (AccessModule.Menus, AccessLevel.Manage),
                (AccessModule.Orders, AccessLevel.Read), (AccessModule.Kitchen, AccessLevel.Read), (AccessModule.Reports, AccessLevel.Manage));
        await factory.GrantAsync(AnaId, TrattoriaId, Guid.Parse(TrattoriaCentro), (AccessModule.Locations, AccessLevel.Manage));
        return factory;
    }

    [Fact]
    [Trait("Rule", "SH10")]
    public async Task GetLocations_WaiterWithAccessOnlyToBatelSelectsMercadoMunicipal_ReturnsForbidden()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync();
        var client = factory.ClientFor(BiaId, CantinaId, locationHeader: MercadoMunicipal);

        // Act
        var response = await client.GetAsync(LocationsUrl);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "a member with no level at a location may not work in it, even with access at another location");
    }

    [Theory]
    [InlineData(BiaText, Batel)]
    [InlineData(AnaText, Batel)]
    [InlineData(AnaText, MercadoMunicipal)]
    [Trait("Rule", "SH11")]
    public async Task GetLocations_MemberWithALevelAtTheSelectedLocation_ReturnsOk(string member, string location)
    {
        // Arrange
        await using var factory = await CreateFactoryAsync();
        var client = factory.ClientFor(Guid.Parse(member), CantinaId, locationHeader: location);

        // Act
        var response = await client.GetAsync(LocationsUrl);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK, "a member with levels at a location may select it");
    }

    [Fact]
    [Trait("Rule", "SH10")]
    public async Task GetLocations_ManageEverywhereInAnotherOrganizationSelectsItsLocation_ReturnsForbidden()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync();
        var client = factory.ClientFor(AnaId, CantinaId, locationHeader: TrattoriaCentro);
        var ownLocationClient = factory.ClientFor(AnaId, CantinaId, locationHeader: Batel);

        // Act
        var response = await client.GetAsync(LocationsUrl);
        var ownLocation = await ownLocationClient.GetAsync(LocationsUrl);

        // Assert
        ownLocation.StatusCode.ShouldBe(HttpStatusCode.OK, "the same member may select a location of the organization in the token");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "another organization's location is refused even when the member manages everything in that organization");
    }

    [Fact]
    [Trait("Rule", "SH10")]
    public async Task GetLocations_NewAdminWithNoAccessSelectsBatel_ReturnsForbidden()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync();
        var client = factory.ClientFor(CaioId, CantinaId, locationHeader: Batel);
        var before = await client.GetAsync(LocationsUrl);
        await factory.GrantAsync(CaioId, CantinaId, Guid.Parse(Batel), (AccessModule.Menus, AccessLevel.Read));

        // Act
        var afterGrant = await client.GetAsync(LocationsUrl);

        // Assert
        before.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "a new member has no access to any location until it is granted");
        afterGrant.StatusCode.ShouldBe(HttpStatusCode.OK, "once a level at the location is granted, the same token is accepted there");
    }
}
