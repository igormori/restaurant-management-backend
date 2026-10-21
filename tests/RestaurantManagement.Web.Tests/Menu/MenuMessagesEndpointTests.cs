using System.Net;
using RestaurantManagement.Shared;
using Shouldly;
using static RestaurantManagement.Web.Tests.LocalizedResponses;
using Currency = RestaurantManagement.Modules.Organization.Entities.Currency;

namespace RestaurantManagement.Web.Tests;

/// <summary>
/// Menu and menu category messages in the requested language (spec Outcomes #1, #2, #4 and #6).
/// Ana owns Cantina da Nonna and is signed in to it; Trattoria do Sol belongs to Bruno.
/// Not-found cases run on PostgreSQL; field errors are answered before any database work.
/// </summary>
public class MenuMessagesEndpointTests : IClassFixture<PostgresDatabaseFixture>
{
    private static readonly Guid AnaId = Guid.Parse("11111111-aaaa-4aaa-8aaa-00000000000a");
    private static readonly Guid BrunoId = Guid.Parse("22222222-bbbb-4bbb-8bbb-00000000000b");
    private static readonly Guid CantinaId = Guid.Parse("a1a1a1a1-0000-4000-8000-000000000001");
    private static readonly Guid TrattoriaId = Guid.Parse("b2b2b2b2-0000-4000-8000-000000000002");
    private static readonly Guid CantinaDinnerMenuId = Guid.Parse("55555555-0000-4000-8000-000000000001");
    private static readonly Guid TrattoriaLunchMenuId = Guid.Parse("55555555-0000-4000-8000-000000000002");
    private static readonly Guid TrattoriaPizzaCategoryId = Guid.Parse("66666666-0000-4000-8000-000000000002");
    private static readonly Guid CantinaPastaCategoryId = Guid.Parse("66666666-0000-4000-8000-000000000001");
    private static readonly Guid DeletedMenuId = Guid.Parse("55555555-0000-4000-8000-0000000000ff");

    private readonly PostgresDatabaseFixture _postgres;

    public MenuMessagesEndpointTests(PostgresDatabaseFixture postgres) => _postgres = postgres;

    private async Task<TenantApiFactory> CreateFactoryAsync()
    {
        var factory = await TenantApiFactory.CreateAsync(_postgres);
        await factory.SeedOrganizationAsync(CantinaId, "Cantina da Nonna", Currency.BRL);
        await factory.SeedOrganizationAsync(TrattoriaId, "Trattoria do Sol", Currency.BRL);
        await factory.SeedUserAsync(AnaId, "ana@cantinadanonna.com.br", "Ana",
            TenantApiFactory.OwnerOf(CantinaId));
        await factory.SeedUserAsync(BrunoId, "bruno@trattoriadosol.com.br", "Bruno",
            TenantApiFactory.OwnerOf(TrattoriaId));
        await factory.SeedMenuAsync(CantinaId, CantinaDinnerMenuId, "Jantar");
        await factory.SeedMenuAsync(TrattoriaId, TrattoriaLunchMenuId, "Pranzo");
        await factory.SeedCategoryAsync(TrattoriaId, TrattoriaLunchMenuId, TrattoriaPizzaCategoryId, "Pizze");
        return factory;
    }

    private static HttpClient AnaSignedInToCantina(AuthApiFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue(
            "Bearer", TestAccessTokens.Create(factory.Services, AnaId, CantinaId));
        return client;
    }

