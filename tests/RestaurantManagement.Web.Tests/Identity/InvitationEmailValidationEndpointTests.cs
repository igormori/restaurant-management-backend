using System.Net;
using System.Net.Http.Json;
using Shouldly;
using static RestaurantManagement.Web.Tests.InvitationWebSupport;

namespace RestaurantManagement.Web.Tests;

/// <summary><c>POST api/invitations</c> refuses a missing, malformed or too long email before doing any work.</summary>
public class InvitationEmailValidationEndpointTests : IClassFixture<PostgresDatabaseFixture>
{
    private readonly PostgresDatabaseFixture _postgres;

    public InvitationEmailValidationEndpointTests(PostgresDatabaseFixture postgres) => _postgres = postgres;

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("x")]
    [InlineData("bruno@")]
    [Trait("Rule", "T83")]
    public async Task CreateInvitation_MissingOrMalformedEmail_ReturnsBadRequestAndStoresNothing(string? email)
    {
        // Arrange
        await using var factory = await CreateFactoryAsync(_postgres);
        var client = factory.ClientFor(AnaId, CantinaId);

        // Act
        var response = await client.PostAsJsonAsync("/api/invitations", new
        {
            email,
            locations = new[] { new { locationId = BatelId, roleNames = new[] { "Waiter" }, levels = new Dictionary<string, string>() } }
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, "an invitation needs a well-formed email address");
        (await InvitationsAsync(factory, CantinaId)).ShouldBeEmpty("nothing is stored");
    }

    [Fact]
    [Trait("Rule", "T83")]
    public async Task CreateInvitation_EmailLongerThan320Characters_ReturnsBadRequestAndStoresNothing()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync(_postgres);
        var client = factory.ClientFor(AnaId, CantinaId);
        var email = new string('a', 310) + "@exemplo.com.br";

        // Act
        var response = await client.PostAsJsonAsync("/api/invitations", InviteBody(email, BatelId, new[] { "Waiter" }));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, "an email longer than 320 characters is refused, not a database error");
        (await InvitationsAsync(factory, CantinaId)).ShouldBeEmpty("nothing is stored");
    }
}
