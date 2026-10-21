using System.Net;
using System.Net.Http.Json;
using Shouldly;
using static RestaurantManagement.Web.Tests.InvitationWebSupport;

namespace RestaurantManagement.Web.Tests;

/// <summary>Size limits on the invitation and member access bodies, and the repeated-location refusal.</summary>
public class InvitationSizeLimitEndpointTests : IClassFixture<PostgresDatabaseFixture>
{
    private readonly PostgresDatabaseFixture _postgres;

    public InvitationSizeLimitEndpointTests(PostgresDatabaseFixture postgres) => _postgres = postgres;

    [Theory]
    [InlineData("locations")]
    [InlineData("roleNames")]
    [InlineData("levels")]
    [Trait("Rule", "T84")]
    public async Task CreateInvitation_OversizedCollection_ReturnsBadRequestAndStoresNothing(string collection)
    {
        // Arrange
        await using var factory = await CreateFactoryAsync(_postgres);
        var client = factory.ClientFor(AnaId, CantinaId);
        var many = Enumerable.Range(0, 101).ToList();
        var locations = collection == "locations"
            ? many.Select(_ => new { locationId = BatelId, roleNames = new[] { "Waiter" }, levels = new Dictionary<string, string>() }).ToArray()
            : new[]
            {
                new
                {
                    locationId = BatelId,
                    roleNames = collection == "roleNames" ? many.Select(i => "Waiter").ToArray() : new[] { "Waiter" },
                    levels = collection == "levels" ? many.ToDictionary(i => $"Module{i}", _ => "Read") : new Dictionary<string, string>()
                }
            };

        // Act
        var response = await client.PostAsJsonAsync("/api/invitations", new { email = BrunoEmail, locations });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, "a body with an oversized list is refused");
        (await InvitationsAsync(factory, CantinaId)).ShouldBeEmpty("nothing is stored");
    }

    [Theory]
    [InlineData("roleNames")]
    [InlineData("levels")]
    [Trait("Rule", "T84")]
    public async Task SetAccess_OversizedCollection_ReturnsBadRequestAndChangesNothing(string collection)
    {
        // Arrange
        await using var factory = await CreateFactoryAsync(_postgres);
        var client = factory.ClientFor(AnaId, CantinaId);
        var many = Enumerable.Range(0, 101).ToList();
        var before = await CellsAsync(factory, CantinaId, BiaId, BatelId);

        // Act
        var response = await client.PutAsJsonAsync($"/api/members/{BiaId}/access/{BatelId}", new
        {
            roleNames = collection == "roleNames" ? many.Select(i => "Waiter").ToArray() : new[] { "Waiter" },
            levels = collection == "levels" ? many.ToDictionary(i => $"Module{i}", _ => "Read") : new Dictionary<string, string>()
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, "a body with an oversized list is refused");
        (await CellsAsync(factory, CantinaId, BiaId, BatelId)).ShouldBe(before, "the member's grid is unchanged");
    }

    [Theory]
    [InlineData("/api/invitations/accept")]
    [InlineData("/api/auth/accept-invitation")]
    [Trait("Rule", "T85")]
    public async Task Accept_TokenLongerThanTheLimit_ReturnsBadRequest(string url)
    {
        // Arrange
        await using var factory = await CreateFactoryAsync(_postgres);
        var client = factory.ClientFor(RitaId, null);
        var token = new string('a', 201);

        // Act
        var response = await client.PostAsJsonAsync(url, new { token, firstName = "Bruno", lastName = "Souza", password = "Senha@Segura9" });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, "an oversized token is refused before it is looked up");
    }

    [Fact]
    [Trait("Rule", "T86")]
    public async Task CreateInvitation_SameLocationTwice_ReturnsBadRequestAndStoresNothing()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync(_postgres);
        var client = factory.ClientFor(AnaId, CantinaId);
        var location = new { locationId = BatelId, roleNames = new[] { "Waiter" }, levels = new Dictionary<string, string>() };

        // Act
        var response = await client.PostAsJsonAsync("/api/invitations", new { email = BrunoEmail, locations = new[] { location, location } });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, "a repeated location is refused, not a database error");
        (await InvitationsAsync(factory, CantinaId)).ShouldBeEmpty("nothing is stored");
    }
}