    // Scenario 7
    [Theory]
    [InlineData("pt", "Menu não encontrado.")]
    [InlineData("en", "Menu not found.")]
    [Trait("Rule", "M7")]
    public async Task EditMenu_MenuDoesNotExist_ReturnsNotFoundWithLocalizedMessage(string culture, string expected)
    {
        // Arrange
        await using var factory = await CreateFactoryAsync();
        var client = AnaSignedInToCantina(factory);
        var request = Request("PUT", $"/api/menus/{DeletedMenuId}", culture, new { name = "Almoço Executivo", isActive = true });

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound, "a menu that does not exist is answered as not found");
        (await MessageAsync(response)).ShouldBe(expected, "the not-found message is written in the requested language");
    }

    // Scenario 8
    [Theory]
    [InlineData("GET", "pt", "Categoria do menu não encontrada.")]
    [InlineData("PUT", "en", "Menu category not found.")]
    [InlineData("DELETE", "pt", "Categoria do menu não encontrada.")]
    [Trait("Rule", "M8")]
    public async Task MenuCategoryById_CategoryOfAnotherOrganization_ReturnsNotFoundWithLocalizedMessage(string method, string culture, string expected)
    {
        // Arrange
        await using var factory = await CreateFactoryAsync();
        var client = AnaSignedInToCantina(factory);
        var request = Request(method, $"/api/menu-categories/{TrattoriaPizzaCategoryId}", culture, new { name = "Pizzas Artesanais", sortOrder = 1 });

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.NotFound, "another organization's category is answered as if it did not exist");
        (await MessageAsync(response)).ShouldBe(expected, "the not-found message is written in the requested language");
    }

    // Scenario 9
    [Theory]
    [InlineData("pt", "O campo Name é obrigatório.")]
    [InlineData("en", "The Name field is required.")]
    [Trait("Rule", "M21")]
    public async Task CreateMenu_NameMissing_ReturnsLocalizedRequiredError(string culture, string expected)
    {
        // Arrange
        using var factory = new AuthApiFactory();
        var client = AnaSignedInToCantina(factory);
        var request = Request("POST", "/api/menus/create", culture, """{ "description": "Pratos servidos a partir das 19h" }""");

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, "a menu without a name is refused");
        (await FieldErrorsAsync(response)).ShouldContain(expected, customMessage: "the refusal is written in the requested language");
    }

    // Scenario 10
    [Theory]
    [InlineData("pt", "O campo Name deve ter no máximo 100 caracteres.")]
    [InlineData("en", "The field Name must be a maximum of 100 characters.")]
    [Trait("Rule", "M21")]
    public async Task EditMenu_NameOver100Characters_ReturnsLocalizedMaxLengthError(string culture, string expected)
    {
        // Arrange
        using var factory = new AuthApiFactory();
        var client = AnaSignedInToCantina(factory);
        var name = string.Concat(Enumerable.Repeat("Jantar de Domingo ", 6));
        var request = Request("PUT", $"/api/menus/{CantinaDinnerMenuId}", culture, new { name, isActive = true });

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, "a menu name over 100 characters is refused");
        (await FieldErrorsAsync(response)).ShouldContain(expected, customMessage: "the refusal is written in the requested language");
    }

    // Scenario 11
    [Theory]
    [InlineData("pt", "O campo Name é obrigatório.")]
    [InlineData("en", "The Name field is required.")]
    [Trait("Rule", "M23")]
    public async Task CreateMenuCategory_NameMissing_ReturnsLocalizedRequiredError(string culture, string expected)
    {
        // Arrange
        using var factory = new AuthApiFactory();
        var client = AnaSignedInToCantina(factory);
        var request = Request("POST", "/api/menu-categories", culture, new { menuId = CantinaDinnerMenuId, name = "", sortOrder = 1 });

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, "a category without a name is refused");
        (await FieldErrorsAsync(response)).ShouldContain(expected, customMessage: "the refusal is written in the requested language");
    }

    // Scenario 12
    [Theory]
    [InlineData("pt", "O campo Description deve ter no máximo 1000 caracteres.")]
    [InlineData("en", "The field Description must be a maximum of 1000 characters.")]
    [Trait("Rule", "M23")]
    public async Task UpdateMenuCategory_DescriptionOver1000Characters_ReturnsLocalizedMaxLengthError(string culture, string expected)
    {
        // Arrange
        using var factory = new AuthApiFactory();
        var client = AnaSignedInToCantina(factory);
        var description = string.Concat(Enumerable.Repeat("Massas frescas feitas na casa. ", 33));
        var request = Request("PUT", $"/api/menu-categories/{CantinaPastaCategoryId}", culture, new { name = "Massas", description, sortOrder = 2 });

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, "a category description over 1000 characters is refused");
        (await FieldErrorsAsync(response)).ShouldContain(expected, customMessage: "the refusal is written in the requested language");
    }

    [Theory]
    [InlineData("pt", "O campo Description deve ter no máximo 500 caracteres.")]
    [InlineData("en", "The field Description must be a maximum of 500 characters.")]
    [Trait("Rule", "M22")]
    public async Task EditMenu_DescriptionOver500Characters_ReturnsLocalizedMaxLengthError(string culture, string expected)
    {
        // Arrange
        using var factory = new AuthApiFactory();
        var client = AnaSignedInToCantina(factory);
        var description = new string('a', 501);
        var request = Request("PUT", $"/api/menus/{CantinaDinnerMenuId}", culture, new { name = "Jantar", description, isActive = true });

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, "a menu description of 501 characters is refused");
        (await FieldErrorsAsync(response)).ShouldContain(expected, customMessage: "the refusal is written in the requested language");
    }

    [Fact]
    [Trait("Rule", "M22")]
    public async Task EditMenu_DescriptionOf500Characters_IsAccepted()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync();
        var client = AnaSignedInToCantina(factory);
        var description = new string('a', 500);
        var request = Request("PUT", $"/api/menus/{CantinaDinnerMenuId}", "pt", new { name = "Jantar", description, isActive = true });

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK, "a menu description of exactly 500 characters is accepted");
    }

    [Theory]
    [InlineData("pt", "O campo Name deve ter no máximo 255 caracteres.")]
    [InlineData("en", "The field Name must be a maximum of 255 characters.")]
    [Trait("Rule", "M24")]
    public async Task CreateMenuCategory_NameOver255Characters_ReturnsLocalizedMaxLengthError(string culture, string expected)
    {
        // Arrange
        using var factory = new AuthApiFactory();
        var client = AnaSignedInToCantina(factory);
        var name = new string('a', 256);
        var request = Request("POST", "/api/menu-categories", culture, new { menuId = CantinaDinnerMenuId, name, sortOrder = 1 });

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest, "a category name of 256 characters is refused");
        (await FieldErrorsAsync(response)).ShouldContain(expected, customMessage: "the refusal is written in the requested language");
    }

    [Fact]
    [Trait("Rule", "M24")]
    public async Task CreateMenuCategory_NameOf255Characters_IsAccepted()
    {
        // Arrange
        await using var factory = await CreateFactoryAsync();
        var client = AnaSignedInToCantina(factory);
        var name = new string('a', 255);
        var request = Request("POST", "/api/menu-categories", "pt", new { menuId = CantinaDinnerMenuId, name, sortOrder = 1 });

        // Act
        var response = await client.SendAsync(request);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.Created, "a category name of exactly 255 characters is accepted");
    }
}
