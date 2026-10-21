using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using Shouldly;

namespace RestaurantManagement.Web.Tests;

/// <summary>Sign-in, rate limit and password rules of the member access and invitation endpoints (no database needed).</summary>
public class InvitationAuthEndpointTests
{
    private const string AcceptWithAccountUrl = "/api/auth/accept-invitation";
    private const string BatelId = "44444444-dddd-4ddd-8ddd-000000000001";
    private const string BiaId = "22222222-bbbb-4bbb-8bbb-00000000000b";

    [Theory]
    [InlineData("PUT", "/api/members/" + BiaId + "/access/" + BatelId)]
    [InlineData("POST", "/api/invitations")]
    [InlineData("DELETE", "/api/invitations/" + BiaId)]
    [InlineData("POST", "/api/invitations/accept")]
    [Trait("Rule", "T80")]
    public async Task SignedInInvitationEndpoints_NoAccessToken_ReturnUnauthorized(string method, string url)
    {
        // Arrange
        using var factory = new AuthApiFactory();
        var client = factory.CreateClient();
        var request = new HttpRequestMessage(new HttpMethod(method), url) { Content = JsonContent.Create(new { token = "abc" }) };

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized, "editing access and managing invitations need a signed-in user");
    }

    [Fact]
    [Trait("Rule", "T81")]
    public async Task AcceptWithNewAccount_MoreRequestsThanTheVerifyLimit_ReturnsTooManyRequests()
    {
        // Arrange
        using var factory = new AuthApiFactory().WithWebHostBuilder(builder => builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AuthRateLimit:Verify:PermitLimit"] = "1",
                ["AuthRateLimit:Verify:WindowSeconds"] = "600"
            })));
        var client = factory.CreateClient();
        var body = new { token = "invalid", firstName = "Bruno", lastName = "Souza", password = "Senha@Segura9" };
        await client.PostAsJsonAsync(AcceptWithAccountUrl, body);

        // Act
        var second = await client.PostAsJsonAsync(AcceptWithAccountUrl, body);

        // Assert
        second.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests, "the public accept endpoint is limited per address like verification");
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("semmaiuscula9!")]
    [Trait("Rule", "T82")]
    public async Task AcceptWithNewAccount_WeakPassword_ReturnsBadRequest(string password)
    {
        // Arrange
        using var factory = new AuthApiFactory();
        var client = factory.CreateClient();

        // Act
        var response = await client.PostAsJsonAsync(AcceptWithAccountUrl,
            new { token = "invalid", firstName = "Bruno", lastName = "Souza", password });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, "the new account follows the registration password rules");
    }
}
