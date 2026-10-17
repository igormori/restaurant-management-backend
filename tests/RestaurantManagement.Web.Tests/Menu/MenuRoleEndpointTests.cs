using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NSubstitute;
using RestaurantManagement.Modules.Menu.Models;
using RestaurantManagement.Modules.Menu.Services;
using RestaurantManagement.Shared;
using Shouldly;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// The role filter on the menu endpoints: every role reads, only Owner and Admin write.
/// The menu service is substituted; its own role check is covered by the Menu unit tests.
/// </summary>
public class MenuRoleEndpointTests : IDisposable
{
    private static readonly Guid JoaoId = Guid.Parse("22222222-bbbb-4bbb-8bbb-00000000000b");
    private static readonly Guid CantinaId = Guid.Parse("c1c1c1c1-0000-4000-8000-000000000001");
    private static readonly Guid CentroLocationId = Guid.Parse("44444444-dddd-4ddd-8ddd-00000000000d");

    private readonly IMenuService _menuService = Substitute.For<IMenuService>();
    private readonly AuthApiFactory _baseFactory = new();
    private readonly WebApplicationFactory<Program> _factory;

    public MenuRoleEndpointTests()
    {
        _menuService.GetMenusAsync(Arg.Any<Guid>()).Returns(new List<MenuResponse>());
        _menuService.GetMenusByLocationAsync(Arg.Any<Guid>(), Arg.Any<Guid>()).Returns(new List<MenuResponse>());
        _factory = _baseFactory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
        {
            services.RemoveAll<IMenuService>();
            services.AddSingleton(_menuService);
        }));
    }

    public void Dispose()
    {
        _factory.Dispose();
        _baseFactory.Dispose();
    }

    private HttpRequestMessage SignedIn(HttpMethod method, string url, string role)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Bearer", TestAccessTokens.Create(_factory.Services, JoaoId, CantinaId, role));
        return request;
    }

    // Scenario 39
    [Theory]
    [InlineData(Roles.Manager)]
    [InlineData(Roles.Cashier)]
    [InlineData(Roles.Waiter)]
    [InlineData(Roles.Kitchen)]
    [Trait("Rule", "M1")]
    public async Task CreateMenu_RoleOtherThanOwnerOrAdmin_ReturnsForbidden(string role)
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = SignedIn(HttpMethod.Post, "/api/menus/create", role);
        request.Content = JsonContent.Create(new CreateMenuRequest
        {
            Name = "Almoço executivo",
            Description = "Prato feito com arroz, feijão e bife"
        });

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden, "only an Owner or Admin may create a menu");
        await _menuService.DidNotReceive().CreateMenuAsync(Arg.Any<Guid>(), Arg.Any<CreateMenuRequest>());
    }

    // Scenario 37
    [Theory]
    [InlineData(Roles.Owner)]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.Manager)]
    [InlineData(Roles.Cashier)]
    [InlineData(Roles.Waiter)]
    [InlineData(Roles.Kitchen)]
    [Trait("Rule", "M3")]
    public async Task GetMenus_AnyRole_ReturnsOk(string role)
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.SendAsync(SignedIn(HttpMethod.Get, "/api/menus", role));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK, "every role of the organization may list its menus");
    }

    // Scenario 38
    [Theory]
    [InlineData(Roles.Owner)]
    [InlineData(Roles.Admin)]
    [InlineData(Roles.Manager)]
    [InlineData(Roles.Cashier)]
    [InlineData(Roles.Waiter)]
    [InlineData(Roles.Kitchen)]
    [Trait("Rule", "M3")]
    public async Task GetMenusByLocation_AnyRole_ReturnsOk(string role)
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.SendAsync(SignedIn(HttpMethod.Get, $"/api/menus/location/{CentroLocationId}", role));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK, "every role of the organization may list a location's menus");
    }
}
