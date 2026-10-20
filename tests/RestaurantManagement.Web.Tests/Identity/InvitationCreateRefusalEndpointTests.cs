using System.Net;
using System.Net.Http.Json;
using Shouldly;
using static RestaurantManagement.Web.Tests.InvitationWebSupport;

namespace RestaurantManagement.Web.Tests;

/// <summary><c>POST api/invitations</c> refuses an empty location grid and a null location, and survives two requests for one address at once.</summary>
public class InvitationCreateRefusalEndpointTests : IClassFixture<PostgresDatabaseFixture>
{
    private readonly PostgresDatabaseFixture _postgres;

    public InvitationCreateRefusalEndpointTests(PostgresDatabaseFixture postgres) => _postgres = postgres;

    [Fact]
    [Trait("Rule", "T87")]
    public async Task CreateInvitation_LocationWithNoRolesAndNoLevels_ReturnsBadRequestAndStoresNothing()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync(_postgres);
        var client = factory.ClientFor(AnaId, CantinaId);

        // Act
        var response = await client.PostAsJsonAsync("/api/invitations", InviteBody(BrunoEmail, BatelId, Array.Empty<string>()));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, "an invitation location must grant something, or a cancel could not find it");
        (await InvitationsAsync(factory, CantinaId)).ShouldBeEmpty("nothing is stored");
    }

    [Fact]
    [Trait("Rule", "T88")]
    public async Task CreateInvitation_NullLocationEntry_ReturnsBadRequestAndStoresNothing()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync(_postgres);
        var client = factory.ClientFor(AnaId, CantinaId);

        // Act
        var response = await client.PostAsJsonAsync("/api/invitations", new { email = BrunoEmail, locations = new object?[] { null } });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, "a null location is refused, not a server error");
        (await InvitationsAsync(factory, CantinaId)).ShouldBeEmpty("nothing is stored");
    }

    [Fact]
    [Trait("Rule", "T89")]
    public async Task CreateInvitation_SameEmailAtTheSameMoment_NeverFailsWithAServerErrorAndLeavesOneOpenInvitation()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync(_postgres);
        var body = InviteBody(BrunoEmail, BatelId, new[] { "Waiter" });

        // Act
        var responses = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => factory.ClientFor(AnaId, CantinaId).PostAsJsonAsync("/api/invitations", body)));

        // Assert
        responses.ShouldAllBe(r => r.IsSuccessStatusCode || r.StatusCode == HttpStatusCode.Conflict, "the loser of the race gets a refusal, not a server error");
        responses.ShouldContain(r => r.IsSuccessStatusCode, "one request succeeds");
        (await InvitationsAsync(factory, CantinaId)).Count(i => i.UsedAt == null && i.CancelledAt == null && i.ReplacedAt == null)
            .ShouldBe(1, "only one open invitation exists for the address");
    }
}
